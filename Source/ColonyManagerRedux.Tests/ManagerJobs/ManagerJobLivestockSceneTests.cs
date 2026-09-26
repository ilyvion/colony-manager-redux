// ManagerJobLivestockSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;
using static ColonyManagerRedux.Managers.ManagerJob_Livestock;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The parts of <see cref="ManagerJob_Livestock"/> that act on real tame animals: the culling
/// actions, adopting designations placed by the player, and loading the old butcherExcess flag.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobLivestockSceneTests
{
    private static readonly PawnKindDef Kind = PawnKindDefOf.Muffalo;
    private const AgeAndSex TestAgeSex = AgeAndSex.AdultFemale;

    private static ManagerJob_Livestock NewJob(
        ManagerScene scene,
        LivestockCullingStrategy strategy = LivestockCullingStrategy.Butcher
    )
    {
        var job = scene.AddJob<ManagerJob_Livestock>("CM_LivestockManager");
        job.TriggerPawnKind.pawnKind = Kind;
        job.CullingStrategy = strategy;
        InvalidateCaches(scene.Manager);
        return job;
    }

    // The livestock caches expire by game tick and the tests never advance the game, so anything
    // cached by an earlier test would otherwise be served here.
    private static void InvalidateCaches(Manager manager)
    {
        var caches = manager.LivestockCaches();
        var mapId = manager.map.uniqueID;
        foreach (var ageSex in Utilities_Livestock.AgeSexArray)
        {
            caches.AllSexedCache.Invalidate((Kind, mapId, ageSex));
            caches.TameSexedCache.Invalidate((Kind, mapId, ageSex, true));
            caches.TameSexedCache.Invalidate((Kind, mapId, ageSex, false));
        }
        caches.AllCache.Invalidate((Kind, mapId));
        caches.TameCache.Invalidate((Kind, mapId, true));
        caches.TameCache.Invalidate((Kind, mapId, false));
    }

    private static Pawn Tame(ManagerScene scene, int slot)
    {
        var pawn = scene.SpawnAnimal(
            Kind,
            scene.Cells(1, 1, 40 + (slot * 2), 10).First(),
            Faction.OfPlayer
        );
        pawn.gender = Gender.Female;
        pawn.ageTracker.AgeBiologicalTicks = 10L * GenDate.TicksPerYear;
        return pawn;
    }

    private static Pawn Wild(ManagerScene scene, int slot) =>
        scene.SpawnAnimal(Kind, scene.Cells(1, 1, 40 + (slot * 2), 14).First());

    private static Designation Designate(ManagerScene scene, Pawn pawn, DesignationDef def)
    {
        var designation = new Designation(pawn, def);
        scene.Map.designationManager.AddDesignation(designation);
        return designation;
    }

    private static ManagerLog NewLog(ManagerJob job) => new(job) { _workDone = true };

    // PostMake

    /// <summary>
    /// Runs <paramref name="assertions"/> against a job made while the livestock defaults are
    /// <paramref name="defaults"/> plus <paramref name="overrideForKind"/> as the override for
    /// <paramref name="kind"/> (when given), restoring the live settings afterwards.
    /// </summary>
    private static void WithSettings(
        PawnKindSettings defaults,
        PawnKindSettings? overrideForKind,
        Action<ManagerJob_Livestock> assertions,
        PawnKindDef? kind = null
    )
    {
        kind ??= Kind;
        var def = DefDatabase<ManagerDef>.GetNamed("CM_LivestockManager");
        var live =
            ColonyManagerReduxMod.Settings.ManagerDefaultSettingsFor<ManagerDefaultSettings_Livestock>(
                def
            )!;
        var savedDefaults = live.defaults;
        var savedOverrides = live.overrides;
        try
        {
            live.defaults = defaults;
            live.overrides = overrideForKind is null ? [] : new() { [kind] = overrideForKind };

            using var scene = new ManagerScene();
            var job = scene.AddJob<ManagerJob_Livestock>("CM_LivestockManager");
            job.TriggerPawnKind.pawnKind = kind;
            job.PostMake();
            assertions(job);
        }
        finally
        {
            live.defaults = savedDefaults;
            live.overrides = savedOverrides;
        }
    }

    // Muffalo cannot be trained; Husky can learn both tameness and obedience.
    private static readonly PawnKindDef TrainableKind = DefDatabase<PawnKindDef>.GetNamed("Husky");

    private static PawnKindSettings Configured(
        int countOffset,
        bool tameMore,
        TrainableDef target
    ) =>
        new()
        {
            DefaultCountTargets =
            [
                .. Utilities_Livestock.AgeSexArray.Select((_, i) => countOffset + i),
            ],
            DefaultTryTameMore = tameMore,
            DefaultTamePastTargets = tameMore,
            DefaultCullingStrategy = LivestockCullingStrategy.Release,
            DefaultCullTrained = tameMore,
            EnabledTrainingTargets = [target],
        };

    [Test]
    public static void PostMakeSeedsTheJobFromTheSharedDefaultsWithoutAnOverride()
    {
        var defaults = Configured(20, true, TrainableDefOf.Obedience);

        WithSettings(
            defaults,
            null,
            job =>
            {
                Expect.AreEqual(
                    string.Join(",", defaults.DefaultCountTargets),
                    string.Join(",", job.TriggerPawnKind.CountTargets)
                );
                Expect.IsTrue(job.TryTameMore);
                Expect.IsTrue(job.TamePastTargets);
                Expect.IsTrue(job.CullTrained);
                Expect.AreEqual(LivestockCullingStrategy.Release, job.CullingStrategy);
                Expect.IsTrue(job.Training[TrainableDefOf.Obedience]);
                Expect.IsTrue(job.Training[TrainableDefOf.Tameness]);
            },
            TrainableKind
        );
    }

    [Test]
    public static void PostMakePrefersTheOverrideForItsPawnKind()
    {
        var defaults = Configured(20, true, TrainableDefOf.Obedience);
        var @override = Configured(50, false, TrainableDefOf.Tameness);
        @override.DefaultCullingStrategy = LivestockCullingStrategy.Butcher;

        WithSettings(
            defaults,
            @override,
            job =>
            {
                Expect.AreEqual(
                    string.Join(",", @override.DefaultCountTargets),
                    string.Join(",", job.TriggerPawnKind.CountTargets)
                );
                Expect.IsFalse(job.TryTameMore);
                Expect.IsFalse(job.TamePastTargets);
                Expect.IsFalse(job.CullTrained);
                Expect.AreEqual(LivestockCullingStrategy.Butcher, job.CullingStrategy);
                Expect.IsTrue(job.Training[TrainableDefOf.Tameness]);
                Expect.IsFalse(job.Training[TrainableDefOf.Obedience]);
            },
            TrainableKind
        );
    }

    [Test]
    public static void CountTargetsAndDefaultCountTargetsHaveTheSameLength() =>
        WithSettings(
            new PawnKindSettings(),
            null,
            job =>
                Expect.AreEqual(
                    new PawnKindSettings().DefaultCountTargets.Length,
                    job.TriggerPawnKind.CountTargets.Length
                )
        );

    // DesignationCullingAction

    [Test]
    public static void DesignationCullAddsATrackedGameDesignation()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var animal = Tame(scene, 0);
        var action = (DesignationCullingAction)job.CullingStrategyAction;

        Expect.IsFalse(action.IsAlreadyCulling(animal));
        action.Cull(animal);

        Expect.IsTrue(action.IsAlreadyCulling(animal));
        Expect.IsNotNull(
            scene.Map.designationManager.DesignationOn(animal, DesignationDefOf.Slaughter)
        );
        Expect.AreEqual(1, job.Designations.Count);
    }

    [Test]
    public static void DesignationPeekReturnsAtMostTheRequestedCountNewestFirst()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var animals = new[] { Tame(scene, 0), Tame(scene, 1), Tame(scene, 2) };
        var action = job.CullingStrategyAction;
        foreach (var animal in animals)
        {
            action.Cull(animal);
        }

        var two = action.PeekCullingToStop(TestAgeSex, 2);
        var many = action.PeekCullingToStop(TestAgeSex, 10);

        Expect.That(() => two.SequenceEqual([animals[2], animals[1]]));
        Expect.AreEqual(3, many.Count);
        Expect.IsEmpty(action.PeekCullingToStop(TestAgeSex, 0));
        Expect.IsEmpty(action.PeekCullingToStop(AgeAndSex.AdultMale, 3));
    }

    [Test]
    public static void DesignationStopCullingRemovesItAndASecondStopIsANoOp()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var animal = Tame(scene, 0);
        var other = Tame(scene, 1);
        var action = job.CullingStrategyAction;
        action.Cull(animal);
        action.Cull(other);

        action.StopCulling(animal);
        action.StopCulling(animal);

        Expect.IsFalse(action.IsAlreadyCulling(animal));
        Expect.IsNull(
            scene.Map.designationManager.DesignationOn(animal, DesignationDefOf.Slaughter)
        );
        Expect.IsTrue(action.IsAlreadyCulling(other));
        Expect.AreEqual(1, job.Designations.Count);
    }

    [Test]
    public static void DesignationActionRemovesAnimalsAndCountsOnlyItsOwnDesignation()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var animal = Tame(scene, 0);
        var action = job.CullingStrategyAction;

        action.Cull(animal);
        InvalidateCaches(scene.Manager);

        Expect.IsTrue(action.CullingRemovesAnimals);
        Expect.AreEqual(1, action.GetAlreadyCullingCountForAgeSex(TestAgeSex));
        Expect.AreEqual(0, action.GetAlreadyCulledForAgeSex(TestAgeSex));
    }

    // SterilizeCullingAction

    private static Bill_Medical OtherSurgeryBill() => new(RecipeDefOf.RemoveBodyPart, null);

    [Test]
    public static void SterilizeCullAddsASterilizeBill()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene, LivestockCullingStrategy.Sterilize);
        var animal = Tame(scene, 0);
        var action = job.CullingStrategyAction;

        Expect.IsFalse(action.IsAlreadyCulling(animal));
        action.Cull(animal);

        Expect.IsTrue(action.IsAlreadyCulling(animal));
        Expect.IsFalse(action.IsAlreadyCulled(animal));
        Expect.IsFalse(action.CullingRemovesAnimals);
    }

    [Test]
    public static void SterilizeStopCullingRemovesOnlySterilizeBills()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene, LivestockCullingStrategy.Sterilize);
        var animal = Tame(scene, 0);
        var action = job.CullingStrategyAction;
        action.Cull(animal);
        animal.health.surgeryBills.AddBill(OtherSurgeryBill());

        action.StopCulling(animal);

        Expect.IsFalse(action.IsAlreadyCulling(animal));
        Expect.AreEqual(1, animal.health.surgeryBills.Bills.Count);
        Expect.AreEqual(
            RecipeDefOf.RemoveBodyPart,
            ((Bill_Medical)animal.health.surgeryBills.Bills[0]).recipe
        );
    }

    [Test]
    public static void SterilizeStopCullingWithoutABillLeavesOtherBillsAlone()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene, LivestockCullingStrategy.Sterilize);
        var animal = Tame(scene, 0);
        animal.health.surgeryBills.AddBill(OtherSurgeryBill());

        job.CullingStrategyAction.StopCulling(animal);

        Expect.AreEqual(1, animal.health.surgeryBills.Bills.Count);
    }

    [Test]
    public static void SterilizedAnimalCountsAsCulledNotCulling()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene, LivestockCullingStrategy.Sterilize);
        var animal = Tame(scene, 0);
        var action = job.CullingStrategyAction;
        _ = animal.health.AddHediff(HediffDefOf.Sterilized);
        InvalidateCaches(scene.Manager);

        Expect.IsTrue(action.IsAlreadyCulled(animal));
        Expect.IsFalse(action.IsAlreadyCulling(animal));
        Expect.AreEqual(1, action.GetAlreadyCulledForAgeSex(TestAgeSex));
        Expect.AreEqual(0, action.GetAlreadyCullingCountForAgeSex(TestAgeSex));
    }

    [Test]
    public static void SterilizePeekReturnsOnlyAnimalsWithABillUpToTheCount()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene, LivestockCullingStrategy.Sterilize);
        var animals = new[] { Tame(scene, 0), Tame(scene, 1), Tame(scene, 2) };
        var action = job.CullingStrategyAction;
        action.Cull(animals[0]);
        action.Cull(animals[2]);
        InvalidateCaches(scene.Manager);

        var peeked = action.PeekCullingToStop(TestAgeSex, 1);
        var all = action.PeekCullingToStop(TestAgeSex, 5);

        Expect.AreEqual(1, peeked.Count);
        Expect.AreEqual(2, all.Count);
        Expect.IsFalse(all.Contains(animals[1]));
        Expect.AreEqual(2, action.GetAlreadyCullingCountForAgeSex(TestAgeSex));
    }

    // AddRelevantGameDesignations

    [Test]
    public static void AddRelevantGameDesignationsAdoptsOnlyThisKindsDesignations()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var slaughtered = Tame(scene, 0);
        var wild = Wild(scene, 0);
        var otherKind = scene.SpawnAnimal(
            PawnKindDef.Named("Alpaca"),
            scene.Cells(1, 1, 60, 10).First(),
            Faction.OfPlayer
        );
        var slaughter = Designate(scene, slaughtered, DesignationDefOf.Slaughter);
        var tame = Designate(scene, wild, DesignationDefOf.Tame);
        _ = Designate(scene, otherKind, DesignationDefOf.Slaughter);
        var log = NewLog(job);

        ManagerScene.RunToCompletion(job.AddRelevantGameDesignations(log));

        var adopted = job.Designations;
        Expect.AreEqual(2, adopted.Count);
        Expect.IsTrue(adopted.Contains(slaughter));
        Expect.IsTrue(adopted.Contains(tame));
    }

    [Test]
    public static void AddRelevantGameDesignationsAdoptsEachDesignationOnlyOnce()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        _ = Designate(scene, Tame(scene, 0), DesignationDefOf.Slaughter);
        var log = NewLog(job);

        ManagerScene.RunToCompletion(job.AddRelevantGameDesignations(log));
        ManagerScene.RunToCompletion(job.AddRelevantGameDesignations(log));

        Expect.AreEqual(1, job.Designations.Count);
        Expect.AreEqual(1, log.Details.Count());
    }

    [Test]
    public static void AddRelevantGameDesignationsLogsNothingWhenThereIsNothingToAdopt()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var log = NewLog(job);

        ManagerScene.RunToCompletion(job.AddRelevantGameDesignations(log));

        Expect.IsEmpty(job.Designations);
        Expect.IsEmpty(log.Details);
    }

    [Test]
    public static void AddRelevantGameDesignationsSkipsTheCullingDesignationWhenNotCulling()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene, LivestockCullingStrategy.None);
        _ = Designate(scene, Tame(scene, 0), DesignationDefOf.Slaughter);
        var log = NewLog(job);

        ManagerScene.RunToCompletion(job.AddRelevantGameDesignations(log));

        Expect.IsEmpty(job.Designations);
    }

    // ForwardCompatibleButcherExcess

    private static ManagerJob_Livestock LoadWithButcherExcess(ManagerScene scene, string? value)
    {
        var job = NewJob(scene, LivestockCullingStrategy.Release);
        var xml = ScribeRoundTrip.Save(
            new ScribeRoundTrip.ManagerHarness(
                scene.Manager,
                new ScribeRoundTrip.JobListHarness(scene.Manager, [job])
            )
        );
        const string anchor = "<cullingStrategy>Release</cullingStrategy>";
        Expect.IsTrue(
            xml.Contains(anchor, StringComparison.Ordinal),
            "The saved job has no cullingStrategy element."
        );
        if (value != null)
        {
            xml = xml.Replace(
                anchor,
                $"<butcherExcess>{value}</butcherExcess>{anchor}",
                StringComparison.Ordinal
            );
        }

        var loaded = new ScribeRoundTrip.JobListHarness(scene.Manager);
        ScribeRoundTrip.Load(xml, new ScribeRoundTrip.ManagerHarness(scene.Manager, loaded));
        return (ManagerJob_Livestock)loaded.Jobs.Single();
    }

    [Test]
    public static void OldButcherExcessFalseLoadsAsNoCulling()
    {
        using var scene = new ManagerScene();

        var loaded = LoadWithButcherExcess(scene, "false");

        Expect.AreEqual(LivestockCullingStrategy.None, loaded.CullingStrategy);
    }

    [Test]
    public static void OldButcherExcessTrueKeepsTheSavedStrategy()
    {
        using var scene = new ManagerScene();

        var loaded = LoadWithButcherExcess(scene, "true");

        Expect.AreEqual(LivestockCullingStrategy.Release, loaded.CullingStrategy);
    }

    [Test]
    public static void MissingButcherExcessKeepsTheSavedStrategy()
    {
        using var scene = new ManagerScene();

        var loaded = LoadWithButcherExcess(scene, null);

        Expect.AreEqual(LivestockCullingStrategy.Release, loaded.CullingStrategy);
    }

    // PlanMaster

    private static Pawn TameWithMaster(ManagerScene scene, int slot, Pawn master)
    {
        var animal = Tame(scene, slot);
        // The Master property only accepts pawns with the skill to train the animal.
        animal.playerSettings.master = master;
        return animal;
    }

    [Test]
    public static void PlanMasterPlansASpecificMasterTheAnimalDoesNotHave()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var animal = Tame(scene, 0);
        var colonist = scene.SpawnColonist(scene.Cells(1, 1, 40, 18).First());
        var data = new LivestockWorkData();

        job.PlanMaster(animal, MasterMode.Specific, colonist, data);

        Expect.AreEqual(1, data.MastersToSet.Count);
        Expect.ReferencesAreEqual(animal, data.MastersToSet[0].Animal);
        Expect.ReferencesAreEqual(colonist, data.MastersToSet[0].Master);
    }

    [Test]
    public static void PlanMasterPlansNothingWhenTheAnimalAlreadyHasTheSpecificMaster()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var colonist = scene.SpawnColonist(scene.Cells(1, 1, 40, 18).First());
        var animal = TameWithMaster(scene, 0, colonist);
        var data = new LivestockWorkData();

        job.PlanMaster(animal, MasterMode.Specific, colonist, data);

        Expect.IsEmpty(data.MastersToSet);
    }

    [Test]
    public static void PlanMasterPlansToClearTheMasterWhenNoSpecificMasterIsChosen()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var colonist = scene.SpawnColonist(scene.Cells(1, 1, 40, 18).First());
        var animal = TameWithMaster(scene, 0, colonist);
        var data = new LivestockWorkData();

        job.PlanMaster(animal, MasterMode.Specific, null, data);

        Expect.AreEqual(1, data.MastersToSet.Count);
        Expect.IsNull(data.MastersToSet[0].Master);
    }

    [Test]
    public static void PlanMasterPlansNothingWithNoSpecificMasterAndNoCurrentMaster()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var animal = Tame(scene, 0);
        var data = new LivestockWorkData();

        job.PlanMaster(animal, MasterMode.Specific, null, data);

        Expect.IsEmpty(data.MastersToSet);
    }

    [Test]
    public static void PlanMasterPlansNothingInManualMode()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var animal = Tame(scene, 0);
        var colonist = scene.SpawnColonist(scene.Cells(1, 1, 40, 18).First());
        var data = new LivestockWorkData();

        job.PlanMaster(animal, MasterMode.Manual, colonist, data);

        Expect.IsEmpty(data.MastersToSet);
    }
}
