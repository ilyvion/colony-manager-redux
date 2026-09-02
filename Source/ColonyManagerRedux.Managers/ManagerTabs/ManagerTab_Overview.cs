// ManagerTab_Overview.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024–2026 Alexander Krivács Schrøder

using ilyvion.Laboratory.Extensions;
using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed partial class ManagerTab_Overview(Manager manager) : ManagerTab(manager), IExposable
{
    public const float OverviewWidthRatio = .6f;

    internal enum OverviewGroupMode
    {
        None,
        JobType,
        Status,
        Manual,
    }

    internal sealed record OverviewJobGroup<T>(
        string Key,
        string? Header,
        Texture2D? Icon,
        List<T> Jobs
    );

    private float _overviewHeight = 9999f;
    private Vector2 _overviewScrollPosition = Vector2.zero;
    private readonly List<Pawn> _workers = [];

    private readonly QuickSearchWidget _quickSearchWidget = new();
    private OverviewGroupMode _groupMode = OverviewGroupMode.None;
    private HashSet<ManagerDef> _hiddenJobTypes = [];
    private HashSet<string> _collapsedGroups = [];

    public void ExposeData()
    {
        Scribe_Values.Look(ref _groupMode, "groupMode", OverviewGroupMode.None);
        Scribe_Collections.Look(ref _hiddenJobTypes, "hiddenJobTypes", LookMode.Def);
        Scribe_Collections.Look(ref _collapsedGroups, "collapsedGroups", LookMode.Value);

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            _hiddenJobTypes ??= [];
            _collapsedGroups ??= [];
        }
    }

    private SkillDef? SkillDef { get; set; }

    private WorkTypeDef WorkTypeDef
    {
        get => field ??= ManagerWorkTypeDefOf.Managing;
        set
        {
            field = value;
            RefreshWorkers();
        }
    }

    public override void PreOpen() => RefreshWorkers();

    public override void PostOpen() => pawnOverviewTable?.SetDirty();

    protected override void PostSelect()
    {
        WorkTypeDef = Selected?.WorkTypeDef ?? ManagerWorkTypeDefOf.Managing;
        pawnOverviewTable?.SetDirty();
    }

    protected override void Notify_PawnsChanged()
    {
        RefreshWorkers();
        pawnOverviewTable?.SetDirty();
    }

    protected override void DoTabContents(Rect canvas)
    {
        var overviewRect = new Rect(
            0f,
            0f,
            OverviewWidthRatio * canvas.width,
            canvas.height
        ).RoundToInt();
        var sideRectUpper = new Rect(
            overviewRect.xMax + Margin,
            0f,
            ((1 - OverviewWidthRatio) * canvas.width) - Margin,
            (canvas.height - Margin) / 2
        ).RoundToInt();
        var sideRectLower = new Rect(
            overviewRect.xMax + Margin,
            sideRectUpper.yMax + Margin,
            sideRectUpper.width,
            canvas.height - sideRectUpper.height - Margin
        ).RoundToInt();

        // draw the listing of current jobs.
        Widgets.DrawMenuSection(overviewRect);
        DrawOverview(overviewRect);

        // draw the selected job's details
        Widgets.DrawMenuSection(sideRectUpper);
        if (Selected?.Tab is ManagerTab managerTab)
        {
            if (!managerTab.DrawOverviewDetails(Selected, sideRectUpper))
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(
                    sideRectUpper,
                    "ColonyManagerRedux.Overview.NoJobDetails".Translate()
                );
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.LowerLeft;
            }
        }
        else
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.gray;
            Widgets.Label(sideRectUpper, "ColonyManagerRedux.Overview.NoJobSelected".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.LowerLeft;
        }

        // overview of managers & pawns (capable of) doing this job.
        Widgets.DrawMenuSection(sideRectLower);
        GUI.BeginGroup(sideRectLower);
        DrawPawnOverview(sideRectLower.AtZero());
        GUI.EndGroup();
    }

    private List<ManagerJob> GetFilteredJobs()
    {
        var jobs = Manager.JobTracker.JobsOfType<ManagerJob>();

        if (_hiddenJobTypes.Count > 0)
        {
            jobs = jobs.Where(job => !_hiddenJobTypes.Contains(job.Def));
        }

        if (_quickSearchWidget.filter.Active)
        {
            jobs = jobs.Where(job =>
                _quickSearchWidget.filter.Matches(job.Label)
                || _quickSearchWidget.filter.Matches(job.TargetsLabel)
                || _quickSearchWidget.filter.Matches(job.Tab.Label)
            );
        }

        return [.. jobs];
    }

    private static string? GetManualGroup(ManagerJob job) =>
        job.CompOfType<CompManagerJobOverview>()?.ManualGroup;

    private static void SetManualGroup(ManagerJob job, string? value)
    {
        if (job.CompOfType<CompManagerJobOverview>() is { } comp)
        {
            comp.ManualGroup = value;
        }
    }

    private List<OverviewJobGroup<ManagerJob>> GetGroups(List<ManagerJob> jobs) =>
        GetGroups(
            _groupMode,
            jobs,
            job => job.Def,
            job => job.CausedException != null,
            job => job.IsSuspended,
            job => job.IsCompleted,
            GetManualGroup,
            "ColonyManagerRedux.Overview.Status.NeedsAttention".Translate(),
            "ColonyManagerRedux.Overview.Status.Active".Translate(),
            "ColonyManagerRedux.Overview.Status.Suspended".Translate(),
            "ColonyManagerRedux.Overview.Status.Completed".Translate(),
            "ColonyManagerRedux.Overview.ManualGroup.Ungrouped".Translate(),
            Resources.Warning
        );

    /// <summary>
    /// Partitions <paramref name="jobs"/> into <see cref="OverviewJobGroup{T}"/>s according to
    /// <paramref name="mode"/>. Kept generic and free of GUI/game-state dependencies (job
    /// properties are read via delegates) so it can be unit tested directly.
    /// </summary>
    internal static List<OverviewJobGroup<T>> GetGroups<T>(
        OverviewGroupMode mode,
        List<T> jobs,
        Func<T, ManagerDef> getDef,
        Func<T, bool> hasException,
        Func<T, bool> isSuspended,
        Func<T, bool> isCompleted,
        Func<T, string?> getManualGroup,
        string needsAttentionLabel,
        string activeLabel,
        string suspendedLabel,
        string completedLabel,
        string ungroupedLabel,
        Texture2D? needsAttentionIcon
    )
    {
        switch (mode)
        {
            case OverviewGroupMode.JobType:
                return
                [
                    .. jobs.GroupBy(getDef)
                        .OrderBy(g => g.Key.order)
                        .Select(g => new OverviewJobGroup<T>(
                            $"type:{g.Key.defName}",
                            g.Key.label.CapitalizeFirst(),
                            g.Key.icon,
                            [.. g]
                        )),
                ];

            case OverviewGroupMode.Status:
            {
                // Bucket priority mirrors Utilities.DrawStampButton's per-job stamp icon
                // (exception > suspended > completed > active).
                var groups = new List<OverviewJobGroup<T>>();
                var needsAttention = jobs.Where(hasException).ToList();
                var suspended = jobs.Where(job => !hasException(job) && isSuspended(job)).ToList();
                var completed = jobs.Where(job =>
                        !hasException(job) && !isSuspended(job) && isCompleted(job)
                    )
                    .ToList();
                var active = jobs.Where(job =>
                        !hasException(job) && !isSuspended(job) && !isCompleted(job)
                    )
                    .ToList();

                if (needsAttention.Count > 0)
                {
                    groups.Add(
                        new(
                            "status:attention",
                            needsAttentionLabel,
                            needsAttentionIcon,
                            needsAttention
                        )
                    );
                }
                if (active.Count > 0)
                {
                    groups.Add(new("status:active", activeLabel, null, active));
                }
                if (completed.Count > 0)
                {
                    groups.Add(new("status:completed", completedLabel, null, completed));
                }
                if (suspended.Count > 0)
                {
                    groups.Add(new("status:suspended", suspendedLabel, null, suspended));
                }
                return groups;
            }

            case OverviewGroupMode.Manual:
            {
                var groups = jobs.Where(job => getManualGroup(job) != null)
                    .GroupBy(job => getManualGroup(job))
                    .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new OverviewJobGroup<T>($"manual:{g.Key}", g.Key, null, [.. g]))
                    .ToList();

                var ungrouped = jobs.Where(job => getManualGroup(job) == null).ToList();
                if (ungrouped.Count > 0)
                {
                    groups.Add(new("manual:__ungrouped__", ungroupedLabel, null, ungrouped));
                }
                return groups;
            }

            case OverviewGroupMode.None:
            default:
                return [new OverviewJobGroup<T>("all", null, null, jobs)];
        }
    }

    private void DrawGroupByButton(Rect rect)
    {
        Widgets.DrawHighlightIfMouseover(rect);
        using var _ = GUIScope.TextAnchor(TextAnchor.MiddleCenter);
        Widgets.Label(
            rect,
            "ColonyManagerRedux.Overview.GroupBy".Translate(
                $"ColonyManagerRedux.Overview.GroupBy.{_groupMode}".Translate()
            )
        );

        if (!Widgets.ButtonInvisible(rect))
        {
            return;
        }

        var options = Enum.GetValues(typeof(OverviewGroupMode))
            .Cast<OverviewGroupMode>()
            .Select(mode => new FloatMenuOption(
                $"ColonyManagerRedux.Overview.GroupBy.{mode}".Translate(),
                () => _groupMode = mode
            ))
            .ToList();
        Find.WindowStack.Add(new FloatMenu(options));
    }

    private void DrawCollapseExpandAllGroupsButton(
        Rect rect,
        List<OverviewJobGroup<ManagerJob>> groups
    )
    {
        if (_groupMode == OverviewGroupMode.None)
        {
            return;
        }

        var groupKeys = groups.Where(g => g.Header != null).Select(g => g.Key).ToList();
        if (groupKeys.Count == 0)
        {
            return;
        }

        var allCollapsed = groupKeys.All(_collapsedGroups.Contains);

        Widgets.DrawHighlightIfMouseover(rect);
        GUI.DrawTexture(
            rect.ContractedBy(2f),
            allCollapsed ? TexButton.Reveal : TexButton.Collapse
        );
        TooltipHandler.TipRegion(
            rect,
            allCollapsed
                ? "ColonyManagerRedux.Overview.CollapseAllGroups.ExpandAll".Translate()
                : "ColonyManagerRedux.Overview.CollapseAllGroups.CollapseAll".Translate()
        );

        if (!Widgets.ButtonInvisible(rect))
        {
            return;
        }

        foreach (var key in groupKeys)
        {
            _ = allCollapsed ? _collapsedGroups.Remove(key) : _collapsedGroups.Add(key);
        }
    }

    private void DrawJobTypeFilterRow(Rect rect)
    {
        var jobTypes = Manager
            .JobTracker.JobsOfType<ManagerJob>()
            .Select(job => job.Def)
            .Distinct()
            .OrderBy(def => def.order)
            .ToList();

        var iconRect = new Rect(rect.x, rect.y, rect.height, rect.height);

        var allHidden = jobTypes.All(_hiddenJobTypes.Contains);
        if (!allHidden)
        {
            Widgets.DrawHighlightSelected(iconRect);
        }
        Widgets.DrawHighlightIfMouseover(iconRect);
        using (GUIScope.Color(allHidden ? Color.gray : Color.white))
        {
            GUI.DrawTexture(
                iconRect.ContractedBy(2f),
                allHidden ? Resources.EyeClosed : Resources.EyeOpen
            );
        }
        TooltipHandler.TipRegion(
            iconRect,
            allHidden
                ? "ColonyManagerRedux.Overview.JobTypeFilter.ShowAll".Translate()
                : "ColonyManagerRedux.Overview.JobTypeFilter.HideAll".Translate()
        );
        if (Widgets.ButtonInvisible(iconRect))
        {
            _hiddenJobTypes.Clear();
            if (!allHidden)
            {
                foreach (var jobType in jobTypes)
                {
                    _ = _hiddenJobTypes.Add(jobType);
                }
            }
        }
        iconRect.x += iconRect.width + (Margin / 2f);

        foreach (var jobType in jobTypes)
        {
            var hidden = _hiddenJobTypes.Contains(jobType);
            if (!hidden)
            {
                Widgets.DrawHighlightSelected(iconRect);
            }
            Widgets.DrawHighlightIfMouseover(iconRect);

            using (GUIScope.Color(hidden ? Color.gray : Color.white))
            {
                GUI.DrawTexture(iconRect.ContractedBy(2f), jobType.icon);
            }

            TooltipHandler.TipRegion(
                iconRect,
                hidden
                    ? "ColonyManagerRedux.Overview.JobTypeFilter.Show".Translate(
                        jobType.label.CapitalizeFirst()
                    )
                    : "ColonyManagerRedux.Overview.JobTypeFilter.Hide".Translate(
                        jobType.label.CapitalizeFirst()
                    )
            );

            if (Widgets.ButtonInvisible(iconRect))
            {
                _ = hidden ? _hiddenJobTypes.Remove(jobType) : _hiddenJobTypes.Add(jobType);
            }

            iconRect.x += iconRect.width + (Margin / 2f);
        }
    }

    private void DrawGroupHeader(
        ref Vector2 position,
        float width,
        OverviewJobGroup<ManagerJob> group
    )
    {
        var collapsed = _collapsedGroups.Contains(group.Key);
        var headerRect = new Rect(position.x, position.y, width, ListEntryHeight);

        GUI.DrawTexture(headerRect, Resources.SlightlyDarkBackground);
        Widgets.DrawHighlightIfMouseover(headerRect);

        using (GUIScope.TextAnchor(TextAnchor.MiddleLeft))
        {
            Widgets.Label(
                headerRect.TrimLeft(Margin).TrimRight(Margin),
                (collapsed ? "▶ " : "▼ ") + group.Header + $" ({group.Jobs.Count})"
            );
        }

        if (Widgets.ButtonInvisible(headerRect))
        {
            _ = collapsed ? _collapsedGroups.Remove(group.Key) : _collapsedGroups.Add(group.Key);
        }

        position.y += headerRect.height;
    }

    public void DrawOverview(Rect rect)
    {
        if (Manager.JobTracker.HasNoJobs)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.grey;
            Widgets.Label(rect, "ColonyManagerRedux.Overview.NoJobs".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            return;
        }

        const float groupByButtonWidth = 160f;
        const float halfMargin = Margin / 2f;

        var searchRect = new Rect(
            rect.x + Margin,
            rect.y + halfMargin,
            rect.width - (2 * Margin),
            QuickSearchWidget.WidgetHeight
        );
        _quickSearchWidget.OnGUI(searchRect, () => { });

        // Computed here (rather than after the toolbar below) so the collapse/expand-all-groups
        // button can know the current set of groups; this means job-type-filter and group-by
        // clicks in the toolbar below take effect on the next GUI frame rather than this one.
        var filteredJobs = GetFilteredJobs();
        var groups = GetGroups(filteredJobs);

        var filterRowRect = new Rect(
            rect.x + Margin,
            searchRect.yMax + halfMargin,
            rect.width - (2 * Margin) - groupByButtonWidth - Margin - ListEntryHeight - Margin,
            ListEntryHeight
        );
        DrawJobTypeFilterRow(filterRowRect);

        var collapseAllGroupsRect = new Rect(
            filterRowRect.xMax + Margin,
            filterRowRect.y,
            ListEntryHeight,
            ListEntryHeight
        );
        DrawCollapseExpandAllGroupsButton(collapseAllGroupsRect, groups);

        var groupByRect = new Rect(
            collapseAllGroupsRect.xMax + Margin,
            filterRowRect.y,
            groupByButtonWidth,
            ListEntryHeight
        );
        DrawGroupByButton(groupByRect);

        var listRect = new Rect(
            rect.x,
            filterRowRect.yMax + halfMargin,
            rect.width,
            rect.yMax - filterRowRect.yMax - halfMargin
        );

        if (filteredJobs.Count == 0)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.grey;
            Widgets.Label(listRect, "ColonyManagerRedux.Overview.NoMatchingJobs".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            return;
        }

        var viewRect = listRect;
        var contentRect = viewRect.AtZero();
        contentRect.height = _overviewHeight;
        if (_overviewHeight > viewRect.height)
        {
            contentRect.width -= GenUI.ScrollBarWidth;
        }

        IlyvionDebugViewSettings.DrawIfUIHelpers(() =>
        {
            Widgets.DrawRectFast(filterRowRect, ColorLibrary.HotPink.ToTransparent(.5f));
            Widgets.DrawRectFast(collapseAllGroupsRect, ColorLibrary.Indigo.ToTransparent(.5f));
            Widgets.DrawRectFast(groupByRect, ColorLibrary.NavyBlue.ToTransparent(.5f));
            Widgets.DrawRectFast(listRect, ColorLibrary.Salmon.ToTransparent(.5f));
            Widgets.DrawRectFast(viewRect, ColorLibrary.Plum.ToTransparent(.5f));
        });

        Widgets.BeginScrollView(viewRect, ref _overviewScrollPosition, contentRect);

        var cur = Vector2.zero;

        foreach (var group in groups)
        {
            if (group.Header != null)
            {
                DrawGroupHeader(ref cur, contentRect.width, group);
                if (_collapsedGroups.Contains(group.Key))
                {
                    continue;
                }
            }

            var alternate = false;
            foreach (var job in group.Jobs)
            {
                var row = new Rect(cur.x, cur.y, contentRect.width, 0f);
                DrawOverviewListEntry(job, ref cur, contentRect.width);
                row.height = cur.y - row.y;

                // highlights
                if (alternate)
                {
                    Widgets.DrawAltRect(row);
                }
                alternate = !alternate;

                if (job == Selected)
                {
                    Widgets.DrawHighlightSelected(row);
                }

                Widgets.DrawHighlightIfMouseover(row);

                if (job.CausedException is Exception ex)
                {
                    Widgets.DrawBox(row, 2, Resources.Error);

                    TooltipHandler.TipRegion(
                        row,
                        new TipSignal(
                            "ColonyManagerRedux.Job.CausedException".Translate(
                                job.CausedExceptionText
                            )
                        )
                    );
                }

                if (Widgets.ButtonInvisible(row))
                {
                    Selected = Selected != job ? job : null;
                }
            }
        }

        Widgets.EndScrollView();

        _overviewHeight = cur.y;
    }

    private void DrawManualGroupButton(Rect rect, ManagerJob job)
    {
        var manualGroup = GetManualGroup(job);

        using (GUIScope.Color(manualGroup != null ? Color.white : Color.gray))
        {
            GUI.DrawTexture(rect, Resources.Tag);
        }

        TooltipHandler.TipRegion(
            rect,
            manualGroup != null
                ? "ColonyManagerRedux.Overview.ManualGroup.AssignedTooltip".Translate(manualGroup)
                : "ColonyManagerRedux.Overview.ManualGroup.UnassignedTooltip".Translate()
        );

        if (!Widgets.ButtonInvisible(rect))
        {
            return;
        }

        var existingGroups = Manager
            .JobTracker.JobsOfType<ManagerJob>()
            .Select(GetManualGroup)
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var options = new List<FloatMenuOption>();
        foreach (var group in existingGroups)
        {
            options.Add(new FloatMenuOption(group, () => SetManualGroup(job, group)));
        }

        options.Add(
            new FloatMenuOption(
                "ColonyManagerRedux.Overview.ManualGroup.NewGroup".Translate(),
                () =>
                    Find.WindowStack.Add(
                        new Dialog_NewManualGroup(existingGroups, name => SetManualGroup(job, name))
                    )
            )
        );

        if (manualGroup != null)
        {
            options.Add(
                new FloatMenuOption(
                    "ColonyManagerRedux.Overview.ManualGroup.RemoveFromGroup".Translate(),
                    () => SetManualGroup(job, null)
                )
            );
        }

        Find.WindowStack.Add(new FloatMenu(options));
    }

    private void DrawOverviewListEntry(ManagerJob job, ref Vector2 position, float width)
    {
        DrawOverviewListEntryParameters? parameters = null;
        if (
            job.CompOfType<CompDrawOverviewListEntry>()
            is CompDrawOverviewListEntry drawExportListEntry
        )
        {
            var props = drawExportListEntry.Props;
            var worker = props.Worker;
            if (props.takeOverRendering)
            {
                worker.DrawOverviewListEntry(job, ref position, width);
                return;
            }
            else
            {
                parameters = props.drawListEntryParameters;
                worker.ChangeDrawListEntryParameters(job, ref parameters);
            }
        }
        parameters ??= new();

        var tab = job.Tab;

        var labelWidth =
            width
            - (StatusRectWidth + (4 * Margin))
            - (2 * Margin)
            - LargeIconSize
            - LargeListEntryHeight
            - SmallIconSize
            - Margin;

        // create label string
        var subLabel = tab.GetSubLabel(job);
        var (label, labelSize) = tab.GetFullLabel(job, labelWidth, subLabel);

        // set up rects
        Rect iconRect = new(Margin, Margin, LargeIconSize, LargeIconSize);

        Rect labelRect = new(iconRect.xMax + Margin, iconRect.y, labelWidth, labelSize.y);
        Rect groupRect = new(labelRect.xMax + Margin, iconRect.y, SmallIconSize, SmallIconSize);
        Rect statusRect = new(
            groupRect.xMax + Margin,
            Margin,
            StatusRectWidth + Margin,
            LargeListEntryHeight
        );

        Rect stampRegionRect = new(
            statusRect.xMax - StampSize,
            statusRect.y,
            StampSize,
            statusRect.height
        );

        Rect lastUpdateRect = new(
            stampRegionRect.xMin - Margin - LastUpdateRectWidth,
            statusRect.y,
            LastUpdateRectWidth,
            statusRect.height
        );

        Rect progressRect = new(
            lastUpdateRect.xMin - Margin - ProgressRectWidth,
            statusRect.yMin,
            ProgressRectWidth,
            statusRect.height
        );

        Rect orderRect = new(
            statusRect.xMax + Margin,
            statusRect.y,
            LargeListEntryHeight,
            LargeListEntryHeight
        );

        // do the drawing
        var rowHeight = Mathf.Max(labelRect.yMax, statusRect.yMax) + Margin;
        Rect rowRect = new(position.x, position.y, width, rowHeight);
        GUI.BeginGroup(rowRect);
        rowRect = rowRect.AtZero();

        labelRect = labelRect.CenteredOnYIn(rowRect);
        iconRect = iconRect.CenteredOnYIn(rowRect);
        groupRect = groupRect.CenteredOnYIn(rowRect);

        IlyvionDebugViewSettings.DrawIfUIHelpers(() =>
        {
            Widgets.DrawRectFast(iconRect, ColorLibrary.HotPink.ToTransparent(.5f));
            Widgets.DrawRectFast(labelRect, Color.blue.ToTransparent(.2f));
            Widgets.DrawRectFast(statusRect, Color.yellow.ToTransparent(.2f));
            Widgets.DrawRectFast(lastUpdateRect, ColorLibrary.Orange.ToTransparent(.2f));
            Widgets.DrawRectFast(stampRegionRect, Color.red.ToTransparent(.2f));
            Widgets.DrawRectFast(progressRect, Color.green.ToTransparent(.2f));
            Widgets.DrawRectFast(orderRect, ColorLibrary.Aqua.ToTransparent(.5f));
        });

        // draw label
        IlyvionWidgets.Label(labelRect, label, subLabel, TextAnchor.MiddleLeft);

        DrawManualGroupButton(groupRect, job);

        // if the bill has a manager job, give some more info.
        if (tab.Enabled)
        {
            if (Widgets.ButtonImage(iconRect, tab.Def.icon))
            {
                MainTabWindow_Manager.GoTo(tab, job);
            }
            TooltipHandler.TipRegion(
                iconRect,
                "ColonyManagerRedux.Common.GoToJob".Translate(job.Label.UncapitalizeFirst())
            );
        }
        else
        {
            using var color = GUIScope.Color(Color.gray);
            GUI.DrawTexture(iconRect, tab.Def.icon);
            TooltipHandler.TipRegion(
                iconRect,
                tab.Label
                    + "ColonyManagerRedux.Common.TabDisabledBecause".Translate(tab.DisabledReason)
            );
        }

        var stampRect = new Rect(0, 0, StampSize, StampSize).CenteredIn(stampRegionRect);
        if (Utilities.DrawStampButton(stampRect, job))
        {
            job.IsSuspended = !job.IsSuspended;
        }

        if (job.IsSuspended)
        {
            if (job.CausedException != null)
            {
                TooltipHandler.TipRegion(
                    stampRect,
                    new TipSignal(
                        job.IsSuspendedDueToExceptionTooltip
                            + "\n\n"
                            + "ColonyManagerRedux.Job.ClickToChangeJob".Translate(
                                "ColonyManagerRedux.Job.Unsuspend".Translate()
                            )
                    )
                    {
                        // We do this so the exception is shown after
                        priority = TooltipPriority.Pawn,
                    }
                );
            }
            else
            {
                TooltipHandler.TipRegion(
                    stampRect,
                    job.IsSuspendedTooltip
                        + "\n\n"
                        + "ColonyManagerRedux.Job.ClickToChangeJob".Translate(
                            "ColonyManagerRedux.Job.Unsuspend".Translate()
                        )
                );
            }
        }
        else if (job.IsCompleted)
        {
            TooltipHandler.TipRegion(
                stampRect,
                job.IsCompletedTooltip
                    + "\n\n"
                    + "ColonyManagerRedux.Job.ClickToChangeJob".Translate(
                        "ColonyManagerRedux.Job.Suspend".Translate()
                    )
            );
        }
        else
        {
            TooltipHandler.TipRegion(
                stampRect,
                "ColonyManagerRedux.Job.ClickToChangeJob".Translate(
                    "ColonyManagerRedux.Job.Suspend".Translate()
                )
            );
        }

        if (parameters.ShowProgressbar && job.Trigger != null)
        {
            // draw progress bar
            job.Trigger.DrawVerticalProgressBars(
                progressRect,
                !job.IsSuspended && !job.IsCompleted
            );
        }

        // draw update interval
        UpdateInterval.Draw(lastUpdateRect, job, false, job.IsSuspended);

        if (DrawOrderButtons(orderRect, job, Manager.JobTracker))
        {
            Refresh();
        }

        GUI.EndGroup();
        position.y += rowRect.height;
    }

    public void DrawPawnOverview(Rect rect)
    {
        if (pawnOverviewTable == null)
        {
            pawnOverviewTable = CreatePawnOverviewTable();
            pawnOverviewTable.SetFixedSize(new(rect.width, rect.height));
        }

        pawnOverviewTable.PawnTableOnGUI(Vector2.zero);
    }

    private void RefreshWorkers()
    {
        var temp = Manager.map.mapPawns.FreeColonistsSpawned.Where(pawn =>
            !pawn.WorkTypeIsDisabled(WorkTypeDef)
        );

        // sort by either specific skill def or average over job - depending on which is known.
        temp =
            SkillDef != null
                ? temp.OrderByDescending(pawn => pawn.skills.GetSkill(SkillDef).Level)
                : temp.OrderByDescending(pawn =>
                    pawn.skills.AverageOfRelevantSkillsFor(WorkTypeDef)
                );

        _workers.Clear();
        _workers.AddRange(temp);
    }
}
