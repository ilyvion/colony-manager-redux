// RimWorld_Mineable_TrySpawnYield_ForbidIfNecessary.cs
// Copyright (c) 2024–2025 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HarmonyPatch]
internal static class RimWorld_Mineable_TrySpawnYield_ForbidIfNecessary
{
    public static MethodBase TargetMethod()
    {
        var type = AccessTools.FirstInner(
            typeof(Mineable),
            t => t.GetMethodNames()
#if v1_5
                .Any(n => n.Contains("ForbidIfNecessary"))
        );
        return AccessTools.FirstMethod(type, method => method.Name.Contains("ForbidIfNecessary"));
#else
                    .Any(n => n.Contains("ForbidIfNecessary", StringComparison.Ordinal))
        );
        return AccessTools.FirstMethod(
            type,
            method => method.Name.Contains("ForbidIfNecessary", StringComparison.Ordinal)
        );
#endif
    }

    private static void Postfix(Thing thing, Pawn ___pawn) => NotifyChunkMined(___pawn, thing);

    internal static void NotifyChunkMined(Pawn? pawn, Thing thing)
    {
        if (
            pawn?.Map == null
            || pawn.Faction != Faction.OfPlayer
            || thing.def.thingCategories?.Contains(ThingCategoryDefOf.StoneChunks) != true
        )
        {
            return;
        }

        var manager = Manager.For(pawn.Map);
        if (manager == null)
        {
            return;
        }

        foreach (var job in manager.JobTracker.JobsOfType<INotifyStoneChunkMined>())
        {
            job.Notify_StoneChunkMined(pawn, thing);
        }
    }
}
