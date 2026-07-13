// JobDriverManagingAtManagingStationTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class JobDriverManagingAtManagingStationTests
{
    [Test]
    public static void ShouldStartExecutePhaseWaitsWhenGatherStillRunning() =>
        Assert
            .That(
                JobDriver_ManagingAtManagingStation.ShouldStartExecutePhase(
                    gatherCompleted: false,
                    workDone: 100,
                    workNeeded: 100
                )
            )
            .Is.False();

    [Test]
    public static void ShouldStartExecutePhaseWaitsWhenGatherDoneButBefore95Percent() =>
        Assert
            .That(
                JobDriver_ManagingAtManagingStation.ShouldStartExecutePhase(
                    gatherCompleted: true,
                    workDone: 50,
                    workNeeded: 100
                )
            )
            .Is.False();

    [Test]
    public static void ShouldStartExecutePhaseStartsOnceGatherDoneAnd95PercentReached() =>
        Assert
            .That(
                JobDriver_ManagingAtManagingStation.ShouldStartExecutePhase(
                    gatherCompleted: true,
                    workDone: 95,
                    workNeeded: 100
                )
            )
            .Is.True();

    [Test]
    public static void ShouldStartExecutePhaseStartsWhenGatherTookLongerThan95Percent() =>
        // Gathering finishing late shouldn't hold execution back any further than the 95%
        // floor; once workDone catches up to (or passes) that mark, execution may begin.
        Assert
            .That(
                JobDriver_ManagingAtManagingStation.ShouldStartExecutePhase(
                    gatherCompleted: true,
                    workDone: 100,
                    workNeeded: 100
                )
            )
            .Is.True();

    [Test]
    public static void AdvanceWorkDoneWhileWaitingAdvancesNormallyBelowCap() =>
        Assert
            .That(
                JobDriver_ManagingAtManagingStation.AdvanceWorkDoneWhileWaiting(
                    workDone: 10,
                    managingSpeed: 5,
                    workNeeded: 100
                )
            )
            .Is.EqualTo(15f);

    [Test]
    public static void AdvanceWorkDoneWhileWaitingClampsAt95Percent() =>
        // Even though 93 + 5 = 98, the display shouldn't run past the 95% gather-phase
        // ceiling while still waiting for the execute phase to be allowed to start.
        Assert
            .That(
                JobDriver_ManagingAtManagingStation.AdvanceWorkDoneWhileWaiting(
                    workDone: 93,
                    managingSpeed: 5,
                    workNeeded: 100
                )
            )
            .Is.EqualTo(95f);

    [Test]
    public static void ComputeCatchUpSkillGainIsZeroWhenAlreadyAtFullWorkDone() =>
        Assert
            .That(
                JobDriver_ManagingAtManagingStation.ComputeCatchUpSkillGain(
                    workDone: 100,
                    workNeeded: 100
                )
            )
            .Is.EqualTo(0f);

    [Test]
    public static void ComputeCatchUpSkillGainCoversSkippedRemainderAtStandardRate() =>
        // The job finished with 5 units of workNeeded left unspent; the pawn should still
        // get the 0.11-per-unit skill gain that timer would have granted had it run out.
        Assert
            .That(
                JobDriver_ManagingAtManagingStation.ComputeCatchUpSkillGain(
                    workDone: 95,
                    workNeeded: 100
                )
            )
            .Is.EqualTo(0.55f);
}
