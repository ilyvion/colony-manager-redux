// RimWorld_MainTabWindowUtility_NotifyAllPawnTables_PawnsChanged.cs
// Copyright (c) 2024–2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HarmonyPatch(
    typeof(MainTabWindowUtility),
    nameof(MainTabWindowUtility.NotifyAllPawnTables_PawnsChanged)
)]
internal static class RimWorld_MainTabWindowUtility_NotifyAllPawnTables_PawnsChanged
{
    internal static void Postfix()
    {
        if (Find.CurrentMap == null)
        {
            return;
        }
        NotifyTabs(Manager.For(Find.CurrentMap).Tabs);
    }

    internal static void NotifyTabs(IEnumerable<ManagerTab> tabs)
    {
        foreach (var tab in tabs)
        {
            try
            {
                tab.Notify_PawnsChanged();
            }
            catch (Exception err)
            {
                ColonyManagerReduxMod.Instance.LogException(
                    $"ManagerTab caused exception during {nameof(ManagerTab.Notify_PawnsChanged)}",
                    err
                );
            }
        }
    }
}
