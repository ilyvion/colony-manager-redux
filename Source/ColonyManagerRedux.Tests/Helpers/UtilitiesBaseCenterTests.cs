// UtilitiesBaseCenterTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.Playing)]
internal sealed class UtilitiesBaseCenterTests
{
    // With nobody to pick, the map centre is the answer rather than a NullReferenceException.
    [Test]
    public static void NoPawnsFallsBackToMapCenter() =>
        Expect.AreEqual(
            new IntVec3(7, 0, 9),
            Utilities.PawnPositionOrCenter([], new IntVec3(7, 0, 9))
        );

    [Test]
    public static void ColonistPositionIsUsedWhenThereIsOne()
    {
        using var scene = new ManagerScene();
        var pawn = scene.SpawnColonist(scene.Cells(1, 1, 25, 25).First());

        Expect.AreEqual(
            pawn.Position,
            Utilities.PawnPositionOrCenter([pawn], new IntVec3(-1, 0, -1))
        );
    }

    [Test]
    public static void ManagerStationInteractionCellIsTheBaseCenter()
    {
        using var scene = new ManagerScene();
        var cell = scene.Cells(1, 1, offsetX: 20, offsetZ: 20).First();
        _ = scene.SpawnBuilding(ThingDef.Named("CM_BasicManagerStation"), cell);

        var expected = scene
            .Map.listerBuildings.AllBuildingsColonistOfClass<Building_ManagerStation>()
            .First()
            .InteractionCell;

        Expect.AreEqual(expected, scene.Map.GetBaseCenter());
    }

    [Test]
    public static void BaseCenterIsWalkableWithoutAStation()
    {
        var map = Find.CurrentMap;
        if (map.listerBuildings.AllBuildingsColonistOfClass<Building_ManagerStation>().Any())
        {
            return;
        }

        var center = map.GetBaseCenter();

        Expect.IsTrue(center.InBounds(map), "Base center is on the map");
        Expect.IsTrue(center.Walkable(map), "Base center is walkable");
    }
}
