// RimWorld_Mineable_TrySpawnYield_ForbidIfNecessary.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Reflection;

namespace ColonyManagerRedux;

[HarmonyPatch]
internal static class RimWorld_Mineable_TrySpawnYield_ForbidIfNecessary
{
    public static MethodBase TargetMethod()
    {
        var type = AccessTools.FirstInner(typeof(Mineable),
            t => t.GetMethodNames().Any(n => n.Contains("ForbidIfNecessary")));
        return AccessTools.FirstMethod(type, method => method.Name.Contains("ForbidIfNecessary"));
    }

    private static void Postfix(Thing thing, Pawn ___pawn)
    {
        if (___pawn != null && ___pawn.Faction == Faction.OfPlayer &&
            thing.def.thingCategories.Contains(ThingCategoryDefOf.StoneChunks))
        {
            foreach (var miningJob in Manager.For(___pawn.Map).JobTracker
                .JobsOfType<INotifyStoneChunkMined>())
            {
                miningJob.Notify_StoneChunkMined(___pawn, thing);
            }
        }
    }
}
