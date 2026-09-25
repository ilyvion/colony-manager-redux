// ManagerJobProductionLinkedSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;
using static ColonyManagerRedux.Managers.ManagerJob_Production;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The Production job's gather pass: producers linked to consumers, the reserved-stock cap, store
/// group validity and in-place bill updates. The decisions themselves are covered by the
/// pure-function tests.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobProductionLinkedSceneTests
{
    private const int ConsumerTarget = 30;

    private static readonly ThingDef Stove = DefDatabase<ThingDef>.GetNamed("FueledStove");
    private static readonly RecipeDef MealRecipe = DefDatabase<RecipeDef>.GetNamed(
        "CookMealSimple"
    );
    private static readonly RecipeDef ButcherRecipe = DefDatabase<RecipeDef>.GetNamed(
        "ButcherCorpseFlesh"
    );

    private static readonly ThingDef Muffalo = DefDatabase<ThingDef>.GetNamed("Muffalo");
    private static readonly ThingDef Grizzly = DefDatabase<ThingDef>.GetNamed("Bear_Grizzly");
    private static ThingDef MuffaloMeat => Muffalo.race.meatDef;
    private static ThingDef BearMeat => Grizzly.race.meatDef;

    private static ManagerJob_Production AddJob(
        ManagerScene scene,
        RecipeDef recipe,
        bool managed = true
    )
    {
        var job = scene.AddJob<ManagerJob_Production>("CM_ProductionManager", managed);
        job.Recipe = recipe;
        return job;
    }

    private static ManagerJob_Production AddProducer(ManagerScene scene) =>
        AddJob(scene, ButcherRecipe);

    /// <summary>
    /// A meal job that maintains <see cref="ConsumerTarget"/> meals from <paramref name="meat"/>
    /// only and is linked to <paramref name="producer"/>. It is unmanaged, so it never runs
    /// itself.
    /// </summary>
    private static ManagerJob_Production AddConsumer(
        ManagerScene scene,
        ManagerJob_Production producer,
        ThingDef meat,
        ProductionMode mode = ProductionMode.MaintainStock
    )
    {
        var consumer = AddJob(scene, MealRecipe, managed: false);
        consumer.Mode = mode;
        consumer.TriggerThreshold.TargetCount = ConsumerTarget;
        consumer.AllowedIngredients.Clear();
        _ = consumer.AllowedIngredients.Add(meat);
        _ = consumer.LinkedProducers.Add(producer);
        return consumer;
    }

    private static int DemandFor(ManagerJob_Production consumer, ThingDef meat) =>
        ComputeIngredientDemand(
            consumer.Recipe!,
            EffectiveLinkedDemandBufferCount(
                consumer.LinkedDemandBufferCount,
                consumer.TriggerThreshold.TargetCount
            ),
            [meat]
        );

    private static void RunJob(ManagerScene scene, ManagerJob_Production job)
    {
        scene.Map.resourceCounter.UpdateResourceCounts();
        _ = job.TriggerThreshold.GetCurrentCount(cached: false);
        job.Untouch();
        ManagerScene.RunToCompletion(scene.Manager.TryDoWork());
    }

    private static Building_WorkTable AddTable(ManagerScene scene, ThingDef def, int index) =>
        (Building_WorkTable)
            scene.SpawnBuilding(def, scene.Cells(1, 1, offsetX: -10, offsetZ: index * 4).First());

    private static IEnumerable<Bill_Production> BillsOn(Building_WorkTable table) =>
        table.billStack.Bills.OfType<Bill_Production>();

    private static ThingDef CorpseOf(ThingDef race) => race.race.corpseDef;

    // Linked producers

    [Test]
    public static void LinkedTargetCountsOnlyMaintainStockConsumers()
    {
        using var scene = new ManagerScene();
        var producer = AddProducer(scene);
        producer.AutoTargetFromLinks = true;
        var maintaining = AddConsumer(scene, producer, MuffaloMeat);
        _ = AddConsumer(scene, producer, BearMeat, ProductionMode.ConsumeSurplus);
        var expected = DemandFor(maintaining, MuffaloMeat);
        Expect.IsTrue(expected > 0, "The fixture consumer has a real ingredient demand");

        RunJob(scene, producer);

        Expect.AreEqual(expected, producer.TriggerThreshold.TargetCount);
    }

    [Test]
    public static void LinkedTargetSumsConsumerDemandsByDefault()
    {
        using var scene = new ManagerScene();
        var producer = AddProducer(scene);
        producer.AutoTargetFromLinks = true;
        var muffaloConsumer = AddConsumer(scene, producer, MuffaloMeat);
        var bearConsumer = AddConsumer(scene, producer, BearMeat);
        bearConsumer.TriggerThreshold.TargetCount = 10;
        var expected = DemandFor(muffaloConsumer, MuffaloMeat) + DemandFor(bearConsumer, BearMeat);

        RunJob(scene, producer);

        Expect.AreEqual(expected, producer.TriggerThreshold.TargetCount);
    }

    [Test]
    public static void LinkedTargetUsesTheLargestConsumerWhenAggregatingByMax()
    {
        using var scene = new ManagerScene();
        var producer = AddProducer(scene);
        producer.AutoTargetFromLinks = true;
        producer.DemandAggregation = LinkedDemandAggregation.MaxOfConsumers;
        var muffaloConsumer = AddConsumer(scene, producer, MuffaloMeat);
        var bearConsumer = AddConsumer(scene, producer, BearMeat);
        bearConsumer.TriggerThreshold.TargetCount = 10;
        var expected = Math.Max(
            DemandFor(muffaloConsumer, MuffaloMeat),
            DemandFor(bearConsumer, BearMeat)
        );

        RunJob(scene, producer);

        Expect.AreEqual(expected, producer.TriggerThreshold.TargetCount);
    }

    [Test]
    public static void LinkedTargetIsLeftAloneWithoutTheOptIn()
    {
        using var scene = new ManagerScene();
        var producer = AddProducer(scene);
        producer.TriggerThreshold.TargetCount = 7;
        _ = AddConsumer(scene, producer, MuffaloMeat);

        RunJob(scene, producer);

        Expect.AreEqual(7, producer.TriggerThreshold.TargetCount);
    }

    [Test]
    public static void LinkedTargetIgnoresAConsumerWithoutARecipe()
    {
        using var scene = new ManagerScene();
        var producer = AddProducer(scene);
        producer.AutoTargetFromLinks = true;
        producer.AutoRestrictIngredientsFromLinks = true;
        var recipeless = AddJob(scene, MealRecipe, managed: false);
        recipeless.Recipe = null;
        _ = recipeless.LinkedProducers.Add(producer);
        var real = AddConsumer(scene, producer, MuffaloMeat);
        var expected = DemandFor(real, MuffaloMeat);

        RunJob(scene, producer);

        Expect.IsNull(producer.CausedException, "The gather pass didn't fail");
        Expect.AreEqual(expected, producer.TriggerThreshold.TargetCount);
    }

    [Test]
    public static void LinkedIngredientsAreRestrictedToWhatEveryConsumerWants()
    {
        using var scene = new ManagerScene();
        var producer = AddProducer(scene);
        producer.AutoRestrictIngredientsFromLinks = true;
        _ = AddConsumer(scene, producer, MuffaloMeat);
        _ = AddConsumer(scene, producer, BearMeat, ProductionMode.ConsumeSurplus);
        Expect.IsFalse(
            ReferenceEquals(MuffaloMeat, BearMeat),
            "The two races yield different meats"
        );

        RunJob(scene, producer);

        Expect.IsTrue(producer.AllowedIngredients.Contains(CorpseOf(Muffalo)));
        Expect.IsTrue(producer.AllowedIngredients.Contains(CorpseOf(Grizzly)));
        Expect.IsFalse(producer.AllowedIngredients.Contains(ThingDefOf.Human.race.corpseDef));
        Expect.IsTrue(
            producer.AllowedIngredients.All(d =>
                DefDatabase<ThingDef>.AllDefsListForReading.Any(r =>
                    r.race?.corpseDef == d
                    && (
                        ReferenceEquals(r.race.meatDef, MuffaloMeat)
                        || ReferenceEquals(r.race.meatDef, BearMeat)
                    )
                )
            ),
            "Every allowed corpse yields one of the wanted meats"
        );
    }

    [Test]
    public static void LinkedIngredientsAreKeptWhenNoConsumerWantsAnythingTheProducerMakes()
    {
        using var scene = new ManagerScene();
        var producer = AddProducer(scene);
        producer.AutoRestrictIngredientsFromLinks = true;
        var before = producer.AllowedIngredients.ToHashSet();
        Expect.IsNotEmpty(before);
        var consumer = AddConsumer(scene, producer, MuffaloMeat);
        consumer.AllowedIngredients.Clear();
        _ = consumer.AllowedIngredients.Add(ThingDefOf.RawPotatoes);

        RunJob(scene, producer);

        Expect.That(
            () => producer.AllowedIngredients.SetEquals(before),
            "Nothing wanted from this producer leaves its ingredients alone"
        );
    }

    [Test]
    public static void LinkedIngredientsAreKeptWithoutAnyConsumers()
    {
        using var scene = new ManagerScene();
        var producer = AddProducer(scene);
        producer.AutoRestrictIngredientsFromLinks = true;
        var before = producer.AllowedIngredients.ToHashSet();

        RunJob(scene, producer);

        Expect.That(() => producer.AllowedIngredients.SetEquals(before));
    }

    // Reserved stock

    private static ManagerJob_Production AddReservingMealJob(ManagerScene scene)
    {
        var job = AddJob(scene, MealRecipe);
        job.TriggerThreshold.TargetCount = ConsumerTarget;
        job.AllowedIngredients.Clear();
        _ = job.AllowedIngredients.Add(MuffaloMeat);
        return job;
    }

    private static int MuffaloMeatPerMeal() =>
        IngredientCountPerIteration(MealRecipe, [MuffaloMeat]);

    private static int MuffaloMeatOnMap(ManagerScene scene)
    {
        var filter = new ThingFilter();
        filter.SetAllow(MuffaloMeat, true);
        scene.Map.resourceCounter.UpdateResourceCounts();
        return scene.Map.CountProducts(filter, countAllOnMap: true);
    }

    private static void StockMuffaloMeat(ManagerScene scene, int total)
    {
        var stockpile = scene.AddStockpile(scene.Cells(6, 6, offsetX: 10));
        var cells = stockpile.Cells.ToList();
        var remaining = total;
        var index = 0;
        while (remaining > 0)
        {
            var stack = Math.Min(remaining, 25);
            _ = scene.SpawnThing(MuffaloMeat, cells[index++], stack);
            remaining -= stack;
        }
    }

    [Test]
    public static void StockAtTheReserveCompletesTheJobAndAddsNoBill()
    {
        using var scene = new ManagerScene();
        var stove = AddTable(scene, Stove, 0);
        var perMeal = MuffaloMeatPerMeal();
        Expect.IsTrue(perMeal > 0);
        var job = AddReservingMealJob(scene);
        var baseline = MuffaloMeatOnMap(scene);
        StockMuffaloMeat(scene, perMeal * 5);
        job.ReservedStockOverrides[MuffaloMeat] = baseline + (perMeal * 5);

        RunJob(scene, job);

        Expect.AreEqual(ManagerJobState.Completed, job.JobState);
        Expect.AreEqual(0, BillsOn(stove).Count());
    }

    [Test]
    public static void StockAboveTheReserveCapsTheScheduledIterations()
    {
        using var scene = new ManagerScene();
        var stove = AddTable(scene, Stove, 0);
        var perMeal = MuffaloMeatPerMeal();
        var job = AddReservingMealJob(scene);
        var baseline = MuffaloMeatOnMap(scene);
        StockMuffaloMeat(scene, perMeal * 8);
        job.ReservedStockOverrides[MuffaloMeat] = baseline + (perMeal * 5);

        RunJob(scene, job);

        Expect.AreEqual(ManagerJobState.Active, job.JobState);
        var bill = BillsOn(stove).SingleOrDefault();
        Expect.IsNotNull(bill);
        Expect.AreEqual(3, bill?.repeatCount);
    }

    [Test]
    public static void NoReserveLeavesTheScheduleUncapped()
    {
        using var scene = new ManagerScene();
        var stove = AddTable(scene, Stove, 0);
        var job = AddReservingMealJob(scene);

        RunJob(scene, job);

        Expect.AreEqual(ManagerJobState.Active, job.JobState);
        Expect.AreEqual(ConsumerTarget, BillsOn(stove).Single().repeatCount);
    }

    // Store group

    [Test]
    public static void DeletedStockpileResetsTheStoreGroupAndTheManagedBills()
    {
        using var scene = new ManagerScene();
        var stove = AddTable(scene, Stove, 0);
        var stockpile = scene.AddStockpile(scene.Cells(3, 3, offsetX: 20));
        var job = AddJob(scene, MealRecipe);
        job.TriggerThreshold.TargetCount = ConsumerTarget;
        job.StoreMode = BillStoreModeDefOf.SpecificStockpile;
        job.StoreGroup = stockpile.slotGroup;
        RunJob(scene, job);
        var bill = BillsOn(stove).Single();
        Expect.AreEqual(BillStoreModeDefOf.SpecificStockpile, bill.GetStoreMode());

        stockpile.Delete();
        RunJob(scene, job);

        Expect.IsNull(job.StoreGroup);
        Expect.AreEqual(BillStoreModeDefOf.BestStockpile, job.StoreMode);
        Expect.AreEqual(BillStoreModeDefOf.BestStockpile, bill.GetStoreMode());
    }

    [Test]
    public static void ExistingStockpileKeepsTheStoreGroup()
    {
        using var scene = new ManagerScene();
        _ = AddTable(scene, Stove, 0);
        var stockpile = scene.AddStockpile(scene.Cells(3, 3, offsetX: 20));
        var job = AddJob(scene, MealRecipe);
        job.TriggerThreshold.TargetCount = ConsumerTarget;
        job.StoreMode = BillStoreModeDefOf.SpecificStockpile;
        job.StoreGroup = stockpile.slotGroup;

        RunJob(scene, job);

        Expect.ReferencesAreEqual(stockpile.slotGroup, job.StoreGroup);
        Expect.AreEqual(BillStoreModeDefOf.SpecificStockpile, job.StoreMode);
    }

    // Updating existing bills

    [Test]
    public static void ChangedSettingsUpdateTheExistingBillInPlace()
    {
        using var scene = new ManagerScene();
        var stove = AddTable(scene, Stove, 0);
        var stockpile = scene.AddStockpile(scene.Cells(3, 3, offsetX: 20));
        var job = AddJob(scene, MealRecipe);
        job.TriggerThreshold.TargetCount = ConsumerTarget;
        RunJob(scene, job);
        var bill = BillsOn(stove).Single();

        job.AllowedSkillRange = new IntRange(3, 12);
        job.IngredientSearchRadius = 25f;
        job.StoreMode = BillStoreModeDefOf.SpecificStockpile;
        job.StoreGroup = stockpile.slotGroup;
        var restricted = job.AllowedIngredients.First();
        job.AllowedIngredients.Clear();
        _ = job.AllowedIngredients.Add(restricted);
        RunJob(scene, job);

        Expect.AreEqual(1, BillsOn(stove).Count());
        Expect.ReferencesAreEqual(bill, BillsOn(stove).Single());
        Expect.AreEqual(new IntRange(3, 12), bill.allowedSkillRange);
        Expect.AreEqual(25f, bill.ingredientSearchRadius);
        Expect.AreEqual(BillStoreModeDefOf.SpecificStockpile, bill.GetStoreMode());
        Expect.ReferencesAreEqual(stockpile.slotGroup, bill.GetSlotGroup());
        Expect.That(
            () => bill.ingredientFilter.AllowedThingDefs.ToHashSet().SetEquals([restricted]),
            "The bill's ingredient filter follows the job"
        );
    }

    // ConsumeSurplus

    private static ManagerJob_Production AddSurplusJob(ManagerScene scene)
    {
        var job = AddJob(scene, MealRecipe);
        job.Mode = ProductionMode.ConsumeSurplus;
        job.TriggerThreshold.ThresholdFilter.SetDisallowAll();
        job.TriggerThreshold.ThresholdFilter.SetAllow(MuffaloMeat, true);
        job.TriggerThreshold.TargetCount = 10;
        return job;
    }

    [Test]
    public static void SurplusActivatesAndSuspendsTheBillWithTheTrigger()
    {
        using var scene = new ManagerScene();
        var stove = AddTable(scene, Stove, 0);
        var stockpile = scene.AddStockpile(scene.Cells(3, 3, offsetX: 20));
        var job = AddSurplusJob(scene);
        var baseline = job.TriggerThreshold.GetCurrentCount(cached: false);
        var meat = scene.SpawnThing(MuffaloMeat, stockpile.Cells.First(), 40);

        RunJob(scene, job);

        var bill = BillsOn(stove).SingleOrDefault();
        Expect.IsNotNull(bill);
        if (bill == null)
        {
            return;
        }
        Expect.AreEqual(BillRepeatModeDefOf.Forever, bill.repeatMode);
        Expect.IsFalse(bill.suspended, "A surplus above the target runs the bill");

        meat.Destroy();
        RunJob(scene, job);
        Expect.AreEqual(baseline, job.TriggerThreshold.GetCurrentCount(cached: false));
        Expect.IsTrue(bill.suspended, "Losing the surplus suspends the bill");

        _ = scene.SpawnThing(MuffaloMeat, stockpile.Cells.First(), 40);
        RunJob(scene, job);
        Expect.IsFalse(bill.suspended, "Regaining the surplus resumes the bill");
        Expect.AreEqual(1, BillsOn(stove).Count());
    }

    // Specific workbenches

    [Test]
    public static void DestroyedSpecificWorkbenchesArePruned()
    {
        using var scene = new ManagerScene();
        var kept = AddTable(scene, Stove, 0);
        var destroyed = AddTable(scene, Stove, 1);
        var job = AddJob(scene, MealRecipe);
        job.TriggerThreshold.TargetCount = ConsumerTarget;
        job.AssignmentMode = WorkbenchAssignmentMode.Specific;
        _ = job.SpecificWorkbenches.Add(kept);
        _ = job.SpecificWorkbenches.Add(destroyed);
        destroyed.Destroy();

        RunJob(scene, job);

        Expect.AreEqual(1, job.SpecificWorkbenches.Count);
        Expect.IsTrue(job.SpecificWorkbenches.Contains(kept));
        Expect.AreEqual(1, BillsOn(kept).Count());
    }
}
