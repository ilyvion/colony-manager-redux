// RecipeProductResolverTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class RecipeProductResolverTests
{
    [Test]
    public static void SimpleResolverResolvesRecipeWithSingleProduct()
    {
        var thingDef = new ThingDef { defName = "CMR_TestProduct" };
        var recipe = new RecipeDef { products = [new ThingDefCountClass(thingDef, 1)] };

        Assert.That(new RecipeProductResolver_Simple().CanResolve(recipe)).Is.True();
    }

    [Test]
    public static void SimpleResolverDoesNotResolveRecipeWithNoProduct() =>
        Assert.That(new RecipeProductResolver_Simple().CanResolve(new RecipeDef())).Is.False();

    [Test]
    public static void SimpleResolverConfiguresFilterWithProducedThingDef()
    {
        var thingDef = new ThingDef { defName = "CMR_TestProduct2" };
        var recipe = new RecipeDef { products = [new ThingDefCountClass(thingDef, 1)] };
        var filter = new ThingFilter();

        new RecipeProductResolver_Simple().ConfigureFilter(recipe, filter);

        Assert.That(filter.Allows(thingDef)).Is.True();
    }

    [Test]
    public static void ButcherAnimalsResolverResolvesMatchingWorkerCounterClass() =>
        Assert
            .That(
                new RecipeProductResolver_ButcherAnimals().CanResolve(
                    new RecipeDef
                    {
                        workerCounterClass = typeof(RecipeWorkerCounter_ButcherAnimals),
                    }
                )
            )
            .Is.True();

    [Test]
    public static void ButcherAnimalsResolverDoesNotResolveOtherWorkerCounterClass() =>
        Assert
            .That(
                new RecipeProductResolver_ButcherAnimals().CanResolve(
                    new RecipeDef { workerCounterClass = typeof(RecipeWorkerCounter) }
                )
            )
            .Is.False();

    [Test]
    public static void ButcherAnimalsResolverConfiguresFilterWithMeatRawCategory()
    {
        var filter = new ThingFilter();

        new RecipeProductResolver_ButcherAnimals().ConfigureFilter(new RecipeDef(), filter);

        var meatDef = ThingCategoryDefOf.MeatRaw.DescendantThingDefs.First();
        Assert.That(filter.Allows(meatDef)).Is.True();
    }

    [Test]
    public static void MakeStoneBlocksResolverResolvesMatchingWorkerCounterClass() =>
        Assert
            .That(
                new RecipeProductResolver_MakeStoneBlocks().CanResolve(
                    new RecipeDef
                    {
                        workerCounterClass = typeof(RecipeWorkerCounter_MakeStoneBlocks),
                    }
                )
            )
            .Is.True();

    [Test]
    public static void MakeStoneBlocksResolverDoesNotResolveOtherWorkerCounterClass() =>
        Assert
            .That(
                new RecipeProductResolver_MakeStoneBlocks().CanResolve(
                    new RecipeDef { workerCounterClass = typeof(RecipeWorkerCounter) }
                )
            )
            .Is.False();

    [Test]
    public static void MakeStoneBlocksResolverConfiguresFilterWithStoneBlocksCategory()
    {
        var filter = new ThingFilter();

        new RecipeProductResolver_MakeStoneBlocks().ConfigureFilter(new RecipeDef(), filter);

        var blocksDef = ThingCategoryDefOf.StoneBlocks.DescendantThingDefs.First();
        Assert.That(filter.Allows(blocksDef)).Is.True();

        var meatDef = ThingCategoryDefOf.MeatRaw.DescendantThingDefs.First();
        Assert.That(filter.Allows(meatDef)).Is.False();
    }

    [Test]
    public static void SmeltedResolverResolvesRecipeWithSmeltedSpecialProduct() =>
        Assert
            .That(
                new RecipeProductResolver_Smelted().CanResolve(
                    new RecipeDef { specialProducts = [SpecialProductType.Smelted] }
                )
            )
            .Is.True();

    [Test]
    public static void SmeltedResolverDoesNotResolveRecipeWithoutSmeltedSpecialProduct() =>
        Assert.That(new RecipeProductResolver_Smelted().CanResolve(new RecipeDef())).Is.False();

    [Test]
    public static void SmeltedResolverConfiguresFilterWithSmeltableStuff()
    {
        var filter = new ThingFilter();

        new RecipeProductResolver_Smelted().ConfigureFilter(new RecipeDef(), filter);

        Assert.That(filter.Allows(ThingDefOf.Steel)).Is.True();
        Assert.That(filter.Allows(ThingDefOf.Silver)).Is.True();

        var nonSmeltableStuff = DefDatabase<ThingDef>.AllDefsListForReading.First(d =>
            d.IsStuff && !d.smeltable
        );
        Assert.That(filter.Allows(nonSmeltableStuff)).Is.False();
    }
}
