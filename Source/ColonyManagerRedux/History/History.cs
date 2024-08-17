// History.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[HotSwappable]
public partial class History : IExposable
{
    public static readonly Color DefaultLineColor = Color.white;
    public static readonly Period[] Periods = (Period[])Enum.GetValues(typeof(Period));

    internal const int EntriesPerInterval = 100;

    // How often to record a value for a given period
    private const int IntervalPerDay = GenDate.TicksPerDay / EntriesPerInterval;
    private const int IntervalPerMonth = GenDate.TicksPerTwelfth / EntriesPerInterval;
    private const int IntervalPerYear = GenDate.TicksPerYear / EntriesPerInterval;

    internal readonly List<Chapter> _chaptersShown = [];

    // Settings for plot
    private bool _allowTogglingLegend = true;
    public bool AllowTogglingLegend { get => _allowTogglingLegend; set => _allowTogglingLegend = value; }
    private bool _drawInlineLegend = true;
    public bool DrawInlineLegend { get => _drawInlineLegend; set => _drawInlineLegend = value; }
    private bool _drawOptions = true;
    public bool DrawOptions { get => _drawOptions; set => _drawOptions = value; }
    private bool _drawTargetLine = true;
    public bool DrawTargetLine { get => _drawTargetLine; set => _drawTargetLine = value; }

    // Shared settings
    private Period _periodShown = Period.Day;
    public Period PeriodShown { get => _periodShown; set => _periodShown = value; }
    private string _yAxisSuffix = string.Empty;
    public string YAxisSuffix { get => _yAxisSuffix; set => _yAxisSuffix = value; }

    // each chapter holds the history for all periods.
    internal List<Chapter> _chapters = [];

    // for scribe.
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
        if (colors == null)
        {
            // default to white for single line
            if (labels.Length == 1)
            {
                colors = [DefaultLineColor];
            }

            // rainbow!
            else
            {
                colors = HSV_Helper.Range(labels.Length);
            }
        }

        // create a chapter for each label
        for (var i = 0; i < labels.Length; i++)
        {
            _chapters.Add(new Chapter(labels[i], EntriesPerInterval, colors[i % colors.Length]));
        }

        // show all by default
        _chaptersShown.AddRange(_chapters);
    }

    public History(ThingDefCount[] thingCounts, Color[]? colors = null)
    {
        if (thingCounts == null)
        {
            throw new ArgumentNullException(nameof(thingCounts));
        }

        // get range of colors if not set
        if (colors == null)
        {
            // default to white for single line
            if (thingCounts.Length == 1)
            {
                colors = [Color.white];
            }

            // rainbow!
            else
            {
                colors = HSV_Helper.Range(thingCounts.Length);
            }
        }

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

    public static bool IsUpdateTick(int jitter)
    {
        var ticksGame = Find.TickManager.TicksGame;
        var jitterTick = ticksGame + jitter;
        if (jitterTick < 0)
        {
            return false;
        }
        return Periods.Any(p => jitterTick % PeriodTickInterval(p) == 0);
    }

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

    public static int PeriodTickInterval(Period period)
    {
        return period switch
        {
            Period.Month => IntervalPerMonth,
            Period.Year => IntervalPerYear,
            _ => IntervalPerDay,
        };
    }

    private GraphRenderer? graphRenderer;
    public void DrawPlot(in Rect rect, bool positiveOnly = false, bool negativeOnly = false)
    {
        bool recordHistoricalData = ColonyManagerReduxMod.Settings.RecordHistoricalData;

        var sign = negativeOnly ? -1 : 1;

        graphRenderer ??= new(_chapters.Select(c =>
        {
            c.GraphSeries ??= new GraphSeries()
            {
                Color = c.LineColor,
                Label = c.label.Label,
                UnitLabel = c.ChapterSuffix ?? "",
            };
            return c.GraphSeries;
        }).ToArray())
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
        var chapters =
            _chaptersShown.Where(chapter => !positiveOnly || chapter.counts[(int)PeriodShown].Any(i => i > 0))
                .Where(chapter => !negativeOnly || chapter.counts[(int)PeriodShown].Any(i => i < 0))
                .OrderBy(_chapters.IndexOf)
                .ToList();
        foreach (var chapter in chapters)
        {
            chapter.GraphSeries!.Hidden = false;
        }

        graphRenderer.DrawGraph(rect, chapters.Select(c => c.ValuesFor(PeriodShown, sign)).ToArray(), chapters.Select(c => c.TargetsFor(PeriodShown, sign)).ToArray());

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
                            delegate { PeriodShown = p; })).ToList();
                    if (AllowTogglingLegend && _chapters.Count > 1) // add option to show/hide legend if appropriate.
                    {
                        options.Add(new FloatMenuOption("ColonyManagerRedux.History.ShowHideLegend".Translate(),
                            delegate { DrawInlineLegend = !DrawInlineLegend; }));
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

            if (IlyvionDebugViewSettings.DrawUIHelpers)
            {
                Widgets.DrawRectFast(switchRect, ColorLibrary.Aquamarine.ToTransparent(.5f));
                Widgets.DrawRectFast(labelRect, ColorLibrary.Khaki.ToTransparent(.5f));
            }
        }

        if (!recordHistoricalData)
        {
            Widgets.DrawRectFast(rect, Color.white.ToTransparent(.2f));
            var bgRect = new Rect(rect);
            bgRect.yMin += rect.height / 2 - 50f;
            bgRect.yMax -= rect.height / 2 - 50f;
            bgRect = bgRect.ContractedBy(10f);
            Widgets.DrawRectFast(bgRect, Color.black.ToTransparent(.8f));
            IlyvionWidgets.Label(
                new(rect) { height = rect.height - 15f },
                "ColonyManagerRedux.History.HistoryRecordingDisabled".Translate(),
                TextAnchor.MiddleCenter,
                GameFont.Medium);
            IlyvionWidgets.Label(
                new(rect) { y = rect.y + 20, height = rect.height - 15f },
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

    public void Update(int tick, params (int count, int target)[] counts)
    {
        if (counts == null)
        {
            throw new ArgumentNullException(nameof(counts));
        }

        if (counts.Length != _chapters.Count)
        {
            ColonyManagerReduxMod.Instance.LogWarning("History updated with incorrect number of chapters");
        }

        for (var i = 0; i < counts.Length; i++)
        {
            _chapters[i].Add(counts[i].count, counts[i].target, tick);
        }
    }

    public void UpdateMax(params int[] maxes)
    {
        if (maxes == null)
        {
            throw new ArgumentNullException(nameof(maxes));
        }

        if (maxes.Length != _chapters.Count)
        {
            ColonyManagerReduxMod.Instance.LogWarning("History updated with incorrect number of chapters");
        }

        for (var i = 0; i < maxes.Length; i++)
        {
            _chapters[i].TrueMax = maxes[i];
        }
    }

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

    public void UpdateThingCounts(params int[] counts)
    {
        if (counts == null)
        {
            throw new ArgumentNullException(nameof(counts));
        }

        if (counts.Length != _chapters.Count)
        {
            ColonyManagerReduxMod.Instance
                .LogWarning("History updated with incorrect number of chapters");
        }

        for (var i = 0; i < counts.Length; i++)
        {
            _chapters[i].ThingDefCount.count = counts[i];
        }
    }

    public void UpdateThingDefs(in List<ThingDef> newTraderDefs, Color[]? colors = null)
    {
        // So we don't modify a list passed to us
        List<ThingDef> traderDefs = new(newTraderDefs);

        // get range of colors if not set
        if (colors == null)
        {
            // default to white for single line
            if (traderDefs.Count == 1)
            {
                colors = [Color.white];
            }

            // rainbow!
            else
            {
                colors = HSV_Helper.Range(traderDefs.Count);
            }
        }

        foreach (var chapter in _chapters)
        {
            traderDefs.Remove(chapter.ThingDefCount.thingDef);
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

public enum Period
{
    Day = 0,
    Month = 1,
    Year = 2
}
