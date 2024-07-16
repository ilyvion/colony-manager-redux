// Utilities_Mining.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public static class Utilities_Mining
{
    public static bool IsChunk(this ThingDef def)
    {
        return def?.thingCategories?.Any(c => ThingCategoryDefOf.Chunks.ThisAndChildCategoryDefs.Contains(c)) ??
            false;
    }

    internal static IEnumerable<ThingDef> GetDeconstructibleBuildings(Map map)
    {
        return map.listerThings.AllThings.OfType<Building>()
            .Where(b => b.Faction != Faction.OfPlayer
                && !b.Position.Fogged(map)
                && b.def.building.IsDeconstructible
                && !b.CostListAdjusted().NullOrEmpty()
                && b.def.resourcesFractionWhenDeconstructed > 0)
            .Select(b => b.def)
            .Distinct()
            .OrderBy(b => b.LabelCap.RawText);
    }

    internal static IEnumerable<ThingDef> GetMinerals()
    {
        return DefDatabase<ThingDef>.AllDefsListForReading
            .Where(d => d.building != null
                && d.building.isNaturalRock)
            .OrderBy(d => d.LabelCap.RawText);
    }
}
