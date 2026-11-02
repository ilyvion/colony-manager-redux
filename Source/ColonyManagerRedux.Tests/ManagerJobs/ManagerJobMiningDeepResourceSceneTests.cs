// ManagerJobMiningDeepResourceSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// Minerals found by a ground-penetrating scanner only exist in the map's deep resource grid, and
/// must still be offered by the map-locked mineral list.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobMiningDeepResourceSceneTests
{
    private static ThingDef UraniumVein => DefDatabase<ThingDef>.GetNamed("MineableUranium");
    private static ThingDef SilverVein => DefDatabase<ThingDef>.GetNamed("MineableSilver");

    /// <summary>A map with no veins of <paramref name="vein"/> on its surface.</summary>
    private static Map MapWithoutVeins(ManagerScene scene, ThingDef vein)
    {
        var map = scene.AddExtraMap();
        foreach (var thing in map.listerThings.ThingsOfDef(vein).ToList())
        {
            thing.Destroy();
        }
        return map;
    }

    private static void AddLump(Map map, ThingDef vein) =>
        map.deepResourceGrid.SetAt(map.Center, vein.building.mineableThing, 100);

    [Test]
    public static void MineralOfADeepResourceIsListedWithoutASurfaceVein()
    {
        using var scene = new ManagerScene();
        var map = MapWithoutVeins(scene, UraniumVein);
        Expect.IsFalse(Utilities_Mining.GetMinerals(map).Contains(UraniumVein));

        AddLump(map, UraniumVein);

        Expect.AreEqual(1, Utilities_Mining.GetMinerals(map).Count(d => d == UraniumVein));
    }

    [Test]
    public static void MineralOfAnotherDeepResourceIsNotListed()
    {
        using var scene = new ManagerScene();
        var map = MapWithoutVeins(scene, UraniumVein);
        foreach (var thing in map.listerThings.ThingsOfDef(SilverVein).ToList())
        {
            thing.Destroy();
        }

        AddLump(map, SilverVein);

        var minerals = Utilities_Mining.GetMinerals(map).ToList();
        Expect.IsTrue(minerals.Contains(SilverVein));
        Expect.IsFalse(minerals.Contains(UraniumVein));
    }

    [Test]
    public static void ExhaustedDeepResourceIsNoLongerListed()
    {
        using var scene = new ManagerScene();
        var map = MapWithoutVeins(scene, UraniumVein);
        AddLump(map, UraniumVein);

        map.deepResourceGrid.SetAt(map.Center, UraniumVein.building.mineableThing, 0);

        Expect.IsFalse(Utilities_Mining.GetMinerals(map).Contains(UraniumVein));
    }

    [Test]
    public static void MapLockedJobOffersDeepResourceMineralAfterRefresh()
    {
        using var scene = new ManagerScene();
        var map = MapWithoutVeins(scene, UraniumVein);
        using var mapScene = new ManagerScene(map);
        var job = mapScene.AddJob<ManagerJob_Mining>("CM_MiningManager");
        job.MineralsLockedToMap = true;
        job.RefreshAllBuildingsAndMinerals();
        Expect.IsFalse(job.AllMinerals.Contains(UraniumVein));

        AddLump(map, UraniumVein);
        job.RefreshAllBuildingsAndMinerals();

        Expect.IsTrue(job.AllMinerals.Contains(UraniumVein));
    }
}
