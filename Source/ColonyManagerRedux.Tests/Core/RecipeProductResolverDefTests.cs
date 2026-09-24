// RecipeProductResolverDefTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class RecipeProductResolverDefTests
{
    private sealed class FakeResolver : RecipeProductResolver
    {
        public override bool CanResolve(RecipeDef recipe) => false;

        public override void ConfigureFilter(RecipeDef recipe, ThingFilter filter) { }
    }

    [Test]
    public static void ValidResolverClassProducesNoErrors() =>
        Expect.IsEmpty(
            RecipeProductResolverDef.ValidateRecipeProductResolverDefTypes(typeof(FakeResolver))
        );

    [Test]
    public static void NullResolverClassProducesError() =>
        Expect.AreEqual(
            1,
            RecipeProductResolverDef.ValidateRecipeProductResolverDefTypes(null).Count()
        );

    [Test]
    public static void NonResolverTypeProducesError() =>
        Expect.AreEqual(
            1,
            RecipeProductResolverDef.ValidateRecipeProductResolverDefTypes(typeof(object)).Count()
        );
}
