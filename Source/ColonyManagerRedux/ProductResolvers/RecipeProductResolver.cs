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

    /// <summary>
    /// Narrower than <see cref="ConfigureFilter"/>: the subset of that filter's result that
    /// job-linking can treat as this recipe's actual output.
    /// <see cref="ConfigureFilter"/> exists to mirror a vanilla stock-progress counter,
    /// which can be broader than what the recipe can really produce - e.g. a whole category tree
    /// that later grew an unrelated descendant (Odyssey nests a "Fish" category under "MeatRaw",
    /// so a butchery resolver naively allowing all of "MeatRaw" ends up including fish, which no
    /// butchery recipe can actually yield). Using that same broad set for linking would let a
    /// producer job be offered as the source of an ingredient it could never make. Defaults to
    /// <see cref="ConfigureFilter"/>'s own result, since most resolvers' broad set and real set
    /// already coincide; only overridden where they don't.
    /// </summary>
    public virtual void ConfigureLinkableProductFilter(RecipeDef recipe, ThingFilter filter) =>
        ConfigureFilter(recipe, filter);

    /// <summary>
    /// Given every ingredient <paramref name="recipe"/> could accept, the subset that could
    /// actually yield at least one def in <paramref name="desiredOutputs"/> - used to
    /// auto-restrict a linked producer job's own ingredients to what its linked consumers
    /// actually need. Most recipes produce the same thing regardless of which specific
    /// ingredient was used, or their output can't be feasibly predicted per ingredient, so the
    /// default is "no restriction possible" - every candidate is returned unfiltered. Only
    /// overridden by resolvers with a real, computable ingredient-to-output mapping.
    /// </summary>
    public virtual IEnumerable<ThingDef> IngredientsProducing(
        RecipeDef recipe,
        IEnumerable<ThingDef> candidateIngredients,
        IReadOnlyCollection<ThingDef> desiredOutputs
    ) => candidateIngredients;
}
