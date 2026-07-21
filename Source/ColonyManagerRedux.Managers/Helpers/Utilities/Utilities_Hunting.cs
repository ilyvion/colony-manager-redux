// Utilities_Hunting.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024–2026 Alexander Krivács Schrøder

using static ColonyManagerRedux.Managers.ManagerJob_Hunting;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal static class Utilities_Hunting
{
    public static int EstimatedMeatCount(this PawnKindDef kind) =>
        (int)kind.race.GetStatValueAbstract(StatDefOf.MeatAmount);

    public static int EstimatedMeatCount(this Pawn p) => (int)p.GetStatValue(StatDefOf.MeatAmount);

    public static int EstimatedMeatCount(this Corpse c) => EstimatedMeatCount(c.InnerPawn);

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
        resource == HuntingTargetResource.Meat ? p.EstimatedMeatCount() : p.EstimatedLeatherCount();

    public static int EstimatedYield(this Corpse c, HuntingTargetResource resource) =>
        EstimatedYield(c.InnerPawn, resource);

    internal static IEnumerable<PawnKindDef> GetMapPawnKindDefs(Map? map, bool animalsOnly = true)
    {
        if (map != null)
        {
            // Get all the wild animals on the map
            var wild = map.Biome.AllWildAnimals;
            var visible = map
                .mapPawns.AllPawns.Where(p =>
                    (!animalsOnly || (p.RaceProps?.Animal ?? false))
                    && !(map.fogGrid?.IsFogged(p.Position) ?? true)
                )
                .Select(p => p.kindDef);
            // and any corpses on the map
            var corpses = map
                .listerThings.ThingsInGroup(ThingRequestGroup.Corpse)
                .Cast<Corpse>()
                .Where(c =>
                    c?.InnerPawn != null
                    && (!animalsOnly || (c.InnerPawn.RaceProps?.Animal ?? false))
                )
                .Select(c => c.InnerPawn.kindDef);

            return CombineAndOrderPawnKindSources(wild, visible, corpses);
        }
        else
        {
            return DefDatabase<PawnKindDef>.AllDefsListForReading.Where(pkd =>
                !animalsOnly || (pkd.RaceProps?.Animal ?? false)
            );
        }
    }

    /// <summary>
    /// Unions the three sources of on-map <see cref="PawnKindDef"/>s (biome wild animals, visible
    /// unfogged pawns, unfogged corpses), deduplicates, and orders by label. Split out from
    /// <see cref="GetMapPawnKindDefs"/> so the combine/dedupe/order logic is unit-testable without
    /// a live <see cref="Map"/>.
    /// </summary>
    internal static IEnumerable<PawnKindDef> CombineAndOrderPawnKindSources(
        IEnumerable<PawnKindDef> wild,
        IEnumerable<PawnKindDef> visible,
        IEnumerable<PawnKindDef> corpses
    ) => wild.Concat(visible).Concat(corpses).Distinct().OrderBy(pk => pk.label);

    /// <summary>
    /// Chooses the color of the manhunter-risk warning icon drawn next to an animal's toggle
    /// row: gray when the animal isn't allowed (the risk doesn't apply), otherwise red above a
    /// 25% manhunter-on-damage chance and orange below it.
    /// </summary>
    internal static Color GetManhunterIconColor(bool allowed, float manhunterOnDamageChance) =>
        !allowed ? Color.gray
        : manhunterOnDamageChance > 0.25 ? Color.red
        : Resources.Orange;

    /// <summary>
    /// Chooses the color of the venerated-animal icon drawn next to an animal's toggle row:
    /// gray when the animal isn't allowed, otherwise red when every colonist venerates it and
    /// orange when only some do.
    /// </summary>
    internal static Color GetVeneratedIconColor(bool allowed, bool allVenerated) =>
        !allowed ? Color.gray
        : allVenerated ? Color.red
        : Resources.Orange;
}
