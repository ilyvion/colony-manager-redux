// History.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[HotSwappable]
public partial class History : IExposable
{

    public static readonly Color DefaultLineColor = Color.white;
    public static readonly Period[] Periods = (Period[])Enum.GetValues(typeof(Period));

    private const int Breaks = 4;
    internal const int EntriesPerInterval = 100;
    //private const float YAxisMargin = 40f;

    // How often to record a value for a given period
    private const int IntervalPerDay = GenDate.TicksPerDay / EntriesPerInterval;
    private const int IntervalPerMonth = GenDate.TicksPerTwelfth / EntriesPerInterval;
    private const int IntervalPerYear = GenDate.TicksPerYear / EntriesPerInterval;

    internal readonly List<Chapter> _chaptersShown = [];

    // Settings for plot
    public bool AllowTogglingLegend = true;
    public bool DrawInlineLegend = true;
    public bool DrawOptions = true;
    public bool DrawTargetLine = true;

    // Shared settings
    public Period PeriodShown = Period.Day;
    public string YAxisSuffix = string.Empty;

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

    public static bool IsUpdateTick
    {
        get { return Periods.Any(p => Find.TickManager.TicksGame % PeriodTickInterval(p) == 0); }
    }

    public void ExposeData()
    {
        // settings
        Scribe_Values.Look(ref AllowTogglingLegend, "allowToggingLegend", true);
        Scribe_Values.Look(ref DrawInlineLegend, "showLegend", true);
        Scribe_Values.Look(ref DrawTargetLine, "drawTargetLine", true);
        Scribe_Values.Look(ref DrawOptions, "drawOptions", true);
        Scribe_Values.Look(ref PeriodShown, "periodShown", Period.Day);
        Scribe_Values.Look(ref YAxisSuffix, "suffix", "");

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

    /// <summary>
    ///     Round up to given precision
    /// </summary>
    /// <param name="x">input</param>
    /// <param name="precision">number of digits to preserve past the magnitude, should be equal to or greater than zero.</param>
    /// <returns></returns>
    private static int CeilToPrecision(float x, int precision = 1)
    {
        var magnitude = Mathf.FloorToInt(Mathf.Log10(x + 1));
        var unit = Mathf.FloorToInt(Mathf.Pow(10, Mathf.Max(magnitude - precision, 1)));
        return Mathf.CeilToInt((x + 1) / unit) * unit;
    }

    float yAxisMaxWidth;
    public void DrawPlot(Rect rect, bool positiveOnly = false, bool negativeOnly = false)
    {
        // set sign
        var sign = negativeOnly ? -1 : 1;

        // subset chapters
        var chapters =
            _chaptersShown.Where(chapter => !positiveOnly || chapter.counts[(int)PeriodShown].Any(i => i > 0))
                .Where(chapter => !negativeOnly || chapter.counts[(int)PeriodShown].Any(i => i < 0))
                .ToList();

        // get out early if no chapters.
        if (_chapters.Count == 0)
        {
            GUI.DrawTexture(rect.ContractedBy(Margin), Resources.SlightlyDarkBackground);
            Widgets_Labels.Label(rect, "ColonyManagerRedux.History.NoChapters".Translate(), TextAnchor.MiddleCenter,
                color: Color.grey);
            return;
        }

        // stuff we need
        var plot = rect.ContractedBy(Margin);
        plot.xMin += yAxisMaxWidth + Margin;

        GUI.DrawTexture(plot, Resources.SlightlyDarkBackground);

        // period / variables picker
        if (DrawOptions)
        {
            var switchRect = new Rect(rect.xMax - SmallIconSize - Margin,
                rect.yMin + Margin, SmallIconSize,
                SmallIconSize);

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

            Text.Font = GameFont.Tiny;
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

        // plot the line(s)
        GUI.BeginGroup(plot);
        plot = plot.AtZero();

        // draw legend
        var lineCount = _chapters.Count;
        var legendPos = Vector2.zero;
        if (lineCount > 1 && DrawInlineLegend)
        {
            var rowHeight = Text.LineHeightOf(Text.Font);
            var lineLength = 30f;
            var labelWidth = plot.width - lineLength;

            Widgets_Labels.Label(ref legendPos, labelWidth, rowHeight, "ColonyManagerRedux.History.Legend".Translate() + ":",
                font: GameFont.Tiny);

            foreach (var chapter in _chapters)
            {
                Rect butRect = new(legendPos.x, legendPos.y, lineLength + Margin + labelWidth, rowHeight);
                bool isShown = _chaptersShown.Contains(chapter);

                GUI.color = isShown ? chapter.LineColor : chapter.TargetColor;
                Widgets.DrawLineHorizontal(legendPos.x, legendPos.y + rowHeight / 2f, lineLength);
                legendPos.x += lineLength + Margin;
                legendPos.y += 1f;
                Widgets_Labels.Label(ref legendPos, labelWidth, rowHeight, chapter.label.Label,
                    font: GameFont.Tiny, color: isShown ? Color.white : Color.gray);
                legendPos.x = 0f;
                legendPos.y -= 1f;

                var tooltip = "ColonyManagerRedux.History.ClickToEnable"
                    .Translate(isShown
                        ? "ColonyManagerRedux.History.Hide".Translate()
                        : "ColonyManagerRedux.History.Show".Translate(),
                        chapter.label.Label.UncapitalizeFirst());
                TooltipHandler.TipRegion(butRect, tooltip);
                Widgets.DrawHighlightIfMouseover(butRect);
                if (Widgets.ButtonInvisible(butRect))
                {
                    if (Event.current.button == 0)
                    {
                        if (isShown)
                        {
                            _chaptersShown.Remove(chapter);
                        }
                        else
                        {
                            _chaptersShown.Add(chapter);
                        }
                    }
                    else if (Event.current.button == 1)
                    {
                        _chaptersShown.Clear();
                        _chaptersShown.Add(chapter);
                    }
                }
            }

            GUI.color = Color.white;
        }
        else if (DrawOptions)
        {
            legendPos.y += SmallIconSize + Margin;
        }

        if (chapters.Count == 0)
        {
            GUI.EndGroup();
            return;
        }

        plot.yMin += legendPos.y;
        GUI.BeginGroup(plot);
        plot = plot.AtZero();

        // maximum of all chapters.
        var max = CeilToPrecision(
            chapters
                .Select(c => c.Max(PeriodShown, !negativeOnly, DrawTargetLine))
                .Max());

        // size, and pixels per node.
        var w = plot.width;
        var h = plot.height;
        var wu = w / EntriesPerInterval;            // width per section
        var hu = h / Math.Max(max, 2);            // height per count
        var bi = (float)Math.Max(max, 2) / (Breaks + 1); // count per break
        var bu = hu * bi;                         // height per break

        foreach (var chapter in chapters)
        {
            chapter.PlotCount(PeriodShown, plot, wu, hu, sign);
        }

        // handle mouseover events
        if (Mouse.IsOver(plot))
        {
            // very conveniently this is the position within the current group.
            var pos = Event.current.mousePosition;
            var upos = new Vector2(pos.x / wu, (plot.height - pos.y) / hu);

            // get distances
            var distances = chapters
                .Select(c => Math.Abs(c.ValueAt(PeriodShown, (int)upos.x, sign) - upos.y))
                .Concat(chapters
                    .Select(c => Math.Abs(c.TargetAt(PeriodShown, (int)upos.x, sign) - upos.y)))
                .ToArray();

            // get the minimum index
            float min = int.MaxValue;
            var minIndex = 0;
            for (var i = distances.Length - 1; i >= 0; i--)
            {
                if (distances[i] < min && (i < chapters.Count || chapters[i % chapters.Count].HasTarget(PeriodShown)))
                {
                    minIndex = i;
                    min = distances[i];
                }
            }

            var useValue = minIndex < chapters.Count;

            // closest line
            var closest = chapters[minIndex % chapters.Count];

            // do minimum stuff.
            var valueAt = useValue
                ? closest.ValueAt(PeriodShown, (int)upos.x, sign)
                : closest.TargetAt(PeriodShown, (int)upos.x, sign);
            var realpos = new Vector2(
                (int)upos.x * wu,
                plot.height - Math.Max(0, valueAt) * hu);
            var blipRect = new Rect(realpos.x - SmallIconSize / 2f,
                                     realpos.y - SmallIconSize / 2f, SmallIconSize,
                                     SmallIconSize);
            GUI.color = useValue ? closest.LineColor : closest.TargetColor;
            GUI.DrawTexture(blipRect, Resources.StageB);
            GUI.color = Color.white;

            // get orientation of tooltip
            var tippos = realpos + new Vector2(Margin, Margin);
            var tip = useValue
                ? "ColonyManagerRedux.History.ValueTooltip".Translate(
                    closest.label.Label,
                    FormatCount(closest.ValueAt(PeriodShown, (int)upos.x, sign), closest.ChapterSuffix ?? YAxisSuffix))
                : "ColonyManagerRedux.History.TargetTooltip".Translate(
                    closest.label.Label,
                    FormatCount(closest.TargetAt(PeriodShown, (int)upos.x, sign), closest.ChapterSuffix ?? YAxisSuffix));
            var tipsize = Text.CalcSize(tip);
            bool up = false, left = false;
            if (tippos.x + tipsize.x > plot.width)
            {
                left = true;
                tippos.x -= tipsize.x + 2 * Margin;
            }

            if (tippos.y + tipsize.y > plot.height)
            {
                up = true;
                tippos.y -= tipsize.y + 2 * Margin;
            }

            var anchor = TextAnchor.UpperLeft;
            if (up && left)
            {
                anchor = TextAnchor.LowerRight;
            }

            if (up && !left)
            {
                anchor = TextAnchor.LowerLeft;
            }

            if (!up && left)
            {
                anchor = TextAnchor.UpperRight;
            }

            var tooltipRect = new Rect(tippos.x, tippos.y, tipsize.x, tipsize.y);
            Widgets_Labels.Label(tooltipRect, tip, anchor, GameFont.Tiny);
        }

        // draw target line
        if (DrawTargetLine)
        {
            foreach (var chapter in chapters)
            {
                chapter.PlotTarget(PeriodShown, plot, wu, hu, sign);
            }
        }

        GUI.EndGroup();
        GUI.EndGroup();

        // plot axis
        GUI.BeginGroup(rect);
        rect = rect.AtZero();

        Text.Anchor = TextAnchor.MiddleRight;
        Text.Font = GameFont.Tiny;

        // draw ticks + labels
        var labelMaxWidth = 0f;
        for (var i = 0; i <= Breaks + 1; i++)
        {
            string label = FormatCount(i * bi, YAxisSuffix);
            labelMaxWidth = Math.Max(labelMaxWidth, Text.CalcSize(label).x);
            Widgets.DrawLineHorizontal(yAxisMaxWidth + Margin, plot.height - i * bu + legendPos.y + Margin, Margin);
            Rect labRect;
            if (i != 0)
            {
                labRect = new Rect(0f, plot.height - i * bu - 4f + legendPos.y + Margin, yAxisMaxWidth, 20f);
            }
            else
            {
                labRect = new Rect(0f, plot.height - i * bu - 6f + legendPos.y, yAxisMaxWidth, 20f);
            }

            Widgets.Label(labRect, label);
        }
        yAxisMaxWidth = labelMaxWidth + Margin;

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;

        GUI.EndGroup();
    }

    public static string FormatCount(float x, string suffix, int unit = 1000, string[]? unitSuffixes = null)
    {
        unitSuffixes ??= ["", "k", "M", "G"];

        var i = 0;
        while (x > unit && i < unitSuffixes.Length)
        {
            x /= unit;
            i++;
        }

        return x.ToString("0.# " + unitSuffixes[i] + suffix);
    }

    public void Update(params (int count, int target)[] counts)
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
            _chapters[i].Add(counts[i].count, counts[i].target);
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

    internal void UpdateThingDefs(List<ThingDef> traderDefs, Color[]? colors = null)
    {
        // So we don't modify a list passed to us
        traderDefs = new(traderDefs);

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
            Logger.Debug("New defs: " + traderDefs.Join(t => t.defName));

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
