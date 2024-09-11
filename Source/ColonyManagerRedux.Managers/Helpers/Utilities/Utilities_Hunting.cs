// Utilities_Hunting.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using static ColonyManagerRedux.Managers.ManagerJob_Hunting;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal static class Utilities_Hunting
{
    public static int EstimatedMeatCount(this PawnKindDef kind) =>
        (int)kind.race.GetStatValueAbstract(StatDefOf.MeatAmount);

    public static int EstimatedMeatCount(this Pawn p) =>
        (int)p.GetStatValue(StatDefOf.MeatAmount);

    public static int EstimatedMeatCount(this Corpse c) =>
        EstimatedMeatCount(c.InnerPawn);

    public static int EstimatedLeatherCount(this PawnKindDef kind) =>
        (int)kind.race.GetStatValueAbstract(StatDefOf.LeatherAmount);

    public static int EstimatedLeatherCount(this Pawn p) =>
        (int)p.GetStatValue(StatDefOf.LeatherAmount);

    public static int EstimatedLeatherCount(this Corpse c) => EstimatedLeatherCount(c.InnerPawn);

    public static int EstimatedYield(this PawnKindDef kind, HuntingTargetResource resource) =>
        resource == HuntingTargetResource.Meat
            ? kind.EstimatedMeatCount()
            : kind.EstimatedLeatherCount();

    public static int EstimatedYield(this Pawn p, HuntingTargetResource resource) =>
        resource == HuntingTargetResource.Meat
            ? p.EstimatedMeatCount()
            : p.EstimatedLeatherCount();

    public static int EstimatedYield(this Corpse c, HuntingTargetResource resource) =>
        EstimatedYield(c.InnerPawn, resource);

    internal static IEnumerable<PawnKindDef> GetMapPawnKindDefs(Map map, bool animalsOnly = true) =>
        // Get all the wild animals on the map
        map.Biome.AllWildAnimals
            // and any visible pawns on the map
            .Concat(map.mapPawns.AllPawns
                .Where(p => (!animalsOnly || (p.RaceProps?.Animal ?? false))
                    && !(map.fogGrid?.IsFogged(p.Position) ?? true))
                .Select(p => p.kindDef))
            // and any corpses on the map
            .Concat(map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse)
                .Cast<Corpse>()
                .Where(c => c?.InnerPawn != null)
                .Select(c => c.InnerPawn.kindDef))
            .Distinct()
            .OrderBy(pk => pk.label);
}
