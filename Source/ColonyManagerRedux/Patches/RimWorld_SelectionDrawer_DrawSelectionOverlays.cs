// RimWorld_SelectionDrawer_DrawSelectionOverlays.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

// We need this because calls to GenDraw don't work right if you do them in the context of a
// Window.DoWindowContents. See references to PostDrawSelectionOverlaysActions for how it's used
[HarmonyPatch(typeof(SelectionDrawer), nameof(SelectionDrawer.DrawSelectionOverlays))]
internal static class RimWorld_SelectionDrawer_DrawSelectionOverlays
{
    public static List<Action> PostDrawSelectionOverlaysActions = [];
    private static void Postfix()
    {
        PostDrawSelectionOverlaysActions.ForEach(a => a());
    }
}
