// History.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[HotSwappable]
public class History : IExposable
{
    // types
    public enum Period
    {
        Day = 0,
        Month = 1,
        Year = 2
    }

    public static Color DefaultLineColor = Color.white;
    public static Period[] Periods = (Period[])Enum.GetValues(typeof(Period));

    private const int Breaks = 4;
    private const int EntriesPerInterval = 100;
    private const float YAxisMargin = 40f;

    // How often to record a value for a given period
    private const int IntervalPerDay = GenDate.TicksPerDay / EntriesPerInterval;
    private const int IntervalPerMonth = GenDate.TicksPerTwelfth / EntriesPerInterval;
    private const int IntervalPerYear = GenDate.TicksPerYear / EntriesPerInterval;

    private readonly List<Chapter> _chaptersShown = [];

    // Settings for plot
    public bool AllowTogglingLegend = true;
    public bool DrawInlineLegend = true;
    public bool DrawOptions = true;
    public bool DrawTargetLine = true;

    // Settings for detailed legend
    public bool DrawCounts = true;
    public bool DrawIcons = true;
    public bool DrawInfoInBar;
    public bool DrawMaxMarkers;
    public bool MaxPerChapter;

    // Shared settings
    public Period periodShown = Period.Day;
    public string Suffix = string.Empty;

    // each chapter holds the history for all periods.
    private List<Chapter> _chapters = [];

    // for scribe.
    public History()
    {
    }

    public History(List<ManagerJobHistoryChapterDef> chapters)
    {
        // create a chapter for each label
        for (var i = 0; i < chapters.Count; i++)
        {
            _chapters.Add(new Chapter(chapters[i].historyLabel, EntriesPerInterval, chapters[i].color));
        }

        // show all by default
        _chaptersShown.AddRange(_chapters);
    }

    public History(HistoryLabel[] labels, Color[]? colors = null)
    {
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

    public bool IsRelevantTick
    {
        get { return Periods.Any(p => Find.TickManager.TicksGame % Interval(p) == 0); }
    }

    public void ExposeData()
    {
        // settings
        Scribe_Values.Look(ref AllowTogglingLegend, "allowToggingLegend", true);
        Scribe_Values.Look(ref DrawInlineLegend, "showLegend", true);
        Scribe_Values.Look(ref DrawTargetLine, "drawTargetLine", true);
        Scribe_Values.Look(ref DrawOptions, "drawOptions", true);
        Scribe_Values.Look(ref Suffix, "suffix", "");
        Scribe_Values.Look(ref DrawIcons, "drawIcons", true);
        Scribe_Values.Look(ref DrawCounts, "drawCounts", true);
        Scribe_Values.Look(ref DrawInfoInBar, "drawInfoInBar");
        Scribe_Values.Look(ref DrawMaxMarkers, "drawMaxMarkers", true);
        Scribe_Values.Look(ref MaxPerChapter, "maxPerChapter");

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

    public static int Interval(Period period)
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
    public int CeilToPrecision(float x, int precision = 1)
    {
        var magnitude = Mathf.FloorToInt(Mathf.Log10(x + 1));
        var unit = Mathf.FloorToInt(Mathf.Pow(10, Mathf.Max(magnitude - precision, 1)));
        return Mathf.CeilToInt((x + 1) / unit) * unit;
    }

    public void DrawDetailedLegend(Rect canvas, ref Vector2 scrollPos, int? max, bool positiveOnly = false,
        bool negativeOnly = false)
    {
        // set sign
        var sign = negativeOnly ? -1 : 1;

        var chaptersOrdered = _chapters
            .Where(chapter => !positiveOnly || chapter.pages[(int)periodShown].Any(i => i.count > 0))
            .Where(chapter => !negativeOnly || chapter.pages[(int)periodShown].Any(i => i.count < 0))
            .OrderByDescending(chapter => chapter.Last(periodShown).count * sign).ToList();

        // get out early if no chapters.
        if (chaptersOrdered.Count == 0)
        {
            GUI.DrawTexture(canvas.ContractedBy(Margin), Resources.SlightlyDarkBackground);
            Widgets_Labels.Label(canvas, "ColonyManagerRedux.ManagerHistoryNoChapters".Translate(), TextAnchor.MiddleCenter,
                color: Color.grey);
            return;
        }

        // max
        float _max = max
            ?? (DrawMaxMarkers
                ? chaptersOrdered.Max(chapter => chapter.TrueMax)
                : chaptersOrdered.FirstOrDefault()?.Last(periodShown).count * sign)
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
            // set up rects
            var row = new Rect(0f, height * i, viewRect.width, height);
            var icon = new Rect(Margin, height * i, height, height).ContractedBy(Margin / 2f);
            // icon is square, size defined by height.
            var bar = new Rect(Margin + height, height * i, viewRect.width - height - Margin, height);

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
                barFill.width *= chaptersOrdered[i].Last(periodShown).count * sign / (float)chaptersOrdered[i].TrueMax;
            }
            else
            {
                barFill.width *= chaptersOrdered[i].Last(periodShown).count * sign / _max;
            }

            GUI.BeginGroup(viewRect);

            // if DrawIcons and a thing is set, draw the icon.
            var thing = chaptersOrdered[i].ThingDefCount.thingDef;
            if (DrawIcons && thing != null)
            {
                // draw the icon in correct proportions
                var proportion = GenUI.IconDrawScale(thing);
                Widgets.DrawTextureFitted(icon, thing.uiIcon, proportion);

                // draw counts in upper left corner
                if (DrawCounts)
                {
                    Utilities.LabelOutline(icon, chaptersOrdered[i].ThingDefCount.count.ToString(), null,
                        TextAnchor.UpperLeft, 0f, GameFont.Tiny, Color.white, Color.black);
                }
            }

            // if desired, draw ghost bar
            if (DrawMaxMarkers)
            {
                var ghostBarFill = barFill;
                ghostBarFill.width = MaxPerChapter ? maxWidth : maxWidth * (chaptersOrdered[i].TrueMax / _max);
                GUI.color = new Color(1f, 1f, 1f, .2f);
                GUI.DrawTexture(ghostBarFill, chaptersOrdered[i].Texture); // coloured texture
                GUI.color = Color.white;
            }

            // draw the main bar.
            GUI.DrawTexture(barBox, Resources.SlightlyDarkBackground);
            GUI.DrawTexture(barFill, chaptersOrdered[i].Texture); // coloured texture
            GUI.DrawTexture(barFill, Resources.BarShader);        // slightly fancy overlay (emboss).

            // draw on bar info
            if (DrawInfoInBar)
            {
                var info = chaptersOrdered[i].label + ": " +
                    FormatCount(chaptersOrdered[i].Last(periodShown).count * sign);

                if (DrawMaxMarkers)
                {
                    info += " / " + FormatCount(chaptersOrdered[i].TrueMax);
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
            var shown = _chaptersShown.Contains(chaptersOrdered[i]);

            // tooltip on entire row
            var tooltip = $"{chaptersOrdered[i].label}: " +
                FormatCount(Mathf.Abs(chaptersOrdered[i].Last(periodShown).count)) + "\n\n" +
                "ColonyManagerRedux.ManagerHistoryClickToEnable"
                    .Translate(shown
                        ? "ColonyManagerRedux.ManagerHistoryHide".Translate()
                        : "ColonyManagerRedux.ManagerHistoryShow".Translate(),
                        chaptersOrdered[i].label.Label.UncapitalizeFirst());
            TooltipHandler.TipRegion(row, tooltip);

            // handle input
            if (Widgets.ButtonInvisible(row))
            {
                if (Event.current.button == 0)
                {
                    if (shown)
                    {
                        _chaptersShown.Remove(chaptersOrdered[i]);
                    }
                    else
                    {
                        _chaptersShown.Add(chaptersOrdered[i]);
                    }
                }
                else if (Event.current.button == 1)
                {
                    _chaptersShown.Clear();
                    _chaptersShown.Add(chaptersOrdered[i]);
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

    public void DrawPlot(Rect rect, string label = "", bool positiveOnly = false,
                          bool negativeOnly = false)
    {
        // set sign
        var sign = negativeOnly ? -1 : 1;

        // subset chapters
        var chapters =
            _chaptersShown.Where(chapter => !positiveOnly || chapter.pages[(int)periodShown].Any(i => i.count > 0))
                .Where(chapter => !negativeOnly || chapter.pages[(int)periodShown].Any(i => i.count < 0))
                .ToList();

        // get out early if no chapters.
        if (_chapters.Count == 0)
        {
            GUI.DrawTexture(rect.ContractedBy(Margin), Resources.SlightlyDarkBackground);
            Widgets_Labels.Label(rect, "ColonyManagerRedux.ManagerHistoryNoChapters".Translate(), TextAnchor.MiddleCenter,
                color: Color.grey);
            return;
        }

        // stuff we need
        var plot = rect.ContractedBy(Margin);
        plot.xMin += YAxisMargin;

        // period / variables picker
        if (DrawOptions)
        {
            var switchRect = new Rect(rect.xMax - SmallIconSize - Margin,
                rect.yMin + Margin, SmallIconSize,
                SmallIconSize);

            Widgets.DrawHighlightIfMouseover(switchRect);
            if (Widgets.ButtonImage(switchRect, Resources.Cog))
            {
                var options =
                    Periods.Select(p =>
                        new FloatMenuOption("ColonyManagerRedux.ManagerHistoryPeriod".Translate() + ": " + p.ToString(),
                            delegate { periodShown = p; })).ToList();
                if (AllowTogglingLegend && _chapters.Count > 1) // add option to show/hide legend if appropriate.
                {
                    options.Add(new FloatMenuOption("ColonyManagerRedux.ManagerHistoryShowHideLegend".Translate(),
                        delegate { DrawInlineLegend = !DrawInlineLegend; }));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        // plot the line(s)
        GUI.DrawTexture(plot, Resources.SlightlyDarkBackground);
        GUI.BeginGroup(plot);
        plot = plot.AtZero();

        // draw legend
        var lineCount = _chapters.Count;
        var legendPos = Vector2.zero;
        if (lineCount > 1 && DrawInlineLegend)
        {
            var rowHeight = 20f;
            var lineLength = 30f;
            var labelWidth = plot.width - lineLength;

            Widgets_Labels.Label(ref legendPos, labelWidth, rowHeight, "Legend:",
                font: GameFont.Tiny);

            foreach (var chapter in _chapters)
            {
                Rect butRect = new(legendPos.x, legendPos.y, lineLength + Margin + labelWidth, rowHeight);
                bool isShown = _chaptersShown.Contains(chapter);

                GUI.color = isShown ? chapter.LineColor : chapter.TargetColor;
                Widgets.DrawLineHorizontal(legendPos.x, legendPos.y + rowHeight / 2f, lineLength);
                legendPos.x += lineLength + Margin;
                Widgets_Labels.Label(ref legendPos, labelWidth, rowHeight, chapter.label.Label,
                    font: GameFont.Tiny, color: isShown ? Color.white : Color.gray);
                legendPos.x = 0f;

                var tooltip = "ColonyManagerRedux.ManagerHistoryClickToEnable"
                    .Translate(isShown
                        ? "ColonyManagerRedux.ManagerHistoryHide".Translate()
                        : "ColonyManagerRedux.ManagerHistoryShow".Translate(),
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
                .Select(c => c.Max(periodShown, !negativeOnly, DrawTargetLine))
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
            chapter.PlotCount(periodShown, plot, wu, hu, sign);
        }

        // handle mouseover events
        if (Mouse.IsOver(plot))
        {
            // very conveniently this is the position within the current group.
            var pos = Event.current.mousePosition;
            var upos = new Vector2(pos.x / wu, (plot.height - pos.y) / hu);

            // get distances
            var distances = chapters
                .Select(c => Math.Abs(c.ValueAt(periodShown, (int)upos.x, sign) - upos.y))
                .Concat(chapters
                    .Select(c => Math.Abs(c.TargetAt(periodShown, (int)upos.x, sign) - upos.y)))
                .ToArray();

            // get the minimum index
            float min = int.MaxValue;
            var minIndex = 0;
            for (var i = distances.Count() - 1; i >= 0; i--)
            {
                if (distances[i] < min && (i < chapters.Count || chapters[i % chapters.Count].HasTarget(periodShown)))
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
                ? closest.ValueAt(periodShown, (int)upos.x, sign)
                : closest.TargetAt(periodShown, (int)upos.x, sign);
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
                ? "ColonyManagerRedux.ManagerHistoryValueTooltip".Translate(
                    closest.label.Label,
                    FormatCount(closest.ValueAt(periodShown, (int)upos.x, sign)))
                : "ColonyManagerRedux.ManagerHistoryTargetTooltip".Translate(
                    closest.label.Label,
                    FormatCount(closest.TargetAt(periodShown, (int)upos.x, sign)));
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
                chapter.PlotTarget(periodShown, plot, wu, hu, sign);
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
        for (var i = 0; i <= Breaks + 1; i++)
        {
            Widgets.DrawLineHorizontal(YAxisMargin + Margin / 2, plot.height - i * bu + legendPos.y + Margin, Margin);
            Rect labRect;
            if (i != 0)
            {
                labRect = new Rect(0f, plot.height - i * bu - 4f + legendPos.y + Margin, YAxisMargin, 20f);
            }
            else
            {
                labRect = new Rect(0f, plot.height - i * bu - 6f + legendPos.y, YAxisMargin, 20f);
            }
            Widgets.Label(labRect, FormatCount(i * bi));
        }

        Text.Font = GameFont.Small;
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = Color.white;

        GUI.EndGroup();
    }

    public string FormatCount(float x, int unit = 1000, string[]? suffixes = null)
    {
        suffixes ??= ["", "k", "M", "G"];

        var i = 0;
        while (x > unit && i < suffixes.Length)
        {
            x /= unit;
            i++;
        }

        return x.ToString("0.#" + suffixes[i] + Suffix);
    }

    public void Update(params (int count, int target)[] counts)
    {
        if (counts.Length != _chapters.Count)
        {
            Log.Warning("History updated with incorrect number of chapters");
        }

        for (var i = 0; i < counts.Length; i++)
        {
            _chapters[i].Add(counts[i].count, counts[i].target);
        }
    }

    public void UpdateMax(params int[] maxes)
    {
        if (maxes.Length != _chapters.Count)
        {
            Log.Warning("History updated with incorrect number of chapters");
        }

        for (var i = 0; i < maxes.Length; i++)
        {
            _chapters[i].TrueMax = maxes[i];
        }
    }

    public void UpdateThingCountAndMax(int[] counts, int[] maxes)
    {
        if (maxes.Length != _chapters.Count || maxes.Length != _chapters.Count)
        {
            Log.Warning("History updated with incorrect number of chapters");
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
        if (counts.Length != _chapters.Count)
        {
            Log.Warning("History updated with incorrect number of chapters");
        }

        for (var i = 0; i < counts.Length; i++)
        {
            _chapters[i].ThingDefCount.count = counts[i];
        }
    }

    [HotSwappable]
    public class Chapter : IExposable
    {
        public Texture2D? _texture;
        public HistoryLabel label = new DirectHistoryLabel(string.Empty);
        public List<(int count, int target)>[] pages;
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
            pages = Periods.Select(_ => new List<(int, int)>([(0, 0)])).ToArray();
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
            return pages[(int)period].Any(p => p.target != 0);
        }

        public void ExposeData()
        {
            Scribe_Deep.Look(ref label, "label");
            Scribe_Values.Look(ref entriesPerInterval, "entriesPerInterval", 100);
            Scribe_Values.Look(ref _lineColor, "lineColor", Color.white);
            Scribe_Values.Look(ref ThingDefCount.count, "count");
            Scribe_Defs.Look(ref ThingDefCount.thingDef, "thingDef");

            Scribe_Values.Look(ref _observedMax, "observedMax");
            Scribe_Values.Look(ref _specificMax, "specificMax");

            foreach (var period in Periods)
            {
                var page = pages[(int)period];
                Utilities.Scribe_IntTupleArray(ref page, period.ToString().UncapitalizeFirst());

#if DEBUG_SCRIBE
                Log.Message( Scribe.mode + " for " + label + ", daycount: " + pages[Period.Day].Count );
#endif

                pages[(int)period] = page;
            }
        }

        public void Add(int count, int target)
        {
            var curTick = Find.TickManager.TicksGame;
            foreach (var period in Periods)
            {
                if (curTick % Interval(period) == 0)
                {
                    var page = pages[(int)period];

                    page.Add((count, target));
                    if (Utilities.SafeAbs(count) > _observedMax)
                    {
                        _observedMax = Utilities.SafeAbs(count);
                    }

                    // cull the list back down to size.
                    // TODO: Use a ring buffer instead a list to avoid having to do `RemoveAt(0)`.
                    while (page.Count > EntriesPerInterval)
                    {
                        page.RemoveAt(0);
                    }
                }
            }
        }

        public (int count, int target) Last(Period period)
        {
            return pages[(int)period].Last();
        }

        public int Max(Period period, bool positive = true, bool showTargets = true)
        {
            var page = pages[(int)period];
            return positive
                ? page.Max(p => showTargets ? Math.Max(p.count, p.target) : p.count)
                : Math.Abs(page.Min(p => p.count));
        }

        public void PlotCount(Period period, Rect canvas, float wu, float hu, int sign = 1)
        {
            var page = pages[(int)period];
            if (page.Count > 1)
            {
                var hist = page;
                Vector2? lastEnd = null;
                for (var i = 0; i < hist.Count - 1; i++) // line segments, so up till n-1
                {
                    var start = lastEnd ?? new Vector2(wu * i, canvas.height - hu * hist[i].count * sign);
                    var end = new Vector2(Mathf.Round(wu * (i + 1)), Mathf.Round(canvas.height - hu * hist[i + 1].count * sign));
                    Widgets.DrawLine(start, end, LineColor, 1f);
                }
            }
        }

        public void PlotTarget(Period period, Rect canvas, float wu, float hu, int sign = 1)
        {
            var page = pages[(int)period];
            if (page.Count > 1)
            {
                Color targetColor = TargetColor;
                Vector2? lastEnd = null;
                for (var i = 0; i < page.Count - 1; i++) // line segments, so up till n-1
                {
                    var start = lastEnd ?? new Vector2(wu * i, canvas.height - hu * page[i].target * sign);
                    var end = new Vector2(wu * (i + 1) - 1, canvas.height - hu * page[i + 1].target * sign);

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
            var page = pages[(int)period];
            if (x < 0 || x >= page.Count)
            {
                return -1;
            }

            return page[x].count * sign;
        }

        public int TargetAt(Period period, int x, int sign = 1)
        {
            var page = pages[(int)period];
            if (x < 0 || x >= page.Count)
            {
                return -1;
            }

            return page[x].target * sign;
        }
    }
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

    public static implicit operator DefHistoryLabel<T>(T def)
    {
        return new(def);
    }
}

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
}
