// Trigger.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HotSwappable]
public abstract class Trigger(ManagerJob job) : IExposable
{
    private ManagerJob _job = job;
    public ManagerJob Job { get => _job; protected internal set => _job = value; }

    public abstract bool State { get; }
    public virtual string StatusTooltip { get; } = string.Empty;

    public virtual void ExposeData()
    {
        if (_job.Manager.ScribeGameSpecificData)
        {
            Scribe_References.Look(ref _job, "job");
        }
    }

    public virtual void DrawVerticalProgressBars(Rect progressRect, bool active)
    {
    }

    protected static void DrawVerticalProgressBar(
        Rect progressRect,
        float currentValue,
        float maxValue,
        string tooltip,
        bool active,
        Texture2D progressBarTexture)
    {
        // bar always goes a little beyond the actual target
        var max = Math.Max(Math.Max((int)(maxValue * 1.2f), maxValue + 1), currentValue);

        // draw a box for the bar
        GUI.color = Color.gray;
        Widgets.DrawBox(progressRect.ContractedBy(1f));
        GUI.color = Color.white;

        // get the bar rect
        var barRect = progressRect.ContractedBy(2f);
        var unit = barRect.height / max;
        var markHeight = barRect.yMin + (max - maxValue) * unit;
        barRect.yMin += (max - currentValue) * unit;

        // draw the bar
        // if the job is active and pending, make the bar blueish green - otherwise white.
        var barTex = active
            ? progressBarTexture
            : Resources.BarBackgroundInactiveTexture;
        GUI.DrawTexture(barRect, barTex);

        // draw a mark at the treshold
        Widgets.DrawLineHorizontal(progressRect.xMin, markHeight, progressRect.width);

        TooltipHandler.TipRegion(progressRect, tooltip);
    }

    public virtual void DrawHorizontalProgressBars(Rect progressRect, bool active)
    {
    }

    protected static void DrawHorizontalProgressBar(
        Rect progressRect,
        float currentValue,
        float maxValue,
        string tooltip,
        bool active,
        Texture2D progressBarTexture)
    {
        // bar always goes a little beyond the actual target
        var max = Math.Max(Math.Max((int)(maxValue * 1.2f), maxValue + 1), currentValue);

        // draw a box for the bar
        GUI.color = Color.gray;
        Widgets.DrawBox(progressRect.ContractedBy(1f));
        GUI.color = Color.white;

        // get the bar rect
        var barRect = progressRect.ContractedBy(2f);
        var unit = barRect.width / max;
        var markWidth = barRect.xMin + maxValue * unit;
        barRect.width = currentValue * unit;

        // draw the bar
        // if the job is active and pending, make the bar blueish green - otherwise white.
        var barTex = active
            ? progressBarTexture
            : Resources.BarBackgroundInactiveTexture;
        GUI.DrawTexture(barRect, barTex);

        // draw a mark at the treshold
        Widgets.DrawLineVertical(markWidth, progressRect.yMin, progressRect.height);

        TooltipHandler.TipRegion(progressRect, tooltip);
    }

    public abstract void DrawTriggerConfig(ref Vector2 cur, float width, float entryHeight,
        string? label = null, string? tooltip = null,
        List<Designation>? targets = null, Action? onOpenFilterDetails = null,
        Func<Designation, string>? designationLabelGetter = null);
}
