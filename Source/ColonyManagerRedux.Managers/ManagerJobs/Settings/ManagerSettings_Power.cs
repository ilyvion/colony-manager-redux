// ManagerSettings_Power.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerSettings_Power : ManagerSettings
{
    public bool AutoSuspendOnNonHomeMaps = true;

    public override void DoTabContents(Rect rect)
    {
        var panelRect = new Rect(rect.xMin, rect.yMin, rect.width, rect.height - Margin);

        Widgets_Section.BeginSectionColumn(
            panelRect,
            "Power.Settings",
            out var position,
            out var width
        );
        Widgets_Section.Section(ref position, width, DrawAutoSuspendOnNonHomeMaps);
        Widgets_Section.EndSectionColumn("Power.Settings", position);
    }

    public float DrawAutoSuspendOnNonHomeMaps(Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(
            rowRect,
            "ColonyManagerRedux.AutoSuspendPowerJobOnNonHomeMaps".Translate(),
            "ColonyManagerRedux.AutoSuspendPowerJobOnNonHomeMaps.Tip".Translate(),
            ref AutoSuspendOnNonHomeMaps
        );

        return ListEntryHeight;
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref AutoSuspendOnNonHomeMaps, "autoSuspendOnNonHomeMaps", true);
    }
}
