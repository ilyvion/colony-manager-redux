// WorkGiverManageTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;
using static ColonyManagerRedux.WorkGiver_Manage;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class WorkGiverManageTests
{
    private static ManageBlockReason Evaluate(
        bool isStation = true,
        bool hasComp = true,
        bool aiManagerPowered = false,
        bool incapable = false,
        bool canReserveAndReach = true,
        bool? powerOn = true,
        int jobCount = 1,
        bool hasNextJob = true
    ) =>
        WorkGiver_Manage.Evaluate(
            isStation,
            () => hasComp,
            () => aiManagerPowered,
            incapable,
            () => canReserveAndReach,
            powerOn,
            () => jobCount,
            () => hasNextJob
        );

    [Test]
    public static void EverythingInOrderAllowsManaging() =>
        Expect.AreEqual(ManageBlockReason.None, Evaluate());

    [Test]
    public static void NonStationIsRejected() =>
        Expect.AreEqual(ManageBlockReason.NotAStation, Evaluate(isStation: false));

    [Test]
    public static void StationWithoutCompIsRejected() =>
        Expect.AreEqual(ManageBlockReason.NoStationComp, Evaluate(hasComp: false));

    [Test]
    public static void PoweredAIManagerBlocksManualManaging() =>
        Expect.AreEqual(ManageBlockReason.AIManagerPowered, Evaluate(aiManagerPowered: true));

    [Test]
    public static void IncapablePawnIsRejected() =>
        Expect.AreEqual(ManageBlockReason.PawnIncapable, Evaluate(incapable: true));

    [Test]
    public static void UnreachableStationIsRejected() =>
        Expect.AreEqual(
            ManageBlockReason.CannotReserveOrReach,
            Evaluate(canReserveAndReach: false)
        );

    [Test]
    public static void UnpoweredStationIsRejected() =>
        Expect.AreEqual(ManageBlockReason.NoPower, Evaluate(powerOn: false));

    [Test]
    public static void StationWithoutPowerCompNeedsNoPower() =>
        Expect.AreEqual(ManageBlockReason.None, Evaluate(powerOn: null));

    [Test]
    public static void NoJobsIsRejected() =>
        Expect.AreEqual(ManageBlockReason.NoJobs, Evaluate(jobCount: 0));

    [Test]
    public static void OnlySuspendedJobsIsRejected() =>
        Expect.AreEqual(ManageBlockReason.NoActiveJobs, Evaluate(hasNextJob: false));

    [Test]
    public static void ChecksApplyInTheDocumentedOrder()
    {
        // Every condition is failing; each removal of the current first reason must reveal the
        // next one down the ladder.
        Expect.AreEqual(
            ManageBlockReason.NotAStation,
            Evaluate(false, false, true, true, false, false, 0, false)
        );
        Expect.AreEqual(
            ManageBlockReason.NoStationComp,
            Evaluate(true, false, true, true, false, false, 0, false)
        );
        Expect.AreEqual(
            ManageBlockReason.AIManagerPowered,
            Evaluate(true, true, true, true, false, false, 0, false)
        );
        Expect.AreEqual(
            ManageBlockReason.PawnIncapable,
            Evaluate(true, true, false, true, false, false, 0, false)
        );
        Expect.AreEqual(
            ManageBlockReason.CannotReserveOrReach,
            Evaluate(true, true, false, false, false, false, 0, false)
        );
        Expect.AreEqual(
            ManageBlockReason.NoPower,
            Evaluate(true, true, false, false, true, false, 0, false)
        );
        Expect.AreEqual(
            ManageBlockReason.NoJobs,
            Evaluate(true, true, false, false, true, true, 0, false)
        );
    }

    [Test]
    public static void LaterChecksAreNotEvaluatedOnceOneFails()
    {
        var reachChecked = false;

        var reason = WorkGiver_Manage.Evaluate(
            true,
            () => true,
            () => false,
            true,
            () =>
            {
                reachChecked = true;
                return true;
            },
            true,
            () => 1,
            () => true
        );

        Expect.AreEqual(ManageBlockReason.PawnIncapable, reason);
        Expect.IsFalse(reachChecked, "The costly reachability check must be skipped.");
    }
}
