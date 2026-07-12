// AlertsTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class AlertsTests
{
    [Test]
    public static void MostOutdatedTicksIsZeroForEmptySequence() =>
        Assert.That(Alert_JobsNotUpdating.MostOutdatedTicks([])).Is.EqualTo(0);

    [Test]
    public static void MostOutdatedTicksIsZeroWhenAllJobsSuspended() =>
        Assert
            .That(Alert_JobsNotUpdating.MostOutdatedTicks([(true, true, 500), (true, true, 1000)]))
            .Is.EqualTo(0);

    [Test]
    public static void MostOutdatedTicksExcludesSuspendedJobsFromTheMax() =>
        // A job with a larger backlog must not win if it's suspended.
        Assert
            .That(Alert_JobsNotUpdating.MostOutdatedTicks([(true, true, 9000), (false, true, 500)]))
            .Is.EqualTo(500);

    [Test]
    public static void MostOutdatedTicksExcludesJobsThatAreNotDueYet() =>
        // A job that's due but suspended, or suspended-but-not-due, must both be excluded; only
        // active-and-due jobs (!IsSuspended && ShouldDoNow) contribute to the max.
        Assert
            .That(
                Alert_JobsNotUpdating.MostOutdatedTicks([(false, false, 9000), (false, true, 500)])
            )
            .Is.EqualTo(500);

    [Test]
    public static void MostOutdatedTicksPicksMaxAmongActiveAndDueJobs() =>
        Assert
            .That(
                Alert_JobsNotUpdating.MostOutdatedTicks([
                    (false, true, 500),
                    (false, true, 1500),
                    (false, true, 900),
                ])
            )
            .Is.EqualTo(1500);

    [Test]
    public static void ClassifyOutdatedPriorityIsMediumWellUnderThresholds() =>
        Assert
            .That(Alert_JobsNotUpdating.ClassifyOutdatedPriority(0, 10f, 5f))
            .Is.EqualTo(AlertPriority.Medium);

    [Test]
    public static void ClassifyOutdatedPriorityIsHighAtTheHighBoundary() =>
        Assert
            .That(
                Alert_JobsNotUpdating.ClassifyOutdatedPriority(
                    (int)(GenDate.TicksPerDay * 5f),
                    10f,
                    5f
                )
            )
            .Is.EqualTo(AlertPriority.High);

    [Test]
    public static void ClassifyOutdatedPriorityIsMediumJustUnderTheHighBoundary() =>
        Assert
            .That(
                Alert_JobsNotUpdating.ClassifyOutdatedPriority(
                    (int)(GenDate.TicksPerDay * 5f) - 1,
                    10f,
                    5f
                )
            )
            .Is.EqualTo(AlertPriority.Medium);

    [Test]
    public static void ClassifyOutdatedPriorityIsCriticalAtTheCriticalBoundary() =>
        Assert
            .That(
                Alert_JobsNotUpdating.ClassifyOutdatedPriority(
                    (int)(GenDate.TicksPerDay * 10f),
                    10f,
                    5f
                )
            )
            .Is.EqualTo(AlertPriority.Critical);

    [Test]
    public static void ClassifyOutdatedPriorityIsHighJustUnderTheCriticalBoundary() =>
        Assert
            .That(
                Alert_JobsNotUpdating.ClassifyOutdatedPriority(
                    (int)(GenDate.TicksPerDay * 10f) - 1,
                    10f,
                    5f
                )
            )
            .Is.EqualTo(AlertPriority.High);

    [Test]
    public static void ClassifyOutdatedPriorityChecksCriticalFirstEvenIfHighExceedsCritical() =>
        // The method doesn't itself enforce the high <= critical ordering invariant (that's
        // Settings.ClampAlertTiers's job) - verify classification still checks critical first, so
        // it stays sane even if that invariant were ever violated (e.g. a hand-edited save).
        Assert
            .That(
                Alert_JobsNotUpdating.ClassifyOutdatedPriority(
                    (int)(GenDate.TicksPerDay * 20f),
                    10f,
                    15f
                )
            )
            .Is.EqualTo(AlertPriority.Critical);

    [Test]
    public static void AnyUnsuspendedJobsIsFalseForEmptySequence() =>
        Assert.That(Alert_NoManager.AnyUnsuspendedJobs([])).Is.False();

    [Test]
    public static void AnyUnsuspendedJobsIsFalseWhenAllJobsSuspended() =>
        // A colony that only has a suspended job (e.g. an auto-created ManagerJob_Power the
        // player never asked for) shouldn't be nagged to build a manager desk or hire a manager
        // for it - see GitHub issue #18.
        Assert.That(Alert_NoManager.AnyUnsuspendedJobs([true, true])).Is.False();

    [Test]
    public static void AnyUnsuspendedJobsIsTrueWhenAnyJobIsNotSuspended() =>
        Assert.That(Alert_NoManager.AnyUnsuspendedJobs([true, false, true])).Is.True();

    [Test]
    public static void AutoSlaughterConfigWithAllSentinelsIsInactive() =>
        Assert
            .That(Alert_AutoslaughterOverlap.IsAutoSlaughterConfigActive(-1, -1, -1, -1, -1))
            .Is.False();

    [Test]
    public static void AutoSlaughterConfigWithAnySingleNonSentinelFieldIsActive()
    {
        Assert
            .That(Alert_AutoslaughterOverlap.IsAutoSlaughterConfigActive(0, -1, -1, -1, -1))
            .Is.True();
        Assert
            .That(Alert_AutoslaughterOverlap.IsAutoSlaughterConfigActive(-1, 0, -1, -1, -1))
            .Is.True();
        Assert
            .That(Alert_AutoslaughterOverlap.IsAutoSlaughterConfigActive(-1, -1, 0, -1, -1))
            .Is.True();
        Assert
            .That(Alert_AutoslaughterOverlap.IsAutoSlaughterConfigActive(-1, -1, -1, 0, -1))
            .Is.True();
        Assert
            .That(Alert_AutoslaughterOverlap.IsAutoSlaughterConfigActive(-1, -1, -1, -1, 0))
            .Is.True();
    }

    [Test]
    public static void AutoSlaughterConfigWithAllZeroesIsActive() =>
        // 0 is a valid, real "slaughter everything over 0" threshold, not the -1 "off" sentinel
        // - easily confused with "off" if the sentinel check were ever changed to `<= 0`.
        Assert
            .That(Alert_AutoslaughterOverlap.IsAutoSlaughterConfigActive(0, 0, 0, 0, 0))
            .Is.True();
}
