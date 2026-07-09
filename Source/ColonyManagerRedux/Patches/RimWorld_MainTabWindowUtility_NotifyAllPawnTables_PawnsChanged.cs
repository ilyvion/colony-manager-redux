// RimWorld_MainTabWindowUtility_NotifyAllPawnTables_PawnsChanged.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HarmonyPatch(
    typeof(MainTabWindowUtility),
    nameof(MainTabWindowUtility.NotifyAllPawnTables_PawnsChanged)
)]
internal static class RimWorld_MainTabWindowUtility_NotifyAllPawnTables_PawnsChanged
{
    private static void Postfix()
    {
        if (Find.CurrentMap == null)
        {
            return;
        }
        foreach (var tab in Manager.For(Find.CurrentMap).Tabs)
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
