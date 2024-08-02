﻿// History.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using CircularBuffer;
using ilyvion.Laboratory;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[HotSwappable]
public class History : IExposable
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

    [HotSwappable]
    internal sealed class Chapter : IExposable
    {
        public ManagerJobHistoryChapterDef? def;
        public string? ChapterSuffix => def?.suffix;

        public Texture2D? _texture;
        public HistoryLabel label = new DirectHistoryLabel(string.Empty);
        internal CircularBuffer<int>[] counts;

        // Since targets don't change very often, they're stored in a sparse format of (pos, value). Once there are
        // [EntriesPerInterval] history values stored in [counts], the earliest ones start falling off. For the targets
        // to keep matching their position, they must be decremented by 1 for each new value added to [counts] at that
        // point. Once the *second* value's position in the buffer reaches 0, the *first* value should fall off, i.e.
        // be removed.
        //
        // This logic is currently all handled in Chapter.Add for the removal of "old" values and in the static
        // Chapter.TargetAt for calculating the "non-sparse" value at any given position from the sparse data.
        internal CircularBuffer<(int position, int target)>[] targets;
        public int entriesPerInterval = EntriesPerInterval;
        public ThingDefCountClass ThingDefCount = new();
        private int _observedMax = -1;
        private int _specificMax = -1;


        private Color _lineColor = DefaultLineColor;
        public Color LineColor
        {
            get => _lineColor;
            set
            {
                _lineColor = value;
                _targetColor = null;
            }
        }

        private Color? _targetColor;
        public Color TargetColor
        {
            get
            {
                if (!_targetColor.HasValue)
                {
                    Color.RGBToHSV(LineColor, out var H, out var S, out var V);
                    S /= 2;
                    V /= 2;
                    _targetColor = Color.HSVToRGB(H, S, V);
                }
                return _targetColor.Value;
            }
        }

        public Chapter()
        {
            counts = Periods
                .Select(_ => new CircularBuffer<int>(EntriesPerInterval, [0]))
                .ToArray();
            targets = Periods
                .Select(_ => new CircularBuffer<(int, int)>(EntriesPerInterval, [(0, 0)]))
                .ToArray();
        }

        public Chapter(HistoryLabel label, int entriesPerInterval, Color color) : this()
        {
            this.label = label;
            this.entriesPerInterval = entriesPerInterval;
            LineColor = color;
        }

        public Chapter(ThingDefCountClass thingDefCount, int entriesPerInterval, Color color) : this()
        {
            label = new DefHistoryLabel<ThingDef>(thingDefCount.thingDef);
            ThingDefCount = thingDefCount;
            this.entriesPerInterval = entriesPerInterval;
            LineColor = color;
        }

        public Texture2D Texture
        {
            get
            {
                if (_texture == null)
                {
                    _texture = SolidColorMaterials.NewSolidColorTexture(LineColor);
                }

                return _texture;
            }
        }

        public int TrueMax
        {
            get => Mathf.Max(_observedMax, _specificMax, 1);
            set
            {
                _observedMax = value != 0 ? value : Max(Period.Day);
                _specificMax = value;
            }
        }

        public bool HasTarget(Period period)
        {
            return !targets[(int)period].IsEmpty && targets[(int)period].Any(t => t.target != 0);
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Deep.Look(ref label, "label");
            Scribe_Values.Look(ref entriesPerInterval, "entriesPerInterval", 100);
            Scribe_Values.Look(ref _lineColor, "lineColor", Color.white);
            Scribe_Values.Look(ref ThingDefCount.count, "count");
            Scribe_Defs.Look(ref ThingDefCount.thingDef, "thingDef");

            Scribe_Values.Look(ref _observedMax, "observedMax");
            Scribe_Values.Look(ref _specificMax, "specificMax");

            foreach (var period in Periods)
            {
                var count = counts[(int)period];
                var target = targets[(int)period];
                Utilities.Scribe_IntArray(ref count, period.ToString().UncapitalizeFirst());
                Utilities.Scribe_IntTupleArray(ref target, period.ToString().UncapitalizeFirst() + "Targets");

#if DEBUG_SCRIBE
                Log.Message( Scribe.mode + " for " + label + ", daycount: " + pages[Period.Day].Count );
#endif

                counts[(int)period] = count;
                targets[(int)period] = target;
            }
        }

        public void Add(int newCount, int newTarget)
        {
            var curTick = Find.TickManager.TicksGame;
            foreach (var period in Periods)
            {
                if (curTick % PeriodTickInterval(period) == 0)
                {
                    var page = counts[(int)period];
                    var pageTarget = targets[(int)period];

                    var shiftTargets = page.Size == page.Capacity;
                    page.PushBack(newCount);

                    if (pageTarget.Back().target != newTarget)
                    {
                        pageTarget.PushBack((page.Size - 1, newTarget));
                    }
                    if (shiftTargets)
                    {
                        for (int i = 0; i < pageTarget.Size; i++)
                        {
                            pageTarget[i] = (pageTarget[i].position - 1, pageTarget[i].target);
                        }
                        while (pageTarget.Size > 1 && pageTarget[1].position == 0)
                        {
                            pageTarget.PopFront();
                        }
                        if (pageTarget.Front().position < 0)
                        {
                            var (_, target) = pageTarget.Front();
                            pageTarget.PopFront();
                            pageTarget.PushFront((0, target));
                        }
                    }
                    if (Utilities.SafeAbs(newCount) > _observedMax)
                    {
                        _observedMax = Utilities.SafeAbs(newCount);
                    }
                }
            }
        }

        public (int count, int target) Last(Period period)
        {
            return (counts[(int)period].Back(), targets[(int)period].Back().target);
        }

        public int Max(Period period, bool positive = true, bool showTargets = true)
        {
            var page = counts[(int)period];
            var pageTarget = targets[(int)period];
            return positive
                ? page.Append(showTargets ? pageTarget.Max(p => p.target) : 0).Max()
                : Math.Abs(page.Min());
        }

        public void PlotCount(Period period, Rect canvas, float wu, float hu, int sign = 1)
        {
            var page = counts[(int)period];
            if (page.Size > 1)
            {
                Vector2? lastEnd = null;
                for (var i = 0; i < page.Size - 1; i++) // line segments, so up till n-1
                {
                    var start = lastEnd ?? new Vector2(wu * i, canvas.height - hu * page[i] * sign);
                    var end = new Vector2(Mathf.Round(wu * (i + 1)), Mathf.Round(canvas.height - hu * page[i + 1] * sign));
                    Widgets.DrawLine(start, end, LineColor, 1f);

                    lastEnd = end;
                }
            }
        }

        public void PlotTarget(Period period, Rect canvas, float wu, float hu, int sign = 1)
        {
            var page = counts[(int)period];
            var pageTarget = targets[(int)period];
            if (page.Size > 1)
            {
                Color targetColor = TargetColor;
                Vector2? lastEnd = null;
                for (var i = 0; i < page.Size - 1; i++) // line segments, so up till n-1
                {
                    int targetAtI = TargetAt(pageTarget, page.Size, i, sign);
                    int targetAtI1 = TargetAt(pageTarget, page.Size, i + 1, sign);
                    var start = lastEnd ?? new Vector2(wu * i, canvas.height - hu * targetAtI);
                    var end = new Vector2(wu * (i + 1) - 1, canvas.height - hu * targetAtI1);

                    // When a target value changes, make the line non-continuous
                    if (start.y != end.y)
                    {
                        lastEnd = null;
                        continue;
                    }

                    Widgets.DrawLine(start, end, targetColor, 1.5f);

                    lastEnd = end;
                }
            }
        }

        public int ValueAt(Period period, int x, int sign = 1)
        {
            var page = counts[(int)period];
            if (x < 0 || x >= page.Size)
            {
                return -1;
            }

            return page[x] * sign;
        }

        private static int TargetAt(CircularBuffer<(int position, int target)> pageTarget, int pageSize, int x, int sign)
        {
            if (x < 0 || x >= pageSize)
            {
                return -1;
            }

            var lastValue = -1;
            foreach (var (position, target) in pageTarget)
            {
                if (position > x)
                {
                    break;
                }
                lastValue = target;
            }

            return lastValue * sign;
        }

        public int TargetAt(Period period, int x, int sign = 1)
        {
            var page = counts[(int)period];
            var pageTarget = targets[(int)period];
            return TargetAt(pageTarget, page.Size, x, sign);
        }
    }
}

[HotSwappable]
public class DetailedLegendRenderer : IExposable
{
    // Settings for detailed legend
    public bool DrawCounts = true;
    public bool DrawIcons = true;
    public bool DrawInfoInBar;
    public bool DrawMaxMarkers;
    public bool MaxPerChapter;

    public void DrawDetailedLegend(History history, Rect canvas, ref Vector2 scrollPos, int? max, bool positiveOnly = false,
        bool negativeOnly = false)
    {
        if (history == null)
        {
            throw new ArgumentNullException(nameof(history));
        }

        // set sign
        var sign = negativeOnly ? -1 : 1;

        var chaptersOrdered = history._chapters
            .Where(chapter => !positiveOnly || chapter.counts[(int)history.PeriodShown].Any(i => i > 0))
            .Where(chapter => !negativeOnly || chapter.counts[(int)history.PeriodShown].Any(i => i < 0))
            .OrderByDescending(chapter => chapter.Last(history.PeriodShown).count * sign).ToList();

        if (IlyvionDebugViewSettings.DrawUIHelpers)
        {
            Widgets.DrawRectFast(canvas, ColorLibrary.NeonGreen.ToTransparent(.5f));
        }

        // get out early if no chapters.
        if (chaptersOrdered.Count == 0)
        {
            GUI.DrawTexture(canvas.ContractedBy(Margin), Resources.SlightlyDarkBackground);
            Widgets_Labels.Label(canvas, "ColonyManagerRedux.History.NoChapters".Translate(), TextAnchor.MiddleCenter,
                color: Color.grey);
            return;
        }

        // max
        float _max = max
            ?? (DrawMaxMarkers
                ? chaptersOrdered.Max(chapter => chapter.TrueMax)
                : chaptersOrdered.FirstOrDefault()?.Last(history.PeriodShown).count * sign)
            ?? 0;

        // cell height
        var height = 30f;
        var barHeight = 18f;

        // n rows
        var n = chaptersOrdered.Count;

        // scrolling region
        var viewRect = canvas;
        viewRect.height = n * height;
        if (viewRect.height > canvas.height)
        {
            viewRect.width -= 16f + Margin;
            canvas.width -= Margin;
            canvas.height -= 1f;
        }

        Widgets.BeginScrollView(canvas, ref scrollPos, viewRect);
        for (var i = 0; i < n; i++)
        {
            History.Chapter chapter = chaptersOrdered[i];

            // set up rects
            var row = new Rect(0f, height * i, viewRect.width, height);
            var icon = new Rect(Margin, height * i, height, height).ContractedBy(Margin / 2f);
            // icon is square, size defined by height.
            var bar = new Rect(Margin + height, height * i, viewRect.width - height - Margin, height);

            if (IlyvionDebugViewSettings.DrawUIHelpers)
            {
                Widgets.DrawRectFast(row, ColorLibrary.Red.ToTransparent(.5f));
                Widgets.DrawRectFast(icon, ColorLibrary.Teal.ToTransparent(.5f));
                Widgets.DrawRectFast(bar, ColorLibrary.Yellow.ToTransparent(.5f));
            }

            // if icons should not be drawn make the bar full size.
            if (!DrawIcons)
            {
                bar.xMin -= height + Margin;
            }

            // bar details.
            var barBox = bar.ContractedBy((height - barHeight) / 2f);
            var barFill = barBox.ContractedBy(2f);
            var maxWidth = barFill.width;
            if (MaxPerChapter)
            {
                barFill.width *= chapter.Last(history.PeriodShown).count * sign / (float)chapter.TrueMax;
            }
            else
            {
                barFill.width *= chapter.Last(history.PeriodShown).count * sign / _max;
            }

            GUI.BeginGroup(viewRect);

            // if DrawIcons and a thing is set, draw the icon.
            var thing = chapter.ThingDefCount.thingDef;
            if (DrawIcons && thing != null)
            {
                // draw the icon in correct proportions
                var proportion = GenUI.IconDrawScale(thing);
                Widgets.DrawTextureFitted(icon, thing.uiIcon, proportion);

                // draw counts in upper left corner
                if (DrawCounts)
                {
                    Utilities.LabelOutline(icon, chapter.ThingDefCount.count.ToString(), null,
                        TextAnchor.UpperLeft, 0f, GameFont.Tiny, Color.white, Color.black);
                }
            }

            // if desired, draw ghost bar
            if (DrawMaxMarkers)
            {
                var ghostBarFill = barFill;
                ghostBarFill.width = MaxPerChapter ? maxWidth : maxWidth * (chapter.TrueMax / _max);
                GUI.color = new Color(1f, 1f, 1f, .2f);
                GUI.DrawTexture(ghostBarFill, chapter.Texture); // coloured texture
                GUI.color = Color.white;
            }

            // draw the main bar.
            GUI.DrawTexture(barBox, Resources.SlightlyDarkBackground);
            GUI.DrawTexture(barFill, chapter.Texture); // coloured texture
            GUI.DrawTexture(barFill, Resources.BarShader);        // slightly fancy overlay (emboss).

            // draw on bar info
            if (DrawInfoInBar)
            {
                var info = chapter.label + ": " +
                    History.FormatCount(chapter.Last(history.PeriodShown).count * sign, chapter.ChapterSuffix ?? history.YAxisSuffix);

                if (DrawMaxMarkers)
                {
                    info += " / " + History.FormatCount(chapter.TrueMax, chapter.ChapterSuffix ?? history.YAxisSuffix);
                }

                // offset label a bit downwards and to the right
                var rowInfoRect = row;
                rowInfoRect.y += 1f;
                rowInfoRect.x += Margin * 2;

                // x offset
                var xOffset = DrawIcons && thing != null ? height + Margin * 2 : Margin * 2;

                Utilities.LabelOutline(rowInfoRect, info, null, TextAnchor.MiddleLeft, xOffset, GameFont.Tiny,
                    Color.white, Color.black);
            }

            // are we currently showing this line?
            var shown = history._chaptersShown.Contains(chapter);

            // tooltip on entire row
            var tooltip = $"{chapter.label}: " +
                History.FormatCount(
                    Mathf.Abs(chapter.Last(history.PeriodShown).count),
                    chapter.ChapterSuffix ?? history.YAxisSuffix) + "\n\n" +
                "ColonyManagerRedux.History.ClickToEnable"
                    .Translate(shown
                        ? "ColonyManagerRedux.History.Hide".Translate()
                        : "ColonyManagerRedux.History.Show".Translate(),
                        chapter.label.Label.UncapitalizeFirst());
            TooltipHandler.TipRegion(row, tooltip);

            // handle input
            if (Widgets.ButtonInvisible(row))
            {
                if (Event.current.button == 0)
                {
                    if (shown)
                    {
                        history._chaptersShown.Remove(chapter);
                    }
                    else
                    {
                        history._chaptersShown.Add(chapter);
                    }
                }
                else if (Event.current.button == 1)
                {
                    history._chaptersShown.Clear();
                    history._chaptersShown.Add(chapter);
                }
            }

            // UI feedback for disabled row
            if (!shown)
            {
                GUI.DrawTexture(row.ContractedBy(1f), Resources.SlightlyDarkBackground);
            }

            GUI.EndGroup();
        }

        Widgets.EndScrollView();
    }


    public void ExposeData()
    {
        Scribe_Values.Look(ref DrawIcons, "drawIcons", true);
        Scribe_Values.Look(ref DrawCounts, "drawCounts", true);
        Scribe_Values.Look(ref DrawInfoInBar, "drawInfoInBar");
        Scribe_Values.Look(ref DrawMaxMarkers, "drawMaxMarkers", true);
        Scribe_Values.Look(ref MaxPerChapter, "maxPerChapter");
    }
}

public enum Period
{
    Day = 0,
    Month = 1,
    Year = 2
}

public abstract class HistoryLabel : IExposable
{
    public abstract string Label { get; }

    public abstract void ExposeData();

    public override string ToString()
    {
        return Label;
    }
}

public class DirectHistoryLabel : HistoryLabel
{
    private string direct;

    public override string Label => direct;

    public DirectHistoryLabel(string direct)
    {
        this.direct = direct;
    }

#pragma warning disable CS8618 // Used by scribe
    public DirectHistoryLabel()
#pragma warning restore CS8618
    {
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref direct, "direct", string.Empty);
    }

    public static implicit operator DirectHistoryLabel(string direct)
    {
        return new(direct);
    }

    public static DirectHistoryLabel FromString(string direct)
    {
        return direct;
    }
}

public class DefHistoryLabel<T> : HistoryLabel where T : Def, new()
{
    private T def;

    public DefHistoryLabel(T def)
    {
        this.def = def;
    }

#pragma warning disable CS8618 // Used by scribe
    public DefHistoryLabel()
#pragma warning restore CS8618
    {
    }

    public override string Label => def.LabelCap;

    public override void ExposeData()
    {
        Scribe_Defs.Look(ref def, "def");
    }

#pragma warning disable CA2225 // Operator overloads have named alternates
    public static implicit operator DefHistoryLabel<T>(T def)
#pragma warning restore CA2225 // Operator overloads have named alternates
    {
        return new(def);
    }
}

public class ManagerJobHistoryChapterDefLabel : HistoryLabel
{
    private ManagerJobHistoryChapterDef historyChapterDef;

    public ManagerJobHistoryChapterDefLabel(ManagerJobHistoryChapterDef historyChapterDef)
    {
        this.historyChapterDef = historyChapterDef;
    }

#pragma warning disable CS8618 // Used by scribe
    public ManagerJobHistoryChapterDefLabel()
#pragma warning restore CS8618
    {
    }

    public override string Label => historyChapterDef.historyLabel.Label;

    public override void ExposeData()
    {
        Scribe_Defs.Look(ref historyChapterDef, "historyChapterDef");
    }
}

[Obsolete("Use ManagerJobHistoryChapterDefs instead of this directly.")]
public class TranslationHistoryLabel : HistoryLabel
{
    private string translationKey;

    public TranslationHistoryLabel(string translationKey)
    {
        this.translationKey = translationKey;
    }

#pragma warning disable CS8618 // Used by scribe and defs
    public TranslationHistoryLabel()
#pragma warning restore CS8618
    {
    }

    public override string Label => translationKey.Translate();

    public override void ExposeData()
    {
        Scribe_Values.Look(ref translationKey, "translationKey", string.Empty);
    }

    public static implicit operator TranslationHistoryLabel(string translationKey)
    {
        return new(translationKey);
    }

    public static DirectHistoryLabel FromString(string translationKey)
    {
        return translationKey;
    }
}