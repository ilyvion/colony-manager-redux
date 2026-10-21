// ManagerJobProductionSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The Production job's real bills: what a gather and execute pass adds to work tables, and what
/// clean-up removes. The decisions themselves are covered by the pure-function tests.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobProductionSceneTests
{
    private const int TargetCount = 30;

    private static readonly ThingDef Stove = DefDatabase<ThingDef>.GetNamed("FueledStove");
    private static readonly RecipeDef Recipe = DefDatabase<RecipeDef>.GetNamed("CookMealSimple");

    private static ManagerJob_Production AddProductionJob(ManagerScene scene)
    {
        var job = scene.AddJob<ManagerJob_Production>("CM_ProductionManager");
        job.Recipe = Recipe;
        job.TriggerThreshold.TargetCount = TargetCount;
        return job;
    }

    private static Building_WorkTable AddStove(ManagerScene scene, int index) =>
        (Building_WorkTable)
            scene.SpawnBuilding(Stove, scene.Cells(1, 1, offsetX: -10, offsetZ: index * 4).First());

    private static void RunJob(ManagerScene scene, ManagerJob_Production job)
    {
        // Meals count through the resource counter, which the game only refreshes periodically.
        scene.Map.resourceCounter.UpdateResourceCounts();
        _ = job.TriggerThreshold.GetCurrentCount(cached: false);
        job.Untouch();
        ManagerScene.RunToCompletion(scene.Manager.TryDoWork());
    }

    private static IEnumerable<Bill_Production> BillsOn(Building_WorkTable table) =>
        table.billStack.Bills.OfType<Bill_Production>();

    [Test]
    public static void NewJobsSkillRangeFollowsTheMaxAllowedSkillLevel()
    {
        var originalMaxAllowedSkillLevel = ManagerJob_Production.MaxAllowedSkillLevel;
        ManagerJob_Production.MaxAllowedSkillLevel = () => 100;
        try
        {
            using var scene = new ManagerScene();
            var job = AddProductionJob(scene);

            Expect.AreEqual(new IntRange(0, 100), job.AllowedSkillRange);

            job.OnMaxAllowedSkillLevelChanged(100, 120);

            Expect.AreEqual(new IntRange(0, 120), job.AllowedSkillRange);
        }
        finally
        {
            ManagerJob_Production.MaxAllowedSkillLevel = originalMaxAllowedSkillLevel;
        }
    }

    [Test]
    public static void UnmetTargetAddsAManagedBillWithTheJobsSettings()
    {
        using var scene = new ManagerScene();
        var stove = AddStove(scene, 0);
        var job = AddProductionJob(scene);
        job.IngredientSearchRadius = 50f;
        job.AllowedSkillRange = new IntRange(2, 10);

        RunJob(scene, job);

        var bill = BillsOn(stove).SingleOrDefault();
        Expect.IsNotNull(bill);
        if (bill == null)
        {
            return;
        }
        Expect.ReferencesAreEqual(Recipe, bill.recipe);
        Expect.AreEqual(BillRepeatModeDefOf.RepeatCount, bill.repeatMode);
        Expect.AreEqual(TargetCount, bill.repeatCount);
        Expect.AreEqual(50f, bill.ingredientSearchRadius);
        Expect.AreEqual(new IntRange(2, 10), bill.allowedSkillRange);
        Expect.That(
            () =>
                bill
                    .ingredientFilter.AllowedThingDefs.ToHashSet()
                    .SetEquals(job.AllowedIngredients),
            "The bill only allows the job's allowed ingredients"
        );
        Expect.AreEqual(1, job.ManagedBills.Count);
        Expect.IsTrue(job.ManagedBills.Contains(bill));
    }

    [Test]
    public static void TwoWorkbenchesShareTheShortfall()
    {
        using var scene = new ManagerScene();
        var first = AddStove(scene, 0);
        var second = AddStove(scene, 1);
        var job = AddProductionJob(scene);

        RunJob(scene, job);

        var firstBill = BillsOn(first).SingleOrDefault();
        var secondBill = BillsOn(second).SingleOrDefault();
        Expect.IsNotNull(firstBill);
        Expect.IsNotNull(secondBill);
        if (firstBill == null || secondBill == null)
        {
            return;
        }
        Expect.AreEqual(TargetCount, firstBill.repeatCount + secondBill.repeatCount);
        Expect.IsTrue(firstBill.repeatCount > 0 && secondBill.repeatCount > 0);
    }

    [Test]
    public static void MetTargetLeavesTheBillWithNothingToDo()
    {
        using var scene = new ManagerScene();
        var stove = AddStove(scene, 0);
        _ = scene.AddStockpile(scene.Cells(3, 3, offsetX: 10));
        var job = AddProductionJob(scene);
        RunJob(scene, job);
        var bill = BillsOn(stove).Single();
        Expect.IsTrue(bill.repeatCount > 0);

        _ = scene.SpawnThing(ThingDefOf.MealSimple, scene.Cells(1, 1, offsetX: 10).First(), 10);
        _ = scene.SpawnThing(ThingDefOf.MealSimple, scene.Cells(1, 1, offsetX: 11).First(), 10);
        _ = scene.SpawnThing(ThingDefOf.MealSimple, scene.Cells(1, 1, offsetX: 12).First(), 10);
        scene.Map.resourceCounter.UpdateResourceCounts();
        Expect.AreEqual(
            30,
            job.TriggerThreshold.GetCurrentCount(cached: false),
            "The threshold sees the stocked meals"
        );
        RunJob(scene, job);

        Expect.AreEqual(0, bill.repeatCount);
        Expect.AreEqual(1, BillsOn(stove).Count());
    }

    [Test]
    public static void CleanUpRemovesEveryManagedBillButNotOtherBills()
    {
        using var scene = new ManagerScene();
        var first = AddStove(scene, 0);
        var second = AddStove(scene, 1);
        var userBill = new Bill_Production(Recipe, null);
        second.billStack.AddBill(userBill);
        var job = AddProductionJob(scene);
        RunJob(scene, job);
        Expect.AreEqual(2, job.ManagedBills.Count);

        job.CleanUp();

        Expect.AreEqual(0, job.ManagedBills.Count);
        Expect.AreEqual(0, BillsOn(first).Count());
        Expect.AreEqual(1, BillsOn(second).Count());
        Expect.ReferencesAreEqual(userBill, BillsOn(second).Single());
    }

    [Test]
    public static void ChangingTheRecipeRemovesManagedBills()
    {
        using var scene = new ManagerScene();
        var stove = AddStove(scene, 0);
        var job = AddProductionJob(scene);
        RunJob(scene, job);
        Expect.AreEqual(1, BillsOn(stove).Count());

        job.Recipe = DefDatabase<RecipeDef>.GetNamed("CookMealFine");

        Expect.AreEqual(0, job.ManagedBills.Count);
        Expect.AreEqual(0, BillsOn(stove).Count());
    }

    [Test]
    public static void WorkbenchLeavingScopeLosesItsManagedBill()
    {
        using var scene = new ManagerScene();
        var kept = AddStove(scene, 0);
        var dropped = AddStove(scene, 1);
        var job = AddProductionJob(scene);
        RunJob(scene, job);
        Expect.AreEqual(2, job.ManagedBills.Count);

        job.AssignmentMode = ManagerJob_Production.WorkbenchAssignmentMode.Specific;
        _ = job.SpecificWorkbenches.Add(kept);
        RunJob(scene, job);

        Expect.AreEqual(1, BillsOn(kept).Count());
        Expect.AreEqual(0, BillsOn(dropped).Count());
        Expect.AreEqual(1, job.ManagedBills.Count);
    }

    [Test]
    public static void BillsDeletedByThePlayerAreForgottenAndRecreated()
    {
        using var scene = new ManagerScene();
        var stove = AddStove(scene, 0);
        var job = AddProductionJob(scene);
        RunJob(scene, job);
        var bill = BillsOn(stove).Single();

        stove.billStack.Delete(bill);
        RunJob(scene, job);

        Expect.AreEqual(1, job.ManagedBills.Count);
        var replacement = BillsOn(stove).SingleOrDefault();
        Expect.IsNotNull(replacement);
        Expect.IsFalse(ReferenceEquals(bill, replacement));
    }

    [Test]
    public static void BillsDeletedByThePlayerStopCountingBeforeTheNextPass()
    {
        using var scene = new ManagerScene();
        var first = AddStove(scene, 0);
        var second = AddStove(scene, 1);
        var job = AddProductionJob(scene);
        RunJob(scene, job);
        var deleted = BillsOn(first).Single();
        var kept = BillsOn(second).Single();
        Expect.AreEqual(TargetCount, job.ExpectedYield);

        first.billStack.Delete(deleted);

        Expect.AreEqual(2, job.ManagedBills.Count, "Not yet pruned");
        Expect.ReferencesAreEqual(kept, job.LiveManagedBills.Single());
        Expect.AreEqual(kept.repeatCount, job.ExpectedYield);
    }

    [Test]
    public static void BillsOnADestroyedWorkTableStopCountingBeforeTheNextPass()
    {
        using var scene = new ManagerScene();
        var stove = AddStove(scene, 0);
        var job = AddProductionJob(scene);
        RunJob(scene, job);
        Expect.AreEqual(TargetCount, job.ExpectedYield);

        stove.Destroy(DestroyMode.Vanish);

        Expect.AreEqual(1, job.ManagedBills.Count, "Not yet pruned");
        Expect.IsEmpty(job.LiveManagedBills);
        Expect.AreEqual(0, job.ExpectedYield);
    }

    [Test]
    public static void BillsOnADestroyedWorkTableAreForgotten()
    {
        using var scene = new ManagerScene();
        var stove = AddStove(scene, 0);
        var job = AddProductionJob(scene);
        RunJob(scene, job);

        stove.Destroy(DestroyMode.Vanish);
        RunJob(scene, job);

        Expect.IsEmpty(job.ManagedBills);
    }

    [Test]
    public static void SwitchingBackToMaintainStockResetsTheOperatorWithoutWarning()
    {
        using var scene = new ManagerScene();
        var job = AddProductionJob(scene);

        job.Mode = ManagerJob_Production.ProductionMode.ConsumeSurplus;
        Expect.AreEqual(Trigger_Threshold.Ops.HigherThan, job.TriggerThreshold.Op);
        job.Mode = ManagerJob_Production.ProductionMode.MaintainStock;

        Expect.AreEqual(Trigger_Threshold.Ops.LowerThan, job.TriggerThreshold.Op);
    }

    // Quality range

    private static readonly RecipeDef KnifeRecipe = DefDatabase<RecipeDef>.GetNamed(
        "Make_MeleeWeapon_Knife"
    );

    private static readonly QualityRange GoodOrBetter = new(
        QualityCategory.Good,
        QualityCategory.Legendary
    );

    private static ManagerJob_Production AddKnifeJob(ManagerScene scene)
    {
        var job = scene.AddJob<ManagerJob_Production>("CM_ProductionManager");
        job.Recipe = KnifeRecipe;
        job.TriggerThreshold.TargetCount = TargetCount;
        return job;
    }

    private static void SpawnKnife(ManagerScene scene, IntVec3 cell, QualityCategory quality) =>
        scene
            .SpawnThing(ThingDefOf.MeleeWeapon_Knife, cell)
            .TryGetComp<CompQuality>()
            .SetQuality(quality, ArtGenerationContext.Colony);

    [Test]
    public static void NewJobsCountEveryQuality()
    {
        using var scene = new ManagerScene();
        var job = AddKnifeJob(scene);

        Expect.AreEqual(QualityRange.All, job.CountedQualityRange);
    }

    [Test]
    public static void OnlyItemsWithinTheQualityRangeCountTowardTheTarget()
    {
        using var scene = new ManagerScene();
        var cells = scene.Cells(3, 3, offsetX: 10).ToList();
        _ = scene.AddStockpile(cells);
        var job = AddKnifeJob(scene);
        var before = job.TriggerThreshold.GetCurrentCount(cached: false);
        SpawnKnife(scene, cells[0], QualityCategory.Awful);
        SpawnKnife(scene, cells[1], QualityCategory.Normal);
        SpawnKnife(scene, cells[2], QualityCategory.Good);
        SpawnKnife(scene, cells[3], QualityCategory.Legendary);
        var allQualities = job.TriggerThreshold.GetCurrentCount(cached: false);
        Expect.AreEqual(before + 4, allQualities);

        job.CountedQualityRange = GoodOrBetter;

        Expect.AreEqual(allQualities - 2, job.TriggerThreshold.GetCurrentCount(cached: false));
    }

    [Test]
    public static void QualityRangeSurvivesSwitchingModesAndRecipes()
    {
        using var scene = new ManagerScene();
        var job = AddKnifeJob(scene);
        job.CountedQualityRange = GoodOrBetter;

        job.Mode = ManagerJob_Production.ProductionMode.ConsumeSurplus;
        job.Mode = ManagerJob_Production.ProductionMode.MaintainStock;
        Expect.AreEqual(GoodOrBetter, job.CountedQualityRange);

        job.Recipe = Recipe;
        job.Recipe = KnifeRecipe;
        Expect.AreEqual(GoodOrBetter, job.CountedQualityRange);
    }

    [Test]
    public static void QualityRangeIsSavedAndLoaded()
    {
        using var scene = new ManagerScene();
        var job = AddKnifeJob(scene);
        job.CountedQualityRange = GoodOrBetter;

        var loadedJob = (ManagerJob_Production)
            ScribeRoundTrip.RoundTripJobs(scene.Manager, [job]).Single();
        Expect.AreEqual(GoodOrBetter, loadedJob.CountedQualityRange);
    }

    // Bills on another map

    private static Building_WorkTable AddStoveOn(Map map)
    {
        var stove = (Building_WorkTable)
            ThingMaker.MakeThing(Stove, GenStuff.DefaultStuffFor(Stove));
        stove.SetFactionDirect(Faction.OfPlayer);
        _ = GenSpawn.Spawn(stove, map.Center, map);
        return stove;
    }

    [Test]
    public static void JobIgnoresWorkTablesOnAnotherMap()
    {
        using var scene = new ManagerScene();
        var other = scene.AddExtraMap();
        var here = AddStove(scene, 0);
        var there = AddStoveOn(other);
        var job = AddProductionJob(scene);

        RunJob(scene, job);

        Expect.AreEqual(1, BillsOn(here).Count());
        Expect.AreEqual(0, BillsOn(there).Count());
        Expect.AreEqual(1, job.ManagedBills.Count);
        Expect.IsTrue(job.ManagedBills.All(b => b.billStack == here.billStack));
    }

    [Test]
    public static void JobWithOnlyAnotherMapsWorkTableAddsNoBills()
    {
        using var scene = new ManagerScene();
        var other = scene.AddExtraMap();
        var there = AddStoveOn(other);
        var job = AddProductionJob(scene);

        RunJob(scene, job);

        Expect.AreEqual(0, BillsOn(there).Count());
        Expect.IsEmpty(job.ManagedBills);
    }

    [Test]
    public static void JobsOnDifferentMapsEachManageOnlyTheirOwnMapsBills()
    {
        using var scene = new ManagerScene();
        var other = scene.AddExtraMap();
        using var otherScene = new ManagerScene(other);
        var here = AddStove(scene, 0);
        var there = AddStoveOn(other);
        var job = AddProductionJob(scene);
        var otherJob = AddProductionJob(otherScene);

        RunJob(scene, job);
        RunJob(otherScene, otherJob);

        Expect.AreEqual(1, BillsOn(here).Count());
        Expect.AreEqual(1, BillsOn(there).Count());
        Expect.ReferencesAreEqual(BillsOn(here).Single(), job.ManagedBills.Single());
        Expect.ReferencesAreEqual(BillsOn(there).Single(), otherJob.ManagedBills.Single());

        job.CleanUp();
        Expect.AreEqual(0, BillsOn(here).Count());
        Expect.AreEqual(1, BillsOn(there).Count(), "Cleaning up one map leaves the other's bills");
    }

    // Scribing of managed bills

    private sealed class BillsHarness(Manager manager, Bill_Production[] bills, IExposable inner)
        : IExposable
    {
        public void ExposeData()
        {
            foreach (var bill in bills)
            {
                if (Scribe.mode == LoadSaveMode.Saving)
                {
                    Scribe.saver.loadIDsErrorsChecker.RegisterDeepSaved(bill, "bill");
                }
                else if (Scribe.mode == LoadSaveMode.LoadingVars)
                {
                    Scribe.loader.crossRefs.loadedObjectDirectory.RegisterLoaded(bill);
                }
            }
            new ScribeRoundTrip.ManagerHarness(manager, inner).ExposeData();
        }
    }

    private static Bill_Production ManagedBillAfterARun(
        ManagerScene scene,
        ManagerJob_Production job
    )
    {
        _ = AddStove(scene, 0);
        RunJob(scene, job);
        return job.ManagedBills.Single();
    }

    [Test]
    public static void ManagedBillsAreSavedByIdAndResolvedOnLoad()
    {
        using var scene = new ManagerScene();
        var job = AddProductionJob(scene);
        var bill = ManagedBillAfterARun(scene, job);
        var loaded = new ScribeRoundTrip.JobListHarness(scene.Manager);
        Bill_Production[] bills = [bill];

        ScribeRoundTrip.Run(
            new BillsHarness(
                scene.Manager,
                bills,
                new ScribeRoundTrip.JobListHarness(scene.Manager, [job])
            ),
            new BillsHarness(scene.Manager, bills, loaded)
        );

        var loadedJob = (ManagerJob_Production)loaded.Jobs.Single();
        Expect.AreEqual(1, loadedJob.ManagedBills.Count);
        Expect.ReferencesAreEqual(bill, loadedJob.ManagedBills[0]);
    }

    [Test]
    [WarningsAllowed("Could not resolve reference to object with loadID Bill_")]
    public static void ManagedBillsThatNoLongerExistAreDroppedOnLoad()
    {
        using var scene = new ManagerScene();
        var job = AddProductionJob(scene);
        var bill = ManagedBillAfterARun(scene, job);
        var loaded = new ScribeRoundTrip.JobListHarness(scene.Manager);

        ScribeRoundTrip.Run(
            new BillsHarness(
                scene.Manager,
                [bill],
                new ScribeRoundTrip.JobListHarness(scene.Manager, [job])
            ),
            new BillsHarness(scene.Manager, [], loaded)
        );

        var loadedJob = (ManagerJob_Production)loaded.Jobs.Single();
        Expect.IsEmpty(loadedJob.ManagedBills);
        Expect.AreEqual(0, loadedJob.ExpectedYield);
    }

    [Test]
    public static void DeletedManagedBillsAreNotSaved()
    {
        using var scene = new ManagerScene();
        var first = AddStove(scene, 0);
        var second = AddStove(scene, 1);
        var job = AddProductionJob(scene);
        RunJob(scene, job);
        var deleted = BillsOn(first).Single();
        var kept = BillsOn(second).Single();
        first.billStack.Delete(deleted);
        var loaded = new ScribeRoundTrip.JobListHarness(scene.Manager);

        var xml = ScribeRoundTrip.Save(
            new BillsHarness(
                scene.Manager,
                [kept],
                new ScribeRoundTrip.JobListHarness(scene.Manager, [job])
            )
        );
        Expect.IsFalse(xml.Contains(deleted.GetUniqueLoadID(), StringComparison.Ordinal));

        ScribeRoundTrip.Load(xml, new BillsHarness(scene.Manager, [kept], loaded));
        var loadedJob = (ManagerJob_Production)loaded.Jobs.Single();
        Expect.ReferencesAreEqual(kept, loadedJob.ManagedBills.Single());
    }

    [Test]
    public static void ManagedBillsAreNotWrittenWhenTheJobIsExportedToAnotherMap()
    {
        using var scene = new ManagerScene();
        var job = AddProductionJob(scene);
        var bill = ManagedBillAfterARun(scene, job);
        var manager = scene.Manager;
        var previousMap = manager.ScribeSameMapData;
        var previousGame = manager.ScribeSameGameData;
        manager.ScribeSameMapData = false;
        manager.ScribeSameGameData = false;
        try
        {
            var loaded = new ScribeRoundTrip.JobListHarness(manager);
            Bill_Production[] bills = [bill];
            var xml = ScribeRoundTrip.Save(
                new BillsHarness(manager, bills, new ScribeRoundTrip.JobListHarness(manager, [job]))
            );

            Expect.IsFalse(xml.Contains("managedBills", StringComparison.Ordinal));

            ScribeRoundTrip.Load(xml, new BillsHarness(manager, bills, loaded));
            Expect.IsEmpty(((ManagerJob_Production)loaded.Jobs.Single()).ManagedBills);
        }
        finally
        {
            manager.ScribeSameMapData = previousMap;
            manager.ScribeSameGameData = previousGame;
        }
    }
}
