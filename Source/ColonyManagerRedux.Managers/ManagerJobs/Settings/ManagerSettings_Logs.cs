// ManagerSettings_Logs.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerSettings_Logs : ManagerSettings
{
    public int KeepLogCount = 100;
    public bool ShowLogsWithNoWorkDone = true;

    public override void DoTabContents(Rect rect)
    {
        var panelRect = new Rect(
            rect.xMin,
            rect.yMin,
            rect.width,
            rect.height - Margin);

        Widgets_Section.BeginSectionColumn(panelRect, "Logs.Settings", out Vector2 position, out float width);
        Widgets_Section.Section(ref position, width, DrawLogSettings);
        Widgets_Section.EndSectionColumn("Logs.Settings", position);
    }

    public float DrawLogSettings(Vector2 cur, float width)
    {
        // target threshold
        var thresholdLabelRect = new Rect(
            cur.x,
            cur.y,
            width,
            ListEntryHeight);
        cur.y += ListEntryHeight;

        var thresholdRect = new Rect(
            cur.x,
            cur.y,
            width,
            SliderHeight);
        cur.y += SliderHeight;

        var rowRect = new Rect(cur.x, cur.y, width, ListEntryHeight);
        cur.y += ListEntryHeight;

        IlyvionWidgets.Label(
            thresholdLabelRect,
            "ColonyManagerRedux.Logs.ManagerSettings.KeepLogCount".Translate(KeepLogCount),
            "ColonyManagerRedux.Logs.ManagerSettings.KeepLogCount.Tip".Translate());
        KeepLogCount = (int)GUI.HorizontalSlider(thresholdRect, KeepLogCount, 1, DefaultMaxUpperThreshold);

        //rowRect.y += ListEntryHeight;
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.Logs.ManagerSettings.ShowLogsWithNoWorkDone".Translate(),
            "ColonyManagerRedux.Logs.ManagerSettings.ShowLogsWithNoWorkDone.Tip".Translate(
                "ColonyManagerRedux.Logs.NoWorkDone".Translate()
            ),
            ref ShowLogsWithNoWorkDone);

        return cur.y;
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref KeepLogCount, "keepLogCount", 100);

        Scribe_Values.Look(ref ShowLogsWithNoWorkDone, "showLogsWithNoWorkDone", true);
    }
}
