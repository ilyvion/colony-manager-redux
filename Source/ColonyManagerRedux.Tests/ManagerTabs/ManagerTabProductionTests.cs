// ManagerTabProductionTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;
using static ColonyManagerRedux.Managers.ManagerTab_Production;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class ManagerTabProductionTests
{
    private sealed class FakeResolver(ThingDef output) : RecipeProductResolver
    {
        public override bool CanResolve(RecipeDef recipe) => true;

        public override void ConfigureFilter(RecipeDef recipe, ThingFilter filter) =>
            filter.SetAllow(output, true);
    }

    // Regression guard: CanPossiblyStore used to check TriggerThreshold.ThresholdFilter instead
    // of the resolver's output filter whenever the job wasn't MaintainStock - but that filter is
    // seeded from the recipe's raw ingredients in ConsumeSurplus mode, not its product, so it
    // wrongly greyed out stockpiles that accept the bill's real output and wrongly permitted
    // ones that only accept the input material. The fix (and this test) drops the mode check
    // entirely: storage compatibility is always about the recipe's actual output.
    [Test]
    public static void CanPossiblyStoreAllowsSlotThatAcceptsResolverOutput()
    {
        var meal = new ThingDef { defName = "CMR_TestCanStoreMeal" };
        var recipe = new RecipeDef { defName = "CMR_TestCanStoreRecipe" };
        var resolver = new FakeResolver(meal);

        Assert.That(CanPossiblyStore(resolver, recipe, def => def == meal)).Is.True();
    }

    [Test]
    public static void CanPossiblyStoreRejectsSlotThatOnlyAcceptsIngredient()
    {
        var meal = new ThingDef { defName = "CMR_TestCanStoreMeal2" };
        var rawFood = new ThingDef { defName = "CMR_TestCanStoreRawFood" };
        var recipe = new RecipeDef { defName = "CMR_TestCanStoreRecipe2" };
        var resolver = new FakeResolver(meal);

        Assert.That(CanPossiblyStore(resolver, recipe, def => def == rawFood)).Is.False();
    }

    [Test]
    public static void CanPossiblyStoreWithNoResolverIsAlwaysCompatible()
    {
        var recipe = new RecipeDef { defName = "CMR_TestCanStoreRecipeNoResolver" };

        Assert.That(CanPossiblyStore(null, recipe, _ => false)).Is.True();
    }
}
