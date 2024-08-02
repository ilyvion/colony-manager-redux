// Chapter.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using CircularBuffer;

namespace ColonyManagerRedux;

public partial class History
{
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
