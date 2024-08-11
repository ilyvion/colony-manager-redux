// ManagerTab_Logs.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory;
using ilyvion.Laboratory.Collections;
using ilyvion.Laboratory.Extensions;
using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[HotSwappable]
internal sealed class ManagerTab_Logs(Manager manager) : ManagerTab(manager), IExposable
{

    // TODO: Let the number of tracked jobs be configurable
    protected override bool AllowJobDeselect => true;
    protected override bool DoMainContentWhenNothingSelected => true;
    protected override bool ShouldHaveNewJobButton => false;

    private ManagerLog? selectedLog;

    private ScrollViewStatus _logListScrollViewStatus = new();

    // This is set by PostMake immediately after the tab is instantiated
    internal CircularBuffer<ManagerLog> Logs = null!;

    private readonly List<LookTargets[]> cachedLookTargets = [];
    private void RecacheLookTargets()
    {
        cachedLookTargets.Clear();
        if (selectedLog != null)
        {
            cachedLookTargets.AddRange(selectedLog.Details
                .Select((d) => d.Targets
                    .Select(t => new LookTargets(t.ToTargetInfo(manager)))
                    .ToArray()));
        }
    }

    public override void PostMake()
    {
        base.PostMake();
        var logSettings = ColonyManagerReduxMod.Settings
            .ManagerJobSettingsFor<ManagerJobSettings_Logs>(def)!;

        Logs = new(logSettings.KeepLogCount);
    }

    protected override void PostSelect()
    {
        selectedLog = null;
    }

    protected override void DoMainContent(Rect rect)
    {
        Widgets.DrawMenuSection(rect);

        using var scrollView = GUIScope.ScrollView(rect, _logListScrollViewStatus);
        using var _g = GUIScope.WidgetGroup(scrollView.ViewRect);

        var cur = Vector2.zero;
        var i = 0;

        IEnumerable<ManagerLog> logs = Logs;
        if (Selected != null)
        {
            logs = logs.Where(l => l.IsForJob(Selected));
        }

        var logSettings = ColonyManagerReduxMod.Settings
            .ManagerJobSettingsFor<ManagerJobSettings_Logs>(def)!;
        foreach (var log in logs.Reverse()
            .Where(l => logSettings.ShowLogsWithNoWorkDone || l._workDone))
        {
            bool isSelectedLog = selectedLog == log;

            var row = new Rect(0f, cur.y, scrollView.ViewRect.width, 0f);
            DrawLogEntry(log, ref cur, scrollView.ViewRect.width, isSelectedLog);
            row.height = cur.y - row.y;

            Widgets.DrawHighlightIfMouseover(row);
            if (isSelectedLog)
            {
                Widgets.DrawHighlightSelected(row);
            }

            if (i++ % 2 == 1)
            {
                Widgets.DrawAltRect(row);
            }

            if (Widgets.ButtonInvisible(row))
            {
                if (!isSelectedLog)
                {
                    selectedLog = log;
                    RecacheLookTargets();
                }
                else
                {
                    selectedLog = null;
                }
            }


            var curDetail = new Vector2(LargeIconSize, row.yMax);
            var j = 0;
            if (isSelectedLog)
            {
                foreach (var details in log.Details)
                {
                    var detailRow = new Rect(
                        curDetail.x,
                        curDetail.y,
                        scrollView.ViewRect.width - LargeIconSize,
                        0f);
                    DrawLogDetailsEntry(
                        details,
                        ref curDetail,
                        scrollView.ViewRect.width - LargeIconSize);
                    detailRow.height = curDetail.y - detailRow.y;

                    if (j % 2 == 1)
                    {
                        Widgets.DrawAltRect(detailRow);
                    }

                    if (details.Targets.Count > 0)
                    {
                        Widgets.DrawHighlightIfMouseover(detailRow);
                        if (Mouse.IsOver(detailRow))
                        {
                            foreach (var target in cachedLookTargets[j])
                            {
                                target.Highlight();
                            }
                        }
                        if (Widgets.ButtonInvisible(detailRow))
                        {
                            CameraJumper.TryJumpAndSelect(
                                details.Targets[details.NextTargetIndex]
                                    .ToGlobalTargetInfo(manager));
                            if (Event.current.button == 0)
                            {
                                Find.MainTabsRoot.EscapeCurrentTab(playSound: false);
                            }
                        }
                    }

                    j++;
                }
                cur.y = curDetail.y;

                Widgets.DrawLineHorizontal(row.x, row.yMax, row.width, Color.gray);
                using (GUIScope.Color(Color.gray))
                {
                    Widgets.DrawLineVertical(LargeIconSize, row.yMax, cur.y - row.yMax);
                }
                Widgets.DrawLineHorizontal(
                    LargeIconSize,
                    cur.y,
                    row.width - LargeIconSize,
                    Color.gray);
            }
        }
        if (i == 0)
        {
            Widgets_Labels.Label(
                rect,
                "ColonyManagerRedux.Logs.NoLogs".Translate(),
                TextAnchor.MiddleCenter,
                color: Color.gray);
        }

        scrollView.Height = cur.y;
    }

    private static void DrawLogEntry(
        ManagerLog log,
        ref Vector2 position,
        float width,
        bool isSelectedLog)
    {
        // set up rects
        var iconRect = new Rect(Margin, Margin,
            LargeIconSize, LargeIconSize);

        var labelWidth = width - LargeIconSize - 3 * Margin;
        var headerLabel = log.JobLabelCap;
        if (!headerLabel.Fits(labelWidth, out var labelSize))
        {
            headerLabel = headerLabel.Truncate(labelWidth);
        }
        if (!log._workDone)
        {
            headerLabel = $"<color=#7f7f7f>{headerLabel}</color>";
        }
        var headerLabelRect = new Rect(
            iconRect.xMax + Margin,
            iconRect.y,
            labelWidth,
            labelSize.y);

        var dateLabel = $"<color=#7f7f7f>{log.LogDate}</color>";
        var dateLabelRect = new Rect(
            iconRect.xMax + Margin,
            headerLabelRect.yMax,
            labelWidth,
            Text.LineHeight);

        float rowHeight = Mathf.Max(dateLabelRect.yMax, iconRect.yMax) + Margin;
        var rowRect = new Rect(
            position.x,
            position.y,
            width,
            rowHeight
        );

        using var _g = GUIScope.WidgetGroup(rowRect);

        if (IlyvionDebugViewSettings.DrawUIHelpers)
        {
            Widgets.DrawRectFast(iconRect, ColorLibrary.HotPink.ToTransparent(.5f));
            Widgets.DrawRectFast(headerLabelRect, Color.blue.ToTransparent(.2f));
            Widgets.DrawRectFast(dateLabelRect, Color.green.ToTransparent(.2f));
        }

        if (log.HasJob)
        {
            var tab = log.Tab!;
            if (tab.Enabled)
            {
                if (Widgets.ButtonImage(iconRect, tab.def.icon))
                {
                    log.GoToJobTab();
                }
                TooltipHandler.TipRegion(iconRect,
                    "ColonyManagerRedux.Common.GoToJob".Translate(log.JobLabel));
            }
            else
            {
                using var color = GUIScope.Color(Color.gray);
                GUI.DrawTexture(iconRect, tab.def.icon);
                TooltipHandler.TipRegion(iconRect, tab.Label +
                    "ColonyManagerRedux.Common.TabDisabledBecause".Translate(tab.DisabledReason));
            }
        }
        else
        {
            using var color = GUIScope.Color(Color.gray);
            GUI.DrawTexture(iconRect, log.Icon);
            TooltipHandler.TipRegion(iconRect,
                "ColonyManagerRedux.Logs.JobDoesNotExist".Translate());
        }

        // draw label
        Widgets_Labels.Label(headerLabelRect, headerLabel, null, TextAnchor.UpperLeft);
        TooltipHandler.TipRegion(headerLabelRect,
        "ColonyManagerRedux.Logs.ClickToExpandCollapse".Translate(
            (isSelectedLog
                ? "ColonyManagerRedux.Logs.Collapse"
                : "ColonyManagerRedux.Logs.Expand").Translate()));

        Widgets_Labels.Label(dateLabelRect, dateLabel, null, TextAnchor.UpperLeft);
        TooltipHandler.TipRegion(dateLabelRect,
        "ColonyManagerRedux.Logs.ClickToExpandCollapse".Translate(
            (isSelectedLog
                ? "ColonyManagerRedux.Logs.Collapse"
                : "ColonyManagerRedux.Logs.Expand").Translate()));

        position.y += rowRect.height;
    }

    private static void DrawLogDetailsEntry(
        ManagerLog.LogDetails details,
        ref Vector2 position,
        float width)
    {
        var labelWidth = width - 2 * Margin;
        var labelHeight = Text.CalcHeight(details.Text, labelWidth);
        var labelRect = new Rect(
            Margin + position.x,
            Margin + position.y,
            labelWidth,
            labelHeight);

        Widgets.Label(labelRect, details.Text);

        position.y += labelHeight + 2 * Margin;
    }

    public override void DrawLocalListEntry(
        ManagerJob job,
        ref Vector2 position,
        float width,
        DrawLocalListEntryParameters? parameters)
    {
        // set up rects
        var iconRect = new Rect(Margin, Margin,
            LargeIconSize, LargeIconSize);

        var labelWidth = width - LargeIconSize - 3 * Margin;
        var tab = job.Tab;
        var (label, labelSize) = tab.GetFullLabel(job, labelWidth, null);
        var labelRect = new Rect(
            iconRect.xMax + Margin,
            iconRect.y,
            labelWidth,
            labelSize.y);

        float rowHeight = labelRect.yMax + Margin;
        var rowRect = new Rect(
            position.x,
            position.y,
            width,
            rowHeight
        );

        using var _g = GUIScope.WidgetGroup(rowRect);

        if (IlyvionDebugViewSettings.DrawUIHelpers)
        {
            Widgets.DrawRectFast(iconRect, ColorLibrary.HotPink.ToTransparent(.5f));
            Widgets.DrawRectFast(labelRect, Color.blue.ToTransparent(.2f));
        }

        // draw label
        var selectToFilter = "ColonyManagerRedux.Logs.SelectToFilterLogsByJob".Translate();
        Widgets_Labels.Label(labelRect, label, selectToFilter, TextAnchor.UpperLeft);

        if (tab.Enabled)
        {
            if (Widgets.ButtonImage(iconRect, tab.def.icon))
            {
                MainTabWindow_Manager.GoTo(tab, job);
            }
        }
        else
        {
            using var color = GUIScope.Color(Color.gray);
            GUI.DrawTexture(iconRect, tab.def.icon);
            TooltipHandler.TipRegion(iconRect, tab.Label +
                "ColonyManagerRedux.Common.TabDisabledBecause".Translate(tab.DisabledReason));
        }

        position.y += rowRect.height;
    }

    public void ExposeData()
    {
        Scribe_CircularBuffer.Look(ref Logs!, "logs");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            var logSettings = ColonyManagerReduxMod.Settings
                .ManagerJobSettingsFor<ManagerJobSettings_Logs>(def)!;
            if (Logs == null)
            {
                Logs = new(logSettings.KeepLogCount);
            }
            else if (Logs.Capacity != logSettings.KeepLogCount)
            {
                // The KeepLogCount setting has changed; we need to resize the logs buffer.
                var oldLogs = Logs;
                Logs = new(logSettings.KeepLogCount);
                foreach (var log in oldLogs.Reverse().Take(logSettings.KeepLogCount))
                {
                    Logs.PushFront(log);
                }
            }
        }
    }
}
