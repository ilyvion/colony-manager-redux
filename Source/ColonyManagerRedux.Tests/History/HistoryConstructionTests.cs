// HistoryConstructionTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class HistoryConstructionTests
{
    private static HistoryLabel[] Labels(int count) =>
        [.. Enumerable.Range(0, count).Select(i => (HistoryLabel)new DirectHistoryLabel($"c{i}"))];

    [Test]
    public static void SingleLabelGetsTheDefaultLineColor()
    {
        var history = new History(Labels(1));

        Expect.AreEqual(History.DefaultLineColor, history._chapters[0].LineColor);
    }

    [Test]
    public static void MultipleLabelsGetDistinctColors()
    {
        var history = new History(Labels(4));

        Expect.AreEqual(4, history._chapters.Select(c => c.LineColor).Distinct().Count());
    }

    [Test]
    public static void SuppliedColorsWrapAroundWhenFewerThanLabels()
    {
        var history = new History(Labels(3), [Color.red, Color.blue]);

        Expect.AreEqual(Color.red, history._chapters[0].LineColor);
        Expect.AreEqual(Color.blue, history._chapters[1].LineColor);
        Expect.AreEqual(Color.red, history._chapters[2].LineColor);
    }

    // An empty colors array used to be indexed with `i % 0`, which threw DivideByZeroException.
    [Test]
    public static void EmptyColorsArrayFallsBackToTheDefaultColors()
    {
        var single = new History(Labels(1), []);
        var multiple = new History(Labels(3), []);

        Expect.AreEqual(History.DefaultLineColor, single._chapters[0].LineColor);
        Expect.AreEqual(3, multiple._chapters.Select(c => c.LineColor).Distinct().Count());
    }

    [Test]
    public static void EveryChapterIsShownByDefault()
    {
        var history = new History(Labels(3));

        Expect.AreEqual(3, history._chapters.Count);
        Expect.AreEqual(3, history._chaptersShown.Count);
    }

    [Test]
    public static void NullLabelsThrow() =>
        _ = Expect.Throws<ArgumentNullException>(() => _ = new History((HistoryLabel[])null!));

    [Test]
    public static void NullThingCountsThrow() =>
        _ = Expect.Throws<ArgumentNullException>(() => _ = new History((ThingDefCount[])null!));

    [Test]
    public static void PeriodTickIntervalsAreAHundredthOfThePeriod()
    {
        Expect.AreEqual(GenDate.TicksPerDay / 100, History.PeriodTickInterval(Period.Day));
        Expect.AreEqual(GenDate.TicksPerTwelfth / 100, History.PeriodTickInterval(Period.Month));
        Expect.AreEqual(GenDate.TicksPerYear / 100, History.PeriodTickInterval(Period.Year));
    }

    [Test]
    public static void UndefinedPeriodHasNoTickInterval() =>
        _ = Expect.Throws<NotImplementedException>(() => History.PeriodTickInterval((Period)99));
}
