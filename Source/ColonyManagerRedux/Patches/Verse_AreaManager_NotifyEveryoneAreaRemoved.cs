// Verse_AreaManager_NotifyEveryoneAreaRemoved.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HarmonyPatch(typeof(AreaManager), "NotifyEveryoneAreaRemoved")]
internal static class Verse_AreaManager_NotifyEveryoneAreaRemoved
{
    private static void Postfix(Area area)
    {
        if (area.Map == null)
        {
            return;
        }
        foreach (var job in Manager.For(area.Map).JobTracker.Jobs)
        {
            job.Notify_AreaRemoved(area);
        }
    }
}
