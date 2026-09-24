// ManagerJobMiningPhaseTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;
using static ColonyManagerRedux.Managers.ManagerJob_Mining;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ManagerJobMiningPhaseTests
{
    [Test]
    public static void FirstPassWithQuotaMetCompletes()
    {
        Expect.AreEqual(
            InactivePhase.Complete,
            DecideInactivePhase(ManagerJobState.Active, controlDeepDrills: false)
        );
        Expect.AreEqual(
            InactivePhase.Complete,
            DecideInactivePhase(ManagerJobState.Active, controlDeepDrills: true)
        );
    }

    // Regression guard (#35): deep drills stayed unmanaged once the quota was met.
    [Test]
    public static void CompletedJobWithDrillControlRechecksDrills() =>
        Expect.AreEqual(
            InactivePhase.RecheckDrills,
            DecideInactivePhase(ManagerJobState.Completed, controlDeepDrills: true)
        );

    [Test]
    public static void CompletedJobWithoutDrillControlDoesNothing() =>
        Expect.AreEqual(
            InactivePhase.Nothing,
            DecideInactivePhase(ManagerJobState.Completed, controlDeepDrills: false)
        );

    [Test]
    public static void HoldAndDecreaseReduce()
    {
        Expect.AreEqual(
            ActivePhase.Reduce,
            DecideActivePhase(Trigger_Threshold.Directive.Hold, false, true, true)
        );
        Expect.AreEqual(
            ActivePhase.Reduce,
            DecideActivePhase(Trigger_Threshold.Directive.Decrease, false, true, true)
        );
    }

    [Test]
    public static void IncreaseWithDesignationCapExceededReduces() =>
        Expect.AreEqual(
            ActivePhase.Reduce,
            DecideActivePhase(Trigger_Threshold.Directive.Increase, true, true, true)
        );

    [Test]
    public static void IncreaseWithNoTasksEnabledAddsNothing() =>
        Expect.AreEqual(
            ActivePhase.AddNothingEnabled,
            DecideActivePhase(Trigger_Threshold.Directive.Increase, false, false, true)
        );

    [Test]
    public static void IncreaseThatCannotAddMoreSaysSo() =>
        Expect.AreEqual(
            ActivePhase.AddCannotAddMore,
            DecideActivePhase(Trigger_Threshold.Directive.Increase, false, true, false)
        );

    [Test]
    public static void IncreaseWithRoomAdds() =>
        Expect.AreEqual(
            ActivePhase.Add,
            DecideActivePhase(Trigger_Threshold.Directive.Increase, false, true, true)
        );
}
