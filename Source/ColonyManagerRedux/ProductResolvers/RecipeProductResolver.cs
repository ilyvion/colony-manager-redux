// RecipeProductResolver.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Maps a <see cref="RecipeDef"/> to the <see cref="ThingFilter"/> content a production
/// manager job should threshold against, for recipes whose output isn't a single static
/// <see cref="RecipeDef.ProducedThingDef"/> (e.g. butchery, stonecutting). Registered via
/// <see cref="RecipeProductResolverDef"/> so third-party mods can add their own without
/// depending on any concrete manager job implementation.
/// </summary>
public abstract class RecipeProductResolver
{
    /// <summary>
    /// Whether this resolver knows how to configure a threshold filter for
    /// <paramref name="recipe"/>.
    /// </summary>
    public abstract bool CanResolve(RecipeDef recipe);

    /// <summary>
    /// Populates <paramref name="filter"/> with whatever <paramref name="recipe"/> is
    /// considered to produce. Only called after <see cref="CanResolve"/> returned true for
    /// the same recipe.
    /// </summary>
    public abstract void ConfigureFilter(RecipeDef recipe, ThingFilter filter);
}
