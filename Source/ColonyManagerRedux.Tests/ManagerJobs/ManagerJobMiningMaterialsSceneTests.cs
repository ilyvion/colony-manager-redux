// ManagerJobMiningMaterialsSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The material lookups of <see cref="ManagerJob_Mining"/>: what a building costs, what a chunk
/// yields and what a mineral yields.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobMiningMaterialsSceneTests
{
    private static ThingDef SteelVein => DefDatabase<ThingDef>.GetNamed("MineableSteel");
    private static ThingDef GraniteRock => DefDatabase<ThingDef>.GetNamed("Granite");

    [Test]
    public static void NoBuildingHasNoMaterials() =>
        Expect.IsEmpty(ManagerJob_Mining.GetMaterialsInBuilding(null!).ToList());

    [Test]
    public static void BuildingMaterialsAreItsCostsAndAllowedStuffs()
    {
        var materials = ManagerJob_Mining.GetMaterialsInBuilding(ThingDefOf.Door).ToList();

        Expect.IsTrue(materials.Contains(ThingDefOf.Steel), "cost");
        Expect.IsTrue(materials.Contains(ThingDefOf.WoodLog), "allowed stuff");
    }

    [Test]
    public static void BuildingWithoutACostListOnlyHasItsAllowedStuffs()
    {
        var wall = ThingDefOf.Wall;
        var materials = ManagerJob_Mining.GetMaterialsInBuilding(wall).ToList();

        Expect.IsTrue(wall.costList.NullOrEmpty(), "walls have no fixed cost");
        Expect.IsTrue(materials.Contains(ThingDefOf.WoodLog), "wood is an allowed stuff");
        Expect.IsTrue(materials.All(m => m.IsStuff), "only stuffs");
    }

    [Test]
    public static void ChunkMaterialsAreItsProducts()
    {
        var chunk = ThingDef.Named("ChunkGranite");
        var expected = chunk.butcherProducts.Select(p => p.thingDef).ToList();

        var materials = ManagerJob_Mining.GetMaterialsInChunk(chunk).ToList();

        Expect.IsTrue(materials.Count > 0);
        Expect.AreEqual(
            string.Join(",", expected.Select(d => d.defName)),
            string.Join(",", materials.Select(d => d.defName))
        );
    }

    [Test]
    public static void SteelVeinYieldsSteel()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<ManagerJob_Mining>("CM_MiningManager");

        var materials = job.GetMaterialsInMineral(SteelVein);

        Expect.AreEqual(1, materials.Count);
        Expect.AreEqual(ThingDefOf.Steel, materials[0]);
    }

    [Test]
    public static void StoneRockYieldsItsChunksProducts()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<ManagerJob_Mining>("CM_MiningManager");
        var chunk = GraniteRock.building.mineableThing;

        var materials = job.GetMaterialsInMineral(GraniteRock);

        Expect.IsTrue(chunk.IsChunk(), "granite mines to a chunk");
        Expect.AreEqual(
            string.Join(",", ManagerJob_Mining.GetMaterialsInChunk(chunk).Select(d => d.defName)),
            string.Join(",", materials.Select(d => d.defName))
        );
    }

    [Test]
    public static void MineralWithoutAMineableThingYieldsNothing()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<ManagerJob_Mining>("CM_MiningManager");
        var barren = new ThingDef
        {
            defName = "TestBarrenRock",
            building = new BuildingProperties(),
        };

        Expect.IsEmpty(job.GetMaterialsInMineral(barren));
    }

    [Test]
    public static void MineralMaterialsAreCachedPerMineral()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<ManagerJob_Mining>("CM_MiningManager");

        var first = job.GetMaterialsInMineral(SteelVein);
        var second = job.GetMaterialsInMineral(SteelVein);

        Expect.ReferencesAreEqual(first, second);
    }
}
