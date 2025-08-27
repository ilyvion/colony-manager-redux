// History.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.UI;

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

/// <summary>
/// Stores and manages historical data for manager jobs, including plotting and update logic.
/// </summary>
[HotSwappable]
public partial class History : IExposable
{
    /// <summary>
    /// The default color used for history plot lines.
    /// </summary>
    public static readonly Color DefaultLineColor = Color.white;
    /// <summary>
    /// Gets an array of all possible <see cref="Period"/> values.
    /// </summary>
    public static readonly Period[] Periods = (Period[])Enum.GetValues(typeof(Period));

    internal const int EntriesPerInterval = 100;

    // How often to record a value for a given period
    private const int IntervalPerDay = GenDate.TicksPerDay / EntriesPerInterval;
    private const int IntervalPerMonth = GenDate.TicksPerTwelfth / EntriesPerInterval;
    private const int IntervalPerYear = GenDate.TicksPerYear / EntriesPerInterval;

    internal readonly List<Chapter> _chaptersShown = [];

    // Settings for plot
    private bool _allowTogglingLegend = true;
    /// <summary>
    /// Gets or sets whether the legend can be toggled in the plot.
    /// </summary>
    public bool AllowTogglingLegend
    {
        get => _allowTogglingLegend; set => _allowTogglingLegend = value;
    }
    private bool _drawInlineLegend = true;
    /// <summary>
    /// Gets or sets whether to draw the legend inline with the plot.
    /// </summary>
    public bool DrawInlineLegend
    {
        get => _drawInlineLegend; set => _drawInlineLegend = value;
    }
    private bool _drawOptions = true;
    /// <summary>
    /// Gets or sets whether to draw options for the plot.
    /// </summary>
    public bool DrawOptions
    {
        get => _drawOptions; set => _drawOptions = value;
    }
    private bool _drawTargetLine = true;
    /// <summary>
    /// Gets or sets whether to draw the target line in the plot.
    /// </summary>
    public bool DrawTargetLine
    {
        get => _drawTargetLine; set => _drawTargetLine = value;
    }

    // Shared settings
    private Period _periodShown = Period.Day;
    /// <summary>
    /// Gets or sets the period currently shown in the plot.
    /// </summary>
    public Period PeriodShown
    {
        get => _periodShown; set => _periodShown = value;
    }
    private string _yAxisSuffix = string.Empty;
    /// <summary>
    /// Gets or sets the suffix for the Y axis label.
    /// </summary>
    public string YAxisSuffix
    {
        get => _yAxisSuffix; set => _yAxisSuffix = value;
    }

    // each chapter holds the history for all periods.
    internal List<Chapter> _chapters = [];

    // for scribe.
    /// <summary>
    /// Default constructor for scribing only.
    /// </summary>
    public History()
    {
    }

    internal History(List<ManagerJobHistoryChapterDef> chapters)
    {
        // create a chapter for each label
        for (var i = 0; i < chapters.Count; i++)
        {
            _chapters.Add(
                new Chapter(
                    new ManagerJobHistoryChapterDefLabel(chapters[i]),
                    EntriesPerInterval,
                    chapters[i].color)
                {
                    def = chapters[i]
                });
        }

        // show all by default
        _chaptersShown.AddRange(_chapters);
    }

    /// <summary>
    /// Creates a new history with the specified labels and optional colors.
    /// </summary>
    /// <param name="labels">The labels for each chapter.</param>
    /// <param name="colors">Optional colors for each chapter.</param>
    public History(HistoryLabel[] labels, Color[]? colors = null)
    {
        if (labels == null)
        {
            throw new ArgumentNullException(nameof(labels));
        }

#if DEBUG_HISTORY
        Log.Message( "History created" + string.Join( ", ", labels ) );
#endif
        // get range of colors if not set
        // default to white for single line
        colors ??= labels.Length == 1
            ? [DefaultLineColor]

            // rainbow!
            : HSV_Helper.Range(labels.Length);

        // create a chapter for each label
        for (var i = 0; i < labels.Length; i++)
        {
            _chapters.Add(new Chapter(labels[i], EntriesPerInterval, colors[i % colors.Length]));
        }

        // show all by default
        _chaptersShown.AddRange(_chapters);
    }

    /// <summary>
    /// Creates a new history with the specified thing counts and optional colors.
    /// </summary>
    /// <param name="thingCounts">The thing counts for each chapter.</param>
    /// <param name="colors">Optional colors for each chapter.</param>
    public History(ThingDefCount[] thingCounts, Color[]? colors = null)
    {
        if (thingCounts == null)
        {
            throw new ArgumentNullException(nameof(thingCounts));
        }

        // get range of colors if not set
        // default to white for single line
        colors ??= thingCounts.Length == 1
            ? [Color.white]

            // rainbow!
            : HSV_Helper.Range(thingCounts.Length);

        // create a chapter for each label
        for (var i = 0; i < thingCounts.Length; i++)
        {
            _chapters.Add(new Chapter(new ThingDefCountClass(thingCounts[i].ThingDef, thingCounts[i].Count),
                EntriesPerInterval,
                colors[i % colors.Length]));
        }

        // show all by default
        _chaptersShown.AddRange(_chapters);
    }

    /// <summary>
    /// Gets whether the current tick is an update tick for any period.
    /// </summary>
    public static bool IsUpdateTick
    {
        get
        {
            var ticksGame = Find.TickManager.TicksGame;
            return Periods.Any(p => ticksGame % PeriodTickInterval(p) == 0);
        }
    }

    /// <inheritdoc/>
    public void ExposeData()
    {
        // settings
        Scribe_Values.Look(ref _allowTogglingLegend, "allowToggingLegend", true);
        Scribe_Values.Look(ref _drawInlineLegend, "showLegend", true);
        Scribe_Values.Look(ref _drawTargetLine, "drawTargetLine", true);
        Scribe_Values.Look(ref _drawOptions, "drawOptions", true);
        Scribe_Values.Look(ref _periodShown, "periodShown", Period.Day);
        Scribe_Values.Look(ref _yAxisSuffix, "suffix", "");

        // history chapters
        Scribe_Collections.Look(ref _chapters, "chapters", LookMode.Deep);

        // some post load tweaks
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            // set chapters shown to the newly loaded chapters (instead of the default created empty chapters).
            _chaptersShown.Clear();
            _chaptersShown.AddRange(_chapters);
        }
    }

    /// <summary>
    /// Gets the tick interval for the specified period.
    /// </summary>
    /// <param name="period">The period to get the interval for.</param>
    /// <returns>The tick interval for the period.</returns>
    public static int PeriodTickInterval(Period period) => period switch
    {
        Period.Month => IntervalPerMonth,
        Period.Year => IntervalPerYear,
        Period.Day => IntervalPerDay,
        _ => throw new NotImplementedException(),
    };

    private GraphRenderer? graphRenderer;
    private readonly List<Chapter> _tmpChapters = [];
    /// <summary>
    /// Draws the plot for the history, including legend and options.
    /// </summary>
    /// <param name="rect">The rectangle in which to draw.</param>
    /// <param name="positiveOnly">Whether to show only positive chapters.</param>
    /// <param name="negativeOnly">Whether to show only negative chapters.</param>
    public void DrawPlot(in Rect rect, bool positiveOnly = false, bool negativeOnly = false)
    {
        var recordHistoricalData = ColonyManagerReduxMod.Settings.RecordHistoricalData;

        var sign = negativeOnly ? -1 : 1;

        graphRenderer ??= new([.. _chapters.Select(c =>
        {
            c.GraphSeries ??= new GraphSeries()
            {
                Color = c.LineColor,
                Label = c.label.Label,
                UnitLabel = c.ChapterSuffix ?? "",
            };
            return c.GraphSeries;
        })])
        {
            LegendLabel = "ColonyManagerRedux.History.Legend".Translate(),
            NoDataLabel = "ColonyManagerRedux.History.NoChapters".Translate(),
        };

        graphRenderer.DrawInlineLegend = DrawInlineLegend;
        graphRenderer.DrawTargetLine = DrawTargetLine;
        graphRenderer.Interactive = recordHistoricalData;
        graphRenderer.MaxEntries = EntriesPerInterval;
        graphRenderer.YAxisUnitLabel = YAxisSuffix;

        foreach (var chapter in _chapters)
        {
            chapter.GraphSeries!.Hidden = true;
        }

        // subset chapters
        _tmpChapters.AddRange(
            _chaptersShown.Where(chapter =>
                !positiveOnly || chapter.counts[(int)PeriodShown].Any(i => i > 0))
                .Where(chapter => !negativeOnly || chapter.counts[(int)PeriodShown].Any(i => i < 0))
                .OrderBy(_chapters.IndexOf));
        using var _ = new DoOnDispose(_tmpChapters.Clear);

        foreach (var chapter in _tmpChapters)
        {
            chapter.GraphSeries!.Hidden = false;
        }

        graphRenderer.DrawGraph(
            rect,
            [.. _tmpChapters.Select(c => c.ValuesFor(PeriodShown, sign))],
            [.. _tmpChapters.Select(c => c.TargetsFor(PeriodShown, sign))]);

        // period / variables picker
        if (DrawOptions)
        {
            var switchRect = new Rect(rect.xMax - SmallIconSize - Margin,
                rect.yMin + Margin, SmallIconSize,
                SmallIconSize);
            if (recordHistoricalData)
            {
                Widgets.DrawHighlightIfMouseover(switchRect);
                if (Widgets.ButtonImage(switchRect, Resources.Cog))
                {
                    var options = Periods.Select(p =>
                        new FloatMenuOption("ColonyManagerRedux.History.Period".Translate() +
                            ": " + $"ColonyManagerRedux.History.PeriodShown.{p}"
                                .Translate().CapitalizeFirst(),
                            () => PeriodShown = p)).ToList();
                    // add option to show/hide legend if appropriate.
                    if (AllowTogglingLegend && _chapters.Count > 1)
                    {
                        options.Add(new FloatMenuOption(
                            "ColonyManagerRedux.History.ShowHideLegend".Translate(),
                            delegate
                            {
                                DrawInlineLegend = !DrawInlineLegend;
                            }));
                    }

                    Find.WindowStack.Add(new FloatMenu(options));
                }
            }
            else
            {
                GUI.DrawTexture(switchRect, Resources.Cog);
            }

            using var _f = GUIScope.Font(GameFont.Tiny);
            var periodShown =
                "ColonyManagerRedux.History.Period".Translate() + ": " +
                $"ColonyManagerRedux.History.PeriodShown.{PeriodShown}".Translate();
            var labelSize = Text.CalcSize(periodShown);

            var labelRect = switchRect;
            labelRect.width = labelSize.x;
            labelRect.height = labelSize.y;
            labelRect.x -= Margin + labelRect.width;
            GUI.color = Color.white;
            Widgets.Label(labelRect, periodShown);

            IlyvionDebugViewSettings.DrawIfUIHelpers(() =>
            {
                Widgets.DrawRectFast(switchRect, ColorLibrary.Aquamarine.ToTransparent(.5f));
                Widgets.DrawRectFast(labelRect, ColorLibrary.Khaki.ToTransparent(.5f));
            });
        }

        if (!recordHistoricalData)
        {
            Widgets.DrawRectFast(rect, Color.white.ToTransparent(.2f));
            var bgRect = new Rect(rect);
            bgRect.yMin += (rect.height / 2) - 50f;
            bgRect.yMax -= (rect.height / 2) - 50f;
            bgRect = bgRect.ContractedBy(10f);
            Widgets.DrawRectFast(bgRect, Color.black.ToTransparent(.8f));
            IlyvionWidgets.Label(
                new(rect)
                {
                    height = rect.height - 15f
                },
                "ColonyManagerRedux.History.HistoryRecordingDisabled".Translate(),
                TextAnchor.MiddleCenter,
                GameFont.Medium);
            IlyvionWidgets.Label(
                new(rect)
                {
                    y = rect.y + 20,
                    height = rect.height - 15f
                },
                "(" + "ColonyManagerRedux.History.ClickToEnableHistoryRecording".Translate() + ")",
                TextAnchor.MiddleCenter,
                GameFont.Small);

            if (Widgets.ButtonInvisible(rect, false))
            {
                ColonyManagerReduxMod.Settings.RecordHistoricalData = true;
                ColonyManagerReduxMod.Settings.Write();
            }
        }
    }

    /// <summary>
    /// Updates the history with new counts and targets for each chapter.
    /// </summary>
    /// <param name="tick">The current tick.</param>
    /// <param name="counts">The counts and targets for each chapter.</param>
    public void Update(int tick, params (int count, int target)[] counts)
    {
        if (counts == null)
        {
            throw new ArgumentNullException(nameof(counts));
        }

        if (counts.Length != _chapters.Count)
        {
            ColonyManagerReduxMod.Instance
                .LogWarning($"History updated with incorrect number of chapters; got {counts.Length}, expected {_chapters.Count}");
        }

        for (var i = 0; i < counts.Length; i++)
        {
            _chapters[i].Add(counts[i].count, counts[i].target, tick);
        }
    }

    /// <summary>
    /// Updates the history with new counts and targets for each chapter.
    /// </summary>
    /// <param name="tick">The current tick.</param>
    /// <param name="counts">The counts for each chapter.</param>
    /// <param name="targets">The targets for each chapter.</param>
    public void Update(int tick, int[] counts, int[] targets)
    {
        if (counts == null)
        {
            throw new ArgumentNullException(nameof(counts));
        }
        if (targets == null)
        {
            throw new ArgumentNullException(nameof(targets));
        }

        if (counts.Length != _chapters.Count || targets.Length != _chapters.Count)
        {
            ColonyManagerReduxMod.Instance.LogWarning(
                $"History updated with incorrect number of chapters; got counts={counts.Length} and targets={targets.Length}, expected {_chapters.Count}");
        }

        for (var i = 0; i < counts.Length; i++)
        {
            _chapters[i].Add(counts[i], targets[i], tick);
        }
    }

    /// <summary>
    /// Updates the maximum values for each chapter.
    /// </summary>
    /// <param name="maxes">The maximum values for each chapter.</param>
    public void UpdateMax(params int[] maxes)
    {
        if (maxes == null)
        {
            throw new ArgumentNullException(nameof(maxes));
        }

        if (maxes.Length != _chapters.Count)
        {
            ColonyManagerReduxMod.Instance
                .LogWarning($"History maxes updated with incorrect number of chapters; got {maxes.Length}, expected {_chapters.Count}");
        }

        for (var i = 0; i < maxes.Length; i++)
        {
            _chapters[i].TrueMax = maxes[i];
        }
    }

    /// <summary>
    /// Updates the thing counts and maximums for each chapter.
    /// </summary>
    /// <param name="counts">The thing counts for each chapter.</param>
    /// <param name="maxes">The maximum values for each chapter.</param>
    public void UpdateThingCountAndMax(int[] counts, int[] maxes)
    {
        if (counts == null)
        {
            throw new ArgumentNullException(nameof(counts));
        }
        if (maxes == null)
        {
            throw new ArgumentNullException(nameof(maxes));
        }

        if (counts.Length != _chapters.Count || maxes.Length != _chapters.Count)
        {
            ColonyManagerReduxMod.Instance
                .LogWarning($"History updated with incorrect number of chapters; got {counts.Length}, expected {_chapters.Count}");
        }

        for (var i = 0; i < maxes.Length; i++)
        {
            if (_chapters[i].ThingDefCount.count != counts[i])
            {
                _chapters[i].TrueMax = maxes[i];
                _chapters[i].ThingDefCount.count = counts[i];
            }
        }
    }

    /// <summary>
    /// Updates the thing counts for each chapter.
    /// </summary>
    /// <param name="counts">The thing counts for each chapter.</param>
    public void UpdateThingCounts(params int[] counts)
    {
        if (counts == null)
        {
            throw new ArgumentNullException(nameof(counts));
        }

        if (counts.Length != _chapters.Count)
        {
            ColonyManagerReduxMod.Instance
                .LogWarning($"History updated with incorrect number of chapters; got {counts.Length}, expected {_chapters.Count}");
        }

        for (var i = 0; i < counts.Length; i++)
        {
            _chapters[i].ThingDefCount.count = counts[i];
        }
    }

    /// <summary>
    /// Updates the thing definitions and colors for each chapter.
    /// </summary>
    /// <param name="newTraderDefs">The new thing definitions.</param>
    /// <param name="colors">Optional colors for each chapter.</param>
    public void UpdateThingDefs(in List<ThingDef> newTraderDefs, Color[]? colors = null)
    {
        // So we don't modify a list passed to us
        List<ThingDef> traderDefs = [.. newTraderDefs];

        // get range of colors if not set
        // default to white for single line
        colors ??= traderDefs.Count == 1
            ? [Color.white]

            // rainbow!
            : HSV_Helper.Range(traderDefs.Count);

        for (var i = _chapters.Count - 1; i >= 0; i--)
        {
            var chapter = _chapters[i];
            if (!traderDefs.Remove(chapter.ThingDefCount.thingDef))
            {
                // Attempted to remove a def we don't actually have. This most likely means it's a
                // building that no longer exists. Let's remove it.
                ColonyManagerReduxMod.Instance.LogWarning("Removing thingDef chapter "
                    + chapter.ThingDefCount.thingDef + " because it no longer exists. "
                    + "(Most likely cause: a mod with a building consuming/producing power "
                    + "was removed.)");
                _chapters.RemoveAt(i);
            }
        }
        if (traderDefs.Count > 0)
        {
            ColonyManagerReduxMod.Instance.LogDebug("New defs: " + traderDefs.Join(t => t.defName));

            // create a chapter for each new def
            var currentChapterCount = _chapters.Count;
            var currentChapterCountCounts = _chapters.First().counts.Select(c => c.Size).ToArray();
            for (var i = 0; i < traderDefs.Count; i++)
            {
                Chapter chapter = new(new ThingDefCountClass(traderDefs[i], 0),
                    EntriesPerInterval,
                    colors[(currentChapterCount + i) % colors.Length]);
                for (var j = 0; j < currentChapterCountCounts.Length; j++)
                {
                    for (var k = 0; k < currentChapterCountCounts[j]; k++)
                    {
                        chapter.counts[j].PushBack(0);
                    }
                }

                _chapters.Add(chapter);
            }
        }
    }
}

/// <summary>
/// Represents the period for which history is tracked (day, month, year).
/// </summary>
public enum Period
{
    /// <summary>
    /// Daily period.
    /// </summary>
    Day = 0,
    /// <summary>
    /// Monthly period.
    /// </summary>
    Month = 1,
    /// <summary>
    /// Yearly period.
    /// </summary>
    Year = 2
}
