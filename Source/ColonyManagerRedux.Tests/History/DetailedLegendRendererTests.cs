// DetailedLegendRendererTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The chapter selection, bar scaling and persisted defaults of
/// <see cref="DetailedLegendRenderer"/>, without drawing anything.
/// </summary>
[TestFixture(TestType.MainMenu)]
internal sealed class DetailedLegendRendererTests
{
    private const string EmptyDocument = "<?xml version=\"1.0\" encoding=\"utf-8\"?><root />";

    private sealed class Row(string name, int[] counts, int last)
    {
        public string Name { get; } = name;
        public int[] Counts { get; } = counts;
        public int Last { get; } = last;
    }

    private static string Select(
        Row[] rows,
        bool positiveOnly = false,
        bool negativeOnly = false
    ) =>
        string.Join(
            ",",
            DetailedLegendRenderer
                .SelectLegendChapters(rows, r => r.Counts, r => r.Last, positiveOnly, negativeOnly)
                .Select(r => r.Name)
        );

    [Test]
    public static void ChaptersAreOrderedByLastValueDescending() =>
        Expect.AreEqual(
            "b,c,a",
            Select([new Row("a", [1], 1), new Row("b", [9], 9), new Row("c", [5], 5)])
        );

    [Test]
    public static void NegativeOnlyOrdersByLargestNegativeFirstAndDropsPositiveChapters() =>
        Expect.AreEqual(
            "big,small",
            Select(
                [new Row("small", [-1], -1), new Row("pos", [4], 4), new Row("big", [-8], -8)],
                negativeOnly: true
            )
        );

    [Test]
    public static void PositiveOnlyKeepsAChapterThatWasPositiveEarlierInThePeriodAndSortsItLast() =>
        Expect.AreEqual(
            "now,earlier",
            Select([new Row("earlier", [3, 0], 0), new Row("now", [0, 2], 2)], positiveOnly: true)
        );

    [Test]
    public static void PositiveOnlyDropsAllZeroChapters() =>
        Expect.AreEqual("", Select([new Row("zero", [0, 0], 0)], positiveOnly: true));

    [Test]
    public static void TiedChaptersKeepTheirOriginalOrder() =>
        Expect.AreEqual(
            "a,b,c",
            Select([new Row("a", [2], 2), new Row("b", [2], 2), new Row("c", [2], 2)])
        );

    [Test]
    public static void NoChaptersSelectsNothing() => Expect.AreEqual("", Select([]));

    [Test]
    public static void ExplicitMaxWins() =>
        Expect.AreEqual(50f, DetailedLegendRenderer.ComputeLegendMax(50, true, [10, 20], 5, 1));

    [Test]
    public static void MaxMarkersUseTheLargestTrueMax() =>
        Expect.AreEqual(
            20f,
            DetailedLegendRenderer.ComputeLegendMax(null, true, [10, 20, 15], 5, 1)
        );

    [Test]
    public static void MaxMarkersWithoutChaptersGiveZero() =>
        Expect.AreEqual(0f, DetailedLegendRenderer.ComputeLegendMax(null, true, [], null, 1));

    [Test]
    public static void WithoutMaxMarkersTheFirstChaptersLastValueIsTheMax() =>
        Expect.AreEqual(7f, DetailedLegendRenderer.ComputeLegendMax(null, false, [100], 7, 1));

    [Test]
    public static void NegativeSignFlipsTheFirstChaptersLastValue() =>
        Expect.AreEqual(7f, DetailedLegendRenderer.ComputeLegendMax(null, false, [], -7, -1));

    [Test]
    public static void NoFirstChapterGivesZero() =>
        Expect.AreEqual(0f, DetailedLegendRenderer.ComputeLegendMax(null, false, [], null, 1));

    [Test]
    public static void BarFractionIsTheShareOfTheDenominator() =>
        Expect.AreEqual(0.25f, DetailedLegendRenderer.BarFraction(5, 1, 20f));

    [Test]
    public static void BarFractionAppliesTheSign() =>
        Expect.AreEqual(0.5f, DetailedLegendRenderer.BarFraction(-5, -1, 10f));

    [Test]
    public static void BarFractionIsZeroWhenTheDenominatorIsZero()
    {
        Expect.AreEqual(0f, DetailedLegendRenderer.BarFraction(5, 1, 0f));
        Expect.AreEqual(0f, DetailedLegendRenderer.BarFraction(0, 1, 0f));
    }

    [Test]
    public static void BarFractionIsZeroWhenTheDenominatorIsNegative() =>
        Expect.AreEqual(0f, DetailedLegendRenderer.BarFraction(5, 1, -10f));

    [Test]
    public static void BarFractionNeverGoesNegative() =>
        Expect.AreEqual(0f, DetailedLegendRenderer.BarFraction(-4, 1, 10f));

    [Test]
    public static void BarFractionNeverExceedsAFullBar() =>
        Expect.AreEqual(1f, DetailedLegendRenderer.BarFraction(30, 1, 10f));

    // The field initialisers and the Scribe fallbacks must agree, or a renderer loaded from
    // XML that lacks a node behaves differently from a freshly constructed one.
    [Test]
    public static void LoadingAnEmptyNodeMatchesAFreshRenderer()
    {
        var fresh = new DetailedLegendRenderer();
        var loaded = new DetailedLegendRenderer();
        ScribeRoundTrip.Load(EmptyDocument, loaded);

        Expect.AreEqual(fresh.DrawCounts, loaded.DrawCounts, "DrawCounts");
        Expect.AreEqual(fresh.DrawIcons, loaded.DrawIcons, "DrawIcons");
        Expect.AreEqual(fresh.DrawInfoInBar, loaded.DrawInfoInBar, "DrawInfoInBar");
        Expect.AreEqual(fresh.DrawMaxMarkers, loaded.DrawMaxMarkers, "DrawMaxMarkers");
        Expect.AreEqual(fresh.MaxPerChapter, loaded.MaxPerChapter, "MaxPerChapter");
    }

    [Test]
    public static void EveryFlagSurvivesARoundTrip()
    {
        var saved = new DetailedLegendRenderer
        {
            DrawCounts = false,
            DrawIcons = false,
            DrawInfoInBar = true,
            DrawMaxMarkers = true,
            MaxPerChapter = true,
        };
        var loaded = new DetailedLegendRenderer();

        ScribeRoundTrip.Run(saved, loaded);

        Expect.IsFalse(loaded.DrawCounts, "DrawCounts");
        Expect.IsFalse(loaded.DrawIcons, "DrawIcons");
        Expect.IsTrue(loaded.DrawInfoInBar, "DrawInfoInBar");
        Expect.IsTrue(loaded.DrawMaxMarkers, "DrawMaxMarkers");
        Expect.IsTrue(loaded.MaxPerChapter, "MaxPerChapter");
    }
}
