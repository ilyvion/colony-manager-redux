// HistoryIsUpdateTickTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class HistoryIsUpdateTickTests
{
    private static readonly int dayInterval = History.PeriodTickInterval(Period.Day);
    private static readonly int monthInterval = History.PeriodTickInterval(Period.Month);
    private static readonly int yearInterval = History.PeriodTickInterval(Period.Year);

    [Test]
    public static void TickZeroIsAnUpdateTickForEveryPeriod() =>
        Expect.IsTrue(History.IsUpdateTickFor(0));

    [Test]
    public static void TickThatOnlyAlignsWithDayIntervalIsStillAnUpdateTick()
    {
        // dayInterval isn't a multiple of monthInterval/yearInterval, so this exercises the
        // "Any", not "All" semantics: a single aligned period is enough.
        Expect.AreNotEqual(0, dayInterval % monthInterval);
        Expect.AreNotEqual(0, dayInterval % yearInterval);

        Expect.IsTrue(History.IsUpdateTickFor(dayInterval));
    }

    [Test]
    public static void TickThatAlignsWithMonthIntervalIsAnUpdateTick() =>
        Expect.IsTrue(History.IsUpdateTickFor(monthInterval));

    [Test]
    public static void TickThatAlignsWithNoPeriodIsNotAnUpdateTick()
    {
        var ticksGame = dayInterval + 1;
        Expect.AreNotEqual(0, ticksGame % dayInterval);
        Expect.AreNotEqual(0, ticksGame % monthInterval);
        Expect.AreNotEqual(0, ticksGame % yearInterval);

        Expect.IsFalse(History.IsUpdateTickFor(ticksGame));
    }
}
