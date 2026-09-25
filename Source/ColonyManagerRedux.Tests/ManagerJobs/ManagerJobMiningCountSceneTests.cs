// ManagerJobMiningCountSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The stockpiled-chunk and designation counting coroutines and
/// <see cref="ManagerJob_Mining.GetCountsInBuilding"/> of <see cref="ManagerJob_Mining"/>
/// against real chunks, stockpiles, veins and buildings.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobMiningCountSceneTests
{
    private static ThingDef SteelVein => DefDatabase<ThingDef>.GetNamed("MineableSteel");
    private static ThingDef GoldVein => DefDatabase<ThingDef>.GetNamed("MineableGold");
    private static ThingDef GraniteChunk => ThingDef.Named("ChunkGranite");
    private static ThingDef SlagChunk => ThingDef.Named("ChunkSlagSteel");

    private static IntVec3 Slot(ManagerScene scene, int i, int row = 0) =>
        scene.Cells(1, 1, -60 + (i * 3), -40 + (row * 3)).First();

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
        job.TriggerThreshold.ThresholdFilter.SetDisallowAll();
        job.TriggerThreshold.ThresholdFilter.SetAllow(ThingDefOf.Steel, true);
        return job;
    }

    private static int CountInChunks(ManagerJob_Mining job)
    {
        var count = new AnyBoxed<int>(0);
        ManagerScene.RunToCompletion(job.GetCountInChunksCoroutine(count));
        return count.Value;
    }

    private static int CountInDesignations(ManagerJob_Mining job)
    {
        var count = new AnyBoxed<int>(0);
        ManagerScene.RunToCompletion(job.GetCountInDesignationsCoroutine(count));
        return count.Value;
    }

    // Chunks in storage

    [Test]
    public static void ChunkInAStockpileIsCounted()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var cell = Slot(scene, 0);
        _ = scene.AddStockpile([cell]);
        _ = scene.SpawnThing(SlagChunk, cell);
        var yield = job.GetCountInChunk(SlagChunk);
        Expect.IsTrue(yield > 0, "The chunk yields nothing.");

        Expect.AreEqual(yield, CountInChunks(job));
    }

    [Test]
    public static void ChunkLyingOnTheFloorIsNotCounted()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        _ = scene.SpawnThing(SlagChunk, Slot(scene, 0));

        Expect.AreEqual(0, CountInChunks(job));
    }

    [Test]
    public static void StockpiledChunkWhoseProductsAreNotCountedContributesNothing()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var cell = Slot(scene, 0);
        _ = scene.AddStockpile([cell]);
        _ = scene.SpawnThing(GraniteChunk, cell);
        Expect.AreEqual(0, job.GetCountInChunk(GraniteChunk), "Granite is counted.");

        Expect.AreEqual(0, CountInChunks(job));
    }

    [Test]
    public static void CountingChunksTwiceGivesTheSameAnswer()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var cells = new[] { Slot(scene, 0), Slot(scene, 1) };
        _ = scene.AddStockpile(cells);
        _ = scene.SpawnThing(SlagChunk, cells[0]);
        _ = scene.SpawnThing(SlagChunk, cells[1]);

        var first = CountInChunks(job);
        var second = CountInChunks(job);

        Expect.AreEqual(2 * job.GetCountInChunk(SlagChunk), first);
        Expect.AreEqual(first, second);
    }

    // Designations

    [Test]
    public static void MixedDesignationsProduceTheSumOfTheirParts()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var wall = (Building)
            scene.SpawnThing(ThingDefOf.Wall, Slot(scene, 0), stuff: ThingDefOf.Steel);
        var veins = new[]
        {
            scene.SpawnThing(SteelVein, Slot(scene, 1)),
            scene.SpawnThing(SteelVein, Slot(scene, 2)),
        };
        var chunk = scene.SpawnThing(SlagChunk, Slot(scene, 3));
        job.AddDesignation(new Designation(wall, DesignationDefOf.Deconstruct));
        foreach (var vein in veins)
        {
            job.AddDesignation(new Designation(vein, DesignationDefOf.Mine));
        }
        job.AddDesignation(new Designation(chunk, DesignationDefOf.Haul));
        var expected =
            job.GetCountInBuilding(wall)
            + (2 * job.GetCountInMineral((Mineable)veins[0]))
            + job.GetCountInChunk(chunk);
        Expect.IsTrue(expected > 0, "Nothing to count.");

        Expect.AreEqual(expected, CountInDesignations(job));
    }

    [Test]
    public static void MineDesignationOnACellCountsTheMineralInIt()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var vein = (Mineable)scene.SpawnThing(SteelVein, Slot(scene, 0));
        job._designations.Add(new Designation(vein.Position, DesignationDefOf.Mine));

        Expect.AreEqual(job.GetCountInMineral(vein), CountInDesignations(job));
    }

    [Test]
    public static void MineDesignationOnACellWithoutAnAllowedMineralIsSkipped()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var gold = scene.SpawnThing(GoldVein, Slot(scene, 0));
        var empty = Slot(scene, 1);
        job._designations.Add(new Designation(gold.Position, DesignationDefOf.Mine));
        job._designations.Add(new Designation(empty, DesignationDefOf.Mine));

        Expect.AreEqual(0, CountInDesignations(job));
    }

    // The mineral in a designated cell is the mineable there, not whatever thing was placed in
    // the cell first.
    [Test]
    public static void MineDesignationCountsTheVeinEvenWhenAnItemWasPlacedInItsCellFirst()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var cell = Slot(scene, 0);
        _ = scene.SpawnThing(ThingDefOf.Steel, cell, 5);
        var vein = (Mineable)scene.SpawnThing(SteelVein, cell);
        Expect.IsTrue(
            scene.Map.thingGrid.ThingsListAtFast(cell).Any(t => t == vein),
            "The vein should be in the cell."
        );
        job._designations.Add(new Designation(cell, DesignationDefOf.Mine));

        Expect.AreEqual(job.GetCountInMineral(vein), CountInDesignations(job));
    }

    [Test]
    public static void HaulDesignationOnACellIsIgnored()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job._designations.Add(new Designation(Slot(scene, 0), DesignationDefOf.Haul));

        Expect.AreEqual(0, CountInDesignations(job));
    }

    [Test]
    public static void DeconstructDesignationOnANonBuildingAddsNothing()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var item = scene.SpawnThing(ThingDefOf.Steel, Slot(scene, 0), 5);
        job._designations.Add(new Designation(item, DesignationDefOf.Deconstruct));

        Expect.AreEqual(0, CountInDesignations(job));
    }

    // GetCountsInBuilding

    [Test]
    public static void BuildingCountsResultsDoNotShareOneList()
    {
        using var scene = new ManagerScene();
        var wall = (Building)
            scene.SpawnThing(ThingDefOf.Wall, Slot(scene, 0), stuff: ThingDefOf.Steel);
        var door = (Building)
            scene.SpawnThing(ThingDefOf.Door, Slot(scene, 1), stuff: ThingDefOf.Steel);

        var wallCounts = ManagerJob_Mining.GetCountsInBuilding(wall);
        var wallDefs = wallCounts.Select(c => c.thingDef).ToList();
        var doorCounts = ManagerJob_Mining.GetCountsInBuilding(door);

        Expect.IsNotEmpty(wallDefs);
        Expect.ReferencesAreNotEqual(wallCounts, doorCounts);
        Expect.That(() => wallCounts.Select(c => c.thingDef).SequenceEqual(wallDefs));
    }

    [Test]
    public static void BuildingCountsForNoBuildingIsEmpty() =>
        Expect.IsEmpty(ManagerJob_Mining.GetCountsInBuilding(null));
}
