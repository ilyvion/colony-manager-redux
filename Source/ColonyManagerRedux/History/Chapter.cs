// Chapter.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.Collections;
using ilyvion.Laboratory.UI;

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
            set => _lineColor = value;
        }

        public Chapter()
        {
            counts = Periods
                .Select(_ => new CircularBuffer<int>(entriesPerInterval, [0]))
                .ToArray();
            targets = Periods
                .Select(_ => new CircularBuffer<(int, int)>(entriesPerInterval, [(0, 0)]))
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

        internal GraphSeries? GraphSeries { get; set; }

        public bool HasTargets(Period period)
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

        public int[] ValuesFor(Period period, int sign = 1)
        {
            var page = counts[(int)period];
            return page.Select(v => v * sign).ToArray();
        }

        public int[]? TargetsFor(Period period, int sign = 1)
        {
            if (!HasTargets(period))
            {
                return null;
            }

            var page = counts[(int)period];
            var pageTarget = targets[(int)period];
            var output = new int[page.Size];

            var currentPage = 0;
            var (position, target) = pageTarget[currentPage];
            for (int i = 0; i < output.Length; i++)
            {
                if (position < i && currentPage < pageTarget.Size - 1)
                {
                    currentPage++;
                    (position, target) = pageTarget[currentPage];
                }
                output[i] = target * sign;
            }
            return output;
        }
    }
}
