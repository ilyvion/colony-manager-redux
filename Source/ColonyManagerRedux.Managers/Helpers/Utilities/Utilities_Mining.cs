// Utilities_Mining.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024–2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

internal static class Utilities_Mining
{
    private static List<ThingCategoryDef> ChunkCategoryDefs =>
        field ??= [.. ThingCategoryDefOf.Chunks.ThisAndChildCategoryDefs];

    public static bool IsChunk(this ThingDef def) =>
        def?.thingCategories?.Any(ChunkCategoryDefs.Contains) ?? false;

    internal static IEnumerable<ThingDef> GetDeconstructibleBuildings(Map? map) =>
        (
            map != null
                ? map
                    .listerThings.AllThings.OfType<Building>()
                    .Where(b =>
                        b != null
                        && b.Faction != Faction.OfPlayer
                        && !b.Position.Fogged(map)
                        && !b.CostListAdjusted().NullOrEmpty()
                    )
                    .Select(b => b.def)
                : DefDatabase<ThingDef>.AllDefsListForReading.Where(d =>
                    !d.costList.NullOrEmpty() || d.MadeFromStuff
                )
        )
            .Where(d =>
                d.building != null
                && d.building.IsDeconstructible
                && d.resourcesFractionWhenDeconstructed > 0
            )
            .Distinct()
            .OrderBy(d => d.LabelCap.RawText);

    internal static IEnumerable<ThingDef> GetMinerals(Map? map) =>
        (
            map != null
                ? map.listerThings.AllThings.OfType<Building>().Select(b => b.def)
                : DefDatabase<ThingDef>.AllDefsListForReading
        )
            .Where(d =>
                d.building != null && d.building.isNaturalRock && d.building.mineableThing != null
            )
            .Distinct()
            .OrderBy(d => d.LabelCap.RawText);

    internal static IEnumerable<ThingDefCountClass> GetChunkProducts(this ThingDef chunk) =>
        (chunk.butcherProducts ?? Enumerable.Empty<ThingDefCountClass>()).Concat(
            chunk.smeltProducts ?? Enumerable.Empty<ThingDefCountClass>()
        );

    /// <summary>
    /// Repairs a saved priority-order list after loading: appends any <typeparamref name="T"/>
    /// enum values missing from <paramref name="current"/> (e.g. because a newer mod version
    /// added a task) in enum-declaration order, preserving the existing order and never removing
    /// stale/unknown values already present.
    /// </summary>
    internal static List<T> EnsureAllEnumValuesPresent<T>(
        List<T>? current,
        IReadOnlyList<T> allValues
    )
        where T : struct, Enum
    {
        current ??= [];
        if (current.Count != allValues.Count)
        {
            foreach (var value in allValues)
            {
                if (!current.Contains(value))
                {
                    current.Add(value);
                }
            }
        }
        return current;
    }
}
