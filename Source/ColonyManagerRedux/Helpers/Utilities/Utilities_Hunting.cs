// Utilities_Hunting.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyManagerRedux;

public static class Utilities_Hunting
{
    public static ThingCategoryDef FoodRaw = DefDatabase<ThingCategoryDef>.GetNamed("FoodRaw");
    public static ThingDef HumanMeat = ThingDefOf.Human.race.meatDef;
    public static ThingDef InsectMeat = ThingDef.Named("Megaspider").race.meatDef;
    public static ThingCategoryDef MeatRaw = ThingCategoryDefOf.MeatRaw;

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
