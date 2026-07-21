// Trigger.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024–2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Base class for triggers that determine whether a manager job should be active based on custom conditions.
/// </summary>
[HotSwappable]
public abstract class Trigger(ManagerJob job) : IExposable
{
    private ManagerJob _job = job;

    /// <summary>
    /// Gets or sets the manager job associated with this trigger.
    /// </summary>
    public ManagerJob Job
    {
        get => _job;
        protected internal set => _job = value;
    }

    /// <summary>
    /// Whether the trigger thinks the job should be active.
    /// </summary>
    public abstract bool State { get; }

    /// <summary>
    /// Gets the tooltip describing the current status of the trigger.
    /// </summary>
    public virtual string StatusTooltip { get; } = string.Empty;

    /// <summary>
    /// Exposes data for saving and loading.
    /// </summary>
    public virtual void ExposeData()
    {
        if (_job.Manager.ScribeSameMapData)
        {
            Scribe_References.Look(ref _job, "job");
        }
    }

    /// <summary>
    /// Draws vertical progress bars for the trigger's progress.
    /// </summary>
    /// <param name="progressRect">The rectangle in which to draw.</param>
    /// <param name="active">Whether the trigger is active.</param>
    public virtual void DrawVerticalProgressBars(Rect progressRect, bool active) { }

    /// <summary>
    /// Draws a vertical progress bar for the trigger's progress.
    /// </summary>
    /// <param name="progressRect">The rectangle in which to draw.</param>
    /// <param name="currentValue">The current value to display.</param>
    /// <param name="maxValue">The maximum value for the bar.</param>
    /// <param name="tooltip">Tooltip to display for the bar.</param>
    /// <param name="active">Whether the trigger is active.</param>
    /// <param name="progressBarTexture">The texture to use for the progress bar.</param>
    /// <param name="expectedValue">
    /// Additional value expected to be added on top of <paramref name="currentValue"/> once
    /// pending designations complete, drawn as a dimmer segment stacked above the current value.
    /// </param>
    protected static void DrawVerticalProgressBar(
        Rect progressRect,
        float currentValue,
        float maxValue,
        string tooltip,
        bool active,
        Texture2D progressBarTexture,
        float expectedValue = 0f
    )
    {
        var (_, _, markPosition, barSpan, expectedBarSpan) = ComputeProgressBarMetrics(
            currentValue,
            expectedValue,
            maxValue,
            progressRect.ContractedBy(2f).height
        );

        // draw a box for the bar
        GUI.color = Color.gray;
        Widgets.DrawBox(progressRect.ContractedBy(1f));
        GUI.color = Color.white;

        // get the bar rect
        var barRect = progressRect.ContractedBy(2f);

        // draw the expected segment first, underneath the current-value bar, so it shows
        // as a dimmer cap above the solid current-value fill.
        if (expectedValue > 0f)
        {
            var expectedRect = barRect;
            expectedRect.yMin += expectedRect.height - expectedBarSpan;
            GUI.DrawTexture(expectedRect, Resources.BarBackgroundExpectedTexture);
        }

        var markHeight = barRect.yMin + (barRect.height - markPosition);
        barRect.yMin += barRect.height - barSpan;

        // draw the bar
        // if the job is active and pending, make the bar blueish green - otherwise white.
        var barTex = active ? progressBarTexture : Resources.BarBackgroundInactiveTexture;
        GUI.DrawTexture(barRect, barTex);

        // draw a mark at the treshold
        Widgets.DrawLineHorizontal(progressRect.xMin, markHeight, progressRect.width);

        TooltipHandler.TipRegion(progressRect, tooltip);
    }

    /// <summary>
    /// Draws horizontal progress bars for the trigger's progress.
    /// </summary>
    /// <param name="progressRect">The rectangle in which to draw.</param>
    /// <param name="active">Whether the trigger is active.</param>
    public virtual void DrawHorizontalProgressBars(Rect progressRect, bool active) { }

    /// <summary>
    /// Draws a horizontal progress bar for the trigger's progress.
    /// </summary>
    /// <param name="progressRect">The rectangle in which to draw.</param>
    /// <param name="currentValue">The current value to display.</param>
    /// <param name="maxValue">The maximum value for the bar.</param>
    /// <param name="tooltip">Tooltip to display for the bar.</param>
    /// <param name="active">Whether the trigger is active.</param>
    /// <param name="progressBarTexture">The texture to use for the progress bar.</param>
    /// <param name="expectedValue">
    /// Additional value expected to be added on top of <paramref name="currentValue"/> once
    /// pending designations complete, drawn as a dimmer segment stacked past the current value.
    /// </param>
    protected static void DrawHorizontalProgressBar(
        Rect progressRect,
        float currentValue,
        float maxValue,
        string tooltip,
        bool active,
        Texture2D progressBarTexture,
        float expectedValue = 0f
    )
    {
        var (_, _, markPosition, barSpan, expectedBarSpan) = ComputeProgressBarMetrics(
            currentValue,
            expectedValue,
            maxValue,
            progressRect.ContractedBy(2f).width
        );

        // draw a box for the bar
        GUI.color = Color.gray;
        Widgets.DrawBox(progressRect.ContractedBy(1f));
        GUI.color = Color.white;

        // get the bar rect
        var barRect = progressRect.ContractedBy(2f);
        var markWidth = barRect.xMin + markPosition;

        // draw the expected segment first, underneath the current-value bar, so it shows
        // as a dimmer cap past the solid current-value fill.
        if (expectedValue > 0f)
        {
            var expectedRect = barRect;
            expectedRect.width = expectedBarSpan;
            GUI.DrawTexture(expectedRect, Resources.BarBackgroundExpectedTexture);
        }

        barRect.width = barSpan;

        // draw the bar
        // if the job is active and pending, make the bar blueish green - otherwise white.
        var barTex = active ? progressBarTexture : Resources.BarBackgroundInactiveTexture;
        GUI.DrawTexture(barRect, barTex);

        // draw a mark at the treshold
        Widgets.DrawLineVertical(markWidth, progressRect.yMin, progressRect.height);

        TooltipHandler.TipRegion(progressRect, tooltip);
    }

    /// <summary>
    /// Computes the numeric metrics (logical max, per-unit scale, target-mark position, and
    /// filled-bar span) shared by both the vertical and horizontal progress bar drawing methods.
    /// </summary>
    /// <param name="currentValue">The current value to display.</param>
    /// <param name="maxValue">The maximum value for the bar.</param>
    /// <param name="barLength">The length (height or width) of the bar's drawable area.</param>
    internal static (
        float max,
        float unit,
        float markPosition,
        float barSpan
    ) ComputeProgressBarMetrics(float currentValue, float maxValue, float barLength)
    {
        var (max, unit, markPosition, barSpan, _) = ComputeProgressBarMetrics(
            currentValue,
            0f,
            maxValue,
            barLength
        );
        return (max, unit, markPosition, barSpan);
    }

    /// <summary>
    /// Computes the numeric metrics (logical max, per-unit scale, target-mark position,
    /// filled-bar span, and expected-bar span) shared by both the vertical and horizontal
    /// progress bar drawing methods.
    /// </summary>
    /// <param name="currentValue">The current value to display.</param>
    /// <param name="expectedValue">
    /// Additional value expected on top of <paramref name="currentValue"/> once pending
    /// designations complete.
    /// </param>
    /// <param name="maxValue">The maximum value for the bar.</param>
    /// <param name="barLength">The length (height or width) of the bar's drawable area.</param>
    internal static (
        float max,
        float unit,
        float markPosition,
        float barSpan,
        float expectedBarSpan
    ) ComputeProgressBarMetrics(
        float currentValue,
        float expectedValue,
        float maxValue,
        float barLength
    )
    {
        var totalValue = currentValue + expectedValue;

        // bar always goes a little beyond the actual target
        var max = Math.Max(Math.Max((int)(maxValue * 1.2f), maxValue + 1), totalValue);
        var unit = barLength / max;
        var markPosition = maxValue * unit;
        var barSpan = currentValue * unit;
        var expectedBarSpan = totalValue * unit;
        return (max, unit, markPosition, barSpan, expectedBarSpan);
    }

    /// <summary>
    /// Draws the configuration UI for this trigger.
    /// </summary>
    /// <param name="cur">The current position for drawing.</param>
    /// <param name="width">The width of the config area.</param>
    /// <param name="entryHeight">The height of each entry.</param>
    /// <param name="label">Optional label for the config.</param>
    /// <param name="tooltip">Optional tooltip for the config.</param>
    /// <param name="targets">Optional list of designations to display.</param>
    /// <param name="onOpenFilterDetails">Optional action to invoke when filter details are opened.</param>
    /// <param name="designationLabelGetter">Optional function to get a label for a designation.</param>
    public abstract void DrawTriggerConfig(
        ref Vector2 cur,
        float width,
        float entryHeight,
        string? label = null,
        string? tooltip = null,
        List<Designation>? targets = null,
        Action? onOpenFilterDetails = null,
        Func<Designation, string>? designationLabelGetter = null
    );
}
