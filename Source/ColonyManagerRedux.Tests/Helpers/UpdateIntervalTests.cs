// UpdateIntervalTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;
using static ColonyManagerRedux.UpdateInterval;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class UpdateIntervalTests
{
    private static IntervalDisplay Display(
        int intervalHours,
        float sinceLastUpdateHours = 0,
        bool exporting = false,
        bool forcePending = false,
        bool hasBeenUpdated = true,
        bool suspended = false
    ) =>
        ComputeIntervalDisplay(
            intervalHours * GenDate.TicksPerHour,
            (int)(sinceLastUpdateHours * GenDate.TicksPerHour),
            exporting,
            forcePending,
            hasBeenUpdated,
            suspended
        );

    [Test]
    public static void ClockIsUsedBelowTwelveHours() => Expect.IsTrue(Display(11, 1).UseClock);

    [Test]
    public static void CalendarIsUsedFromExactlyTwelveHours() =>
        Expect.IsFalse(Display(12, 1).UseClock);

    [Test]
    public static void CalendarIsUsedWhenProgressReachesTwelveHours()
    {
        Expect.IsTrue(Display(6, 11.9f).UseClock);
        Expect.IsFalse(Display(6, 12).UseClock);
    }

    [Test]
    public static void OnScheduleProgressIsWhite() =>
        Expect.AreEqual(Color.white, Display(6, 3).ProgressColor);

    [Test]
    public static void ProgressExactlyAtTheIntervalIsPureWhite()
    {
        var display = Display(6, 6);

        Expect.AreEqual(Color.white, display.ProgressColor);
    }

    [Test]
    public static void HalfwayOverdueIsHalfwayToRed()
    {
        // (progress - next) / next * 2 == 0.5 for progress == 1.25 * next.
        var display = Display(8, 10);

        Expect.AreEqual(Color.Lerp(Color.white, Color.red, 0.5f), display.ProgressColor);
    }

    [Test]
    public static void TwiceOverdueIsFullyRed() =>
        Expect.AreEqual(Color.red, Display(4, 12).ProgressColor);

    [Test]
    public static void FarBeyondOverdueStaysRed() =>
        Expect.AreEqual(Color.red, Display(2, 20).ProgressColor);

    [Test]
    public static void SuspendedIsGrayRegardlessOfOverdue()
    {
        Expect.AreEqual(Color.gray, Display(4, 2, suspended: true).ProgressColor);
        Expect.AreEqual(Color.gray, Display(4, 20, suspended: true).ProgressColor);
    }

    // A custom update interval of zero ticks can be added in the settings; dividing by it made
    // the colour NaN.
    [Test]
    public static void ZeroLengthIntervalIsFullyRedInsteadOfNaN()
    {
        var display = ComputeIntervalDisplay(0, 100, false, false, true, false);

        Expect.AreEqual(Color.red, display.ProgressColor);
        Expect.IsFalse(float.IsNaN(display.ProgressColor.r));
    }

    [Test]
    public static void ExportingIgnoresProgress()
    {
        var display = Display(6, 20, exporting: true);

        Expect.AreEqual(IntervalDisplayState.Exporting, display.State);
        Expect.AreEqual(0f, display.ProgressHours);
        Expect.AreEqual(Color.white, display.ProgressColor);
        Expect.IsTrue(display.UseClock);
    }

    [Test]
    public static void ExportingUsesCalendarForLongIntervals() =>
        Expect.IsFalse(Display(24, exporting: true).UseClock);

    [Test]
    public static void ExportingTakesPriorityOverForceUpdatePending() =>
        Expect.AreEqual(
            IntervalDisplayState.Exporting,
            Display(6, exporting: true, forcePending: true).State
        );

    [Test]
    public static void ForceUpdatePendingTakesPriorityOverHavingBeenUpdated() =>
        Expect.AreEqual(
            IntervalDisplayState.ForceUpdatePending,
            Display(6, 3, forcePending: true, hasBeenUpdated: true).State
        );

    [Test]
    public static void NeverUpdatedJobShowsNoProgress()
    {
        var display = Display(6, 3, hasBeenUpdated: false);

        Expect.AreEqual(IntervalDisplayState.NeverUpdated, display.State);
        Expect.AreEqual(0f, display.ProgressHours);
    }

    [Test]
    public static void UpdatedJobShowsProgress()
    {
        var display = Display(6, 3);

        Expect.AreEqual(IntervalDisplayState.Progress, display.State);
        Expect.AreEqual(3f, display.ProgressHours);
        Expect.AreEqual(6f, display.NextUpdateHours);
    }

    [TestCase(false, false, ExpectedResult = true)]
    [TestCase(true, false, ExpectedResult = false)]
    [TestCase(false, true, ExpectedResult = false)]
    [TestCase(true, true, ExpectedResult = false)]
    public static bool ForceUpdateIsOnlyOfferedForIdleActiveJobs(
        bool suspended,
        bool shouldDoNow
    ) => CanForceUpdate(suspended, shouldDoNow);
}
