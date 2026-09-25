// ManagerJobMiningGatherSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;
using static ColonyManagerRedux.Managers.ManagerJob_Mining;
using MiningTask = ColonyManagerRedux.Managers.ManagerJob_Mining.Task;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The add-designation planners of <see cref="ManagerJob_Mining"/> (hauling chunks,
/// deconstructing buildings, mining veins) and the order and cut-off between them in a full
/// gather-and-execute pass.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobMiningGatherSceneTests
{
    private const int FarTarget = 100000;

    private static ThingDef SteelVein => DefDatabase<ThingDef>.GetNamed("MineableSteel");
    private static ThingDef GraniteChunk => ThingDef.Named("ChunkGranite");
    private static ThingDef SlagChunk => ThingDef.Named("ChunkSlagSteel");

    private static IntVec3 Slot(ManagerScene scene, int i) =>
        scene.Cells(1, 1, -60 + (i % 10 * 4), -20 + (i / 10 * 4)).First();

    private static ManagerJob_Mining NewJob(ManagerScene scene, int target = FarTarget)
    {
        var job = scene.AddJob<ManagerJob_Mining>("CM_MiningManager");
        job.CheckRoofSupport = false;
        job.CheckRoomDivision = false;
        job.ShouldCheckReachable = false;
        job.MineThickRoofs = true;
        job.MiningArea = null;
        job.InvertMiningArea = false;
        job.DeconstructAncientDangerWhenFogged = false;
        job.AllowMining = true;
        job.HaulMapChunks = true;
        job.DeconstructBuildings = true;
        job.AllowedMinerals.Clear();
        _ = job.AllowedMinerals.Add(SteelVein);
        job.AllowedBuildings.Clear();
        _ = job.AllowedBuildings.Add(ThingDefOf.Wall);
        job.TriggerThreshold.ThresholdFilter.SetDisallowAll();
        job.TriggerThreshold.ThresholdFilter.SetAllow(ThingDefOf.Steel, true);
        job.TriggerThreshold.Op = Trigger_Threshold.Ops.LowerThan;
        job.TriggerThreshold.TargetCount = target;
        return job;
    }

    private static ManagerLog NewLog(ManagerJob job) => new(job) { _workDone = true };

    private static Boxed<int> ZeroCount() => new(0);

    private static Thing LooseChunk(ManagerScene scene, int slot) =>
        scene.SpawnThing(SlagChunk, Slot(scene, slot));

    private static Building FoeWallAt(ManagerScene scene, IntVec3 cell) =>
        (Building)scene.SpawnThing(ThingDefOf.Wall, cell, stuff: ThingDefOf.Steel);

    private static Building FoeWall(ManagerScene scene, int slot) =>
        FoeWallAt(scene, Slot(scene, slot));

    private static void Run(ManagerScene scene, ManagerJob_Mining job)
    {
        scene.Map.resourceCounter.UpdateResourceCounts();
        _ = job.TriggerThreshold.GetCurrentCount(cached: false);
        job.Untouch();
        ManagerScene.RunToCompletion(scene.Manager.TryDoWork());
    }

    private static void WithFog(Map map, IntVec3 cell, Action action)
    {
        var fogIndex = map.cellIndices.CellToIndex(cell);
        var fogGrid = map.fogGrid.fogGrid;
        var wasFogged = fogGrid.IsSet(fogIndex);
        fogGrid.Set(fogIndex, true);
        try
        {
            action();
        }
        finally
        {
            fogGrid.Set(fogIndex, wasFogged);
        }
    }

    private static void WithMaxDesignations(int max, Action action)
    {
        var settings = ColonyManagerReduxMod.Settings;
        var previous = settings.MaxDesignationsPerJob;
        settings.MaxDesignationsPerJob = max;
        try
        {
            action();
        }
        finally
        {
            settings.MaxDesignationsPerJob = previous;
        }
    }

    private static string CantAddMoreText(ManagerJob_Mining job) =>
        "ColonyManagerRedux.Logs.CantAddMoreDesignations"
            .Translate("ColonyManagerRedux.Mining.Logs.Rocks".Translate(), job.Def.label)
            .Resolve();

    private static bool LoggedCantAddMore(ManagerScene scene, ManagerJob_Mining job) =>
        scene.NewLogs.SelectMany(l => l.Details).Any(d => d.Text == CantAddMoreText(job));

    // PlanHaulChunks

    [Test]
    public static void HaulPlansOnlyLooseUnclaimedUndesignatedChunksThatYieldSomething()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var pawn = scene.SpawnColonist(Slot(scene, 20));

        var loose = LooseChunk(scene, 0);
        var stockpileCell = Slot(scene, 1);
        _ = scene.AddStockpile([stockpileCell]);
        _ = scene.SpawnThing(SlagChunk, stockpileCell);
        var reserved = LooseChunk(scene, 3);
        var designated = LooseChunk(scene, 4);
        job.AddDesignation(new Designation(designated, DesignationDefOf.Haul));
        _ = scene.SpawnThing(GraniteChunk, Slot(scene, 5));

        var reservationJob = JobMaker.MakeJob(JobDefOf.Wait);
        Expect.IsTrue(
            scene.Map.reservationManager.Reserve(pawn, reservationJob, reserved),
            "Could not reserve the chunk."
        );
        var data = new MiningWorkData();
        try
        {
            ManagerScene.RunToCompletion(job.PlanHaulChunks(ZeroCount(), data));
        }
        finally
        {
            scene.Map.reservationManager.ReleaseAllClaimedBy(pawn);
        }

        Expect.AreEqual(1, data.DesignationsToAdd.Count);
        Expect.ReferencesAreEqual(loose, data.DesignationsToAdd[0].Target);
        Expect.AreEqual(DesignationDefOf.Haul, data.DesignationsToAdd[0].Def);
        Expect.AreEqual(job.GetCountInChunk(loose), data.DesignationsToAdd[0].Amount);
    }

    // PlanDeconstructBuildings and the ancient-danger rule

    private static CellRect RectAround(IntVec3 center) => CellRect.CenteredOn(center, 2);

    private static void WithAncientDangerRect(ManagerScene scene, CellRect rect, Action action)
    {
        var rects = scene.Manager.AncientDangerRects;
        rects.Add(rect);
        try
        {
            action();
        }
        finally
        {
            _ = rects.Remove(rect);
        }
    }

    [Test]
    public static void DeconstructSkipsABuildingInsideAStillFoggedAncientDangerRect()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var center = Slot(scene, 0);
        _ = FoeWallAt(scene, center + new IntVec3(2, 0, 0));
        var log = NewLog(job);
        var data = new MiningWorkData();

        WithAncientDangerRect(
            scene,
            RectAround(center),
            () =>
                WithFog(
                    scene.Map,
                    center,
                    () =>
                        ManagerScene.RunToCompletion(
                            job.PlanDeconstructBuildings(log, ZeroCount(), data)
                        )
                )
        );

        Expect.IsEmpty(data.DesignationsToAdd);
        Expect.AreEqual(1, log.Details.Count());
    }

    [Test]
    public static void DeconstructPlansABuildingInAFoggedAncientDangerRectWhenAllowed()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job.DeconstructAncientDangerWhenFogged = true;
        var center = Slot(scene, 0);
        var wall = FoeWallAt(scene, center + new IntVec3(2, 0, 0));
        var log = NewLog(job);
        var data = new MiningWorkData();

        WithAncientDangerRect(
            scene,
            RectAround(center),
            () =>
                WithFog(
                    scene.Map,
                    center,
                    () =>
                        ManagerScene.RunToCompletion(
                            job.PlanDeconstructBuildings(log, ZeroCount(), data)
                        )
                )
        );

        Expect.AreEqual(1, data.DesignationsToAdd.Count);
        Expect.ReferencesAreEqual(wall, data.DesignationsToAdd[0].Target);
        Expect.IsEmpty(log.Details);
    }

    [Test]
    public static void DeconstructPrunesAnAncientDangerRectWhoseCentreIsNoLongerFogged()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var center = Slot(scene, 0);
        var wall = FoeWallAt(scene, center + new IntVec3(2, 0, 0));
        var rect = RectAround(center);
        var log = NewLog(job);
        var data = new MiningWorkData();
        var pruned = false;

        WithAncientDangerRect(
            scene,
            rect,
            () =>
            {
                ManagerScene.RunToCompletion(job.PlanDeconstructBuildings(log, ZeroCount(), data));
                pruned = !scene.Manager.AncientDangerRects.Contains(rect);
            }
        );

        Expect.AreEqual(1, data.DesignationsToAdd.Count);
        Expect.IsTrue(pruned, "The rect should have been pruned.");
    }

    // PlanMineResources

    [Test]
    public static void MineSkipsARoofSupportAndPlansTheNextVeinInstead()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job.CheckRoofSupport = true;
        job.CheckRoofSupportAdvanced = true;
        var support = (Mineable)scene.SpawnThing(SteelVein, Slot(scene, 0));
        var free = (Mineable)scene.SpawnThing(SteelVein, Slot(scene, 15));
        var yield = job.GetCountInMineral(free);
        Expect.IsTrue(yield > 0, "The vein yields nothing.");
        job.TriggerThreshold.TargetCount = yield;

        var roofed = support.Position + new IntVec3(0, 0, 1);
        var roofGrid = scene.Map.roofGrid;
        var previousRoof = roofGrid.RoofAt(roofed);
        roofGrid.SetRoof(roofed, RoofDefOf.RoofConstructed);
        var data = new MiningWorkData();
        try
        {
            Expect.IsTrue(job.IsARoofSupport_Advanced(support), "The vein should hold the roof.");
            Expect.IsFalse(job.IsARoofSupport_Advanced(free), "The other vein holds no roof.");

            ManagerScene.RunToCompletion(job.PlanMineResources(ZeroCount(), data));
        }
        finally
        {
            roofGrid.SetRoof(roofed, previousRoof);
        }

        Expect.AreEqual(1, data.DesignationsToAdd.Count);
        Expect.ReferencesAreEqual(free, data.DesignationsToAdd[0].Target);
        Expect.AreEqual(DesignationDefOf.Mine, data.DesignationsToAdd[0].Def);
    }

    // Gather

    private static void AddOneOfEach(ManagerScene scene)
    {
        _ = scene.SpawnThing(SteelVein, Slot(scene, 0));
        _ = LooseChunk(scene, 1);
        _ = FoeWall(scene, 2);
    }

    private static DesignationDef[] DesignationOrder(ManagerJob_Mining job) =>
        [.. job.Designations.Select(d => d.def)];

    [Test]
    public static void GatherRunsTheTasksInPriorityOrder()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job.TaskPriorityOrder =
        [
            MiningTask.Mine,
            MiningTask.HaulChunks,
            MiningTask.DeconstructBuildings,
        ];
        AddOneOfEach(scene);

        Run(scene, job);

        Expect.That(() =>
            DesignationOrder(job)
                .SequenceEqual([
                    DesignationDefOf.Mine,
                    DesignationDefOf.Haul,
                    DesignationDefOf.Deconstruct,
                ])
        );
    }

    [Test]
    public static void GatherFollowsAReorderedPriorityList()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job.TaskPriorityOrder =
        [
            MiningTask.DeconstructBuildings,
            MiningTask.HaulChunks,
            MiningTask.Mine,
        ];
        AddOneOfEach(scene);

        Run(scene, job);

        Expect.That(() =>
            DesignationOrder(job)
                .SequenceEqual([
                    DesignationDefOf.Deconstruct,
                    DesignationDefOf.Haul,
                    DesignationDefOf.Mine,
                ])
        );
    }

    [Test]
    public static void GatherSkipsDisabledTasks()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job.HaulMapChunks = false;
        job.TaskPriorityOrder =
        [
            MiningTask.HaulChunks,
            MiningTask.DeconstructBuildings,
            MiningTask.Mine,
        ];
        AddOneOfEach(scene);

        Run(scene, job);

        Expect.That(() =>
            DesignationOrder(job)
                .SequenceEqual([DesignationDefOf.Deconstruct, DesignationDefOf.Mine])
        );
    }

    [Test]
    public static void GatherPlansNothingInLaterTasksOnceTheTargetIsMet()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job.TaskPriorityOrder =
        [
            MiningTask.HaulChunks,
            MiningTask.Mine,
            MiningTask.DeconstructBuildings,
        ];
        var chunk = LooseChunk(scene, 0);
        _ = scene.SpawnThing(SteelVein, Slot(scene, 1));
        _ = FoeWall(scene, 2);
        scene.Map.resourceCounter.UpdateResourceCounts();
        job.TriggerThreshold.TargetCount =
            job.TriggerThreshold.GetCurrentCount(cached: false) + job.GetCountInChunk(chunk);

        Run(scene, job);

        Expect.That(() => DesignationOrder(job).SequenceEqual([DesignationDefOf.Haul]));
    }

    [Test]
    public static void GatherLogsAndStopsWhenTheDesignationCapIsHitBetweenTasks()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job.TaskPriorityOrder =
        [
            MiningTask.HaulChunks,
            MiningTask.DeconstructBuildings,
            MiningTask.Mine,
        ];
        for (var i = 0; i < 12; i++)
        {
            _ = LooseChunk(scene, i);
        }
        var wall = FoeWall(scene, 20);

        WithMaxDesignations(10, () => Run(scene, job));

        Expect.AreEqual(10, job.Designations.Count);
        Expect.All(job.Designations, d => d.def == DesignationDefOf.Haul);
        Expect.IsNull(scene.Map.designationManager.DesignationOn(wall));
        Expect.IsTrue(LoggedCantAddMore(scene, job), "The cap was not logged.");
    }

    [Test]
    public static void GatherDoesNotLogTheCapAfterTheLastTask()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job.HaulMapChunks = false;
        job.DeconstructBuildings = false;
        job.TaskPriorityOrder =
        [
            MiningTask.HaulChunks,
            MiningTask.DeconstructBuildings,
            MiningTask.Mine,
        ];
        for (var i = 0; i < 12; i++)
        {
            _ = scene.SpawnThing(SteelVein, Slot(scene, i));
        }

        WithMaxDesignations(10, () => Run(scene, job));

        Expect.AreEqual(10, job.Designations.Count);
        Expect.IsFalse(LoggedCantAddMore(scene, job), "The cap was logged after the last task.");
    }
}
