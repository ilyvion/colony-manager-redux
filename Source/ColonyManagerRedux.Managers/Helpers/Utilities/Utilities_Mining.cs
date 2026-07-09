// Utilities_Mining.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

internal static class Utilities_Mining
{
    private static List<ThingCategoryDef>? _chunkCategoryDefs;
    private static List<ThingCategoryDef> ChunkCategoryDefs
    {
        get
        {
            _chunkCategoryDefs ??= [.. ThingCategoryDefOf.Chunks.ThisAndChildCategoryDefs];
            return _chunkCategoryDefs;
        }
    }

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
}
