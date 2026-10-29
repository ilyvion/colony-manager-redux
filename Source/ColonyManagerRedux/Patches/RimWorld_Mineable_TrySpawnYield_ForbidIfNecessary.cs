// RimWorld_Mineable_TrySpawnYield_ForbidIfNecessary.cs
// Copyright (c) 2024–2025 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HarmonyPatch]
internal static class RimWorld_Mineable_TrySpawnYield_ForbidIfNecessary
{
    internal static bool Prepare() =>
        HarmonyTargetUtility.ExpectSingle(
            TargetMethods(),
            "patch Mineable.TrySpawnYield's ForbidIfNecessary",
            ColonyManagerReduxMod.Instance.LogError
        );

    internal static List<MethodInfo> TargetMethods() =>
        HarmonyTargetUtility.FindMethods(typeof(Mineable), "ForbidIfNecessary");

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
