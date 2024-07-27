// ManagerTab.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

#pragma warning disable CS8618 // Set by ManagerDefMaker.MakeManagerTab
[HotSwappable]
public abstract class ManagerTab(Manager manager)
#pragma warning restore CS8618
{
    public const float DefaultLeftRowSize = 300f;


    public const float
        StampWidth = MediumIconSize,
        LastUpdateRectWidth = 50f,
        ProgressRectWidth = 60f,
        StatusRectWidth = StampWidth + LastUpdateRectWidth + ProgressRectWidth + 2 * Margin;

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

    public abstract void DoWindowContents(Rect canvas);

    public enum ListEntryDrawMode
    {
        Local,
        Overview,
        Export
    }

#pragma warning disable CA1062 // Validate arguments of public methods
    public virtual void DrawListEntry(ManagerJob job, Rect rect, ListEntryDrawMode mode, bool active = true)
    {
        // set up rects
        var labelRect = new Rect(
            Margin,
            Margin,
            rect.width - (active ? StatusRectWidth + 4 * Margin : 2 * Margin),
            rect.height - 2 * Margin);
        var statusRect = new Rect(labelRect.xMax + Margin, Margin, StatusRectWidth, rect.height - 2 * Margin);

        // create label string
        var subtext = GetSubLabel(job);
        var text = GetMainLabel(job, labelRect, subtext);

        // do the drawing
        GUI.BeginGroup(rect);

        // draw label
        Widgets_Labels.Label(labelRect, text, subtext, TextAnchor.MiddleLeft);

        // if the bill has a manager job, give some more info.
        if (active && job.Trigger != null)
        {
            job.DrawStatusForListEntry(statusRect, job.Trigger, mode == ListEntryDrawMode.Export);
        }

        GUI.EndGroup();
    }

    public virtual string GetMainLabel(ManagerJob job, Rect labelRect, string subLabel)
    {
        var text = Label + "\n";
        if (subLabel.Fits(labelRect))
        {
            text += subLabel.Italic();
        }
        else
        {
            text += subLabel.Truncate(labelRect.width).Italic();
        }
        return text;
    }

    public virtual string GetSubLabel(ManagerJob job)
    {
        return string.Join(", ", job.Targets);
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

    /// <summary>
    ///     Draw a square group of ordering buttons for a job in rect.
    /// </summary>
    public static bool DrawOrderButtons<T>(Rect rect, Manager manager, T job) where T : ManagerJob
    {
        if (manager == null)
        {
            throw new ArgumentNullException(nameof(manager));
        }
        if (job == null)
        {
            throw new ArgumentNullException(nameof(job));
        }

        var ret = false;
        var jobStack = manager.JobTracker;

        float width = rect.width / 2,
              height = rect.height / 2;

        Rect upRect = new Rect(rect.xMin, rect.yMin, width, height).ContractedBy(1f),
             downRect = new Rect(rect.xMin, rect.yMin + height, width, height).ContractedBy(1f),
             topRect = new Rect(rect.xMin + width, rect.yMin, width, height).ContractedBy(1f),
             bottomRect = new Rect(rect.xMin + width, rect.yMin + height, width, height).ContractedBy(1f);

        var jobsOfType = jobStack.JobsOfType<T>().ToList();

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
