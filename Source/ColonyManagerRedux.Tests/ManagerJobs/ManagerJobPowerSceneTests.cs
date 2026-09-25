// ManagerJobPowerSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The Power job's building/comp list refresh and its post-import deduplication, on real
/// powered buildings.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobPowerSceneTests
{
    private static readonly ThingDef Lamp = DefDatabase<ThingDef>.GetNamed("StandingLamp");
    private static readonly ThingDef Battery = DefDatabase<ThingDef>.GetNamed("Battery");

    private static ManagerJob_Power AddPowerJob(ManagerScene scene) =>
        scene.AddJob<ManagerJob_Power>("CM_PowerManager");

    private static IntVec3 CellFor(ManagerScene scene, int index) =>
        scene.Cells(1, 1, offsetX: -12, offsetZ: index * 3).First();

    [Test]
    public static void PostImportOfSeveralPowerJobsKeepsOnlyTheImportedOne()
    {
        using var scene = new ManagerScene();
        _ = AddPowerJob(scene);
        _ = AddPowerJob(scene);
        var imported = AddPowerJob(scene);

        imported.PostImport();

        Expect.AreEqual(1, scene.Manager.JobTracker.Jobs.Count());
        Expect.IsTrue(scene.Manager.JobTracker.HasJob(imported));
    }

    [Test]
    public static void RefreshListsPicksUpTradersAndBatteriesByDef()
    {
        using var scene = new ManagerScene();
        var job = AddPowerJob(scene);
        var lamp = scene.SpawnBuilding(Lamp, CellFor(scene, 0));
        var battery = scene.SpawnBuilding(Battery, CellFor(scene, 1));

        job.PostImport();

        Expect.IsTrue(job._traderBuildings.Contains(lamp));
        Expect.IsTrue(job._batteryBuildings.Contains(battery));
        var lampIndex = ManagerJob_Power.TraderDefs.IndexOf(Lamp);
        var batteryIndex = ManagerJob_Power.BatteryDefs.IndexOf(Battery);
        Expect.AreEqual(1, job._traders[lampIndex].Count);
        Expect.AreEqual(1, job._batteries[batteryIndex].Count);
    }

    [Test]
    public static void RefreshAfterRemovingTheLastBuildingOfADefEmptiesItsList()
    {
        using var scene = new ManagerScene();
        var job = AddPowerJob(scene);
        var lamp = scene.SpawnBuilding(Lamp, CellFor(scene, 0));
        _ = scene.SpawnBuilding(Battery, CellFor(scene, 1));
        job.PostImport();

        lamp.Destroy();
        job.PostImport();

        var lampIndex = ManagerJob_Power.TraderDefs.IndexOf(Lamp);
        Expect.IsFalse(job._traderBuildings.Contains(lamp));
        Expect.AreEqual(0, job._traders[lampIndex].Count);
        Expect.AreEqual(1, job._batteryBuildings.Count);
    }

    [Test]
    public static void RefreshTrimsCompListsWhenTheTraderDefSetShrinks()
    {
        using var scene = new ManagerScene();
        var job = AddPowerJob(scene);
        job.PostImport();
        var expected = ManagerJob_Power.TraderDefs.Count;
        job._traders.Add([]);
        job._traders.Add([]);
        job._traders.Add([]);

        job.PostImport();

        Expect.AreEqual(expected, job._traders.Count);
    }

    [Test]
    public static void HistoryUpdateJoinsBuildingsByDef()
    {
        using var scene = new ManagerScene();
        var job = AddPowerJob(scene);
        _ = scene.SpawnBuilding(Lamp, CellFor(scene, 0));
        _ = scene.SpawnBuilding(Lamp, CellFor(scene, 1));

        var tick = Find.TickManager.TicksGame;
        ManagerScene.RunToCompletion(
            new ManagerJob_Power.HistoryWorker().HistoryUpdateCoroutine(job, tick)
        );

        var lampIndex = ManagerJob_Power.TraderDefs.IndexOf(Lamp);
        Expect.AreEqual(2, job._traders[lampIndex].Count);
    }

    [Test]
    public static void CachedCountsAreComputedOnFirstAccess()
    {
        using var scene = new ManagerScene();
        var job = AddPowerJob(scene);
        job.PostImport();

        Expect.AreEqual(0, job.ProducerCount);
        Expect.AreEqual(0, job.ConsumerCount);
        Expect.AreEqual(0, job.BatteryCount);
    }
}
