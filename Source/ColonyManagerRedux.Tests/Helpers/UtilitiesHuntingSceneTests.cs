// UtilitiesHuntingSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// <see cref="Utilities_Hunting.GetMapPawnKindDefs"/> against a real map: the combine step it
/// ends with is covered separately, these tests check what feeds it.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class UtilitiesHuntingSceneTests
{
    // Animal kinds the map's biome does not spawn wild, so they only show up in the result when
    // a test puts one on the map.
    private static List<PawnKindDef> KindsOutsideBiome(Map map, int count)
    {
        var wild = map.Biome.AllWildAnimals.ToHashSet();
        return
        [
            .. DefDatabase<PawnKindDef>
                .AllDefsListForReading.Where(k => k.RaceProps.Animal && !wild.Contains(k))
                .Take(count),
        ];
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

    [Test]
    public static void VisibleWildAnimalIsIncluded()
    {
        using var scene = new ManagerScene();
        var kind = KindsOutsideBiome(scene.Map, 1)[0];
        var cell = scene.Cells(1, 1, 10, 10).First();
        _ = scene.SpawnAnimal(kind, cell);

        Expect.IsTrue(Utilities_Hunting.GetMapPawnKindDefs(scene.Map).Contains(kind));
    }

    [Test]
    public static void FoggedAnimalIsExcluded()
    {
        using var scene = new ManagerScene();
        var kind = KindsOutsideBiome(scene.Map, 1)[0];
        var cell = scene.Cells(1, 1, 10, 12).First();
        _ = scene.SpawnAnimal(kind, cell);

        WithFog(
            scene.Map,
            cell,
            () => Expect.IsFalse(Utilities_Hunting.GetMapPawnKindDefs(scene.Map).Contains(kind))
        );
    }

    [Test]
    public static void CorpseKindIsIncludedEvenWhenFogged()
    {
        using var scene = new ManagerScene();
        var kind = KindsOutsideBiome(scene.Map, 1)[0];
        var cell = scene.Cells(1, 1, 10, 14).First();
        _ = scene.SpawnCorpse(kind, cell);

        WithFog(
            scene.Map,
            cell,
            () => Expect.IsTrue(Utilities_Hunting.GetMapPawnKindDefs(scene.Map).Contains(kind))
        );
    }

    [Test]
    public static void HumansAreExcludedOnlyWhenAnimalsOnly()
    {
        using var scene = new ManagerScene();
        var colonist = scene.SpawnColonist(scene.Cells(1, 1, 10, 16).First());

        Expect.IsFalse(
            Utilities_Hunting
                .GetMapPawnKindDefs(scene.Map, animalsOnly: true)
                .Contains(colonist.kindDef)
        );
        Expect.IsTrue(
            Utilities_Hunting
                .GetMapPawnKindDefs(scene.Map, animalsOnly: false)
                .Contains(colonist.kindDef)
        );
    }

    [Test]
    public static void ResultIsDistinctAndOrderedByLabel()
    {
        using var scene = new ManagerScene();
        var kinds = KindsOutsideBiome(scene.Map, 2);
        _ = scene.SpawnAnimal(kinds[0], scene.Cells(1, 1, 10, 18).First());
        _ = scene.SpawnAnimal(kinds[0], scene.Cells(1, 1, 12, 18).First());
        _ = scene.SpawnCorpse(kinds[0], scene.Cells(1, 1, 14, 18).First());
        _ = scene.SpawnAnimal(kinds[1], scene.Cells(1, 1, 16, 18).First());

        var result = Utilities_Hunting.GetMapPawnKindDefs(scene.Map).ToList();

        Expect.AreEqual(result.Count, result.Distinct().Count());
        Expect.AreEqual(1, result.Count(k => k == kinds[0]));
        Expect.That(() => result.SequenceEqual(result.OrderBy(k => k.label)));
    }

    [Test]
    public static void NullMapGivesEveryAnimalKind()
    {
        var animals = Utilities_Hunting.GetMapPawnKindDefs(null).ToList();
        var everything = Utilities_Hunting.GetMapPawnKindDefs(null, animalsOnly: false).ToList();

        Expect.IsNotEmpty(animals);
        Expect.All(animals, k => k.RaceProps.Animal);
        Expect.AreEqual(
            DefDatabase<PawnKindDef>.AllDefsListForReading.Count(k => k.RaceProps.Animal),
            animals.Count
        );
        Expect.IsTrue(everything.Contains(PawnKindDefOf.Colonist));
    }
}
