// RecipeProductResolverTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class RecipeProductResolverTests
{
    [Test]
    public static void SimpleResolverResolvesRecipeWithSingleProduct()
    {
        var thingDef = new ThingDef { defName = "CMR_TestProduct" };
        var recipe = new RecipeDef { products = [new ThingDefCountClass(thingDef, 1)] };

        Expect.IsTrue(new RecipeProductResolver_Simple().CanResolve(recipe));
    }

    [Test]
    public static void SimpleResolverDoesNotResolveRecipeWithNoProduct() =>
        Expect.IsFalse(new RecipeProductResolver_Simple().CanResolve(new RecipeDef()));

    [Test]
    public static void SimpleResolverConfiguresFilterWithProducedThingDef()
    {
        var thingDef = new ThingDef { defName = "CMR_TestProduct2" };
        var recipe = new RecipeDef { products = [new ThingDefCountClass(thingDef, 1)] };
        var filter = new ThingFilter();

        new RecipeProductResolver_Simple().ConfigureFilter(recipe, filter);

        Expect.IsTrue(filter.Allows(thingDef));
    }

    [Test]
    public static void ButcherAnimalsResolverResolvesMatchingWorkerCounterClass() =>
        Expect.IsTrue(
            new RecipeProductResolver_ButcherAnimals().CanResolve(
                new RecipeDef { workerCounterClass = typeof(RecipeWorkerCounter_ButcherAnimals) }
            )
        );

    [Test]
    public static void ButcherAnimalsResolverDoesNotResolveOtherWorkerCounterClass() =>
        Expect.IsFalse(
            new RecipeProductResolver_ButcherAnimals().CanResolve(
                new RecipeDef { workerCounterClass = typeof(RecipeWorkerCounter) }
            )
        );

    [Test]
    public static void ButcherAnimalsResolverConfiguresFilterWithMeatRawCategory()
    {
        var filter = new ThingFilter();

        new RecipeProductResolver_ButcherAnimals().ConfigureFilter(new RecipeDef(), filter);

        var meatDef = ThingCategoryDefOf.MeatRaw.DescendantThingDefs.First();
        Expect.IsTrue(filter.Allows(meatDef));
    }

    [Test]
    public static void MakeStoneBlocksResolverResolvesMatchingWorkerCounterClass() =>
        Expect.IsTrue(
            new RecipeProductResolver_MakeStoneBlocks().CanResolve(
                new RecipeDef { workerCounterClass = typeof(RecipeWorkerCounter_MakeStoneBlocks) }
            )
        );

    [Test]
    public static void MakeStoneBlocksResolverDoesNotResolveOtherWorkerCounterClass() =>
        Expect.IsFalse(
            new RecipeProductResolver_MakeStoneBlocks().CanResolve(
                new RecipeDef { workerCounterClass = typeof(RecipeWorkerCounter) }
            )
        );

    [Test]
    public static void MakeStoneBlocksResolverConfiguresFilterWithStoneBlocksCategory()
    {
        var filter = new ThingFilter();

        new RecipeProductResolver_MakeStoneBlocks().ConfigureFilter(new RecipeDef(), filter);

        var blocksDef = ThingCategoryDefOf.StoneBlocks.DescendantThingDefs.First();
        Expect.IsTrue(filter.Allows(blocksDef));

        var meatDef = ThingCategoryDefOf.MeatRaw.DescendantThingDefs.First();
        Expect.IsFalse(filter.Allows(meatDef));
    }

    [Test]
    public static void SmeltedResolverResolvesRecipeWithSmeltedSpecialProduct() =>
        Expect.IsTrue(
            new RecipeProductResolver_Smelted().CanResolve(
                new RecipeDef { specialProducts = [SpecialProductType.Smelted] }
            )
        );

    [Test]
    public static void SmeltedResolverDoesNotResolveRecipeWithoutSmeltedSpecialProduct() =>
        Expect.IsFalse(new RecipeProductResolver_Smelted().CanResolve(new RecipeDef()));

    [Test]
    public static void SmeltedResolverConfiguresFilterWithSmeltableStuff()
    {
        var filter = new ThingFilter();

        new RecipeProductResolver_Smelted().ConfigureFilter(new RecipeDef(), filter);

        Expect.IsTrue(filter.Allows(ThingDefOf.Steel));
        Expect.IsTrue(filter.Allows(ThingDefOf.Silver));

        var nonSmeltableStuff = DefDatabase<ThingDef>.AllDefsListForReading.First(d =>
            d.IsStuff && !d.smeltable
        );
        Expect.IsFalse(filter.Allows(nonSmeltableStuff));
    }

    [Test]
    public static void RepresentativeThingDefReturnsFirstAllowedDefFromResolverFilter()
    {
        var thingDef = new ThingDef { defName = "CMR_TestProduct3" };
        var recipe = new RecipeDef { products = [new ThingDefCountClass(thingDef, 1)] };

        var result = RecipeProductResolvers.RepresentativeThingDef(
            new RecipeProductResolver_Simple(),
            recipe
        );

        Expect.AreEqual(thingDef.defName, result?.defName);
    }

    [Test]
    public static void RepresentativeThingDefReturnsNullForEmptyResolverFilter()
    {
        var result = RecipeProductResolvers.RepresentativeThingDef(
            new RecipeProductResolver_ButcherAnimals_EmptyFilter(),
            new RecipeDef()
        );

        Expect.IsNull(result?.defName);
    }

    private sealed class RecipeProductResolver_ButcherAnimals_EmptyFilter : RecipeProductResolver
    {
        public override bool CanResolve(RecipeDef recipe) => true;

        public override void ConfigureFilter(RecipeDef recipe, ThingFilter filter) { }
    }
}
