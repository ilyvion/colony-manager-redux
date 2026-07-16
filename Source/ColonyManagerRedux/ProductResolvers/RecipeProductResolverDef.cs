// RecipeProductResolverDef.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Definition registering a <see cref="RecipeProductResolver"/>.
/// </summary>
public sealed class RecipeProductResolverDef : Def
{
    /// <summary>
    /// The order in which resolvers are tried; the first whose <see cref="RecipeProductResolver.CanResolve"/>
    /// returns true for a given recipe wins. Lower values are tried first.
    /// </summary>
    public int order = 100;

    /// <summary>
    /// The type of the <see cref="RecipeProductResolver"/> this def registers.
    /// </summary>
    public Type? resolverClass;

    /// <summary>
    /// Gets the resolver instance for this def, lazily created from <see cref="resolverClass"/>.
    /// </summary>
    public RecipeProductResolver Resolver =>
        field ??= (RecipeProductResolver)Activator.CreateInstance(resolverClass!);

    /// <inheritdoc/>
    public override IEnumerable<string> ConfigErrors()
    {
        foreach (var item in base.ConfigErrors())
        {
            yield return item;
        }

        foreach (var item in ValidateRecipeProductResolverDefTypes(resolverClass))
        {
            yield return item;
        }
    }

    /// <summary>
    /// Validates that <paramref name="resolverClass"/> is non-null and assignable to
    /// <see cref="RecipeProductResolver"/>, yielding a config-error string for each violation.
    /// Kept separate from <see cref="ConfigErrors"/> so this is unit-testable without a live
    /// <see cref="Def"/>.
    /// </summary>
    internal static IEnumerable<string> ValidateRecipeProductResolverDefTypes(Type? resolverClass)
    {
        if (resolverClass == null)
        {
            yield return $"{nameof(resolverClass)} is null";
        }
        else if (!typeof(RecipeProductResolver).IsAssignableFrom(resolverClass))
        {
            yield return $"{nameof(resolverClass)} is not {nameof(RecipeProductResolver)} or a subclass thereof";
        }
    }
}
