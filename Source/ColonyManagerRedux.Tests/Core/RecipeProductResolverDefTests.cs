// RecipeProductResolverDefTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class RecipeProductResolverDefTests
{
    private sealed class FakeResolver : RecipeProductResolver
    {
        public override bool CanResolve(RecipeDef recipe) => false;

        public override void ConfigureFilter(RecipeDef recipe, ThingFilter filter) { }
    }

    [Test]
    public static void ValidResolverClassProducesNoErrors() =>
        Assert
            .ThatCollection(
                RecipeProductResolverDef.ValidateRecipeProductResolverDefTypes(typeof(FakeResolver))
            )
            .Is.Empty();

    [Test]
    public static void NullResolverClassProducesError() =>
        Assert
            .ThatCollection(RecipeProductResolverDef.ValidateRecipeProductResolverDefTypes(null))
            .Has.Count(1);

    [Test]
    public static void NonResolverTypeProducesError() =>
        Assert
            .ThatCollection(
                RecipeProductResolverDef.ValidateRecipeProductResolverDefTypes(typeof(object))
            )
            .Has.Count(1);
}
