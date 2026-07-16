// RecipeProductResolver_MakeStoneBlocks.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

/// <summary>
/// Resolver for stonecutting recipes (no static product; matches vanilla's own
/// <see cref="RecipeWorkerCounter_MakeStoneBlocks"/>, which always counts
/// <see cref="ThingCategoryDefOf.StoneBlocks"/>'s child defs regardless of which stone was cut).
/// </summary>
internal sealed class RecipeProductResolver_MakeStoneBlocks : RecipeProductResolver
{
    public override bool CanResolve(RecipeDef recipe) =>
        recipe.workerCounterClass == typeof(RecipeWorkerCounter_MakeStoneBlocks);

    public override void ConfigureFilter(RecipeDef recipe, ThingFilter filter) =>
        filter.SetAllow(ThingCategoryDefOf.StoneBlocks, true, null, null);
}
