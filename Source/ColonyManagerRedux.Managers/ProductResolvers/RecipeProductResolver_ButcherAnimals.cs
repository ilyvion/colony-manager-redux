// RecipeProductResolver_ButcherAnimals.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

/// <summary>
/// Resolver for butchery recipes (no static product; matches vanilla's own
/// <see cref="RecipeWorkerCounter_ButcherAnimals"/>, which always counts
/// <see cref="ThingCategoryDefOf.MeatRaw"/>'s child defs regardless of which animal was
/// butchered).
/// </summary>
internal sealed class RecipeProductResolver_ButcherAnimals : RecipeProductResolver
{
    public override bool CanResolve(RecipeDef recipe) =>
        recipe.workerCounterClass == typeof(RecipeWorkerCounter_ButcherAnimals);

    public override void ConfigureFilter(RecipeDef recipe, ThingFilter filter) =>
        filter.SetAllow(ThingCategoryDefOf.MeatRaw, true, null, null);
}
