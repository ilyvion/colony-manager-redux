// ManagerTab.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory;
using ilyvion.Laboratory.Extensions;
using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

public abstract class ManagerTab<T>(Manager manager) : ManagerTab(manager) where T : ManagerJob
{
    protected override IEnumerable<ManagerJob> ManagerJobs => manager.JobTracker.JobsOfType<T>();

    public T? SelectedJob => (T?)Selected;
}

#pragma warning disable CS8618 // Set by ManagerDefMaker.MakeManagerTab
[HotSwappable]
public abstract class ManagerTab(Manager manager)
#pragma warning restore CS8618
{
    public const float DefaultLeftRowSize = 300f;

    public const float
        StampSize = SmallIconSize,
        LastUpdateRectWidth = 50f,
        ProgressRectWidth = 60f,
        StatusRectWidth = StampSize + LastUpdateRectWidth + ProgressRectWidth + 2 * Margin;

    public ManagerDef def;

    public Manager manager = manager;

    public virtual string DisabledReason => "";

    public virtual bool Enabled => true;

    protected virtual bool CreateNewSelectedJobOnMake => true;

    public virtual string Label => GetType().ToString();

    private ManagerJob? _selected;
    public ManagerJob? Selected
    {
        get => _selected;
        set
        {
            PreSelect();
            _selected = value;
            PostSelect();
        }
    }

    internal void RenderTab(Rect rect)
    {
        DoTabContents(rect);
    }

    protected virtual void DoTabContents(Rect canvas)
    {
        // set up rects
        var leftRow = new Rect(0f, ButtonSize.y + Margin, DefaultLeftRowSize, canvas.height - ButtonSize.y - Margin);
        var newJobButtonRect = new Rect(leftRow)
        {
            y = 0f,
            height = ButtonSize.y
        };
        var contentCanvas = new Rect(leftRow.xMax + Margin, 0f, canvas.width - leftRow.width - Margin,
                                      canvas.height);

        DrawNewJobButton(newJobButtonRect);

        // draw overview row
        DoJobList(leftRow);

        // draw job interface if something is selected.
        if (Selected != null)
        {
            DoMainContent(contentCanvas);
        }
    }

    protected virtual IEnumerable<ManagerJob> ManagerJobs => manager.JobTracker.JobsOfType<ManagerJob>();

    protected virtual void DoMainContent(Rect rect)
    {
    }

    public enum ListEntryDrawMode
    {
        Local,
        Overview,
        Export
    }

#pragma warning disable CA1062 // Validate arguments of public methods

    public virtual void DrawListEntry(
        ManagerJob job,
        ref Vector2 position,
        float width,
        ListEntryDrawMode mode,
        bool active = true,
        bool showOrdering = true,
        float statusHeight = LargeListEntryHeight)
    {
        var tab = job.Tab;

        float labelWidth;
        if (mode == ListEntryDrawMode.Local)
        {
            labelWidth = width - 3 * Margin - StampSize - LargeListEntryHeight;
        }
        else if (mode == ListEntryDrawMode.Overview)
        {
            labelWidth = width - (active ? StatusRectWidth + 4 * Margin : 2 * Margin) - 2 * Margin - LargeIconSize - LargeListEntryHeight;
        }
        else
        {
            labelWidth = width - LargeListEntryHeight - LastUpdateRectWidth - Margin;
        }

        // create label string
        var subLabel = tab.GetSubLabel(job, mode);
        var (label, labelSize) = tab.GetFullLabel(job, mode, labelWidth, subLabel);

        Rect iconRect;
        Rect labelRect;
        Rect statusRect;
        Rect stampRegionRect;
        Rect progressRect;
        Rect rowRect;
        Rect orderRect;
        if (mode == ListEntryDrawMode.Local)
        {
            iconRect = new();
            labelRect = new Rect(Margin, Margin, labelWidth, labelSize.y);
            statusRect = new Rect(0, labelRect.yMax + Margin, width - (showOrdering ? LargeListEntryHeight : Margin), statusHeight);

            rowRect = new()
            {
                x = position.x,
                y = position.y,
                width = width,
                height = Mathf.Max(labelRect.yMax, statusRect.yMax) + Margin
            };

            stampRegionRect = new Rect(labelRect.xMax + Margin, labelRect.y, StampSize, labelRect.height);
            progressRect = new()
            {
                x = Margin,
                y = statusRect.y,
                width = statusRect.width - Margin,
                height = statusRect.height
            };

            orderRect = new(
                stampRegionRect.xMax + Margin,
                stampRegionRect.y,
                LargeListEntryHeight,
                progressRect.yMax - Margin);
        }
        else if (mode == ListEntryDrawMode.Overview)
        {
            // set up rects
            iconRect = new Rect(Margin, Margin,
                LargeIconSize, LargeIconSize);

            labelRect = new Rect(
                iconRect.xMax + Margin,
                iconRect.y,
                labelWidth,
                labelSize.y);
            statusRect = new Rect(labelRect.xMax + Margin, Margin, StatusRectWidth + Margin, statusHeight);

            float rowHeight = Mathf.Max(labelRect.yMax, statusRect.yMax) + Margin;
            rowRect = new()
            {
                x = position.x,
                y = position.y,
                width = width,
                height = rowHeight
            };

            stampRegionRect = new();
            progressRect = new();

            orderRect = new(
                statusRect.xMax + Margin,
                statusRect.y,
                LargeListEntryHeight,
                LargeListEntryHeight);
        }
        else
        {
            // set up rects
            iconRect = new();

            labelRect = new Rect(
                iconRect.xMax + Margin,
                iconRect.y,
                labelWidth,
                    labelSize.y);
            statusRect = new Rect(labelRect.xMax + Margin, Margin, LastUpdateRectWidth, labelRect.height);

            float v = Mathf.Max(labelRect.yMax, statusRect.yMax) + Margin;
            rowRect = new()
            {
                x = position.x,
                y = position.y,
                width = width,
                height = v
            };

            stampRegionRect = new();
            progressRect = new();
            orderRect = new();
        }

        // do the drawing
        GUI.BeginGroup(rowRect);

        if (IlyvionDebugViewSettings.DrawUIHelpers)
        {
            Widgets.DrawRectFast(iconRect, ColorLibrary.HotPink.ToTransparent(.5f));
            Widgets.DrawRectFast(labelRect, Color.blue.ToTransparent(.2f));
            Widgets.DrawRectFast(statusRect, Color.yellow.ToTransparent(.2f));
            Widgets.DrawRectFast(stampRegionRect, Color.red.ToTransparent(.2f));
            Widgets.DrawRectFast(progressRect, Color.green.ToTransparent(.2f));
            Widgets.DrawRectFast(orderRect, ColorLibrary.Aqua.ToTransparent(.5f));
        }

        // draw label
        Widgets_Labels.Label(labelRect, label, subLabel, mode == ListEntryDrawMode.Local ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft);

        // if the bill has a manager job, give some more info.
        if (mode == ListEntryDrawMode.Overview)
        {
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
        }
        if (mode != ListEntryDrawMode.Local)
        {
            if (active && job.Trigger != null)
            {
                job.DrawStatusForListEntry(statusRect, job.Trigger, mode);
            }
        }
        else
        {
            var stampRect = new Rect(0, 0, StampSize, StampSize).CenteredIn(stampRegionRect);
            if (Widgets.ButtonImage(
                stampRect,
                job.IsSuspended ? Resources.StampStart :
                job.IsCompleted ? Resources.StampCompleted : Resources.StampSuspended))
            {
                job.IsSuspended = !job.IsSuspended;
            }

            if (job.IsSuspended)
            {
                TooltipHandler.TipRegion(stampRect,
                    job.IsSuspendedTooltip + "\n\n" +
                    "ColonyManagerRedux.Overview.ClickToChangeJob".Translate(
                        "ColonyManagerRedux.Overview.Unsuspend".Translate()));
            }
            else if (job.IsCompleted)
            {
                TooltipHandler.TipRegion(stampRect,
                    job.IsCompletedTooltip + "\n\n" +
                    "ColonyManagerRedux.Overview.ClickToChangeJob".Translate(
                        "ColonyManagerRedux.Overview.Suspend".Translate()));
            }
            else
            {
                TooltipHandler.TipRegion(stampRect,
                    "ColonyManagerRedux.Overview.ClickToChangeJob".Translate(
                        "ColonyManagerRedux.Overview.Suspend".Translate()));
            }

            job.Trigger!.DrawHorizontalProgressBars(progressRect, !job.IsSuspended && !job.IsCompleted);
        }

        if (showOrdering && DrawOrderButtons(
            orderRect,
            job,
            ManagerJobs.ToList(),
            manager.JobTracker))
        {
            Refresh();
        }

        GUI.EndGroup();
        position.y += rowRect.height;
    }

    public virtual (string label, Vector2 labelSize) GetFullLabel(
        ManagerJob job,
        ListEntryDrawMode mode,
        float labelWidth,
        string? subLabel = null,
        bool drawSubLabel = true)
    {
        if (drawSubLabel)
        {
            subLabel ??= GetSubLabel(job, mode);
            if (!subLabel.Fits(labelWidth, out var _))
            {
                subLabel = subLabel.Truncate(labelWidth);
            }
        }
        var mainLabel = GetMainLabel(job, mode);
        if (!mainLabel.Fits(labelWidth, out var _))
        {
            mainLabel = mainLabel.Truncate(labelWidth);
        }
        var label = mainLabel + (drawSubLabel ? "\n" + subLabel : "");
        return (label, Text.CalcSize(label));
    }

    public virtual string GetMainLabel(ManagerJob job, ListEntryDrawMode mode)
    {
        return Label;
    }

    public virtual string GetSubLabel(ManagerJob job, ListEntryDrawMode mode)
    {
        var targets = job.Targets.ToList();
        if (targets.Count > 0)
        {
            return string.Join(", ", job.Targets);
        }
        else
        {
            return "ColonyManagerRedux.Common.None".Translate();
        }
    }

    public virtual bool DrawOverviewDetails(ManagerJob job, Rect rect)
    {
        if (job.CompOfType<CompManagerJobHistory>() is not CompManagerJobHistory historyComp)
        {
            return false;
        }

        historyComp.History.DrawPlot(rect);
        return true;
    }
#pragma warning restore CA1062 // Validate arguments of public methods

    public virtual void PostMake()
    {
        if (CreateNewSelectedJobOnMake)
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                // Postpone creation until after LoadingVars, since it's too early in the scribing
                // process for things like defs to be ready yet.
                LongEventHandler.ExecuteWhenFinished(() =>
                {
                    Selected = MakeNewJob();
                });
            }
            else
            {
                Selected = MakeNewJob();
            }
        }
    }

    public ManagerJob? MakeNewJob(params object[] args)
    {
        return ManagerDefMaker.MakeManagerJob(def, manager, args);
    }

    public virtual void PostClose()
    {
    }

    public virtual void PostOpen()
    {
    }

    public virtual void PreClose()
    {
    }

    public virtual void PreOpen()
    {
    }

    public virtual void Tick()
    {
    }

    protected virtual void PreSelect()
    {
    }

    protected virtual void PostSelect()
    {
    }

    protected static void DrawShortcutToggle<T>(List<T> options, HashSet<T> selected, Action<T, bool> setAllowed, Rect rect, string labelKey, string? toolTipKey)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
        if (selected == null)
        {
            throw new ArgumentNullException(nameof(selected));
        }

        var allSelected = options.All(selected.Contains);
        var noneSelected = options.All(p => !selected.Contains(p));

        Utilities.DrawToggle(
            rect,
            labelKey.Translate().Italic(),
            toolTipKey != null ? toolTipKey.Translate() : string.Empty,
            allSelected,
            noneSelected,
            () => options.ForEach(p => setAllowed(p, true)),
            () => options.ForEach(p => setAllowed(p, false)));
    }

    protected float _jobListHeight;
    protected Vector2 _jobListScrollPosition = Vector2.zero;

    protected virtual void DoJobList(Rect rect)
    {
        Widgets.DrawMenuSection(rect);

        // content
        var height = _jobListHeight;
        var scrollView = new Rect(0f, 0f, rect.width, height);
        if (height > rect.height)
        {
            scrollView.width -= ScrollbarWidth;
        }

        Widgets.BeginScrollView(rect, ref _jobListScrollPosition, scrollView);
        var scrollContent = scrollView;

        GUI.BeginGroup(scrollContent);
        var cur = Vector2.zero;
        var i = 0;

        foreach (var job in ManagerJobs)
        {
            var row = new Rect(0f, cur.y, scrollContent.width, 0f);
            DrawListEntry(job, ref cur, scrollContent.width, ListEntryDrawMode.Local, statusHeight: SmallIconSize);
            row.height = cur.y - row.y;

            Widgets.DrawHighlightIfMouseover(row);
            if (Selected == job)
            {
                Widgets.DrawHighlightSelected(row);
            }

            if (i++ % 2 == 1)
            {
                Widgets.DrawAltRect(row);
            }

            if (Widgets.ButtonInvisible(row))
            {
                Selected = job;
            }
        }

        _jobListHeight = cur.y;
        GUI.EndGroup();
        Widgets.EndScrollView();
    }

    protected virtual void DrawNewJobButton(Rect rect)
    {
        if (Widgets.ButtonText(rect, "ColonyManagerRedux.Job.New".Translate().Resolve()))
        {
            Selected = MakeNewJob();
        }
    }

    protected virtual void Refresh()
    {
    }

    /// <summary>
    ///     Draw a square group of ordering buttons for a job in rect.
    /// </summary>
    // TODO: Track job positions directly in the jobs so we don't have to
    //       deal with producing and asking a List<> about positions every
    //       single frame
    public static bool DrawOrderButtons(
        Rect rect,
        ManagerJob job,
        List<ManagerJob> jobs,
        JobTracker jobStack)
    {
        if (job == null)
        {
            throw new ArgumentNullException(nameof(job));
        }
        if (jobs == null)
        {
            throw new ArgumentNullException(nameof(jobs));
        }
        if (jobStack == null)
        {
            throw new ArgumentNullException(nameof(jobStack));
        }

        var ret = false;

        float width = 22;
        float height = 22;

        Rect upRect = new(rect.xMin, rect.yMin, width, height),
            downRect = new(rect.xMin, rect.yMax - height, width, height),
            topRect = new(rect.xMax - width, rect.yMin, width, height),
            bottomRect = new(rect.xMax - width, rect.yMax - height, width, height);

        if (IlyvionDebugViewSettings.DrawUIHelpers)
        {
            Widgets.DrawRectFast(upRect, ColorLibrary.Indigo.ToTransparent(.5f));
            Widgets.DrawRectFast(downRect, ColorLibrary.Indigo.ToTransparent(.5f));
            Widgets.DrawRectFast(topRect, ColorLibrary.Indigo.ToTransparent(.5f));
            Widgets.DrawRectFast(bottomRect, ColorLibrary.Indigo.ToTransparent(.5f));
        }

        bool top = jobs.IndexOf(job) == 0,
            bottom = jobs.IndexOf(job) == jobs.Count - 1;

        if (!top)
        {
            DrawOrderTooltips(upRect, topRect);
            if (Widgets.ButtonImage(topRect, Resources.ArrowTop))
            {
                jobStack.TopPriority(job);
                ret = true;
            }

            if (Widgets.ButtonImage(upRect, Resources.ArrowUp))
            {
                jobStack.IncreasePriority(job);
                ret = true;
            }
        }

        if (!bottom)
        {
            DrawOrderTooltips(downRect, bottomRect, false);
            if (Widgets.ButtonImage(downRect, Resources.ArrowDown))
            {
                jobStack.DecreasePriority(job);
                ret = true;
            }

            if (Widgets.ButtonImage(bottomRect, Resources.ArrowBottom))
            {
                jobStack.BottomPriority(job);
                ret = true;
            }
        }

        return ret;
    }

    private static void DrawOrderTooltips(Rect step, Rect max, bool up = true)
    {
        if (up)
        {
            TooltipHandler.TipRegion(step, "ColonyManagerRedux.Job.IncreasePriority".Translate());
            TooltipHandler.TipRegion(max, "ColonyManagerRedux.Job.TopPriority".Translate());
        }
        else
        {
            TooltipHandler.TipRegion(step, "ColonyManagerRedux.Job.DecreasePriorityn".Translate());
            TooltipHandler.TipRegion(max, "ColonyManagerRedux.Job.BottomPriority".Translate());
        }
    }

    internal virtual void Notify_PawnsChanged()
    {
    }
}
