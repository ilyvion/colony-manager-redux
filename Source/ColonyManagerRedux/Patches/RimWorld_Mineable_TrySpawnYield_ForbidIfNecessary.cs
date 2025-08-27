// RimWorld_Mineable_TrySpawnYield_ForbidIfNecessary.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

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
}
