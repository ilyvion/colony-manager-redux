// UtilitiesMiningTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;
using Task = ColonyManagerRedux.Managers.ManagerJob_Mining.Task;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class UtilitiesMiningTests
{
    private static readonly List<Task> AllTasks =
    [
        Task.HaulChunks,
        Task.DeconstructBuildings,
        Task.Mine,
    ];

    [Test]
    public static void CompleteListIsUnchanged()
    {
        var current = new List<Task> { Task.Mine, Task.HaulChunks, Task.DeconstructBuildings };

        var result = Utilities_Mining.EnsureAllEnumValuesPresent(current, AllTasks);

        Expect.AreEqual(3, result.Count);
        Expect.IsTrue(result.Contains(Task.Mine));
        Expect.IsTrue(result.Contains(Task.HaulChunks));
        Expect.IsTrue(result.Contains(Task.DeconstructBuildings));
    }

    [Test]
    public static void MissingValueIsAppendedAtEndPreservingOrder()
    {
        var current = new List<Task> { Task.Mine, Task.HaulChunks };

        var result = Utilities_Mining.EnsureAllEnumValuesPresent(current, AllTasks);

        Expect.AreEqual(3, result.Count);
        Expect.AreEqual(Task.Mine, result[0]);
        Expect.AreEqual(Task.HaulChunks, result[1]);
        Expect.AreEqual(Task.DeconstructBuildings, result[2]);
    }

    [Test]
    public static void EmptyListGetsAllValuesInEnumOrder()
    {
        var result = Utilities_Mining.EnsureAllEnumValuesPresent([], AllTasks);

        Expect.AreEqual(3, result.Count);
        Expect.AreEqual(Task.HaulChunks, result[0]);
        Expect.AreEqual(Task.DeconstructBuildings, result[1]);
        Expect.AreEqual(Task.Mine, result[2]);
    }

    [Test]
    public static void NullListGetsAllValuesInEnumOrder()
    {
        var result = Utilities_Mining.EnsureAllEnumValuesPresent(null, AllTasks);

        Expect.AreEqual(3, result.Count);
        Expect.AreEqual(Task.HaulChunks, result[0]);
        Expect.AreEqual(Task.DeconstructBuildings, result[1]);
        Expect.AreEqual(Task.Mine, result[2]);
    }

    // ---- GetChunkProducts / IsChunk ----

    private static ThingDef Product(string name) => new() { defName = name };

    [Test]
    public static void ChunkWithNoProductListsHasNoChunkProducts() =>
        // Regression guard for 'chunk with no butcher products' crashing the job (b068b537):
        // both lists are null on defs that don't declare them.
        Expect.IsEmpty(new ThingDef { defName = "TestChunk" }.GetChunkProducts());

    [Test]
    public static void OnlySmeltProductsAreReturnedWhenThereAreNoButcherProducts()
    {
        var steel = Product("TestSteel");
        var chunk = new ThingDef
        {
            defName = "TestChunk",
            smeltProducts = [new ThingDefCountClass(steel, 5)],
        };

        var products = chunk.GetChunkProducts().ToList();

        Expect.AreEqual(1, products.Count);
        Expect.ReferencesAreEqual(steel, products[0].thingDef);
    }

    [Test]
    public static void ButcherProductsComeBeforeSmeltProducts()
    {
        var meat = Product("TestButchered");
        var steel = Product("TestSmelted");
        var chunk = new ThingDef
        {
            defName = "TestChunk",
            butcherProducts = [new ThingDefCountClass(meat, 1)],
            smeltProducts = [new ThingDefCountClass(steel, 2)],
        };

        var products = chunk.GetChunkProducts().ToList();

        Expect.AreEqual(2, products.Count);
        Expect.ReferencesAreEqual(meat, products[0].thingDef);
        Expect.ReferencesAreEqual(steel, products[1].thingDef);
    }

    [Test]
    public static void OnlyButcherProductsAreReturnedWhenThereAreNoSmeltProducts()
    {
        var meat = Product("TestButchered");
        var chunk = new ThingDef
        {
            defName = "TestChunk",
            butcherProducts = [new ThingDefCountClass(meat, 1)],
        };

        Expect.AreEqual(1, chunk.GetChunkProducts().Count());
    }

    [Test]
    public static void DefWithoutThingCategoriesIsNotAChunk()
    {
        Expect.IsFalse(new ThingDef { defName = "TestNoCategories" }.IsChunk());
        Expect.IsFalse(((ThingDef)null!).IsChunk());
    }

    [Test]
    public static void DefInAChunkCategoryIsAChunk() =>
        Expect.IsTrue(
            new ThingDef
            {
                defName = "TestChunk",
                thingCategories = [ThingCategoryDefOf.StoneChunks],
            }.IsChunk()
        );

    [Test]
    public static void DefInAnUnrelatedCategoryIsNotAChunk() =>
        Expect.IsFalse(
            new ThingDef
            {
                defName = "TestNotChunk",
                thingCategories = [ThingCategoryDefOf.Foods],
            }.IsChunk()
        );

    // ---- GetMinerals / GetDeconstructibleBuildings against the def database ----

    [Test]
    public static void MineralsWithoutAMapAreNaturalRockWithMineableThingSortedByLabel()
    {
        var minerals = Utilities_Mining.GetMinerals(null).ToList();

        Expect.IsNotEmpty(minerals);
        Expect.All(minerals, d => d.building.isNaturalRock && d.building.mineableThing != null);
        Expect.That(() =>
            minerals
                .Select(d => d.LabelCap.RawText)
                .SequenceEqual(minerals.Select(d => d.LabelCap.RawText).OrderBy(l => l))
        );
        Expect.AreEqual(minerals.Count, minerals.Distinct().Count());
    }

    [Test]
    public static void DeconstructibleBuildingsWithoutAMapAreDeconstructibleWithRefund()
    {
        var buildings = Utilities_Mining.GetDeconstructibleBuildings(null).ToList();

        Expect.IsNotEmpty(buildings);
        Expect.All(
            buildings,
            d => d.building.IsDeconstructible && d.resourcesFractionWhenDeconstructed > 0
        );
    }
}
