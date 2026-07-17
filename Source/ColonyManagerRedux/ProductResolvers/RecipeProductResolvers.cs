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

    /// <summary>
    /// A single <see cref="ThingDef"/> standing in for whatever <paramref name="recipe"/>
    /// produces, for UI purposes (e.g. an icon) where a whole <see cref="ThingFilter"/> isn't
    /// useful — recipes resolved via a category-based resolver (butchery, stonecutting,
    /// smelting) have no single static product, so this just takes the first def the resolver's
    /// filter allows. Returns <c>null</c> if <paramref name="recipe"/> has no registered resolver
    /// or that resolver's filter ends up empty.
    /// </summary>
    public static ThingDef? RepresentativeThingDef(RecipeDef recipe) =>
        ResolverFor(recipe) is { } resolver ? RepresentativeThingDef(resolver, recipe) : null;

    /// <summary>
    /// Kept separate from <see cref="RepresentativeThingDef(RecipeDef)"/> so it's unit-testable
    /// against a directly-instantiated resolver, without needing a live <see cref="DefDatabase{T}"/>
    /// of <see cref="RecipeProductResolverDef"/>s.
    /// </summary>
    internal static ThingDef? RepresentativeThingDef(
        RecipeProductResolver resolver,
        RecipeDef recipe
    )
    {
        var filter = new ThingFilter();
        resolver.ConfigureFilter(recipe, filter);
        return filter.AllowedThingDefs.FirstOrDefault();
    }
}
