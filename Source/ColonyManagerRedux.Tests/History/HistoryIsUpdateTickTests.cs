// HistoryIsUpdateTickTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class HistoryIsUpdateTickTests
{
    private static readonly int dayInterval = History.PeriodTickInterval(Period.Day);
    private static readonly int monthInterval = History.PeriodTickInterval(Period.Month);
    private static readonly int yearInterval = History.PeriodTickInterval(Period.Year);

    [Test]
    public static void TickZeroIsAnUpdateTickForEveryPeriod() =>
        Assert.That(History.IsUpdateTickFor(0)).Is.True();

    [Test]
    public static void TickThatOnlyAlignsWithDayIntervalIsStillAnUpdateTick()
    {
        // dayInterval isn't a multiple of monthInterval/yearInterval, so this exercises the
        // "Any", not "All" semantics: a single aligned period is enough.
        Assert.That(dayInterval % monthInterval).Is.Not.EqualTo(0);
        Assert.That(dayInterval % yearInterval).Is.Not.EqualTo(0);

        Assert.That(History.IsUpdateTickFor(dayInterval)).Is.True();
    }

    [Test]
    public static void TickThatAlignsWithMonthIntervalIsAnUpdateTick() =>
        Assert.That(History.IsUpdateTickFor(monthInterval)).Is.True();

    [Test]
    public static void TickThatAlignsWithNoPeriodIsNotAnUpdateTick()
    {
        var ticksGame = dayInterval + 1;
        Assert.That(ticksGame % dayInterval).Is.Not.EqualTo(0);
        Assert.That(ticksGame % monthInterval).Is.Not.EqualTo(0);
        Assert.That(ticksGame % yearInterval).Is.Not.EqualTo(0);

        Assert.That(History.IsUpdateTickFor(ticksGame)).Is.False();
    }
}
