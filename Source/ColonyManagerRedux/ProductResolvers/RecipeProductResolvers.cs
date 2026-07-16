// RecipeProductResolvers.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Looks up the registered <see cref="RecipeProductResolver"/> (via <see cref="RecipeProductResolverDef"/>)
/// that applies to a given <see cref="RecipeDef"/>.
/// </summary>
public static class RecipeProductResolvers
{
    /// <summary>
    /// Returns the first registered resolver (in ascending <see cref="RecipeProductResolverDef.order"/>)
    /// whose <see cref="RecipeProductResolver.CanResolve"/> returns true for <paramref name="recipe"/>,
    /// or <c>null</c> if none apply.
    /// </summary>
    public static RecipeProductResolver? ResolverFor(RecipeDef recipe) =>
        DefDatabase<RecipeProductResolverDef>
            .AllDefsListForReading.OrderBy(d => d.order)
            .Select(d => d.Resolver)
            .FirstOrDefault(r => r.CanResolve(recipe));
}
