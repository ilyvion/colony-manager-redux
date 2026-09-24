// AlertsTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class AlertsTests
{
    [Test]
    public static void MostOutdatedTicksIsZeroForEmptySequence() =>
        Expect.AreEqual(0, Alert_JobsNotUpdating.MostOutdatedTicks([]));

    [Test]
    public static void MostOutdatedTicksIsZeroWhenAllJobsSuspended() =>
        Expect.AreEqual(
            0,
            Alert_JobsNotUpdating.MostOutdatedTicks([(true, true, 500), (true, true, 1000)])
        );

    [Test]
    public static void MostOutdatedTicksExcludesSuspendedJobsFromTheMax() =>
        // A job with a larger backlog must not win if it's suspended.
        Expect.AreEqual(
            500,
            Alert_JobsNotUpdating.MostOutdatedTicks([(true, true, 9000), (false, true, 500)])
        );

    [Test]
    public static void MostOutdatedTicksExcludesJobsThatAreNotDueYet() =>
        // A job that's due but suspended, or suspended-but-not-due, must both be excluded; only
        // active-and-due jobs (!IsSuspended && ShouldDoNow) contribute to the max.
        Expect.AreEqual(
            500,
            Alert_JobsNotUpdating.MostOutdatedTicks([(false, false, 9000), (false, true, 500)])
        );

    [Test]
    public static void MostOutdatedTicksPicksMaxAmongActiveAndDueJobs() =>
        Expect.AreEqual(
            1500,
            Alert_JobsNotUpdating.MostOutdatedTicks([
                (false, true, 500),
                (false, true, 1500),
                (false, true, 900),
            ])
        );

    [Test]
    public static void ClassifyOutdatedPriorityIsMediumWellUnderThresholds() =>
        Expect.AreEqual(
            AlertPriority.Medium,
            Alert_JobsNotUpdating.ClassifyOutdatedPriority(0, 10f, 5f)
        );

    [Test]
    public static void ClassifyOutdatedPriorityIsHighAtTheHighBoundary() =>
        Expect.AreEqual(
            AlertPriority.High,
            Alert_JobsNotUpdating.ClassifyOutdatedPriority((int)(GenDate.TicksPerDay * 5f), 10f, 5f)
        );

    [Test]
    public static void ClassifyOutdatedPriorityIsMediumJustUnderTheHighBoundary() =>
        Expect.AreEqual(
            AlertPriority.Medium,
            Alert_JobsNotUpdating.ClassifyOutdatedPriority(
                (int)(GenDate.TicksPerDay * 5f) - 1,
                10f,
                5f
            )
        );

    [Test]
    public static void ClassifyOutdatedPriorityIsCriticalAtTheCriticalBoundary() =>
        Expect.AreEqual(
            AlertPriority.Critical,
            Alert_JobsNotUpdating.ClassifyOutdatedPriority(
                (int)(GenDate.TicksPerDay * 10f),
                10f,
                5f
            )
        );

    [Test]
    public static void ClassifyOutdatedPriorityIsHighJustUnderTheCriticalBoundary() =>
        Expect.AreEqual(
            AlertPriority.High,
            Alert_JobsNotUpdating.ClassifyOutdatedPriority(
                (int)(GenDate.TicksPerDay * 10f) - 1,
                10f,
                5f
            )
        );

    [Test]
    public static void ClassifyOutdatedPriorityChecksCriticalFirstEvenIfHighExceedsCritical() =>
        // The method doesn't itself enforce the high <= critical ordering invariant (that's
        // Settings.ClampAlertTiers's job) - verify classification still checks critical first, so
        // it stays sane even if that invariant were ever violated (e.g. a hand-edited save).
        Expect.AreEqual(
            AlertPriority.Critical,
            Alert_JobsNotUpdating.ClassifyOutdatedPriority(
                (int)(GenDate.TicksPerDay * 20f),
                10f,
                15f
            )
        );

    [Test]
    public static void AnyUnsuspendedJobsIsFalseForEmptySequence() =>
        Expect.IsFalse(Alert_NoManager.AnyUnsuspendedJobs([]));

    [Test]
    public static void AnyUnsuspendedJobsIsFalseWhenAllJobsSuspended() =>
        // A colony that only has a suspended job (e.g. an auto-created ManagerJob_Power the
        // player never asked for) shouldn't be nagged to build a manager desk or hire a manager
        // for it - see GitHub issue #18.
        Expect.IsFalse(Alert_NoManager.AnyUnsuspendedJobs([true, true]));

    [Test]
    public static void AnyUnsuspendedJobsIsTrueWhenAnyJobIsNotSuspended() =>
        Expect.IsTrue(Alert_NoManager.AnyUnsuspendedJobs([true, false, true]));

    [Test]
    public static void AutoSlaughterConfigWithAllSentinelsIsInactive() =>
        Expect.IsFalse(Alert_AutoslaughterOverlap.IsAutoSlaughterConfigActive(-1, -1, -1, -1, -1));

    [Test]
    public static void AutoSlaughterConfigWithAnySingleNonSentinelFieldIsActive()
    {
        Expect.IsTrue(Alert_AutoslaughterOverlap.IsAutoSlaughterConfigActive(0, -1, -1, -1, -1));
        Expect.IsTrue(Alert_AutoslaughterOverlap.IsAutoSlaughterConfigActive(-1, 0, -1, -1, -1));
        Expect.IsTrue(Alert_AutoslaughterOverlap.IsAutoSlaughterConfigActive(-1, -1, 0, -1, -1));
        Expect.IsTrue(Alert_AutoslaughterOverlap.IsAutoSlaughterConfigActive(-1, -1, -1, 0, -1));
        Expect.IsTrue(Alert_AutoslaughterOverlap.IsAutoSlaughterConfigActive(-1, -1, -1, -1, 0));
    }

    [Test]
    public static void AutoSlaughterConfigWithAllZeroesIsActive() =>
        // 0 is a valid, real "slaughter everything over 0" threshold, not the -1 "off" sentinel
        // - easily confused with "off" if the sentinel check were ever changed to `<= 0`.
        Expect.IsTrue(Alert_AutoslaughterOverlap.IsAutoSlaughterConfigActive(0, 0, 0, 0, 0));
}
