// AreaScribeSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// Areas are saved by label and looked up again on load, since they cannot be referenced.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class AreaScribeSceneTests
{
    private sealed class AreaProbe(AreaManager areaManager) : IExposable
    {
        public Area? Single;
        public HashSet<Area> Set = [];
        private string? _singleLabel;
        private List<string>? _setLabels;

        public void ExposeData()
        {
            Utilities.Scribe_AreaByLabel(ref Single, ref _singleLabel, "single", areaManager);
            Utilities.Scribe_AreasByLabel(ref Set, ref _setLabels, "set", areaManager);
        }
    }

    private static string Label() => "CMRTest-" + Guid.NewGuid().ToString("N");

    private static string Save(ManagerScene scene, AreaProbe probe) =>
        ScribeRoundTrip.Save(
            new ScribeRoundTrip.ManagerHarness(
                scene.Manager,
                new ScribeRoundTrip.DeepHarness<AreaProbe>(scene.Map.areaManager) { Value = probe }
            )
        );

    private static AreaProbe Load(ManagerScene scene, string xml)
    {
        var loaded = new ScribeRoundTrip.DeepHarness<AreaProbe>(scene.Map.areaManager);
        ScribeRoundTrip.Load(xml, new ScribeRoundTrip.ManagerHarness(scene.Manager, loaded));
        return loaded.Value!;
    }

    [Test]
    public static void AreaIsResolvedByLabel()
    {
        using var scene = new ManagerScene();
        var area = scene.AddArea(Label(), scene.Cells(2, 2));

        var loaded = Load(
            scene,
            Save(scene, new AreaProbe(scene.Map.areaManager) { Single = area })
        );

        Expect.ReferencesAreEqual(area, loaded.Single);
    }

    [Test]
    public static void NoAreaLoadsAsNoArea()
    {
        using var scene = new ManagerScene();

        var loaded = Load(scene, Save(scene, new AreaProbe(scene.Map.areaManager)));

        Expect.IsNull(loaded.Single);
        Expect.IsEmpty(loaded.Set);
    }

    [Test]
    public static void RenamedAreaLoadsAsNoArea()
    {
        using var scene = new ManagerScene();
        var area = scene.AddArea(Label(), scene.Cells(2, 2));
        var xml = Save(scene, new AreaProbe(scene.Map.areaManager) { Single = area });

        area.SetLabel(Label());
        var loaded = Load(scene, xml);

        Expect.IsNull(loaded.Single);
    }

    [Test]
    public static void DeletedAreaLoadsAsNoArea()
    {
        using var scene = new ManagerScene();
        var area = scene.AddArea(Label(), scene.Cells(2, 2));
        var xml = Save(scene, new AreaProbe(scene.Map.areaManager) { Single = area });

        area.Delete();
        var loaded = Load(scene, xml);

        Expect.IsNull(loaded.Single);
    }

    [Test]
    public static void AreaSetIsResolvedByLabel()
    {
        using var scene = new ManagerScene();
        var first = scene.AddArea(Label(), scene.Cells(2, 2));
        var second = scene.AddArea(Label(), scene.Cells(2, 2, offsetX: 4));

        var loaded = Load(
            scene,
            Save(scene, new AreaProbe(scene.Map.areaManager) { Set = [first, second] })
        );

        Expect.AreEqual(2, loaded.Set.Count);
        Expect.IsTrue(loaded.Set.Contains(first) && loaded.Set.Contains(second));
    }

    // A set that lost an area must not contain a null entry: jobs read each area's label.
    [Test]
    public static void AreaSetLeavesOutAreasThatNoLongerExist()
    {
        using var scene = new ManagerScene();
        var kept = scene.AddArea(Label(), scene.Cells(2, 2));
        var deleted = scene.AddArea(Label(), scene.Cells(2, 2, offsetX: 4));
        var xml = Save(scene, new AreaProbe(scene.Map.areaManager) { Set = [kept, deleted] });

        deleted.Delete();
        var loaded = Load(scene, xml);

        Expect.AreEqual(1, loaded.Set.Count);
        Expect.IsTrue(loaded.Set.Contains(kept));
        Expect.None(loaded.Set, a => a == null);
    }

    [Test]
    public static void EmptyAreaSetRoundTripsAsEmpty()
    {
        using var scene = new ManagerScene();

        var loaded = Load(scene, Save(scene, new AreaProbe(scene.Map.areaManager)));

        Expect.IsEmpty(loaded.Set);
    }

    [Test]
    public static void ScribingAreasRequiresAnAreaManager()
    {
        Area? area = null;
        string? label = null;
        var set = new HashSet<Area>();
        List<string>? labels = null;

        _ = Expect.Throws<ArgumentNullException>(() =>
            Utilities.Scribe_AreaByLabel(ref area, ref label, "x", null!)
        );
        _ = Expect.Throws<ArgumentNullException>(() =>
            Utilities.Scribe_AreasByLabel(ref set, ref labels, "x", null!)
        );
    }
}
