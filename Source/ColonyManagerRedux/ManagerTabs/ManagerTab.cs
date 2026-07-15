// ManagerTab.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.Extensions;
using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

/// <summary>
/// Represents a manager tab for a specific job and settings type.
/// </summary>
/// <typeparam name="TJob">The type of manager job.</typeparam>
/// <typeparam name="TSettings">The type of manager settings.</typeparam>
/// <param name="manager">The manager instance.</param>
public abstract class ManagerTab<TJob, TSettings>(Manager manager) : ManagerTab<TJob>(manager)
    where TJob : ManagerJob
    where TSettings : ManagerSettings
{
    /// <summary>
    /// Gets the manager settings for this tab.
    /// </summary>
    public TSettings ManagerSettings =>
        ColonyManagerReduxMod.Settings.ManagerSettingsFor<TSettings>(Def)
        ?? throw new InvalidOperationException(
            $"Type {GetType().Name} claims to have a "
                + $"manager settings type of {typeof(TSettings).Name}, but no such type has been "
                + "registered. Did you remember to add your settings type to your ManagerDef with a "
                + " managerSettingsClass value?"
        );
}

/// <summary>
/// Represents a manager tab for a specific job type.
/// </summary>
/// <typeparam name="T">The type of manager job.</typeparam>
/// <param name="manager">The manager instance.</param>
public abstract class ManagerTab<T>(Manager manager) : ManagerTab(manager)
    where T : ManagerJob
{
    /// <inheritdoc/>
    protected override IEnumerable<ManagerJob> ManagerJobs => Manager.JobTracker.JobsOfType<T>();

    /// <summary>
    /// Gets the currently selected job of type <typeparamref name="T"/> in this manager tab, or null if none is selected.
    /// </summary>
    public T? SelectedJob => (T?)Selected;

    internal override (bool top, bool bottom) GetJobOrderBounds(
        ManagerJob job,
        JobTracker jobTracker
    )
    {
        var (lowest, highest) = jobTracker.GetBoundsForJobsOfType<T>();
        var top = job.Priority == lowest;
        var bottom = job.Priority == highest;
        return (top, bottom);
    }

    internal override void TopPriority(JobTracker jobTracker, ManagerJob job) =>
        jobTracker.TopPriority((T)job);

    internal override void IncreasePriority(JobTracker jobTracker, ManagerJob job) =>
        jobTracker.IncreasePriority((T)job);

    internal override void DecreasePriority(JobTracker jobTracker, ManagerJob job) =>
        jobTracker.DecreasePriority((T)job);

    internal override void BottomPriority(JobTracker jobTracker, ManagerJob job) =>
        jobTracker.BottomPriority((T)job);

    /// <summary>
    /// Draws a section for the selected job, including pre-render and post-render hooks for job components.
    /// </summary>
    /// <param name="sectionColumn">The section column identifier.</param>
    /// <param name="section">The section identifier.</param>
    /// <param name="position">The position vector, passed by reference and updated after drawing.</param>
    /// <param name="width">The width of the section.</param>
    /// <param name="drawerFunc">A function to draw the section content for the job.</param>
    /// <param name="header">An optional header for the section.</param>
    protected void DrawSection(
        string sectionColumn,
        string section,
        ref Vector2 position,
        float width,
        Func<T, Vector2, float, float> drawerFunc,
        string header = ""
    ) => DrawSection(sectionColumn, section, ref position, width, drawerFunc, header, 0);

    /// <summary>
    /// Draws a section for the selected job, including pre-render and post-render hooks for job components.
    /// </summary>
    /// <param name="sectionColumn">The section column identifier.</param>
    /// <param name="section">The section identifier.</param>
    /// <param name="position">The position vector, passed by reference and updated after drawing.</param>
    /// <param name="width">The width of the section.</param>
    /// <param name="drawerFunc">A function to draw the section content for the job.</param>
    /// <param name="header">An optional header for the section.</param>
    /// <param name="id">A unique identifier for the section, used for stable UI state.</param>
    protected void DrawSection(
        string sectionColumn,
        string section,
        ref Vector2 position,
        float width,
        Func<T, Vector2, float, float> drawerFunc,
        string header,
        int id = 0
    )
    {
        if (drawerFunc == null)
        {
            throw new ArgumentNullException(nameof(drawerFunc));
        }
        if (id == 0 && Utilities.IsLikelyAnonymous(drawerFunc))
        {
            ColonyManagerReduxMod.Instance.LogWarning(
                $"DrawSection drawerFunc seems to be an anonymous function; not providing a manual value for id "
                    + "may lead to unexpected behavior as these don't have a stable hash value. "
                    + $"Auto-generated id for {drawerFunc.Method.Name} is {drawerFunc.GetHashCode()}"
            );
        }
        id = id != 0 ? id : drawerFunc.GetHashCode();

        var localPosition = position;
        SelectedJob!.ForAllCompsOfType<ManagerJobComp>(c =>
            c.PreRenderSection(sectionColumn, section, ref localPosition, width)
        );
        Widgets_Section.Section(
            SelectedJob,
            ref localPosition,
            width,
            (job, pos, width) =>
            {
                var start = pos;
                SelectedJob!.ForAllCompsOfType<ManagerJobComp>(c =>
                    pos.y += c.RenderSectionPrefix(sectionColumn, section, job, pos, width)
                );

                pos.y += drawerFunc(job, pos, width);

                SelectedJob!.ForAllCompsOfType<ManagerJobComp>(c =>
                    pos.y += c.RenderSectionPostfix(sectionColumn, section, job, pos, width)
                );

                return pos.y - start.y;
            },
            header,
            id
        );
        SelectedJob.ForAllCompsOfType<ManagerJobComp>(c =>
            c.PostRenderSection(sectionColumn, section, ref localPosition, width)
        );
        position = localPosition;
    }
}

#pragma warning disable CS8618 // Set by ManagerDefMaker.MakeManagerTab
/// <summary>
/// Base class for all manager tabs in the Colony Manager Redux mod.
/// </summary>
/// <param name="manager">The manager instance associated with this tab.</param>
[HotSwappable]
public abstract class ManagerTab(Manager manager)
#pragma warning restore CS8618
{
    /// <summary>
    /// The default width of the left row in the manager tab UI.
    /// </summary>
    public const float DefaultLeftRowSize = 300f;

    /// <summary>
    /// The size of the stamp icon in the manager tab UI.
    /// </summary>
    public const float StampSize = SmallIconSize;

    /// <summary>
    /// The width of the last update rectangle in the manager tab UI.
    /// </summary>
    public const float LastUpdateRectWidth = 50f;

    /// <summary>
    /// The width of the progress rectangle in the manager tab UI.
    /// </summary>
    public const float ProgressRectWidth = 60f;

    /// <summary>
    /// The width of the status rectangle in the manager tab UI.
    /// </summary>
    public const float StatusRectWidth =
        StampSize + LastUpdateRectWidth + ProgressRectWidth + (2 * Margin);

#pragma warning disable CS8618 // Set externally
    /// <summary>
    /// Gets the manager definition associated with this tab.
    /// </summary>
    public ManagerDef Def { get; internal set; }
#pragma warning restore CS8618

    /// <summary>
    /// Gets a value indicating whether this manager tab should be shown, based on whether it is disabled in the settings.
    /// </summary>
    public bool Show => !ColonyManagerReduxMod.Settings.DisabledManagers.Contains(Def);

    /// <summary>
    /// Gets the manager instance associated with this tab.
    /// </summary>
    public Manager Manager { get; private set; } = manager;

    /// <summary>
    /// Gets the reason why this manager tab is disabled, if any.
    /// </summary>
    public virtual string DisabledReason => "";

    /// <summary>
    /// Gets a value indicating whether this manager tab is enabled.
    /// </summary>
    public virtual bool Enabled => true;

    /// <summary>
    /// Gets a value indicating whether a new job should be created and selected when the manager tab is made.
    /// </summary>
    protected virtual bool CreateNewSelectedJobOnMake => true;

    /// <summary>
    /// Gets the label for this manager tab, with the first letter capitalized.
    /// </summary>
    public virtual string Label => Def.label.CapitalizeFirst();

    /// <summary>
    /// Gets or sets the currently selected manager job in this tab.
    /// </summary>
    public ManagerJob? Selected
    {
        get;
        set
        {
            PreSelect();
            field = value;
            PostSelect();
        }
    }

    /// <summary>
    /// Gets a value indicating whether the currently selected job can be deselected in this manager tab.
    /// </summary>
    protected virtual bool AllowJobDeselect => false;

    /// <summary>
    /// Gets a value indicating whether the main content should be drawn when no job is selected.
    /// </summary>
    protected virtual bool DoMainContentWhenNothingSelected => false;

    internal void RenderTab(Rect rect) => DoTabContents(rect);

    /// <summary>
    /// Gets a value indicating whether the "New Job" button should be shown in the manager tab UI.
    /// </summary>
    protected virtual bool ShouldHaveNewJobButton => true;
    private readonly ScrollViewStatus _exceptionScrollViewStatus = new();

    /// <summary>
    /// Draws the contents of the manager tab, including the job list and main content area.
    /// </summary>
    /// <param name="canvas">The rectangle area in which to draw the tab contents.</param>
    protected virtual void DoTabContents(Rect canvas)
    {
        // set up rects
        var leftRow = new Rect(0f, 0f, DefaultLeftRowSize, canvas.height);
        var contentCanvas = new Rect(
            leftRow.xMax + Margin,
            0f,
            canvas.width - leftRow.width - Margin,
            canvas.height
        );

        if (ShouldHaveNewJobButton)
        {
            leftRow.yMin += ButtonSize.y + Margin;
            var newJobButtonRect = new Rect(leftRow) { y = 0f, height = ButtonSize.y };
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
                    ExceptionBoxHeight + (2 * Margin)
                );

                Widgets.DrawMenuSection(exceptionRect);
                Widgets.DrawBox(exceptionRect, lineTexture: Resources.Error);

                using (
                    var scrollView = GUIScope.ScrollView(exceptionRect, _exceptionScrollViewStatus)
                )
                {
                    var textHeight = Text.CalcHeight(
                        exceptionText,
                        scrollView.ViewRect.width - (2 * Margin)
                    );

                    var textRect = exceptionRect.AtZero();
                    //textRect.yMin += Margin;
                    textRect.width = scrollView.ViewRect.width;
                    textRect.height = Mathf.Max(textHeight + (4 * Margin), exceptionRect.height);
                    scrollView.Height = textRect.height;

                    IlyvionWidgets.Label(
                        textRect.TrimLeft(Margin).TrimRight(Margin),
                        exceptionText,
                        TextAnchor.MiddleLeft,
                        color: ColorLibrary.LogError
                    );
                    contentCanvas.yMin += ExceptionBoxHeight + (3 * Margin);
                }

                using var _t = GUIScope.Font(GameFont.Tiny);
                const float ButtonWidth = 130f;
                Rect buttonRect = new(
                    contentCanvas.xMax - ButtonWidth - Margin,
                    Margin,
                    ButtonWidth,
                    Text.LineHeight
                );
                if (Widgets.ButtonText(buttonRect, "Copy to clipboard"))
                {
                    GUIUtility.systemCopyBuffer = exceptionText;
                    Messages.Message(
                        "Exception copied to clipboard.",
                        MessageTypeDefOf.NeutralEvent,
                        historical: false
                    );
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

    /// <summary>
    /// Gets the collection of manager jobs associated with this tab.
    /// </summary>
    protected virtual IEnumerable<ManagerJob> ManagerJobs =>
        Manager.JobTracker.JobsOfType<ManagerJob>();

    /// <summary>
    /// Draws the main content area of the manager tab.
    /// </summary>
    /// <param name="rect">The rectangle area in which to draw the main content.</param>
    protected virtual void DoMainContent(Rect rect) { }

#pragma warning disable CA1062 // Validate arguments of public methods

    /// <summary>
    /// Draws a local list entry for a manager job in the job list UI.
    /// </summary>
    /// <param name="job">The manager job to draw.</param>
    /// <param name="position">The position vector, passed by reference and updated after drawing.</param>
    /// <param name="width">The width of the entry.</param>
    /// <param name="parameters">Optional parameters for drawing the entry.</param>
    public virtual void DrawLocalListEntry(
        ManagerJob job,
        ref Vector2 position,
        float width,
        DrawLocalListEntryParameters? parameters = null
    )
    {
        parameters ??= new();

        var tab = job.Tab;

        var labelWidth =
            width - (4 * Margin) - StampSize - LargeListEntryHeight - LastUpdateRectWidth;

        // create label string
        var subLabel = tab.GetSubLabel(job);
        var (label, labelSize) = tab.GetFullLabel(job, labelWidth, subLabel);

        Rect labelRect = new(Margin, Margin, labelWidth, labelSize.y);

        Rect statusRect = new(
            0,
            labelRect.yMax + Margin,
            width - (parameters.ShowOrdering ? LargeListEntryHeight : Margin),
            parameters.StatusHeight
        );

        Rect rowRect = new(
            position.x,
            position.y,
            width,
            Mathf.Max(labelRect.yMax, statusRect.yMax) + Margin
        );

        Rect lastUpdateRect = new(
            labelRect.xMax + Margin,
            labelRect.y,
            LastUpdateRectWidth,
            labelRect.height
        );

        Rect stampRegionRect = new(
            lastUpdateRect.xMax + Margin,
            labelRect.y,
            StampSize,
            labelRect.height
        );

        Rect progressRect = new(Margin, statusRect.y, statusRect.width - Margin, statusRect.height);

        Rect orderRect = new(
            stampRegionRect.xMax + Margin,
            stampRegionRect.y,
            LargeListEntryHeight,
            progressRect.yMax - Margin
        );

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
            job.Trigger.DrawHorizontalProgressBars(
                progressRect,
                !job.IsSuspended && !job.IsCompleted
            );
        }

        UpdateInterval.Draw(lastUpdateRect, job, false, job.IsSuspended);

        if (parameters.ShowOrdering && DrawOrderButtons(orderRect, job, Manager.JobTracker))
        {
            Refresh();
        }

        GUI.EndGroup();
        position.y += rowRect.height;
    }

    /// <summary>
    /// Gets the full label and its size for a manager job, optionally including a sublabel.
    /// </summary>
    /// <param name="job">The manager job for which to get the label.</param>
    /// <param name="labelWidth">The maximum width for the label.</param>
    /// <param name="subLabel">An optional sublabel to include below the main label.</param>
    /// <param name="drawSubLabel">Whether to include the sublabel in the returned label.</param>
    /// <returns>A tuple containing the label string and its calculated size.</returns>
    public virtual (string label, Vector2 labelSize) GetFullLabel(
        ManagerJob job,
        float labelWidth,
        string? subLabel = null,
        bool drawSubLabel = true
    )
    {
        if (drawSubLabel)
        {
            subLabel ??= GetSubLabel(job);
            if (!subLabel.Fits(labelWidth, out var _))
            {
                subLabel = TruncateCached(subLabel, labelWidth);
            }
        }
        var mainLabel = GetMainLabel(job);
        if (!mainLabel.Fits(labelWidth, out var _))
        {
            mainLabel = TruncateCached(mainLabel, labelWidth);
        }
        var label = mainLabel + (drawSubLabel ? "\n" + subLabel : "");
        return (label, Text.CalcSize(label));
    }

    // Truncate() trims one character at a time re-measuring text width on every step, which is
    // very expensive for long, untruncated strings (e.g. job sub-labels listing hundreds of
    // targets). Cache results per (text, width) pair so repeated draws of the same job/window
    // size don't repeat that work every frame.
    private readonly Dictionary<float, Dictionary<string, string>> _truncateCache = [];

    private string TruncateCached(string text, float width)
    {
        if (!_truncateCache.TryGetValue(width, out var cache))
        {
            _truncateCache[width] = cache = [];
        }
        else if (cache.Count >= 200)
        {
            cache.Clear();
        }
        return text.Truncate(width, cache);
    }

    /// <summary>
    /// Gets the main label for the specified manager job.
    /// </summary>
    /// <param name="job">The manager job for which to get the main label.</param>
    /// <returns>The main label string.</returns>
    public virtual string GetMainLabel(ManagerJob job) => Label;

    /// <summary>
    /// Gets the sublabel for the specified manager job, typically a comma-separated list of targets or a default string if none exist.
    /// </summary>
    /// <param name="job">The manager job for which to get the sublabel.</param>
    /// <returns>A string representing the sublabel for the job.</returns>
    public virtual string GetSubLabel(ManagerJob job) => job.TargetsLabel;

    /// <summary>
    /// Draws the overview details for the specified manager job in the given rectangle.
    /// </summary>
    /// <param name="job">The manager job for which to draw the overview details.</param>
    /// <param name="rect">The rectangle area in which to draw the overview details.</param>
    /// <returns>True if the overview details were drawn; otherwise, false.</returns>
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
            bgRect.yMin += (rect.height / 2) - 50f;
            bgRect.yMax -= (rect.height / 2) - 50f;
            bgRect = bgRect.ContractedBy(10f);
            Widgets.DrawRectFast(bgRect, Color.black.ToTransparent(.8f));
            IlyvionWidgets.Label(
                new(rect) { height = rect.height - 15f },
                "ColonyManagerRedux.History.JobSuspended".Translate(),
                TextAnchor.MiddleCenter,
                GameFont.Medium
            );
            IlyvionWidgets.Label(
                new(rect) { y = rect.y + 20, height = rect.height - 15f },
                "("
                    + "ColonyManagerRedux.Job.ClickToChangeJob".Translate(
                        "ColonyManagerRedux.Job.Unsuspend".Translate()
                    )
                    + ")",
                TextAnchor.MiddleCenter,
                GameFont.Small
            );

            if (Widgets.ButtonInvisible(rect, false))
            {
                job.IsSuspended = false;
            }
        }

        return true;
    }
#pragma warning restore CA1062 // Validate arguments of public methods

    internal void PostMakeInt()
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
        PostMake();
    }

    /// <summary>
    /// Called after the manager tab is created; can be overridden to perform additional initialization.
    /// </summary>
    public virtual void PostMake() { }

    /// <summary>
    /// Creates a new manager job for this tab using the specified arguments.
    /// </summary>
    /// <param name="args">Arguments to pass to the job constructor.</param>
    /// <returns>A new instance of <see cref="ManagerJob"/> or null if creation fails.</returns>
    public ManagerJob? MakeNewJob(params object[] args) =>
        ManagerDefMaker.MakeManagerJob(Def, Manager, args);

    /// <summary>
    /// Called before the manager tab is closed.
    /// </summary>
    public virtual void PreClose() { }

    /// <summary>
    /// Called after the manager tab is closed.
    /// </summary>
    public virtual void PostClose() { }

    /// <summary>
    /// Called before the manager tab is opened.
    /// </summary>
    public virtual void PreOpen() { }

    /// <summary>
    /// Called after the manager tab is opened.
    /// </summary>
    public virtual void PostOpen() { }

    /// <summary>
    /// Called every tick to update the manager tab.
    /// </summary>
    public virtual void Tick() { }

    /// <summary>
    /// Called before a new job is selected in this manager tab.
    /// </summary>
    protected virtual void PreSelect() { }

    /// <summary>
    /// Called after a new job is selected in this manager tab.
    /// </summary>
    protected virtual void PostSelect() { }

    /// <summary>
    /// Draws a shortcut toggle UI element that allows selecting or deselecting all options in a list.
    /// </summary>
    /// <typeparam name="T">The type of the options.</typeparam>
    /// <param name="options">The list of all available options.</param>
    /// <param name="selected">The set of currently selected options.</param>
    /// <param name="setAllowed">The action called to set whether an option is allowed (selected).</param>
    /// <param name="rect">The rectangle area in which to draw the toggle.</param>
    /// <param name="labelKey">The translation key for the toggle label.</param>
    /// <param name="toolTipKey">The translation key for the tooltip, or null if none.</param>
    protected static void DrawShortcutToggle<T>(
        List<T> options,
        HashSet<T> selected,
        Action<T, bool> setAllowed,
        Rect rect,
        string labelKey,
        string? toolTipKey
    )
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
            () => options.ForEach(p => setAllowed(p, false))
        );
    }

    private readonly ScrollViewStatus _jobListScrollViewStatus = new();

    // Row heights are variable (depend on each job's comps), so a row's height can only be
    // measured by actually drawing it. Cache the last measured height per job so that, on
    // later frames, off-screen rows can be culled without drawing them; rows we haven't
    // measured yet are never culled, so their real height gets established on first draw.
    private readonly Dictionary<int, float> _jobRowHeights = [];

    /// <summary>
    /// Draws the job list UI for the manager tab.
    /// </summary>
    /// <param name="rect">The rectangle area in which to draw the job list.</param>
    protected virtual void DoJobList(Rect rect)
    {
        Widgets.DrawMenuSection(rect);

        using var scrollView = GUIScope.ScrollView(rect, _jobListScrollViewStatus);
        using var _g = GUIScope.WidgetGroup(scrollView.ViewRect);

        var cur = Vector2.zero;
        var i = 0;

        foreach (var job in ManagerJobs)
        {
            var estimatedHeight = _jobRowHeights.TryGetValue(job.LoadID, out var cachedHeight)
                ? cachedHeight
                : float.MaxValue;
            var row = new Rect(0f, cur.y, scrollView.ViewRect.width, estimatedHeight);

            if (!scrollView.CanCull(row.height, cur.y))
            {
                DrawLocalListEntry(job, ref cur, scrollView.ViewRect.width, null);

                row.height = cur.y - row.y;
                _jobRowHeights[job.LoadID] = row.height;

                Widgets.DrawHighlightIfMouseover(row);
                if (Selected == job)
                {
                    Widgets.DrawHighlightSelected(row);
                }

                if (i % 2 == 1)
                {
                    Widgets.DrawAltRect(row);
                }

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
            else
            {
                cur.y += estimatedHeight;
            }

            i++;
        }

        scrollView.Height = cur.y;
    }

    /// <summary>
    /// Draws the "New Job" button in the manager tab UI.
    /// </summary>
    /// <param name="rect">The rectangle area in which to draw the button.</param>
    protected virtual void DrawNewJobButton(Rect rect)
    {
        if (Widgets.ButtonText(rect, "ColonyManagerRedux.Job.New".Translate().Resolve()))
        {
            Selected = MakeNewJob();
        }
    }

    /// <summary>
    /// Called to refresh the manager tab UI or data.
    /// </summary>
    protected virtual void Refresh() { }

    internal virtual (bool top, bool bottom) GetJobOrderBounds(
        ManagerJob job,
        JobTracker jobTracker
    )
    {
        var top = job.Priority == 0;
        var bottom = job.Priority == jobTracker.MaxPriority;

        return (top, bottom);
    }

    internal virtual void TopPriority(JobTracker jobTracker, ManagerJob job) =>
        jobTracker.TopPriority(job);

    internal virtual void IncreasePriority(JobTracker jobTracker, ManagerJob job) =>
        jobTracker.IncreasePriority(job);

    internal virtual void DecreasePriority(JobTracker jobTracker, ManagerJob job) =>
        jobTracker.DecreasePriority(job);

    internal virtual void BottomPriority(JobTracker jobTracker, ManagerJob job) =>
        jobTracker.BottomPriority(job);

    /// <summary>
    ///     Draw a square group of ordering buttons for a job in rect.
    /// </summary>
    public bool DrawOrderButtons(Rect rect, ManagerJob job, JobTracker jobTracker)
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
            IlyvionWidgets.Label(
                rect,
                job.Priority.ToString(CultureInfo.InvariantCulture),
                TextAnchor.MiddleCenter
            );
        });

        reOrdered |= Utilities.DrawReorderButton(
            topRect,
            Resources.ArrowTop,
            "ColonyManagerRedux.Job.TopPriority".Translate(),
            top,
            () => TopPriority(jobTracker, job)
        );
        reOrdered |= Utilities.DrawReorderButton(
            upRect,
            Resources.ArrowUp,
            "ColonyManagerRedux.Job.IncreasePriority".Translate(),
            top,
            () => IncreasePriority(jobTracker, job)
        );
        reOrdered |= Utilities.DrawReorderButton(
            downRect,
            Resources.ArrowDown,
            "ColonyManagerRedux.Job.DecreasePriority".Translate(),
            bottom,
            () => DecreasePriority(jobTracker, job)
        );
        reOrdered |= Utilities.DrawReorderButton(
            bottomRect,
            Resources.ArrowBottom,
            "ColonyManagerRedux.Job.BottomPriority".Translate(),
            bottom,
            () => BottomPriority(jobTracker, job)
        );

        return reOrdered;
    }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    protected internal virtual void Notify_PawnsChanged()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    { }
}

/// <summary>
/// Parameters for drawing a local list entry in the manager tab UI.
/// </summary>
public class DrawLocalListEntryParameters
{
    /// <summary>
    /// Gets or sets a value indicating whether ordering controls should be shown in the local list entry.
    /// </summary>
    public bool ShowOrdering { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the progress bar should be shown in the local list entry.
    /// </summary>
    public bool ShowProgressbar { get; set; } = true;

    /// <summary>
    /// Gets or sets the height of the status area in the local list entry.
    /// </summary>
    public float StatusHeight { get; set; } = SmallIconSize;
}
