// ManagerJobPowerGatherSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using System.Text.RegularExpressions;
using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The Power job's gather outcomes, history chapters, refresh re-entrancy guards and scribing, on
/// real powered buildings.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobPowerGatherSceneTests
{
    private static readonly ThingDef Lamp = DefDatabase<ThingDef>.GetNamed("StandingLamp");
    private static readonly ThingDef Battery = DefDatabase<ThingDef>.GetNamed("Battery");
    private static readonly ThingDef Solar = DefDatabase<ThingDef>.GetNamed("SolarGenerator");

    private static ManagerJob_Power AddPowerJob(ManagerScene scene) =>
        scene.AddJob<ManagerJob_Power>("CM_PowerManager");

    private static IntVec3 CellFor(ManagerScene scene, int index) =>
        scene.Cells(1, 1, offsetX: -12, offsetZ: index * 3).First();

    private static Building Powered(
        ManagerScene scene,
        ThingDef def,
        int index,
        float? output = null,
        bool powerOn = true
    )
    {
        var building = scene.SpawnBuilding(def, CellFor(scene, index));
        var trader = building.GetComp<CompPowerTrader>();
        if (trader == null)
        {
            return building;
        }
        trader.PowerOn = powerOn;
        if (output != null)
        {
            trader.PowerOutput = output.Value;
        }
        return building;
    }

    private static void WithSettings(bool recordHistoricalData, int operationsPerTick, Action test)
    {
        var settings = ColonyManagerReduxMod.Settings;
        var recordBefore = settings.RecordHistoricalData;
        var operationsBefore = settings.OperationsPerTick;
        settings.RecordHistoricalData = recordHistoricalData;
        settings.OperationsPerTick = operationsPerTick;
        try
        {
            test();
        }
        finally
        {
            settings.RecordHistoricalData = recordBefore;
            settings.OperationsPerTick = operationsBefore;
        }
    }

    private static void WithSettings(bool recordHistoricalData, Action test) =>
        WithSettings(recordHistoricalData, ColonyManagerReduxMod.Settings.OperationsPerTick, test);

    private static void RunJob(ManagerScene scene, ManagerJob_Power job)
    {
        job.Untouch();
        ManagerScene.RunToCompletion(scene.Manager.TryDoWork());
    }

    private static void Drain(Coroutine coroutine)
    {
        foreach (var _ in coroutine) { }
    }

    // Gather outcomes

    [Test]
    public static void GatherWithNoStationOnlineLeavesTheJobUntouched() =>
        WithSettings(
            true,
            () =>
            {
                using var scene = new ManagerScene();
                var job = AddPowerJob(scene);
                job._jobState = ManagerJobState.Completed;
                var lamp = Powered(scene, Lamp, 0);

                RunJob(scene, job);

                Expect.AreEqual(ManagerJobState.Completed, job.JobState);
                Expect.IsFalse(job._traderBuildings.Contains(lamp));
            }
        );

    [Test]
    public static void GatherWithRecordingDisabledCompletesTheJobWithoutRefreshing() =>
        WithSettings(
            false,
            () =>
            {
                using var scene = new ManagerScene();
                var job = AddPowerJob(scene);
                _ = Powered(scene, DefDatabase<ThingDef>.GetNamed("CM_ManagerStation"), 0);
                var lamp = Powered(scene, Lamp, 1);

                RunJob(scene, job);

                Expect.AreEqual(ManagerJobState.Completed, job.JobState);
                Expect.IsFalse(job._traderBuildings.Contains(lamp));
            }
        );

    [Test]
    public static void GatherWithStationAndRecordingActivatesTheJobAndRefreshesTheLists() =>
        WithSettings(
            true,
            () =>
            {
                using var scene = new ManagerScene();
                var job = AddPowerJob(scene);
                job._jobState = ManagerJobState.Completed;
                _ = Powered(scene, DefDatabase<ThingDef>.GetNamed("CM_ManagerStation"), 0);
                var lamp = Powered(scene, Lamp, 1);
                var battery = Powered(scene, Battery, 2);

                RunJob(scene, job);

                Expect.AreEqual(ManagerJobState.Active, job.JobState);
                Expect.IsTrue(job._traderBuildings.Contains(lamp));
                Expect.IsTrue(job._batteryBuildings.Contains(battery));
                var lampIndex = ManagerJob_Power.TraderDefs.IndexOf(Lamp);
                Expect.AreEqual(1, job._traders[lampIndex].Count);
                var batteryIndex = ManagerJob_Power.BatteryDefs.IndexOf(Battery);
                Expect.AreEqual(1, job._batteries[batteryIndex].Count);
            }
        );

    // History chapters

    private static (ManagerJob_Power job, ManagerJob_Power.HistoryWorker worker) HistoryScene(
        ManagerScene scene
    )
    {
        var job = AddPowerJob(scene);
        _ = Powered(scene, Solar, 0, output: 1700f);
        _ = Powered(scene, Lamp, 1, output: -50f);
        _ = Powered(scene, Lamp, 2, output: -50f);
        _ = Powered(scene, Lamp, 3, output: -500f, powerOn: false);
        var battery = Powered(scene, Battery, 4);
        battery.GetComp<CompPowerBattery>().SetStoredEnergyPct(0.5f);
        ManagerScene.RunToCompletion(job.RefreshBuildingLists());
        ManagerScene.RunToCompletion(job.RefreshCompLists());
        return (job, new ManagerJob_Power.HistoryWorker());
    }

    private static int Count(
        ManagerJob_Power.HistoryWorker worker,
        ManagerJob_Power job,
        ManagerJobHistoryChapterDef chapter
    )
    {
        var box = new Boxed<int> { Value = -1 };
        Drain(worker.GetCountForHistoryChapterCoroutine(job, 0, chapter, box));
        return box.Value;
    }

    [Test]
    public static void ProductionChapterSumsPositiveNetOutputOfPoweredBuildings()
    {
        using var scene = new ManagerScene();
        var (job, worker) = HistoryScene(scene);

        Expect.AreEqual(
            1700,
            Count(worker, job, ManagerJobHistoryChapterDefOf.CM_HistoryProduction)
        );
    }

    [Test]
    public static void ConsumptionChapterIsTheAbsoluteNetOutputOfPoweredBuildings()
    {
        using var scene = new ManagerScene();
        var (job, worker) = HistoryScene(scene);

        // The unpowered lamp draws nothing.
        Expect.AreEqual(
            100,
            Count(worker, job, ManagerJobHistoryChapterDefOf.CM_HistoryConsumption)
        );
    }

    [Test]
    public static void BatteriesChapterIsTheStoredEnergy()
    {
        using var scene = new ManagerScene();
        var (job, worker) = HistoryScene(scene);
        var capacity = Battery.GetCompProperties<CompProperties_Battery>().storedEnergyMax;

        Expect.AreEqual(
            (int)(capacity * 0.5f),
            Count(worker, job, ManagerJobHistoryChapterDefOf.CM_HistoryBatteries)
        );
    }

    [Test]
    public static void UnknownCountChapterThrows()
    {
        using var scene = new ManagerScene();
        var (job, worker) = HistoryScene(scene);

        _ = Expect.Throws<ArgumentException>(() =>
            Count(worker, job, ManagerJobHistoryChapterDefOf.CM_HistoryStock)
        );
    }

    [Test]
    public static void MaxIsTheSummedBatteryCapacityForBatteriesAndZeroOtherwise()
    {
        using var scene = new ManagerScene();
        var (job, worker) = HistoryScene(scene);
        var capacity = (int)Battery.GetCompProperties<CompProperties_Battery>().storedEnergyMax;

        var batteries = new Boxed<int> { Value = -1 };
        Drain(
            worker.GetMaxForHistoryChapterCoroutine(
                job,
                0,
                ManagerJobHistoryChapterDefOf.CM_HistoryBatteries,
                batteries
            )
        );
        var production = new Boxed<int> { Value = -1 };
        Drain(
            worker.GetMaxForHistoryChapterCoroutine(
                job,
                0,
                ManagerJobHistoryChapterDefOf.CM_HistoryProduction,
                production
            )
        );

        Expect.AreEqual(capacity, batteries.Value);
        Expect.AreEqual(0, production.Value);
    }

    [Test]
    public static void TargetIsAlwaysZero()
    {
        using var scene = new ManagerScene();
        var (job, worker) = HistoryScene(scene);

        var target = new Boxed<int> { Value = -1 };
        Drain(
            worker.GetTargetForHistoryChapterCoroutine(
                job,
                0,
                ManagerJobHistoryChapterDefOf.CM_HistoryBatteries,
                target
            )
        );

        Expect.AreEqual(0, target.Value);
    }

    // Refresh guards

    [Test]
    public static void SecondBuildingRefreshWaitsForTheFirstAndDoesNotDuplicateBuildings() =>
        WithSettings(
            true,
            1,
            () =>
            {
                using var scene = new ManagerScene();
                var job = AddPowerJob(scene);
                _ = Powered(scene, Lamp, 0);

                using var first = job.RefreshBuildingLists().GetEnumerator();
                Expect.IsTrue(first.MoveNext(), "The first refresh finished without yielding.");
                Expect.IsTrue(job._isRefreshingBuildingLists);

                using var second = job.RefreshBuildingLists().GetEnumerator();
                Expect.IsTrue(second.MoveNext());
                var wait = second.Current;
                Expect.IsFalse(wait.ShouldResume(), "The second refresh did not wait.");

                while (first.MoveNext()) { }

                Expect.IsFalse(job._isRefreshingBuildingLists);
                Expect.IsTrue(wait.ShouldResume());
                Expect.IsFalse(second.MoveNext());
                Expect.AreEqual(1, job._traderBuildings.Count);
            }
        );

    [Test]
    public static void DisposingAnUnfinishedBuildingRefreshClearsItsGuard() =>
        WithSettings(
            true,
            1,
            () =>
            {
                using var scene = new ManagerScene();
                var job = AddPowerJob(scene);

                var refresh = job.RefreshBuildingLists().GetEnumerator();
                Expect.IsTrue(refresh.MoveNext(), "The refresh finished without yielding.");
                Expect.IsTrue(job._isRefreshingBuildingLists);

                refresh.Dispose();

                Expect.IsFalse(job._isRefreshingBuildingLists);
            }
        );

    [Test]
    public static void DisposingAnUnfinishedCompRefreshClearsItsGuard() =>
        WithSettings(
            true,
            1,
            () =>
            {
                using var scene = new ManagerScene();
                var job = AddPowerJob(scene);
                _ = Powered(scene, Lamp, 0);
                _ = Powered(scene, Lamp, 1);
                _ = Powered(scene, Lamp, 2);
                ManagerScene.RunToCompletion(job.RefreshBuildingLists());

                var refresh = job.RefreshCompLists().GetEnumerator();
                Expect.IsTrue(refresh.MoveNext(), "The refresh finished without yielding.");
                Expect.IsTrue(job._isRefreshingCompLists);

                refresh.Dispose();

                Expect.IsFalse(job._isRefreshingCompLists);
            }
        );

    [Test]
    public static void InventoryLogIsOnlyAddedWhenTheBuildingCountsChanged()
    {
        using var scene = new ManagerScene();
        var job = AddPowerJob(scene);
        _ = Powered(scene, Lamp, 0);
        var log = new ManagerLog(job);

        ManagerScene.RunToCompletion(job.RefreshBuildingLists(log));
        var afterFirst = log.Details.Count();
        ManagerScene.RunToCompletion(job.RefreshBuildingLists(log));
        var afterSecond = log.Details.Count();

        Expect.AreEqual(1, afterFirst);
        Expect.AreEqual(1, afterSecond);
    }

    // ExposeData

    private sealed class PowerHarness(Manager manager, List<Thing> things, List<ManagerJob>? jobs)
        : IExposable
    {
        public ScribeRoundTrip.JobListHarness Inner = new(manager, jobs);

        public void ExposeData()
        {
            // The buildings are saved with the map in a real save; make them resolvable as the
            // same live instances here.
            foreach (var thing in things)
            {
                if (Scribe.mode == LoadSaveMode.Saving)
                {
                    Scribe.saver.loadIDsErrorsChecker.RegisterDeepSaved(thing, "thing");
                }
                else if (Scribe.mode == LoadSaveMode.LoadingVars)
                {
                    Scribe.loader.crossRefs.loadedObjectDirectory.RegisterLoaded(thing);
                }
            }
            new ScribeRoundTrip.ManagerHarness(manager, Inner).ExposeData();
        }
    }

    private static string Save(ManagerScene scene, ManagerJob_Power job, List<Thing> things) =>
        ScribeRoundTrip.Save(new PowerHarness(scene.Manager, things, [job]));

    private static ManagerJob_Power Load(ManagerScene scene, string xml, List<Thing> things)
    {
        var loaded = new PowerHarness(scene.Manager, things, null);
        ScribeRoundTrip.Load(xml, loaded);
        return (ManagerJob_Power)loaded.Inner.Jobs.Single();
    }

    [Test]
    public static void SameMapRoundTripRestoresTheBuildingReferencesAndRefreshesTheCompLists()
    {
        using var scene = new ManagerScene();
        var job = AddPowerJob(scene);
        var lamp = Powered(scene, Lamp, 0);
        var battery = Powered(scene, Battery, 1);
        job.PostImport();
        List<Thing> things = [lamp, battery];

        var loaded = Load(scene, Save(scene, job, things), things);

        Expect.IsTrue(loaded._traderBuildings.Contains(lamp));
        Expect.IsTrue(loaded._batteryBuildings.Contains(battery));
        var lampIndex = ManagerJob_Power.TraderDefs.IndexOf(Lamp);
        Expect.AreEqual(1, loaded._traders[lampIndex].Count);
    }

    [Test]
    public static void BuildingReferencesAreNotWrittenWhenTheDataIsNotFromTheSameMap()
    {
        using var scene = new ManagerScene();
        var job = AddPowerJob(scene);
        var lamp = Powered(scene, Lamp, 0);
        job.PostImport();
        scene.Manager.ScribeSameMapData = false;
        try
        {
            var xml = Save(scene, job, [lamp]);

            Expect.IsFalse(xml.Contains("<traders", StringComparison.Ordinal));
            Expect.IsFalse(xml.Contains("<batteries", StringComparison.Ordinal));
            var loaded = Load(scene, xml, [lamp]);
            Expect.AreEqual(0, loaded._traderBuildings.Count);
        }
        finally
        {
            scene.Manager.ScribeSameMapData = true;
        }
    }

    [Test]
    public static void LoadingASaveWithoutTheBuildingListsDoesNotThrow()
    {
        using var scene = new ManagerScene();
        var job = AddPowerJob(scene);
        var lamp = Powered(scene, Lamp, 0);
        job.PostImport();
        var xml = Regex.Replace(
            Save(scene, job, [lamp]),
            "<(traders|batteries)\\s*/>|<(traders|batteries)>.*?</\\2>",
            "",
            RegexOptions.Singleline
        );

        var loaded = Load(scene, xml, [lamp]);

        Expect.IsNotNull(loaded);
    }
}
