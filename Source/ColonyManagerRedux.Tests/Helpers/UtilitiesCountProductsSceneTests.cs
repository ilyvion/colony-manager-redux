// UtilitiesCountProductsSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// <see cref="Utilities.CountProducts"/> against real things, stockpiles and the resource counter.
/// The pure predicates it uses are covered elsewhere; these tests check the wiring between them.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class UtilitiesCountProductsSceneTests
{
    private static ThingFilter FilterFor(ThingDef def)
    {
        var filter = new ThingFilter();
        filter.SetDisallowAll();
        filter.SetAllow(def, true);
        return filter;
    }

    private static int Count(
        ManagerScene scene,
        ThingDef def,
        Zone_Stockpile? stockpile = null,
        bool countAllOnMap = false
    )
    {
        scene.Map.resourceCounter.UpdateResourceCounts();
        return scene.Map.CountProducts(FilterFor(def), stockpile, countAllOnMap);
    }

    private static IEnumerable<IntVec3> StockCells(ManagerScene scene) => scene.Cells(5, 5);

    private static IntVec3 OutsideCell(ManagerScene scene) =>
        scene.Cells(1, 1, offsetX: 20).First();

    [Test]
    public static void ResourceInStockpileIsCountedOnceWithAndWithoutCountAllOnMap()
    {
        using var scene = new ManagerScene();
        var before = Count(scene, ThingDefOf.Steel);
        _ = scene.AddStockpile(StockCells(scene));
        _ = scene.SpawnThing(ThingDefOf.Steel, StockCells(scene).First(), 40);

        Expect.AreEqual(before + 40, Count(scene, ThingDefOf.Steel));
        Expect.AreEqual(before + 40, Count(scene, ThingDefOf.Steel, countAllOnMap: true));
    }

    [Test]
    public static void ResourceOutsideStorageIsOnlyCountedWithCountAllOnMap()
    {
        using var scene = new ManagerScene();
        var before = Count(scene, ThingDefOf.Steel);
        _ = scene.SpawnThing(ThingDefOf.Steel, OutsideCell(scene), 30);

        Expect.AreEqual(before, Count(scene, ThingDefOf.Steel));
        Expect.AreEqual(before + 30, Count(scene, ThingDefOf.Steel, countAllOnMap: true));
    }

    [Test]
    public static void ResourceInAndOutsideStorageIsNeverDoubleCounted()
    {
        using var scene = new ManagerScene();
        var before = Count(scene, ThingDefOf.Steel);
        _ = scene.AddStockpile(StockCells(scene));
        _ = scene.SpawnThing(ThingDefOf.Steel, StockCells(scene).First(), 40);
        _ = scene.SpawnThing(ThingDefOf.Steel, OutsideCell(scene), 30);

        Expect.AreEqual(before + 40, Count(scene, ThingDefOf.Steel));
        Expect.AreEqual(before + 70, Count(scene, ThingDefOf.Steel, countAllOnMap: true));
    }

    [Test]
    public static void NonResourceInStockpileIsCountedAndOutsideOnlyWithCountAllOnMap()
    {
        using var scene = new ManagerScene();
        var before = Count(scene, ThingDefOf.MeleeWeapon_Knife);
        _ = scene.AddStockpile(StockCells(scene));
        _ = scene.SpawnThing(ThingDefOf.MeleeWeapon_Knife, StockCells(scene).First());
        _ = scene.SpawnThing(ThingDefOf.MeleeWeapon_Knife, OutsideCell(scene));

        Expect.AreEqual(before + 1, Count(scene, ThingDefOf.MeleeWeapon_Knife));
        Expect.AreEqual(
            before + 2,
            Count(scene, ThingDefOf.MeleeWeapon_Knife, countAllOnMap: true)
        );
    }

    [Test]
    public static void StockpileRestrictsCountToThatZone()
    {
        using var scene = new ManagerScene();
        var zoneA = scene.AddStockpile(scene.Cells(3, 3));
        var zoneB = scene.AddStockpile(scene.Cells(3, 3, offsetX: 6));
        _ = scene.SpawnThing(ThingDefOf.Steel, scene.Cells(1, 1).First(), 25);
        _ = scene.SpawnThing(ThingDefOf.Steel, scene.Cells(1, 1, offsetX: 6).First(), 60);

        Expect.AreEqual(25, Count(scene, ThingDefOf.Steel, zoneA));
        Expect.AreEqual(60, Count(scene, ThingDefOf.Steel, zoneB));
        Expect.AreEqual(25, Count(scene, ThingDefOf.Steel, zoneA, countAllOnMap: true));
    }

    [Test]
    public static void ForbiddenThingsAreNotCounted()
    {
        using var scene = new ManagerScene();
        var before = Count(scene, ThingDefOf.MeleeWeapon_Knife);
        _ = scene.AddStockpile(StockCells(scene));
        var cells = StockCells(scene).ToList();
        _ = scene.SpawnThing(ThingDefOf.MeleeWeapon_Knife, cells[0]);
        var forbidden = scene.SpawnThing(ThingDefOf.MeleeWeapon_Knife, cells[1]);
        forbidden.SetForbidden(true, warnOnFail: false);

        Expect.AreEqual(before + 1, Count(scene, ThingDefOf.MeleeWeapon_Knife));
    }

    [Test]
    public static void FoggedThingsAreNotCounted()
    {
        using var scene = new ManagerScene();
        var before = Count(scene, ThingDefOf.MeleeWeapon_Knife);
        _ = scene.AddStockpile(StockCells(scene));
        var cells = StockCells(scene).ToList();
        _ = scene.SpawnThing(ThingDefOf.MeleeWeapon_Knife, cells[0]);
        _ = scene.SpawnThing(ThingDefOf.MeleeWeapon_Knife, cells[1]);
        var fogIndex = scene.Map.cellIndices.CellToIndex(cells[1]);
        var fogGrid = scene.Map.fogGrid.fogGrid;
        var wasFogged = fogGrid.IsSet(fogIndex);
        fogGrid.Set(fogIndex, true);
        try
        {
            Expect.AreEqual(before + 1, Count(scene, ThingDefOf.MeleeWeapon_Knife));
        }
        finally
        {
            fogGrid.Set(fogIndex, wasFogged);
        }
    }

    [Test]
    public static void QualityRangeFiltersThings()
    {
        using var scene = new ManagerScene();
        _ = scene.AddStockpile(StockCells(scene));
        var cells = StockCells(scene).ToList();
        var awful = scene.SpawnThing(ThingDefOf.MeleeWeapon_Knife, cells[0]);
        var good = scene.SpawnThing(ThingDefOf.MeleeWeapon_Knife, cells[1]);
        awful
            .TryGetComp<CompQuality>()
            .SetQuality(QualityCategory.Awful, ArtGenerationContext.Colony);
        good.TryGetComp<CompQuality>()
            .SetQuality(QualityCategory.Good, ArtGenerationContext.Colony);

        var filter = FilterFor(ThingDefOf.MeleeWeapon_Knife);
        filter.AllowedQualityLevels = new QualityRange(
            QualityCategory.Good,
            QualityCategory.Legendary
        );
        var count = scene.Map.CountProducts(filter);

        var allQualities = Count(scene, ThingDefOf.MeleeWeapon_Knife);
        Expect.AreEqual(allQualities - 1, count);
    }

    [Test]
    public static void HitPointsPercentFiltersThings()
    {
        using var scene = new ManagerScene();
        _ = scene.AddStockpile(StockCells(scene));
        var cells = StockCells(scene).ToList();
        var damaged = scene.SpawnThing(ThingDefOf.MeleeWeapon_Knife, cells[0]);
        _ = scene.SpawnThing(ThingDefOf.MeleeWeapon_Knife, cells[1]);
        damaged.HitPoints = damaged.MaxHitPoints / 2;

        var filter = FilterFor(ThingDefOf.MeleeWeapon_Knife);
        filter.AllowedHitPointsPercents = new FloatRange(0.9f, 1f);
        var count = scene.Map.CountProducts(filter);

        Expect.AreEqual(Count(scene, ThingDefOf.MeleeWeapon_Knife) - 1, count);
    }

    [Test]
    public static void UntrackedHitPointsStillCountWithTheDefaultRange()
    {
        using var scene = new ManagerScene();
        var before = Count(scene, ThingDefOf.MeleeWeapon_Knife);
        _ = scene.AddStockpile(StockCells(scene));
        var thing = scene.SpawnThing(ThingDefOf.MeleeWeapon_Knife, StockCells(scene).First());
        thing.HitPoints = -1;

        Expect.AreEqual(before + 1, Count(scene, ThingDefOf.MeleeWeapon_Knife));
    }

    [Test]
    public static void CountingAcrossManyYieldsMatchesTheNumberOfThings()
    {
        using var scene = new ManagerScene();
        var before = Count(scene, ThingDefOf.MeleeWeapon_Knife);
        _ = scene.AddStockpile(StockCells(scene));
        foreach (var cell in StockCells(scene))
        {
            _ = scene.SpawnThing(ThingDefOf.MeleeWeapon_Knife, cell);
        }

        Expect.AreEqual(before + 25, Count(scene, ThingDefOf.MeleeWeapon_Knife));
    }

    [Test]
    public static void NullArgumentsThrowWhenTheCoroutineStarts()
    {
        using var scene = new ManagerScene();
        var filter = FilterFor(ThingDefOf.Steel);

        _ = Expect.Throws<ArgumentNullException>(() =>
            Utilities
                .CountProductsCoroutine(null!, filter, new Boxed<int>())
                .GetEnumerator()
                .MoveNext()
        );
        _ = Expect.Throws<ArgumentNullException>(() =>
            Utilities
                .CountProductsCoroutine(scene.Map, null!, new Boxed<int>())
                .GetEnumerator()
                .MoveNext()
        );
        _ = Expect.Throws<ArgumentNullException>(() =>
            Utilities.CountProductsCoroutine(scene.Map, filter, null!).GetEnumerator().MoveNext()
        );
    }

    [Test]
    public static void NullThingDefInTheFilterIsSkipped()
    {
        using var scene = new ManagerScene();
        var before = Count(scene, ThingDefOf.Steel);
        _ = scene.AddStockpile(StockCells(scene));
        _ = scene.SpawnThing(ThingDefOf.Steel, StockCells(scene).First(), 40);
        var filter = FilterFor(ThingDefOf.Steel);
        // Not reachable through ThingFilter's own API, but a report showed it can happen.
        _ = filter.allowedDefs.Add(null!);
        scene.Map.resourceCounter.UpdateResourceCounts();

        Expect.AreEqual(before + 40, scene.Map.CountProducts(filter));
        Expect.AreEqual(before + 40, scene.Map.CountProducts(filter, countAllOnMap: true));
    }
}
