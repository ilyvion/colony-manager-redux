// Utilities_Hunting.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public static class Utilities_Hunting
{
    public static int EstimatedMeatCount(this PawnKindDef kind)
    {
        return (int)kind.race.GetStatValueAbstract(StatDefOf.MeatAmount);
    }

    public static int EstimatedMeatCount(this Pawn p)
    {
        return (int)p.GetStatValue(StatDefOf.MeatAmount);
    }

    public static int EstimatedMeatCount(this Corpse c)
    {
        return EstimatedMeatCount(c.InnerPawn);
    }

    internal static IEnumerable<PawnKindDef> GetAnimals(Map map)
    {
        return map.Biome.AllWildAnimals
            .Concat(map.mapPawns.AllPawns
                .Where(p => (p.RaceProps?.Animal ?? false)
                    && !(map.fogGrid?.IsFogged(p.Position) ?? true))
                .Select(p => p.kindDef)
            .Distinct()
            .OrderBy(pk => pk.label));
    }
}
