// AAAA_Patches.cs
// Copyright (c) 2025 Alexander Krivács Schrøder

using ColonyManagerRedux.AAAA.Core;
using seekiworks_AllowedAreaAutomaticAdapter;

namespace ColonyManagerRedux.AAAA.Patches;

[StaticConstructorOnStartup]
internal static class AAAA_Patches
{
    static AAAA_Patches()
    {
        var harmony = new Harmony($"{ColonyManagerReduxMod.PackageId}.AAAA");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}

[HarmonyPatch(typeof(Watcher), nameof(Watcher.AllowedAreaChangeDangerMode))]
internal static class AllowedAreaAutomaticAdapter_Watcher_AllowedAreaChangeDangerMode_Patches
{
    internal static void Postfix(Map map)
    {
        ColonyManagerReduxMod.Instance.LogVerboseMessage($"AAAA entering danger mode for {map}");

        foreach (var job in Manager.For(map).JobTracker.Jobs)
        {
            foreach (var comp in job.CompsOfType<AAAAManagerJobComp>())
            {
                comp.AllowedAreaChangeDangerMode();
            }
        }
    }
}

[HarmonyPatch(typeof(Watcher), nameof(Watcher.AllowedAreaChangeNormalMode))]
internal static class AllowedAreaAutomaticAdapter_Watcher_AllowedAreaChangeNormalMode_Patches
{
    internal static void Postfix(Map map)
    {
        ColonyManagerReduxMod.Instance.LogVerboseMessage($"AAAA entering normal mode for {map}");

        foreach (var job in Manager.For(map).JobTracker.Jobs)
        {
            foreach (var comp in job.CompsOfType<AAAAManagerJobComp>())
            {
                comp.AllowedAreaChangeNormalMode();
            }
        }
    }
}
