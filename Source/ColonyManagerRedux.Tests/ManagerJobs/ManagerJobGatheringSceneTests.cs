// ManagerJobGatheringSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// Full gather-and-execute passes of the Hunting, Foraging and Forestry jobs against real
/// animals, plants and trees, plus how each adopts designations the player placed.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobGatheringSceneTests
{
    private const int FarTarget = 100000;

    private static IntVec3 Slot(ManagerScene scene, int i, int row = 0) =>
        scene.Cells(1, 1, -60 + (i * 3), -40 + (row * 3)).First();

    private static void RunJob(ManagerScene scene, ManagerJob job, Trigger_Threshold trigger)
    {
        scene.Map.resourceCounter.UpdateResourceCounts();
        _ = trigger.GetCurrentCount(cached: false);
        job.Untouch();
        ManagerScene.RunToCompletion(scene.Manager.TryDoWork());
    }

    private static void Configure(Trigger_Threshold trigger, ThingDef counted, int target)
    {
        trigger.ThresholdFilter.SetDisallowAll();
        trigger.ThresholdFilter.SetAllow(counted, true);
        trigger.Op = Trigger_Threshold.Ops.LowerThan;
        trigger.TargetCount = target;
    }

    private static Designation Designate(ManagerScene scene, Thing thing, DesignationDef def)
    {
        var designation = new Designation(thing, def);
        scene.Map.designationManager.AddDesignation(designation);
        return designation;
    }

    private static int GameDesignations(ManagerScene scene, DesignationDef def) =>
        scene.Map.designationManager.SpawnedDesignationsOfDef(def).Count();

    private static ManagerLog NewLog(ManagerJob job) => new(job) { _workDone = true };

    // Hunting

    private static readonly PawnKindDef Prey = PawnKindDefOf.Muffalo;

    private static ManagerJob_Hunting NewHunting(ManagerScene scene, int target = FarTarget)
    {
        var job = scene.AddJob<ManagerJob_Hunting>("CM_HuntingManager");
        job.ShouldCheckReachable = false;
        job.HuntingGrounds = null;
        job.InvertHuntingGrounds = false;
        job.AllowedAnimals.Clear();
        _ = job.AllowedAnimals.Add(Prey);
        Configure(job.TriggerThreshold, Prey.race.race.meatDef, target);
        return job;
    }

    private static Pawn WildPrey(ManagerScene scene, int slot, int row = 0) =>
        scene.SpawnAnimal(Prey, Slot(scene, slot, row));

    [Test]
    public static void HuntingDesignatesAnAllowedWildAnimalWhenTheTargetIsUnmet()
    {
        using var scene = new ManagerScene();
        var job = NewHunting(scene);
        var animal = WildPrey(scene, 0);

        RunJob(scene, job, job.TriggerThreshold);

        Expect.IsNotNull(scene.Map.designationManager.DesignationOn(animal, DesignationDefOf.Hunt));
        Expect.AreEqual(1, job.Designations.Count);
        Expect.IsNotEmpty(scene.NewLogs);
    }

    [Test]
    public static void HuntingLeavesUnsuitableAnimalsAlone()
    {
        using var scene = new ManagerScene();
        var job = NewHunting(scene);
        var tame = scene.SpawnAnimal(Prey, Slot(scene, 0), Faction.OfPlayer);
        var otherKind = scene.SpawnAnimal(PawnKindDef.Named("Alpaca"), Slot(scene, 1));
        var fogged = WildPrey(scene, 2);
        var fogIndex = scene.Map.cellIndices.CellToIndex(fogged.Position);
        var fogGrid = scene.Map.fogGrid.fogGrid;
        var wasFogged = fogGrid.IsSet(fogIndex);
        fogGrid.Set(fogIndex, true);
        try
        {
            RunJob(scene, job, job.TriggerThreshold);
        }
        finally
        {
            fogGrid.Set(fogIndex, wasFogged);
        }

        Expect.IsNull(scene.Map.designationManager.DesignationOn(tame, DesignationDefOf.Hunt));
        Expect.IsNull(scene.Map.designationManager.DesignationOn(otherKind, DesignationDefOf.Hunt));
        Expect.IsNull(scene.Map.designationManager.DesignationOn(fogged, DesignationDefOf.Hunt));
        Expect.IsEmpty(job.Designations);
    }

    [Test]
    public static void HuntingIgnoresAnimalsOutsideTheHuntingGrounds()
    {
        using var scene = new ManagerScene();
        var job = NewHunting(scene);
        var animal = WildPrey(scene, 0);
        job.HuntingGrounds = scene.AddArea(
            "CMRTest-hunting-elsewhere",
            [animal.Position + new IntVec3(20, 0, 0)]
        );

        RunJob(scene, job, job.TriggerThreshold);

        Expect.IsNull(scene.Map.designationManager.DesignationOn(animal, DesignationDefOf.Hunt));

        job.HuntingGrounds = scene.AddArea("CMRTest-hunting-here", [animal.Position]);
        RunJob(scene, job, job.TriggerThreshold);

        Expect.IsNotNull(scene.Map.designationManager.DesignationOn(animal, DesignationDefOf.Hunt));
    }

    [Test]
    public static void HuntingRemovesDesignationsOnceTheyMoreThanCoverTheTarget()
    {
        using var scene = new ManagerScene();
        var animals = new[] { WildPrey(scene, 0), WildPrey(scene, 1), WildPrey(scene, 2) };
        var job = NewHunting(scene);
        Expect.IsTrue(
            animals[0].EstimatedYield(job.TargetResource) > 0,
            "The animal yields nothing."
        );
        job.TriggerThreshold.TargetCount = 1;
        foreach (var animal in animals)
        {
            _ = Designate(scene, animal, DesignationDefOf.Hunt);
        }

        RunJob(scene, job, job.TriggerThreshold);

        Expect.AreEqual(1, GameDesignations(scene, DesignationDefOf.Hunt));
        Expect.AreEqual(1, job.Designations.Count);
    }

    [Test]
    public static void HuntingCompletesAndCleansUpWhenTheTargetIsAlreadyMet()
    {
        using var scene = new ManagerScene();
        var job = NewHunting(scene);
        var animal = WildPrey(scene, 0);
        RunJob(scene, job, job.TriggerThreshold);
        Expect.AreEqual(1, job.Designations.Count, "Precondition: the job designated the animal.");
        Expect.IsNotNull(scene.Map.designationManager.DesignationOn(animal, DesignationDefOf.Hunt));

        job.TriggerThreshold.TargetCount = 0;
        RunJob(scene, job, job.TriggerThreshold);

        Expect.AreEqual(ManagerJobState.Completed, job.JobState);
        Expect.AreEqual(0, GameDesignations(scene, DesignationDefOf.Hunt));
    }

    [Test]
    public static void HuntingUnforbidsAForbiddenCorpseOfAnAllowedAnimal()
    {
        using var scene = new ManagerScene();
        var job = NewHunting(scene);
        job.UnforbidCorpses = true;
        var corpse = scene.SpawnCorpse(Prey, Slot(scene, 0));
        corpse.SetForbidden(true, warnOnFail: false);

        RunJob(scene, job, job.TriggerThreshold);

        Expect.IsFalse(corpse.IsForbidden(Faction.OfPlayer));
    }

    [Test]
    public static void HuntingLeavesCorpsesForbiddenWhenUnforbiddingIsOff()
    {
        using var scene = new ManagerScene();
        var job = NewHunting(scene);
        job.UnforbidCorpses = false;
        var corpse = scene.SpawnCorpse(Prey, Slot(scene, 0));
        corpse.SetForbidden(true, warnOnFail: false);

        RunJob(scene, job, job.TriggerThreshold);

        Expect.IsTrue(corpse.IsForbidden(Faction.OfPlayer));
    }

    [Test]
    public static void HuntingAdoptsOnlyRelevantGameDesignationsAndOnlyOnce()
    {
        using var scene = new ManagerScene();
        var job = NewHunting(scene);
        var wanted = WildPrey(scene, 0);
        var tame = scene.SpawnAnimal(Prey, Slot(scene, 1), Faction.OfPlayer);
        var otherKind = scene.SpawnAnimal(PawnKindDef.Named("Alpaca"), Slot(scene, 2));
        var adopted = Designate(scene, wanted, DesignationDefOf.Hunt);
        _ = Designate(scene, tame, DesignationDefOf.Hunt);
        _ = Designate(scene, otherKind, DesignationDefOf.Hunt);
        var log = NewLog(job);

        job.AddRelevantGameDesignations(log);
        job.AddRelevantGameDesignations(log);

        Expect.AreEqual(1, job.Designations.Count);
        Expect.IsTrue(job.Designations.Contains(adopted));
        Expect.AreEqual(1, log.Details.Count());
    }

    // Foraging

    private static readonly ThingDef Bush = DefDatabase<ThingDef>.GetNamed("Plant_Berry");

    private static ManagerJob_Foraging NewForaging(ManagerScene scene, int target = FarTarget)
    {
        var job = scene.AddJob<ManagerJob_Foraging>("CM_ForagingManager");
        job.ShouldCheckReachable = false;
        job.ForagingArea = null;
        job.InvertForagingArea = false;
        job.AllowedPlants.Clear();
        _ = job.AllowedPlants.Add(Bush);
        Configure(job.TriggerThreshold, Bush.plant.harvestedThingDef, target);
        return job;
    }

    private static Plant Grown(ManagerScene scene, ThingDef def, int slot, int row)
    {
        var plant = (Plant)scene.SpawnThing(def, Slot(scene, slot, row));
        plant.Growth = 1f;
        return plant;
    }

    [Test]
    public static void ForagingDesignatesAnAllowedMaturePlantWhenTheTargetIsUnmet()
    {
        using var scene = new ManagerScene();
        var job = NewForaging(scene);
        var plant = Grown(scene, Bush, 0, 1);

        RunJob(scene, job, job.TriggerThreshold);

        Expect.IsNotNull(
            scene.Map.designationManager.DesignationOn(plant, DesignationDefOf.HarvestPlant)
        );
        Expect.AreEqual(1, job.Designations.Count);
    }

    [Test]
    public static void ForagingIgnoresPlantsItDoesNotAllowOrOutsideItsArea()
    {
        using var scene = new ManagerScene();
        var job = NewForaging(scene);
        var other = Grown(scene, DefDatabase<ThingDef>.GetNamed("Plant_Dandelion"), 0, 1);
        var away = Grown(scene, Bush, 1, 1);
        job.ForagingArea = scene.AddArea("CMRTest-foraging-elsewhere", [other.Position]);

        RunJob(scene, job, job.TriggerThreshold);

        Expect.IsNull(
            scene.Map.designationManager.DesignationOn(other, DesignationDefOf.HarvestPlant)
        );
        Expect.IsNull(
            scene.Map.designationManager.DesignationOn(away, DesignationDefOf.HarvestPlant)
        );
        Expect.IsEmpty(job.Designations);
    }

    [Test]
    public static void ForagingRemovesDesignationsOnceTheyMoreThanCoverTheTarget()
    {
        using var scene = new ManagerScene();
        var plants = new[]
        {
            Grown(scene, Bush, 0, 1),
            Grown(scene, Bush, 1, 1),
            Grown(scene, Bush, 2, 1),
        };
        var job = NewForaging(scene);
        Expect.IsTrue(plants[0].YieldNow() > 0, "The plant yields nothing.");
        job.TriggerThreshold.TargetCount = 1;
        foreach (var plant in plants)
        {
            _ = Designate(scene, plant, DesignationDefOf.HarvestPlant);
        }

        RunJob(scene, job, job.TriggerThreshold);

        Expect.AreEqual(1, GameDesignations(scene, DesignationDefOf.HarvestPlant));
        Expect.AreEqual(1, job.Designations.Count);
    }

    [Test]
    public static void ForagingAdoptsOnlyRelevantGameDesignationsAndOnlyOnce()
    {
        using var scene = new ManagerScene();
        var job = NewForaging(scene);
        var wanted = Grown(scene, Bush, 0, 1);
        var other = Grown(scene, DefDatabase<ThingDef>.GetNamed("Plant_Dandelion"), 1, 1);
        var adopted = Designate(scene, wanted, DesignationDefOf.HarvestPlant);
        _ = Designate(scene, other, DesignationDefOf.HarvestPlant);
        var log = NewLog(job);

        job.AddRelevantGameDesignations(log);
        job.AddRelevantGameDesignations(log);

        Expect.AreEqual(1, job.Designations.Count);
        Expect.IsTrue(job.Designations.Contains(adopted));
        Expect.AreEqual(1, log.Details.Count());
    }

    // Forestry

    private static readonly ThingDef Oak = DefDatabase<ThingDef>.GetNamed("Plant_TreeOak");

    private static ManagerJob_Forestry NewForestry(ManagerScene scene, int target = FarTarget)
    {
        var job = scene.AddJob<ManagerJob_Forestry>("CM_ForestryManager");
        job.ShouldCheckReachable = false;
        job.LoggingArea = null;
        job.InvertLoggingArea = false;
        job.AllowSaplings = false;
        job.ClearAreas.Clear();
        job.AllowedTrees.Clear();
        _ = job.AllowedTrees.Add(Oak);
        Configure(job.TriggerThreshold, Oak.plant.harvestedThingDef, target);
        return job;
    }

    [Test]
    public static void ForestryDesignatesAnAllowedMatureTreeWhenTheTargetIsUnmet()
    {
        using var scene = new ManagerScene();
        var job = NewForestry(scene);
        var tree = Grown(scene, Oak, 0, 2);

        RunJob(scene, job, job.TriggerThreshold);

        Expect.IsNotNull(
            scene.Map.designationManager.DesignationOn(tree, DesignationDefOf.HarvestPlant)
        );
        Expect.AreEqual(1, job.Designations.Count);
    }

    [Test]
    public static void ForestryLeavesSaplingsAloneUnlessTheyAlreadyYield()
    {
        using var scene = new ManagerScene();
        var job = NewForestry(scene);
        var sapling = (Plant)scene.SpawnThing(Oak, Slot(scene, 0, 2));
        sapling.Growth = 0.02f;

        RunJob(scene, job, job.TriggerThreshold);

        Expect.IsNull(
            scene.Map.designationManager.DesignationOn(sapling, DesignationDefOf.HarvestPlant)
        );
    }

    [Test]
    public static void ForestryRemovesDesignationsOnceTheyMoreThanCoverTheTarget()
    {
        using var scene = new ManagerScene();
        var trees = new[]
        {
            Grown(scene, Oak, 0, 2),
            Grown(scene, Oak, 1, 2),
            Grown(scene, Oak, 2, 2),
        };
        var job = NewForestry(scene);
        Expect.IsTrue(trees[0].YieldNow() > 0, "The tree yields nothing.");
        job.TriggerThreshold.TargetCount = 1;
        foreach (var tree in trees)
        {
            _ = Designate(scene, tree, DesignationDefOf.HarvestPlant);
        }

        RunJob(scene, job, job.TriggerThreshold);

        Expect.AreEqual(1, GameDesignations(scene, DesignationDefOf.HarvestPlant));
        Expect.AreEqual(1, job.Designations.Count);
    }

    [Test]
    public static void ForestryAdoptsOnlyRelevantGameDesignationsAndOnlyOnce()
    {
        using var scene = new ManagerScene();
        var job = NewForestry(scene);
        var wanted = Grown(scene, Oak, 0, 2);
        var other = Grown(scene, DefDatabase<ThingDef>.GetNamed("Plant_TreePoplar"), 1, 2);
        var adopted = Designate(scene, wanted, DesignationDefOf.HarvestPlant);
        _ = Designate(scene, other, DesignationDefOf.HarvestPlant);
        var log = NewLog(job);

        job.AddRelevantGameDesignations(log);
        job.AddRelevantGameDesignations(log);

        Expect.AreEqual(1, job.Designations.Count);
        Expect.IsTrue(job.Designations.Contains(adopted));
        Expect.AreEqual(1, log.Details.Count());
    }

    // Forestry clear-cut areas

    private static (
        ManagerJob_Forestry Job,
        Plant Allowed,
        Plant Disallowed,
        Plant Designated,
        Plant Farmed,
        Plant Potted,
        Area Area
    ) ClearCutScene(ManagerScene scene)
    {
        var job = NewForestry(scene);
        job.Type = ManagerJob_Forestry.ForestryJobType.ClearArea;
        var allowed = Grown(scene, Oak, 0, 3);
        var disallowed = Grown(scene, DefDatabase<ThingDef>.GetNamed("Plant_TreePoplar"), 1, 3);
        var designated = Grown(scene, Oak, 2, 3);
        var farmed = Grown(scene, Oak, 3, 3);
        var potted = Grown(scene, Oak, 4, 3);
        _ = Designate(scene, designated, DesignationDefOf.CutPlant);
        _ = scene.AddGrowingZone("CMRTest-clear-grow", [farmed.Position]);
        _ = scene.SpawnBuilding(
            DefDatabase<ThingDef>.GetNamed("HydroponicsBasin"),
            potted.Position
        );
        var area = scene.AddArea(
            "CMRTest-clear-cut",
            new[] { allowed, disallowed, designated, farmed, potted }.Select(p => p.Position)
        );
        _ = job.ClearAreas.Add(area);
        return (job, allowed, disallowed, designated, farmed, potted, area);
    }

    [Test]
    public static void ClearCutPlansOnlyPlantsThatPassEveryCheck()
    {
        using var scene = new ManagerScene();
        var (job, allowed, _, _, _, _, area) = ClearCutScene(scene);
        var data = new ManagerJob_Forestry.ForestryWorkData();

        ManagerScene.RunToCompletion(job.PlanClearAreaDesignations(NewLog(job), area, data));

        Expect.AreEqual(1, data.ClearAreaDesignationsToAdd.Count);
        Expect.ReferencesAreEqual(allowed, data.ClearAreaDesignationsToAdd[0].Target);
    }

    [Test]
    public static void ClearCutWithNothingToCutLogsNoValidTargets()
    {
        using var scene = new ManagerScene();
        var job = NewForestry(scene);
        var area = scene.AddArea("CMRTest-clear-empty", [Slot(scene, 0, 3)]);
        var log = new ManagerLog(job) { _workDone = false };
        var data = new ManagerJob_Forestry.ForestryWorkData();

        ManagerScene.RunToCompletion(job.PlanClearAreaDesignations(log, area, data));

        Expect.IsEmpty(data.ClearAreaDesignationsToAdd);
        Expect.AreEqual(1, log.Details.Count());
    }

    [Test]
    public static void ClearCutPassDesignatesTheTreeForCutting()
    {
        using var scene = new ManagerScene();
        var (job, allowed, disallowed, _, farmed, potted, _) = ClearCutScene(scene);

        RunJob(scene, job, job.TriggerThreshold);

        Expect.IsNotNull(
            scene.Map.designationManager.DesignationOn(allowed, DesignationDefOf.CutPlant)
        );
        Expect.AreEqual(0, GameDesignations(scene, DesignationDefOf.HarvestPlant));
        Expect.IsNull(
            scene.Map.designationManager.DesignationOn(disallowed, DesignationDefOf.CutPlant)
        );
        Expect.IsNull(
            scene.Map.designationManager.DesignationOn(farmed, DesignationDefOf.CutPlant)
        );
        Expect.IsNull(
            scene.Map.designationManager.DesignationOn(potted, DesignationDefOf.CutPlant)
        );
    }

    // Execute-add re-validation

    private static void ExecuteHuntingAdd(ManagerJob_Hunting job, Pawn animal, ManagerLog log) =>
        ManagerScene.RunToCompletion(
            job.ExecuteAddDesignations(
                log,
                new ManagerJob_Hunting.HuntingWorkData { DesignationsToAdd = [(animal, 10, 10)] },
                new Boxed<bool>()
            )
        );

    private static void ExecuteForagingAdd(ManagerJob_Foraging job, Plant plant, ManagerLog log) =>
        ManagerScene.RunToCompletion(
            job.ExecuteAddDesignations(
                log,
                new ManagerJob_Foraging.ForagingWorkData { DesignationsToAdd = [(plant, 10, 10)] },
                100,
                1,
                new Boxed<bool>()
            )
        );

    private static void ExecuteForestryAdd(ManagerJob_Forestry job, Plant tree, ManagerLog log) =>
        ManagerScene.RunToCompletion(
            job.ExecuteAddDesignations(
                log,
                new ManagerJob_Forestry.ForestryWorkData { DesignationsToAdd = [(tree, 10, 10)] },
                new Boxed<bool>()
            )
        );

    [Test]
    public static void HuntingExecuteAddSkipsAnimalDespawnedSincePlanning()
    {
        using var scene = new ManagerScene();
        var job = NewHunting(scene);
        var animal = WildPrey(scene, 0);
        var log = NewLog(job);
        animal.DeSpawn();

        ExecuteHuntingAdd(job, animal, log);

        Expect.IsEmpty(job.Designations);
        Expect.IsEmpty(log.Details);
        animal.Destroy();
    }

    [Test]
    public static void HuntingExecuteAddSkipsAnimalKilledSincePlanning()
    {
        using var scene = new ManagerScene();
        var job = NewHunting(scene);
        var animal = WildPrey(scene, 0);
        var log = NewLog(job);
        animal.Kill(null);
        animal.Corpse.Destroy();

        ExecuteHuntingAdd(job, animal, log);

        Expect.IsEmpty(job.Designations);
        Expect.IsEmpty(log.Details);
    }

    [Test]
    public static void HuntingExecuteAddSkipsAnimalAlreadyDesignatedForHunting()
    {
        using var scene = new ManagerScene();
        var job = NewHunting(scene);
        var animal = WildPrey(scene, 0);
        var log = NewLog(job);
        _ = Designate(scene, animal, DesignationDefOf.Hunt);

        ExecuteHuntingAdd(job, animal, log);

        Expect.IsEmpty(job.Designations);
        Expect.IsEmpty(log.Details);
        Expect.AreEqual(1, GameDesignations(scene, DesignationDefOf.Hunt));
    }

    [Test]
    public static void ForagingExecuteAddSkipsPlantDespawnedSincePlanning()
    {
        using var scene = new ManagerScene();
        var job = NewForaging(scene);
        var plant = Grown(scene, Bush, 0, 1);
        var log = NewLog(job);
        plant.DeSpawn();

        ExecuteForagingAdd(job, plant, log);

        Expect.IsEmpty(job.Designations);
        Expect.IsEmpty(log.Details);
        plant.Destroy();
    }

    [Test]
    public static void ForagingExecuteAddSkipsPlantAlreadyDesignatedForHarvest()
    {
        using var scene = new ManagerScene();
        var job = NewForaging(scene);
        var plant = Grown(scene, Bush, 0, 1);
        var log = NewLog(job);
        _ = Designate(scene, plant, DesignationDefOf.HarvestPlant);

        ExecuteForagingAdd(job, plant, log);

        Expect.IsEmpty(job.Designations);
        Expect.IsEmpty(log.Details);
        Expect.AreEqual(1, GameDesignations(scene, DesignationDefOf.HarvestPlant));
    }

    [Test]
    public static void ForestryExecuteAddSkipsTreeDespawnedSincePlanning()
    {
        using var scene = new ManagerScene();
        var job = NewForestry(scene);
        var tree = Grown(scene, Oak, 0, 2);
        var log = NewLog(job);
        tree.DeSpawn();

        ExecuteForestryAdd(job, tree, log);

        Expect.IsEmpty(job.Designations);
        Expect.IsEmpty(log.Details);
        tree.Destroy();
    }

    [Test]
    public static void ForestryExecuteAddSkipsTreeAlreadyDesignatedForHarvest()
    {
        using var scene = new ManagerScene();
        var job = NewForestry(scene);
        var tree = Grown(scene, Oak, 0, 2);
        var log = NewLog(job);
        _ = Designate(scene, tree, DesignationDefOf.HarvestPlant);

        ExecuteForestryAdd(job, tree, log);

        Expect.IsEmpty(job.Designations);
        Expect.IsEmpty(log.Details);
        Expect.AreEqual(1, GameDesignations(scene, DesignationDefOf.HarvestPlant));
    }

    [Test]
    public static void ExecuteAddStillDesignatesAFreshSpawnedTarget()
    {
        using var scene = new ManagerScene();
        var job = NewForestry(scene);
        var tree = Grown(scene, Oak, 0, 2);
        var log = NewLog(job);

        ExecuteForestryAdd(job, tree, log);

        Expect.AreEqual(1, job.Designations.Count);
        Expect.IsNotNull(
            scene.Map.designationManager.DesignationOn(tree, DesignationDefOf.HarvestPlant)
        );
    }
}
