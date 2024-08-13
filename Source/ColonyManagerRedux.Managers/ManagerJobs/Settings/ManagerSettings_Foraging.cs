// ManagerSettings_Foraging.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerSettings_Foraging : ManagerSettings
{
    public bool DefaultSyncFilterAndAllowed = true;
    public bool DefaultForceFullyMature;

    public override void DoPanelContents(Rect rect)
    {
        var panelRect = new Rect(
            rect.xMin,
            rect.yMin,
            rect.width,
            rect.height - Margin);

        Widgets_Section.BeginSectionColumn(panelRect, "Foraging.Settings", out Vector2 position, out float width);
        Widgets_Section.Section(ref position, width, DrawSyncFilterAndAllowed, "ColonyManagerRedux.ManagerSettings.DefaultThresholdSettings".Translate());
        Widgets_Section.Section(ref position, width, DrawForceFullyMature);
        Widgets_Section.EndSectionColumn("Foraging.Settings", position);
    }

    public float DrawSyncFilterAndAllowed(Vector2 pos, float width)
    {
        var rowRect = new Rect(
            pos.x,
            pos.y,
            width,
            ListEntryHeight);

        Utilities.DrawToggle(
            rowRect,
            "ColonyManagerRedux.Foraging.SyncFilterAndAllowed".Translate(),
            "ColonyManagerRedux.Foraging.SyncFilterAndAllowed.Tip".Translate(),
            ref DefaultSyncFilterAndAllowed);

        return ListEntryHeight;
    }

    public float DrawForceFullyMature(Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(
            rowRect,
            "ColonyManagerRedux.Foraging.ForceFullyMature".Translate(),
            "ColonyManagerRedux.Foraging.ForceFullyMature.Tip".Translate(),
            ref DefaultForceFullyMature);

        return ListEntryHeight;
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref DefaultSyncFilterAndAllowed, "defaultSyncFilterAndAllowed", true);
        Scribe_Values.Look(ref DefaultForceFullyMature, "defaultForceFullyMature", false);
    }
}
