// Verse_AreaManager_NotifyEveryoneAreaRemoved.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HarmonyPatch(typeof(AreaManager), "NotifyEveryoneAreaRemoved")]
internal static class Verse_AreaManager_NotifyEveryoneAreaRemoved
{
}
