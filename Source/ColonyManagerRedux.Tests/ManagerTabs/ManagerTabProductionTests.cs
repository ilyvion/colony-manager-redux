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

    [Test]
    public static void GroupRecipesByWorkbenchCrossListsARecipeUsableAtMultipleWorkbenches()
    {
        var tableA = new ThingDef { defName = "CMR_TestWorkbenchA", label = "table a" };
        var tableB = new ThingDef { defName = "CMR_TestWorkbenchB", label = "table b" };
        var recipe = new RecipeDef
        {
            defName = "CMR_TestGroupRecipe",
            recipeUsers = [tableA, tableB],
        };

        var groups = GroupRecipesByWorkbench([recipe], [tableA, tableB]);

        Assert.That(groups.Count).Is.EqualTo(2);
        Assert.That(groups.All(g => g.Recipes.Contains(recipe))).Is.True();
    }

    [Test]
    public static void GroupRecipesByWorkbenchExcludesWorkbenchesThatArentBuilt()
    {
        var built = new ThingDef { defName = "CMR_TestWorkbenchBuilt", label = "built table" };
        var notBuilt = new ThingDef
        {
            defName = "CMR_TestWorkbenchNotBuilt",
            label = "unbuilt table",
        };
        var recipe = new RecipeDef
        {
            defName = "CMR_TestGroupRecipeUnbuilt",
            recipeUsers = [built, notBuilt],
        };

        var groups = GroupRecipesByWorkbench([recipe], [built]);

        Assert.That(groups.Count).Is.EqualTo(1);
        Assert.That(groups[0].Workbench == built).Is.True();
    }

    [Test]
    public static void GroupRecipesByWorkbenchOrdersGroupsByWorkbenchLabel()
    {
        var tableB = new ThingDef { defName = "CMR_TestWorkbenchOrderB", label = "b table" };
        var tableA = new ThingDef { defName = "CMR_TestWorkbenchOrderA", label = "a table" };
        var recipeB = new RecipeDef
        {
            defName = "CMR_TestGroupRecipeOrderB",
            recipeUsers = [tableB],
        };
        var recipeA = new RecipeDef
        {
            defName = "CMR_TestGroupRecipeOrderA",
            recipeUsers = [tableA],
        };

        var groups = GroupRecipesByWorkbench([recipeB, recipeA], [tableA, tableB]);

        Assert.That(groups.Count).Is.EqualTo(2);
        Assert.That(groups[0].Workbench == tableA).Is.True();
        Assert.That(groups[1].Workbench == tableB).Is.True();
    }
}
