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

    internal static IEnumerable<ThingDef> GetDeconstructibleBuildings(Map map) =>
        map
            .listerThings.AllThings.OfType<Building>()
            .Where(b =>
                b != null
                && b.Faction != Faction.OfPlayer
                && !b.Position.Fogged(map)
                && b.def.building.IsDeconstructible
                && !b.CostListAdjusted().NullOrEmpty()
                && b.def.resourcesFractionWhenDeconstructed > 0
            )
            .Select(b => b.def)
            .Distinct()
            .OrderBy(b => b.LabelCap.RawText);

    private static List<ThingDef>? _minerals;
    internal static List<ThingDef> AllMinerals
    {
        get
        {
            _minerals ??=
            [
                .. DefDatabase<ThingDef>
                    .AllDefsListForReading.Where(d =>
                        d.building != null
                        && d.building.isNaturalRock
                        && d.building.mineableThing != null
                    )
                    .OrderBy(d => d.LabelCap.RawText),
            ];
            return _minerals;
        }
    }

    internal static IEnumerable<ThingDefCountClass> GetChunkProducts(this ThingDef chunk) =>
        (chunk.butcherProducts ?? Enumerable.Empty<ThingDefCountClass>()).Concat(
            chunk.smeltProducts ?? Enumerable.Empty<ThingDefCountClass>()
        );
}
