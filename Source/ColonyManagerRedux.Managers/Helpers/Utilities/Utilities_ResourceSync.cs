// Utilities_ResourceSync.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal static class Utilities_ResourceSync
{
    /// <summary>
    /// Decides whether an "allow" flag for <paramref name="resource"/> should stay set after
    /// toggling one item in <paramref name="allowedItems"/>: true as long as some remaining
    /// allowed item still maps to the same resource. Shared by Hunting's
    /// <c>SetAnimalAllowed</c> and Foraging's <c>SetPlantAllowed</c> — both hit the CHANGELOG
    /// 0.2.0 bug where the filter was cleared even though another allowed item still produced the
    /// resource.
    /// </summary>
    internal static bool ShouldResourceStayAllowed<TItem, TResource>(
        IEnumerable<TItem> allowedItems,
        Func<TItem, TResource> resourceOf,
        TResource resource
    )
        where TResource : class => allowedItems.Any(item => resourceOf(item) == resource);
}
