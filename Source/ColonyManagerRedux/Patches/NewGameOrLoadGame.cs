// Verse_AreaManager_NotifyEveryoneAreaRemoved.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HarmonyPatch(typeof(Game), nameof(Game.InitNewGame))]
internal static class Verse_Game_InitNewGame
{
    private static void Prefix()
    {
        CompManagerJobHistory.UsedUpdateJitters.Clear();
    }
}

[HarmonyPatch(typeof(Game), nameof(Game.LoadGame))]
internal static class Verse_Game_LoadGame
{
    private static void Prefix()
    {
        CompManagerJobHistory.UsedUpdateJitters.Clear();
    }
}
