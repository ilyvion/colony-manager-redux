// Verse_AreaManager_NotifyEveryoneAreaRemoved.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HarmonyPatch(typeof(AreaManager), "NotifyEveryoneAreaRemoved")]
internal static class Verse_AreaManager_NotifyEveryoneAreaRemoved
{
    private static void Postfix(Area area)
    {
        if (Find.CurrentMap == null)
        {
            return;
        }
        foreach (var job in Manager.For(Find.CurrentMap).JobTracker.Jobs)
        {
            job.Notify_AreaRemoved(area);
        }
    }
}
