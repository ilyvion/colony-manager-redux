// RecipeProductResolver_Simple.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

/// <summary>
/// Fallback resolver for recipes with a single, static product
/// (<see cref="RecipeDef.ProducedThingDef"/>) — the case every production job already handled
/// before pluggable resolvers existed.
/// </summary>
internal sealed class RecipeProductResolver_Simple : RecipeProductResolver
{
    public override bool CanResolve(RecipeDef recipe) => recipe.ProducedThingDef != null;

    public override void ConfigureFilter(RecipeDef recipe, ThingFilter filter) =>
        filter.SetAllow(recipe.ProducedThingDef!, true);
}
