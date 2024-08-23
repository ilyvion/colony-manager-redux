// RimWorld_Dialog_ModSettings_Ctor.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HarmonyPatch(typeof(Dialog_ModSettings))]
[HarmonyPatch([typeof(Mod)])]
internal static class RimWorld_Dialog_ModSettings_Ctor
{
    [HarmonyPatch(MethodType.Constructor)]
    private static void Postfix(Mod mod)
    {
        if (mod != ColonyManagerReduxMod.Instance)
        {
            return;
        }
        ColonyManagerReduxMod.Settings.PreOpen();
    }
}
