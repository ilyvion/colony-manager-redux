﻿// ManagerTab.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.Extensions;
using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

public abstract class ManagerTab<TJob, TSettings>(Manager manager) : ManagerTab<TJob>(manager)
    where TJob : ManagerJob
    where TSettings : ManagerSettings
{
    public TSettings ManagerSettings => ColonyManagerReduxMod.Settings
        .ManagerSettingsFor<TSettings>(Def)
            ?? throw new InvalidOperationException($"Type {GetType().Name} claims to have a "
            + $"manager settings type of {typeof(TSettings).Name}, but no such type has been "
            + "registered. Did you remember to add your settings type to your ManagerDef with a "
            + " managerSettingsClass value?");
}

public abstract class ManagerTab<T>(Manager manager) : ManagerTab(manager) where T : ManagerJob
{
    protected override IEnumerable<ManagerJob> ManagerJobs => Manager.JobTracker.JobsOfType<T>();

    public T? SelectedJob => (T?)Selected;

    internal override (bool top, bool bottom) GetJobOrderBounds(ManagerJob job, JobTracker jobTracker)
    {
        var (lowest, highest) = jobTracker.GetBoundsForJobsOfType<T>();
        bool top = job.Priority == lowest;
        bool bottom = job.Priority == highest;
        return (top, bottom);
    }

    internal override void TopPriority(JobTracker jobTracker, ManagerJob job)
    {
        jobTracker.TopPriority((T)job);
    }

    internal override void IncreasePriority(JobTracker jobTracker, ManagerJob job)
    {
        jobTracker.IncreasePriority((T)job);
    }

    internal override void DecreasePriority(JobTracker jobTracker, ManagerJob job)
    {
        jobTracker.DecreasePriority((T)job);
    }

    internal override void BottomPriority(JobTracker jobTracker, ManagerJob job)
    {
        jobTracker.BottomPriority((T)job);
    }
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

    private ManagerDef _def;
    public ManagerDef Def { get => _def; internal set => _def = value; }

    public bool Show => !ColonyManagerReduxMod.Settings.DisabledManagers.Contains(_def);

    private Manager _manager = manager;
    public Manager Manager { get => _manager; private set => _manager = value; }

    public virtual string DisabledReason => "";

    public virtual bool Enabled => true;

    protected virtual bool CreateNewSelectedJobOnMake => true;

    public virtual string Label => _def.label.CapitalizeFirst();

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

    protected virtual bool AllowJobDeselect => false;
    protected virtual bool DoMainContentWhenNothingSelected => false;

    internal void RenderTab(Rect rect)
    {
        DoTabContents(rect);
    }

    protected virtual bool ShouldHaveNewJobButton => true;
    private ScrollViewStatus _exceptionScrollViewStatus = new();
    protected virtual void DoTabContents(Rect canvas)
    {
        // set up rects
        var leftRow = new Rect(0f, 0f, DefaultLeftRowSize, canvas.height);
        var contentCanvas = new Rect(leftRow.xMax + Margin, 0f, canvas.width - leftRow.width - Margin,
                                      canvas.height);

        if (ShouldHaveNewJobButton)
        {
            leftRow.yMin += ButtonSize.y + Margin;
            var newJobButtonRect = new Rect(leftRow)
            {
                y = 0f,
                height = ButtonSize.y
            };
            DrawNewJobButton(newJobButtonRect);
        }

        // draw overview row
        DoJobList(leftRow);

        // draw job interface if something is selected.
        if (Selected != null)
        {
            if (Selected.CausedException != null)
            {
                const float ExceptionBoxHeight = 110f;

                var exceptionText = Selected.CausedExceptionText!;
                var exceptionRect = new Rect(
                    contentCanvas.x,
                    contentCanvas.y,
                    contentCanvas.width,
                    ExceptionBoxHeight + 2 * Margin);

                Widgets.DrawMenuSection(exceptionRect);
                Widgets.DrawBox(exceptionRect, lineTexture: Resources.Error);

                using (var scrollView = GUIScope.ScrollView(
                    exceptionRect,
                    _exceptionScrollViewStatus))
                {

                    var textHeight = Text.CalcHeight(
                        exceptionText, scrollView.ViewRect.width - 2 * Margin);

                    var textRect = exceptionRect.AtZero();
                    //textRect.yMin += Margin;
                    textRect.width = scrollView.ViewRect.width;
                    textRect.height = Mathf.Max(textHeight + 4 * Margin, exceptionRect.height);
                    scrollView.Height = textRect.height;

                    IlyvionWidgets.Label(
                        textRect.TrimLeft(Margin).TrimRight(Margin),
                        exceptionText,
                        TextAnchor.MiddleLeft,
                        color: ColorLibrary.LogError);
                    contentCanvas.yMin += ExceptionBoxHeight + 3 * Margin;
                }

                using var _t = GUIScope.Font(GameFont.Tiny);
                const float ButtonWidth = 130f;
                Rect buttonRect = new(
                    contentCanvas.xMax - ButtonWidth - Margin,
                    Margin,
                    ButtonWidth,
                    Text.LineHeight);
                if (Widgets.ButtonText(buttonRect, "Copy to clipboard"))
                {
                    GUIUtility.systemCopyBuffer = exceptionText;
                    Messages.Message(
                        "Exception copied to clipboard.",
                        MessageTypeDefOf.NeutralEvent,
                        historical: false);
                }
            }
            using var _g = GUIScope.WidgetGroup(contentCanvas);
            DoMainContent(contentCanvas.AtZero());
        }
        else if (DoMainContentWhenNothingSelected)
        {
            using var _g = GUIScope.WidgetGroup(contentCanvas);
            DoMainContent(contentCanvas.AtZero());
        }
    }

    protected virtual IEnumerable<ManagerJob> ManagerJobs => _manager.JobTracker.JobsOfType<ManagerJob>();


    protected virtual void DoMainContent(Rect rect)
    {
    }

#pragma warning disable CA1062 // Validate arguments of public methods

    public virtual void DrawLocalListEntry(
        ManagerJob job,
        ref Vector2 position,
        float width,
        DrawLocalListEntryParameters? parameters = null)
    {
        parameters ??= new();

        var tab = job.Tab;

        float labelWidth = width - 4 * Margin - StampSize
            - LargeListEntryHeight - LastUpdateRectWidth;

        // create label string
        var subLabel = tab.GetSubLabel(job);
        var (label, labelSize) = tab.GetFullLabel(job, labelWidth, subLabel);

        Rect labelRect = new(Margin, Margin, labelWidth, labelSize.y);

        Rect statusRect = new(
            0,
            labelRect.yMax + Margin,
            width - (parameters.ShowOrdering ? LargeListEntryHeight : Margin),
            parameters.StatusHeight);

        Rect rowRect = new(
            position.x,
            position.y,
            width,
            Mathf.Max(labelRect.yMax, statusRect.yMax) + Margin);

        Rect lastUpdateRect = new(
            labelRect.xMax + Margin,
            labelRect.y,
            LastUpdateRectWidth,
            labelRect.height);

        Rect stampRegionRect = new(
            lastUpdateRect.xMax + Margin,
            labelRect.y,
            StampSize,
            labelRect.height);

        Rect progressRect = new(
            Margin,
            statusRect.y,
            statusRect.width - Margin,
            statusRect.height);

        Rect orderRect = new(
            stampRegionRect.xMax + Margin,
            stampRegionRect.y,
            LargeListEntryHeight,
            progressRect.yMax - Margin);

        // do the drawing
        GUI.BeginGroup(rowRect);

        IlyvionDebugViewSettings.DrawIfUIHelpers(() =>
        {
            Widgets.DrawRectFast(labelRect, Color.blue.ToTransparent(.2f));
            Widgets.DrawRectFast(statusRect, Color.yellow.ToTransparent(.2f));
            Widgets.DrawRectFast(lastUpdateRect, ColorLibrary.Orange.ToTransparent(.2f));
            Widgets.DrawRectFast(stampRegionRect, Color.red.ToTransparent(.2f));
            Widgets.DrawRectFast(progressRect, Color.green.ToTransparent(.2f));
            Widgets.DrawRectFast(orderRect, ColorLibrary.Aqua.ToTransparent(.5f));
        });

        // draw label
        IlyvionWidgets.Label(labelRect, label, subLabel, TextAnchor.UpperLeft);

        // if we're not doing export, render stamp
        var stampRect = new Rect(0, 0, StampSize, StampSize).CenteredIn(stampRegionRect);
        if (Utilities.DrawStampButton(stampRect, job))
        {
            job.IsSuspended = !job.IsSuspended;
        }

        if (job.IsSuspended)
        {
            if (job.CausedException != null)
            {
                TooltipHandler.TipRegion(stampRect, new TipSignal(
                    job.IsSuspendedDueToExceptionTooltip + "\n\n" +
                    "ColonyManagerRedux.Job.ClickToChangeJob".Translate(
                        "ColonyManagerRedux.Job.Unsuspend".Translate()))
                {
                    // We do this so the exception is shown after
                    priority = TooltipPriority.Pawn
                });
            }
            else
            {
                TooltipHandler.TipRegion(stampRect,
                    job.IsSuspendedTooltip + "\n\n" +
                    "ColonyManagerRedux.Job.ClickToChangeJob".Translate(
                        "ColonyManagerRedux.Job.Unsuspend".Translate()));
            }
        }
        else if (job.IsCompleted)
        {
            TooltipHandler.TipRegion(stampRect,
                job.IsCompletedTooltip + "\n\n" +
                "ColonyManagerRedux.Job.ClickToChangeJob".Translate(
                    "ColonyManagerRedux.Job.Suspend".Translate()));
        }
        else
        {
            TooltipHandler.TipRegion(stampRect,
                "ColonyManagerRedux.Job.ClickToChangeJob".Translate(
                    "ColonyManagerRedux.Job.Suspend".Translate()));
        }

        if (parameters.ShowProgressbar && job.Trigger != null)
        {
            job.Trigger.DrawHorizontalProgressBars(
                progressRect,
                !job.IsSuspended && !job.IsCompleted);
        }

        UpdateInterval.Draw(
            lastUpdateRect,
            job,
            false,
            job.IsSuspended);

        if (parameters.ShowOrdering && DrawOrderButtons(
            orderRect,
            job,
            _manager.JobTracker))
        {
            Refresh();
        }

        GUI.EndGroup();
        position.y += rowRect.height;
    }

    public virtual (string label, Vector2 labelSize) GetFullLabel(
        ManagerJob job,
        float labelWidth,
        string? subLabel = null,
        bool drawSubLabel = true)
    {
        if (drawSubLabel)
        {
            subLabel ??= GetSubLabel(job);
            if (!subLabel.Fits(labelWidth, out var _))
            {
                subLabel = subLabel.Truncate(labelWidth);
            }
        }
        var mainLabel = GetMainLabel(job);
        if (!mainLabel.Fits(labelWidth, out var _))
        {
            mainLabel = mainLabel.Truncate(labelWidth);
        }
        var label = mainLabel + (drawSubLabel ? "\n" + subLabel : "");
        return (label, Text.CalcSize(label));
    }

    public virtual string GetMainLabel(ManagerJob job)
    {
        return Label;
    }

    public virtual string GetSubLabel(ManagerJob job)
    {
        if (job.Targets.Any())
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
        if (job.IsSuspended)
        {
            Widgets.DrawRectFast(rect, Color.white.ToTransparent(.2f));
            var bgRect = new Rect(rect);
            bgRect.yMin += rect.height / 2 - 50f;
            bgRect.yMax -= rect.height / 2 - 50f;
            bgRect = bgRect.ContractedBy(10f);
            Widgets.DrawRectFast(bgRect, Color.black.ToTransparent(.8f));
            IlyvionWidgets.Label(
                new(rect) { height = rect.height - 15f },
                "ColonyManagerRedux.History.JobSuspended".Translate(),
                TextAnchor.MiddleCenter,
                GameFont.Medium);
            IlyvionWidgets.Label(
                new(rect) { y = rect.y + 20, height = rect.height - 15f },
                "(" + "ColonyManagerRedux.Job.ClickToChangeJob".Translate(
                    "ColonyManagerRedux.Job.Unsuspend".Translate()) + ")",
                TextAnchor.MiddleCenter,
                GameFont.Small);

            if (Widgets.ButtonInvisible(rect, false))
            {
                job.IsSuspended = false;
            }
        }

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
        return ManagerDefMaker.MakeManagerJob(_def, _manager, args);
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

    private readonly ScrollViewStatus _jobListScrollViewStatus = new();
    protected virtual void DoJobList(Rect rect)
    {
        Widgets.DrawMenuSection(rect);

        using var scrollView = GUIScope.ScrollView(rect, _jobListScrollViewStatus);
        using var _g = GUIScope.WidgetGroup(scrollView.ViewRect);

        var cur = Vector2.zero;
        var i = 0;

        foreach (var job in ManagerJobs)
        {
            var row = new Rect(0f, cur.y, scrollView.ViewRect.width, 0f);
            DrawLocalListEntry(
                job,
                ref cur,
                scrollView.ViewRect.width,
                null);

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

            if (job.CausedException is Exception ex)
            {
                Widgets.DrawBox(row, 2, Resources.Error);

                TooltipHandler.TipRegion(row, new TipSignal(
                    "ColonyManagerRedux.Job.CausedException".Translate(job.CausedExceptionText)));
            }

            if (Widgets.ButtonInvisible(row))
            {
                if (Selected != job)
                {
                    Selected = job;
                }
                else if (AllowJobDeselect)
                {
                    Selected = null;
                }
            }
        }

        scrollView.Height = cur.y;
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

    internal virtual (bool top, bool bottom) GetJobOrderBounds(
        ManagerJob job,
        JobTracker jobTracker)
    {
        bool top = job.Priority == 0;
        bool bottom = job.Priority == jobTracker.MaxPriority;

        return (top, bottom);
    }

    internal virtual void TopPriority(JobTracker jobTracker, ManagerJob job)
    {
        jobTracker.TopPriority(job);
    }

    internal virtual void IncreasePriority(JobTracker jobTracker, ManagerJob job)
    {
        jobTracker.IncreasePriority(job);
    }

    internal virtual void DecreasePriority(JobTracker jobTracker, ManagerJob job)
    {
        jobTracker.DecreasePriority(job);
    }

    internal virtual void BottomPriority(JobTracker jobTracker, ManagerJob job)
    {
        jobTracker.BottomPriority(job);
    }

    /// <summary>
    ///     Draw a square group of ordering buttons for a job in rect.
    /// </summary>
    public bool DrawOrderButtons(
        Rect rect,
        ManagerJob job,
        JobTracker jobTracker)
    {
        if (job == null)
        {
            throw new ArgumentNullException(nameof(job));
        }
        if (jobTracker == null)
        {
            throw new ArgumentNullException(nameof(jobTracker));
        }

        float width = 22;
        float height = 22;

        Rect upRect = new(rect.xMin, rect.yMin, width, height),
            downRect = new(rect.xMin, rect.yMax - height, width, height),
            topRect = new(rect.xMax - width, rect.yMin, width, height),
            bottomRect = new(rect.xMax - width, rect.yMax - height, width, height);

        var reOrdered = false;

        var (top, bottom) = GetJobOrderBounds(job, jobTracker);

        IlyvionDebugViewSettings.DrawIfUIHelpers(() =>
        {
            Widgets.DrawRectFast(upRect, ColorLibrary.Indigo.ToTransparent(.5f));
            Widgets.DrawRectFast(downRect, ColorLibrary.Indigo.ToTransparent(.5f));
            Widgets.DrawRectFast(topRect, ColorLibrary.Indigo.ToTransparent(.5f));
            Widgets.DrawRectFast(bottomRect, ColorLibrary.Indigo.ToTransparent(.5f));
            IlyvionWidgets.Label(rect, job.Priority.ToString(), TextAnchor.MiddleCenter);
        });

        if (!top)
        {
            DrawOrderTooltips(upRect, topRect);
            if (Widgets.ButtonImage(topRect, Resources.ArrowTop))
            {
                TopPriority(jobTracker, job);
                reOrdered = true;
            }

            if (Widgets.ButtonImage(upRect, Resources.ArrowUp))
            {
                IncreasePriority(jobTracker, job);
                reOrdered = true;
            }
        }

        if (!bottom)
        {
            DrawOrderTooltips(downRect, bottomRect, increase: false);
            if (Widgets.ButtonImage(downRect, Resources.ArrowDown))
            {
                DecreasePriority(jobTracker, job);
                reOrdered = true;
            }

            if (Widgets.ButtonImage(bottomRect, Resources.ArrowBottom))
            {
                BottomPriority(jobTracker, job);
                reOrdered = true;
            }
        }

        return reOrdered;
    }

    private static void DrawOrderTooltips(Rect step, Rect max, bool increase = true)
    {
        if (increase)
        {
            TooltipHandler.TipRegion(step, "ColonyManagerRedux.Job.IncreasePriority".Translate());
            TooltipHandler.TipRegion(max, "ColonyManagerRedux.Job.TopPriority".Translate());
        }
        else
        {
            TooltipHandler.TipRegion(step, "ColonyManagerRedux.Job.DecreasePriority".Translate());
            TooltipHandler.TipRegion(max, "ColonyManagerRedux.Job.BottomPriority".Translate());
        }
    }

    protected internal virtual void Notify_PawnsChanged()
    {
    }
}

public class DrawLocalListEntryParameters
{
    public bool ShowOrdering { get; set; } = true;
    public bool ShowProgressbar { get; set; } = true;
    public float StatusHeight { get; set; } = SmallIconSize;
}
