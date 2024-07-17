// ManagerTab.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

#pragma warning disable CS8618 // Set by ManagerTabMaker.MakeManagerTab
public abstract class ManagerTab(Manager manager)
#pragma warning restore CS8618
{
    public float DefaultLeftRowSize = 300f;


    public static float SuspendStampWidth = Constants.MediumIconSize,
                        LastUpdateRectWidth = 50f,
                        ProgressRectWidth = 10f,
                        StatusRectWidth = SuspendStampWidth + LastUpdateRectWidth + ProgressRectWidth;

    public ManagerTabDef def;

    public Manager manager = manager;
    private ManagerJob? selected;

    public virtual string DisabledReason => "";

    public virtual bool Enabled => true;

    public virtual string Label => GetType().ToString();

    public ManagerJob? Selected
    {
        get => selected;
        set
        {
            PreSelect();
            selected = value;
            PostSelect();
        }
    }

    public abstract void DoWindowContents(Rect canvas);

    /// <summary>
    /// Used by the
    /// </summary>
    public virtual void DrawListEntry(ManagerJob job, Rect rect, bool overview = true, bool active = true)
    {
    }

    public virtual void DrawOverviewDetails(ManagerJob job, Rect rect)
    {
        if (job is IHasHistory hasHistory)
        {
            hasHistory.History.DrawPlot(rect, hasHistory.Trigger.TargetCount);
        }
    }

    public virtual void PostMake()
    {
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

    protected void DrawShortcutToggle<T>(List<T> options, HashSet<T> selected, Action<T, bool> setAllowed, Rect rect, string labelKey, string? toolTipKey)
    {
        var allSelected = options.All(selected.Contains);
        var noneSelected = options.All(p => !selected.Contains(p));

        Utilities.DrawToggle(
            rect,
            $"ColonyManagerRedux.{labelKey}".Translate().Italic(),
            toolTipKey != null ? $"ColonyManagerRedux.{toolTipKey}".Translate() : string.Empty,
            allSelected,
            noneSelected,
            () => options.ForEach(p => setAllowed(p, true)),
            () => options.ForEach(p => setAllowed(p, false)));
    }

    /// <summary>
    ///     Draw a square group of ordering buttons for a job in rect.
    /// </summary>
    public static bool DrawOrderButtons<T>(Rect rect, Manager manager, T job) where T : ManagerJob
    {
        var ret = false;
        var jobStack = manager.JobStack;

        float width = rect.width / 2,
              height = rect.height / 2;

        Rect upRect = new Rect(rect.xMin, rect.yMin, width, height).ContractedBy(1f),
             downRect = new Rect(rect.xMin, rect.yMin + height, width, height).ContractedBy(1f),
             topRect = new Rect(rect.xMin + width, rect.yMin, width, height).ContractedBy(1f),
             bottomRect = new Rect(rect.xMin + width, rect.yMin + height, width, height).ContractedBy(1f);

        var jobsOfType = jobStack.FullStack<T>();

        bool top = jobsOfType.IndexOf(job) == 0,
             bottom = jobsOfType.IndexOf(job) == jobsOfType.Count - 1;

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
            TooltipHandler.TipRegion(step, "ColonyManagerRedux.ManagerOrderUp".Translate());
            TooltipHandler.TipRegion(max, "ColonyManagerRedux.ManagerOrderTop".Translate());
        }
        else
        {
            TooltipHandler.TipRegion(step, "ColonyManagerRedux.ManagerOrderDown".Translate());
            TooltipHandler.TipRegion(max, "ColonyManagerRedux.ManagerOrderBottom".Translate());
        }
    }
}
