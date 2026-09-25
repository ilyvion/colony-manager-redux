// TriggerThresholdSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// <see cref="Trigger_Threshold"/> on a live map: op restriction, counting and caching, and the
/// by-label stockpile round trip.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class TriggerThresholdSceneTests
{
    private static Trigger_Threshold AddTrigger(
        ManagerScene scene,
        out TestManagerJobA job,
        IReadOnlyList<Trigger_Threshold.Ops>? ops = null
    )
    {
        job = scene.AddJob<TestManagerJobA>();
        var trigger = ops == null ? new Trigger_Threshold(job) : new Trigger_Threshold(job, ops);
        job.SetTrigger(trigger);
        return trigger;
    }

    private static Trigger_Threshold AddSteelTrigger(
        ManagerScene scene,
        Zone_Stockpile stockpile,
        out TestManagerJobA job
    )
    {
        var trigger = AddTrigger(scene, out job);
        trigger.Stockpile = stockpile;
        trigger.CountAllOnMap = false;
        trigger.ThresholdFilter.SetAllow(ThingDefOf.Steel, true);
        return trigger;
    }

    // CORE-1: RestrictSupportedOps, constructor validation, DefaultOp

    [Test]
    public static void RestrictingToASetThatKeepsTheCurrentOpChangesNothing()
    {
        using var scene = new ManagerScene();
        var trigger = AddTrigger(scene, out _);
        trigger.Op = Trigger_Threshold.Ops.Equals;

        trigger.RestrictSupportedOps([
            Trigger_Threshold.Ops.LowerThan,
            Trigger_Threshold.Ops.Equals,
        ]);

        Expect.AreEqual(Trigger_Threshold.Ops.Equals, trigger.Op);
        Expect.AreEqual(Trigger_Threshold.Ops.LowerThan, trigger.DefaultOp);
        Expect.IsTrue(trigger.SupportsOp(Trigger_Threshold.Ops.Equals));
        Expect.IsFalse(trigger.SupportsOp(Trigger_Threshold.Ops.HigherThan));
        Expect.AreEqual(2, trigger.SupportedOps.Count);
    }

    [Test]
    [WarningsAllowed("no longer supported")]
    public static void RestrictingAwayTheCurrentOpMigratesToTheNewDefault()
    {
        using var scene = new ManagerScene();
        var trigger = AddTrigger(scene, out _);
        trigger.Op = Trigger_Threshold.Ops.HigherThan;

        trigger.RestrictSupportedOps(Trigger_Threshold.AccumulationOnlyOps);

        Expect.AreEqual(Trigger_Threshold.Ops.LowerThan, trigger.Op);
        Expect.AreEqual(Trigger_Threshold.Ops.LowerThan, trigger.DefaultOp);
        Expect.IsFalse(trigger.SupportsOp(Trigger_Threshold.Ops.HigherThan));
    }

    [Test]
    public static void RestrictingSupportedOpsRejectsNullAndEmpty()
    {
        using var scene = new ManagerScene();
        var trigger = AddTrigger(scene, out _);

        _ = Expect.Throws<ArgumentNullException>(() => trigger.RestrictSupportedOps(null!));
        _ = Expect.Throws<ArgumentException>(() => trigger.RestrictSupportedOps([]));
        Expect.AreEqual(Trigger_Threshold.AllOps.Count, trigger.SupportedOps.Count);
    }

    [Test]
    public static void ConstructorRejectsNullJobAndNullOrEmptyOps()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();

        _ = Expect.Throws<ArgumentNullException>(() => _ = new Trigger_Threshold(null!));
        _ = Expect.Throws<ArgumentNullException>(() => _ = new Trigger_Threshold(job, null!));
        _ = Expect.Throws<ArgumentException>(() => _ = new Trigger_Threshold(job, []));
    }

    [Test]
    public static void ConstructorStartsOnTheFirstSupportedOp()
    {
        using var scene = new ManagerScene();
        var trigger = AddTrigger(
            scene,
            out _,
            [Trigger_Threshold.Ops.NotEquals, Trigger_Threshold.Ops.Equals]
        );

        Expect.AreEqual(Trigger_Threshold.Ops.NotEquals, trigger.Op);
        Expect.AreEqual(Trigger_Threshold.Ops.NotEquals, trigger.DefaultOp);
    }

    // CORE-2: counting, caching and State

    [Test]
    public static void CountIsCachedUntilTheCacheIsInvalidated()
    {
        using var scene = new ManagerScene();
        var stockpile = scene.AddStockpile(scene.Cells(3, 1));
        var cells = scene.Cells(3, 1).ToList();
        _ = scene.SpawnThing(ThingDefOf.Steel, cells[0], 60);
        var trigger = AddSteelTrigger(scene, stockpile, out _);
        Expect.AreEqual(60, trigger.GetCurrentCount());

        _ = scene.SpawnThing(ThingDefOf.Steel, cells[1], 10);

        Expect.AreEqual(60, trigger.GetCurrentCount());
        Expect.AreEqual(70, trigger.GetCurrentCount(cached: false));
        Expect.AreEqual(70, trigger.GetCurrentCount());
    }

    // Regression guard: toggling CountAllOnMap must not keep serving the previous count. A
    // revolver is not a resource, so it is counted from the map rather than from storage.
    [Test]
    public static void TogglingCountAllOnMapInvalidatesTheCachedCount()
    {
        using var scene = new ManagerScene();
        var revolver = DefDatabase<ThingDef>.GetNamed("Gun_Revolver");
        var stockpile = scene.AddStockpile(scene.Cells(2, 1));
        var outside = scene.Cells(2, 1, offsetZ: 5).First();
        _ = scene.SpawnThing(revolver, scene.Cells(1, 1).First());
        var trigger = AddSteelTrigger(scene, stockpile, out _);
        trigger.ThresholdFilter.SetAllow(revolver, true);
        Expect.AreEqual(1, trigger.GetCurrentCount());
        _ = scene.SpawnThing(revolver, outside);
        trigger.Stockpile = null;
        Expect.AreEqual(1, trigger.GetCurrentCount());

        trigger.CountAllOnMap = true;

        Expect.IsTrue(trigger.GetCurrentCount() >= 2);
    }

    [Test]
    public static void ChangingTheThresholdFilterInvalidatesTheCountAndRaisesSettingsChanged()
    {
        using var scene = new ManagerScene();
        var stockpile = scene.AddStockpile(scene.Cells(3, 1));
        var cells = scene.Cells(3, 1).ToList();
        _ = scene.SpawnThing(ThingDefOf.Steel, cells[0], 60);
        var trigger = AddSteelTrigger(scene, stockpile, out _);
        var changes = 0;
        trigger.SettingsChanged = () => changes++;
        Expect.AreEqual(60, trigger.GetCurrentCount());
        _ = scene.SpawnThing(ThingDefOf.Steel, cells[1], 10);

        trigger.ThresholdFilter.SetAllow(ThingDefOf.Silver, true);

        Expect.AreEqual(1, changes);
        Expect.AreEqual(70, trigger.GetCurrentCount());
    }

    [Test]
    public static void StateIsTrueBelowTheTargetAndFalseAtIt()
    {
        using var scene = new ManagerScene();
        var stockpile = scene.AddStockpile(scene.Cells(2, 1));
        _ = scene.SpawnThing(ThingDefOf.Steel, scene.Cells(1, 1).First(), 60);
        var trigger = AddSteelTrigger(scene, stockpile, out _);
        trigger.Op = Trigger_Threshold.Ops.LowerThan;

        trigger.TargetCount = 61;
        Expect.IsTrue(trigger.State);

        trigger.TargetCount = 60;
        Expect.IsFalse(trigger.State);
    }

    // CORE-3: stockpile round trip by label

    private static (string xml, TestManagerJobA job) SaveTriggerJob(
        ManagerScene scene,
        Trigger_Threshold trigger,
        TestManagerJobA job
    )
    {
        job.SetTrigger(trigger);
        var xml = ScribeRoundTrip.Save(
            new ScribeRoundTrip.ManagerHarness(
                scene.Manager,
                new ScribeRoundTrip.JobListHarness(scene.Manager, [job])
            )
        );
        return (xml, job);
    }

    private static Trigger_Threshold? LoadTrigger(ManagerScene scene, string xml)
    {
        var loaded = new ScribeRoundTrip.JobListHarness(scene.Manager);
        ScribeRoundTrip.Load(xml, new ScribeRoundTrip.ManagerHarness(scene.Manager, loaded));
        return loaded.Jobs.Single().Trigger as Trigger_Threshold;
    }

    private static Zone_Stockpile Labeled(Zone_Stockpile stockpile, string label)
    {
        stockpile.label = label;
        return stockpile;
    }

    [Test]
    public static void StockpileIsResolvedByLabelOnLoad()
    {
        using var scene = new ManagerScene();
        var stockpile = Labeled(scene.AddStockpile(scene.Cells(1, 1)), "CMRTest-" + Guid.NewGuid());
        var trigger = AddTrigger(scene, out var job);
        trigger.Stockpile = stockpile;
        trigger.TargetCount = 42;

        var (xml, _) = SaveTriggerJob(scene, trigger, job);
        var loaded = LoadTrigger(scene, xml);

        Expect.IsNotNull(loaded);
        Expect.ReferencesAreEqual(stockpile, loaded!.Stockpile);
        Expect.AreEqual(42, loaded.TargetCount);
    }

    [Test]
    public static void NoStockpileLoadsAsNoStockpile()
    {
        using var scene = new ManagerScene();
        var trigger = AddTrigger(scene, out var job);

        var (xml, _) = SaveTriggerJob(scene, trigger, job);
        var loaded = LoadTrigger(scene, xml);

        Expect.IsNotNull(loaded);
        Expect.IsNull(loaded!.Stockpile);
    }

    [Test]
    public static void StockpilesWithTheSameLabelResolveToTheFirst()
    {
        using var scene = new ManagerScene();
        var label = "CMRTest-" + Guid.NewGuid();
        var first = Labeled(scene.AddStockpile(scene.Cells(1, 1)), label);
        var second = Labeled(scene.AddStockpile(scene.Cells(1, 1, offsetX: 3)), label);
        var trigger = AddTrigger(scene, out var job);
        trigger.Stockpile = second;

        var (xml, _) = SaveTriggerJob(scene, trigger, job);
        var loaded = LoadTrigger(scene, xml);

        Expect.ReferencesAreEqual(first, loaded!.Stockpile);
    }

    [Test]
    public static void StockpileRenamedBetweenSaveAndLoadResolvesToNothing()
    {
        using var scene = new ManagerScene();
        var stockpile = Labeled(scene.AddStockpile(scene.Cells(1, 1)), "CMRTest-" + Guid.NewGuid());
        var trigger = AddTrigger(scene, out var job);
        trigger.Stockpile = stockpile;
        var (xml, _) = SaveTriggerJob(scene, trigger, job);

        stockpile.label = "CMRTest-renamed-" + Guid.NewGuid();
        var loaded = LoadTrigger(scene, xml);

        Expect.IsNull(loaded!.Stockpile);
    }

    [Test]
    public static void ZoneThatIsNotAStockpileIsIgnoredWhenResolvingTheLabel()
    {
        using var scene = new ManagerScene();
        var label = "CMRTest-" + Guid.NewGuid();
        var stockpile = Labeled(scene.AddStockpile(scene.Cells(1, 1)), label);
        var trigger = AddTrigger(scene, out var job);
        trigger.Stockpile = stockpile;
        var (xml, _) = SaveTriggerJob(scene, trigger, job);

        stockpile.label = "CMRTest-renamed-" + Guid.NewGuid();
        _ = scene.AddGrowingZone(label, scene.Cells(1, 1, offsetX: 3));
        var loaded = LoadTrigger(scene, xml);

        Expect.IsNull(loaded!.Stockpile);
    }
}
