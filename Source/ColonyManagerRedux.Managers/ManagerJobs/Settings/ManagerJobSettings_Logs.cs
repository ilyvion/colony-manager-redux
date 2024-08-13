// ManagerJobSettings_Logs.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerJobSettings_Logs : ManagerJobSettings
{
    public int KeepLogCount = 100;
    public bool ShowLogsWithNoWorkDone = true;

    public override void DoPanelContents(Rect rect)
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
        cur.y += ListEntryHeight;

        var rowRect = new Rect(cur.x, cur.y, width, ListEntryHeight);
        cur.y += ListEntryHeight;

        Widgets_Labels.Label(
            thresholdLabelRect,
            "ColonyManagerRedux.Logs.JobSettings.KeepLogCount".Translate(KeepLogCount),
            "ColonyManagerRedux.Logs.JobSettings.KeepLogCount.Tip".Translate());
        KeepLogCount = (int)GUI.HorizontalSlider(thresholdRect, KeepLogCount, 1, DefaultMaxUpperThreshold);

        //rowRect.y += ListEntryHeight;
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.Logs.JobSettings.ShowLogsWithNoWorkDone".Translate(),
            "ColonyManagerRedux.Logs.JobSettings.ShowLogsWithNoWorkDone.Tip".Translate(
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
