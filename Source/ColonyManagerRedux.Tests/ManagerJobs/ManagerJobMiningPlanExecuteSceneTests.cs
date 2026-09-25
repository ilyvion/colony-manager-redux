// ManagerJobMiningPlanExecuteSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;
using static ColonyManagerRedux.Managers.ManagerJob_Mining;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The reduce planning loops and the execute phases of <see cref="ManagerJob_Mining"/> against
/// real designations, veins, buildings and deep drills.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobMiningPlanExecuteSceneTests
{
    private static ThingDef SteelVein => DefDatabase<ThingDef>.GetNamed("MineableSteel");

    private static ManagerJob_Mining NewJob(ManagerScene scene)
    {
        var job = scene.AddJob<ManagerJob_Mining>("CM_MiningManager");
        job.CheckRoofSupport = false;
        job.CheckRoomDivision = false;
        job.ShouldCheckReachable = false;
        job.AllowedMinerals.Clear();
        _ = job.AllowedMinerals.Add(SteelVein);
        job.AllowedBuildings.Clear();
        _ = job.AllowedBuildings.Add(ThingDefOf.Wall);
        job.TriggerThreshold.ThresholdFilter.SetAllow(ThingDefOf.Steel, true);
        job.TriggerThreshold.Op = Trigger_Threshold.Ops.LowerThan;
        return job;
    }

    private static ManagerLog NewLog(ManagerJob job) => new(job) { _workDone = true };

    private static IntVec3 Slot(ManagerScene scene, int i) =>
        scene.Cells(1, 1, 60 + (i * 3), -30).First();

    private static Mineable DesignatedVein(ManagerScene scene, ManagerJob_Mining job, int slot)
    {
        var vein = (Mineable)scene.SpawnThing(SteelVein, Slot(scene, slot));
        job.AddDesignation(new Designation(vein, DesignationDefOf.Mine));
        return vein;
    }

    private static Building DesignatedWall(ManagerScene scene, ManagerJob_Mining job, int slot)
    {
        var wall = (Building)
            scene.SpawnThing(ThingDefOf.Wall, Slot(scene, slot), stuff: ThingDefOf.Steel);
        job.AddDesignation(new Designation(wall, DesignationDefOf.Deconstruct));
        return wall;
    }

    private static Thing DesignatedChunk(ManagerScene scene, ManagerJob_Mining job, int slot)
    {
        var chunk = scene.SpawnThing(ThingDef.Named("ChunkSlagSteel"), Slot(scene, slot));
        job.AddDesignation(new Designation(chunk, DesignationDefOf.Haul));
        return chunk;
    }

    // Mine designations may be recorded against the vein or its cell; find either.
    private static Designation? DesignationOf(
        ManagerScene scene,
        Thing thing,
        DesignationDef def
    ) =>
        scene.Map.designationManager.AllDesignations.FirstOrDefault(d =>
            d.def == def
            && (d.target.Thing == thing || (!d.target.HasThing && d.target.Cell == thing.Position))
        );

    private static MiningWorkData Plan(ManagerJob_Mining job, int count)
    {
        var data = new MiningWorkData { Kind = MiningWorkKind.ReduceDesignations };
        ManagerScene.RunToCompletion(
            job.PlanReduceDesignations(NewLog(job), new Boxed<int>(count), data)
        );
        return data;
    }

    // PlanReduceDesignations - mine designations

    [Test]
    public static void ReducePlansRemovalsDownToTheTarget()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var veins = new[]
        {
            DesignatedVein(scene, job, 0),
            DesignatedVein(scene, job, 1),
            DesignatedVein(scene, job, 2),
        };
        var yield = job.GetCountInMineral(veins[0]);
        Expect.IsTrue(yield > 0, "The vein yields nothing.");
        job.TriggerThreshold.TargetCount = yield;

        var data = Plan(job, 3 * yield);

        // Removing two leaves the count at the target; a third would drop below it.
        Expect.AreEqual(2, data.MineDesignationsToRemove.Count);
        Expect.That(() =>
            data
                .MineDesignationsToRemove.Select(d => d.CountAfter)
                .OrderBy(c => c)
                .SequenceEqual([yield, 2 * yield])
        );
        Expect.All(data.MineDesignationsToRemove, d => d.Yield == yield);
        Expect.IsEmpty(data.DeconstructDesignationsToRemove);
        Expect.IsEmpty(data.HaulDesignationsToRemove);
    }

    [Test]
    public static void ReducePlansEveryRemovalWhenTheTargetIsZero()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var veins = new[]
        {
            DesignatedVein(scene, job, 0),
            DesignatedVein(scene, job, 1),
            DesignatedVein(scene, job, 2),
        };
        var yield = job.GetCountInMineral(veins[0]);
        job.TriggerThreshold.TargetCount = 0;

        var data = Plan(job, 3 * yield);

        Expect.AreEqual(3, data.MineDesignationsToRemove.Count);
    }

    [Test]
    public static void ReducePlansNothingAndLogsItWhenRemovingWouldFallBelowTheTarget()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var vein = DesignatedVein(scene, job, 0);
        var yield = job.GetCountInMineral(vein);
        job.TriggerThreshold.TargetCount = 5 * yield;
        var log = new ManagerLog(job) { _workDone = false };
        var data = new MiningWorkData { Kind = MiningWorkKind.ReduceDesignations };

        ManagerScene.RunToCompletion(job.PlanReduceDesignations(log, new Boxed<int>(yield), data));

        Expect.IsEmpty(data.MineDesignationsToRemove);
        Expect.AreEqual(1, log.Details.Count());
    }

    [Test]
    public static void ReducePlansOnlyDesignationsOfTheJobItself()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var tracked = DesignatedVein(scene, job, 0);
        var untracked = (Mineable)scene.SpawnThing(SteelVein, Slot(scene, 1));
        scene.Map.designationManager.AddDesignation(
            new Designation(untracked, DesignationDefOf.Mine)
        );
        job.TriggerThreshold.TargetCount = 0;

        var data = Plan(job, 2 * job.GetCountInMineral(tracked));

        Expect.AreEqual(1, data.MineDesignationsToRemove.Count);
    }

    // PlanReduceDesignations - deconstruct and haul designations

    [Test]
    public static void ReducePlansDeconstructRemovalsWhenThereAreNoMineDesignations()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var walls = new[]
        {
            DesignatedWall(scene, job, 0),
            DesignatedWall(scene, job, 1),
            DesignatedWall(scene, job, 2),
        };
        var yield = job.GetCountInBuilding(walls[0]);
        Expect.IsTrue(yield > 0, "The wall yields nothing.");
        job.TriggerThreshold.TargetCount = yield;

        var data = Plan(job, 3 * yield);

        Expect.IsEmpty(data.MineDesignationsToRemove);
        Expect.AreEqual(2, data.DeconstructDesignationsToRemove.Count);
        Expect.IsEmpty(data.HaulDesignationsToRemove);
    }

    [Test]
    public static void ReducePlansHaulRemovalsWhenThereAreNoOtherDesignations()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var chunks = new[]
        {
            DesignatedChunk(scene, job, 0),
            DesignatedChunk(scene, job, 1),
            DesignatedChunk(scene, job, 2),
        };
        var yield = job.GetCountInChunk(chunks[0]);
        if (yield <= 0)
        {
            Test.Skip("The chunk yields nothing towards the threshold.");
            return;
        }
        job.TriggerThreshold.TargetCount = yield;

        var data = Plan(job, 3 * yield);

        Expect.IsEmpty(data.MineDesignationsToRemove);
        Expect.IsEmpty(data.DeconstructDesignationsToRemove);
        Expect.AreEqual(2, data.HaulDesignationsToRemove.Count);
    }

    [Test]
    public static void ReduceMovesOnToLaterKindsOnceTheEarlierOnesAreAllPlanned()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var vein = DesignatedVein(scene, job, 0);
        var wall = DesignatedWall(scene, job, 1);
        job.TriggerThreshold.TargetCount = 0;

        var data = Plan(job, job.GetCountInMineral(vein) + job.GetCountInBuilding(wall));

        Expect.AreEqual(1, data.MineDesignationsToRemove.Count);
        Expect.AreEqual(1, data.DeconstructDesignationsToRemove.Count);
    }

    [Test]
    public static void ReduceCarriesTheUnremovedCandidatesYieldIntoTheNextKind()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var vein = DesignatedVein(scene, job, 0);
        var wall = DesignatedWall(scene, job, 1);
        var veinYield = job.GetCountInMineral(vein);
        var wallYield = job.GetCountInBuilding(wall);
        Expect.IsTrue(
            veinYield > wallYield && wallYield > 0,
            "Precondition: the vein yields more than the wall."
        );
        // Removing the vein would drop below the target, so it stays; the wall alone is small
        // enough to remove while the vein still keeps the count at the target.
        job.TriggerThreshold.TargetCount = veinYield;

        var data = Plan(job, veinYield + wallYield);

        Expect.IsEmpty(data.MineDesignationsToRemove);
        Expect.AreEqual(1, data.DeconstructDesignationsToRemove.Count);
        Expect.AreEqual(veinYield, data.DeconstructDesignationsToRemove[0].CountAfter);
    }

    // ExecuteReduceDesignations

    [Test]
    public static void ExecuteReduceDeletesPlannedDesignationsAndLogsThem()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var vein = DesignatedVein(scene, job, 0);
        var wall = DesignatedWall(scene, job, 1);
        var chunk = DesignatedChunk(scene, job, 2);
        var untouched = DesignatedVein(scene, job, 3);
        var data = new MiningWorkData
        {
            Kind = MiningWorkKind.ReduceDesignations,
            MineDesignationsToRemove =
            [
                (DesignationOf(scene, vein, DesignationDefOf.Mine)!, 10, 90),
            ],
            DeconstructDesignationsToRemove =
            [
                (DesignationOf(scene, wall, DesignationDefOf.Deconstruct)!, 5, 85),
            ],
            HaulDesignationsToRemove =
            [
                (DesignationOf(scene, chunk, DesignationDefOf.Haul)!, 2, 83),
            ],
        };
        var log = NewLog(job);
        var workDone = new Boxed<bool>();

        ManagerScene.RunToCompletion(job.ExecuteReduceDesignations(log, data, workDone));

        Expect.IsNull(DesignationOf(scene, vein, DesignationDefOf.Mine));
        Expect.IsNull(DesignationOf(scene, wall, DesignationDefOf.Deconstruct));
        Expect.IsNull(DesignationOf(scene, chunk, DesignationDefOf.Haul));
        Expect.IsNotNull(DesignationOf(scene, untouched, DesignationDefOf.Mine));
        Expect.AreEqual(1, job.Designations.Count);
        Expect.AreEqual(3, log.Details.Count());
        Expect.IsTrue(workDone.Value);
    }

    [Test]
    public static void ExecuteReduceStillCleansUpWhenTheTargetIsAlreadyGone()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var vein = DesignatedVein(scene, job, 0);
        var designation = DesignationOf(scene, vein, DesignationDefOf.Mine)!;
        var data = new MiningWorkData
        {
            Kind = MiningWorkKind.ReduceDesignations,
            MineDesignationsToRemove = [(designation, 10, 90)],
        };
        var log = NewLog(job);
        var workDone = new Boxed<bool>();
        vein.Destroy(DestroyMode.Vanish);

        ManagerScene.RunToCompletion(job.ExecuteReduceDesignations(log, data, workDone));

        Expect.IsEmpty(job.Designations);
        Expect.IsEmpty(log.Details);
        Expect.IsFalse(workDone.Value);
    }

    // ExecuteAddDesignations

    [Test]
    public static void ExecuteAddDesignatesEveryPlannedTargetAndLogsIt()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var vein = (Mineable)scene.SpawnThing(SteelVein, Slot(scene, 0));
        var wall = scene.SpawnThing(ThingDefOf.Wall, Slot(scene, 1), stuff: ThingDefOf.Steel);
        var chunk = scene.SpawnThing(ThingDef.Named("ChunkSlagSteel"), Slot(scene, 2));
        var data = new MiningWorkData
        {
            Kind = MiningWorkKind.AddDesignations,
            DesignationsToAdd =
            [
                (vein, DesignationDefOf.Mine, 10, 10),
                (wall, DesignationDefOf.Deconstruct, 5, 15),
                (chunk, DesignationDefOf.Haul, 2, 17),
            ],
        };
        var log = NewLog(job);
        var workDone = new Boxed<bool>();

        ManagerScene.RunToCompletion(job.ExecuteAddDesignations(log, data, workDone));

        Expect.AreEqual(
            DesignationDefOf.Mine,
            DesignationOf(scene, vein, DesignationDefOf.Mine)?.def
        );
        Expect.AreEqual(
            DesignationDefOf.Deconstruct,
            DesignationOf(scene, wall, DesignationDefOf.Deconstruct)?.def
        );
        Expect.AreEqual(
            DesignationDefOf.Haul,
            DesignationOf(scene, chunk, DesignationDefOf.Haul)?.def
        );
        Expect.AreEqual(3, job.Designations.Count);
        Expect.AreEqual(3, log.Details.Count());
        Expect.IsTrue(workDone.Value);
    }

    [Test]
    public static void ExecuteAddSkipsTargetsThatAreGoneByNow()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var vein = (Mineable)scene.SpawnThing(SteelVein, Slot(scene, 0));
        var data = new MiningWorkData
        {
            Kind = MiningWorkKind.AddDesignations,
            DesignationsToAdd = [(vein, DesignationDefOf.Mine, 10, 10)],
        };
        var log = NewLog(job);
        var workDone = new Boxed<bool>();
        vein.Destroy(DestroyMode.Vanish);

        ManagerScene.RunToCompletion(job.ExecuteAddDesignations(log, data, workDone));

        Expect.IsEmpty(job.Designations);
        Expect.IsEmpty(log.Details);
        Expect.IsFalse(workDone.Value);
    }

    // ExecuteDrillFlicks

    private static (Building Drill, CompFlickable Flickable) Drill(
        ManagerScene scene,
        int slot,
        bool on
    )
    {
        var drill = scene.SpawnBuilding(ThingDef.Named("DeepDrill"), Slot(scene, slot));
        var flickable = drill.GetComp<CompFlickable>();
        flickable.SwitchIsOn = on;
        flickable.wantSwitchOn = on;
        return (drill, flickable);
    }

    private static MiningWorkData DrillData(bool enable, params CompFlickable[] drills) =>
        new()
        {
            Kind = MiningWorkKind.AddDesignations,
            ShouldEnableDrills = enable,
            DrillCandidatesToFlick = [.. drills.Select(d => (d, ThingDefOf.Steel))],
        };

    [Test]
    public static void ExecuteDrillFlicksTurnsAnOffDrillOnAndCountsItAsWork()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var (drill, flickable) = Drill(scene, 0, on: false);
        var log = NewLog(job);
        var workDone = new Boxed<bool>();

        ManagerScene.RunToCompletion(ExecuteDrillFlicks(log, DrillData(true, flickable), workDone));

        Expect.IsTrue(flickable.wantSwitchOn);
        Expect.IsNotNull(scene.Map.designationManager.DesignationOn(drill, DesignationDefOf.Flick));
        Expect.AreEqual(1, log.Details.Count());
        Expect.IsTrue(workDone.Value);
    }

    [Test]
    public static void ExecuteDrillFlicksTurnsAnOnDrillOffWithoutCountingItAsWork()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var (drill, flickable) = Drill(scene, 0, on: true);
        var log = NewLog(job);
        var workDone = new Boxed<bool>();

        ManagerScene.RunToCompletion(
            ExecuteDrillFlicks(log, DrillData(false, flickable), workDone)
        );

        Expect.IsFalse(flickable.wantSwitchOn);
        Expect.IsNotNull(scene.Map.designationManager.DesignationOn(drill, DesignationDefOf.Flick));
        Expect.AreEqual(1, log.Details.Count());
        Expect.IsFalse(workDone.Value);
    }

    [Test]
    public static void ExecuteDrillFlicksLeavesADrillInTheWantedStateAlone()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var (drill, flickable) = Drill(scene, 0, on: true);
        var log = NewLog(job);
        var workDone = new Boxed<bool>();

        ManagerScene.RunToCompletion(ExecuteDrillFlicks(log, DrillData(true, flickable), workDone));

        Expect.IsNull(scene.Map.designationManager.DesignationOn(drill, DesignationDefOf.Flick));
        Expect.IsEmpty(log.Details);
        Expect.IsFalse(workDone.Value);
    }

    [Test]
    public static void ExecuteDrillFlicksSkipsDestroyedDrills()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var (drill, flickable) = Drill(scene, 0, on: false);
        var log = NewLog(job);
        var workDone = new Boxed<bool>();
        drill.Destroy();

        ManagerScene.RunToCompletion(ExecuteDrillFlicks(log, DrillData(true, flickable), workDone));

        Expect.IsEmpty(log.Details);
        Expect.IsFalse(workDone.Value);
    }

    [Test]
    [WarningsAllowed("workDone is null when trying to enable deep drill")]
    public static void ExecuteDrillFlicksWarnsWhenEnablingWithoutAWorkDoneBox()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var (_, flickable) = Drill(scene, 0, on: false);
        var log = NewLog(job);

        ManagerScene.RunToCompletion(ExecuteDrillFlicks(log, DrillData(true, flickable), null));

        Expect.IsTrue(flickable.wantSwitchOn);
    }
}
