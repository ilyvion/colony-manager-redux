// HistoryChapterSelectionTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class HistoryChapterSelectionTests
{
    private static readonly int interval = History.PeriodTickInterval(Period.Day);

    private static History.Chapter Chapter(string label, params int[] values)
    {
        var chapter = new History.Chapter(new DirectHistoryLabel(label), 10, Color.white);
        for (var i = 0; i < values.Length; i++)
        {
            chapter.Add(values[i], 0, i * interval);
        }
        return chapter;
    }

    private static List<string> Select(
        IEnumerable<History.Chapter> shown,
        IReadOnlyList<History.Chapter> all,
        bool positiveOnly = false,
        bool negativeOnly = false
    ) =>
        [
            .. History
                .SelectChaptersToDraw(shown, all, Period.Day, positiveOnly, negativeOnly)
                .Select(c => c.label.Label),
        ];

    [Test]
    public static void NoFilterKeepsEveryShownChapter()
    {
        var all = new[] { Chapter("a", 1), Chapter("b", -1), Chapter("c") };

        Expect.AreEqual("a,b,c", string.Join(",", Select(all, all)));
    }

    [Test]
    public static void PositiveOnlyDropsAllNegativeAndAllZeroChapters()
    {
        var all = new[] { Chapter("pos", 1, 2), Chapter("neg", -1, -2), Chapter("zero", 0, 0) };

        Expect.AreEqual("pos", string.Join(",", Select(all, all, positiveOnly: true)));
    }

    [Test]
    public static void NegativeOnlyDropsAllPositiveAndAllZeroChapters()
    {
        var all = new[] { Chapter("pos", 1, 2), Chapter("neg", -1, -2), Chapter("zero", 0, 0) };

        Expect.AreEqual("neg", string.Join(",", Select(all, all, negativeOnly: true)));
    }

    [Test]
    public static void MixedSignChapterCountsForBothFilters()
    {
        var all = new[] { Chapter("mixed", 3, -3) };

        Expect.AreEqual("mixed", string.Join(",", Select(all, all, positiveOnly: true)));
        Expect.AreEqual("mixed", string.Join(",", Select(all, all, negativeOnly: true)));
    }

    [Test]
    public static void OutputFollowsChapterOrderNotShownOrder()
    {
        var all = new[] { Chapter("a", 1), Chapter("b", 1), Chapter("c", 1) };
        var shown = new[] { all[2], all[0], all[1] };

        Expect.AreEqual("a,b,c", string.Join(",", Select(shown, all)));
    }

    [Test]
    public static void OnlyShownChaptersAreConsidered()
    {
        var all = new[] { Chapter("a", 1), Chapter("b", 1) };

        Expect.AreEqual("b", string.Join(",", Select([all[1]], all)));
    }

    // Pins the current behavior: a shown chapter that is no longer part of the history has no
    // position (-1) and so sorts before every chapter that does.
    [Test]
    public static void ShownChapterMissingFromTheHistorySortsFirst()
    {
        var all = new[] { Chapter("a", 1), Chapter("b", 1) };
        var stale = Chapter("stale", 1);

        Expect.AreEqual("stale,a,b", string.Join(",", Select([all[0], stale, all[1]], all)));
    }
}
