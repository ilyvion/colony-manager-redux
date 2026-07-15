// ManagerJobProductionTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;
using static ColonyManagerRedux.Managers.ManagerJob_Production;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class ManagerJobProductionTests
{
    [Test]
    public static void NoManagedBillAndInactiveTriggerDoesNothing() =>
        Assert
            .That(
                DecideBillAction(
                    hasManagedBill: false,
                    managedBillSuspended: false,
                    triggerActive: false
                )
            )
            .Is.EqualTo(ProductionBillDecision.None);

    [Test]
    public static void NoManagedBillAndActiveTriggerCreatesNewBill() =>
        Assert
            .That(
                DecideBillAction(
                    hasManagedBill: false,
                    managedBillSuspended: false,
                    triggerActive: true
                )
            )
            .Is.EqualTo(ProductionBillDecision.CreateNew);

    [Test]
    public static void SuspendedManagedBillWithActiveTriggerIsActivated() =>
        Assert
            .That(
                DecideBillAction(
                    hasManagedBill: true,
                    managedBillSuspended: true,
                    triggerActive: true
                )
            )
            .Is.EqualTo(ProductionBillDecision.Activate);

    [Test]
    public static void ActiveManagedBillWithActiveTriggerIsLeftAlone() =>
        Assert
            .That(
                DecideBillAction(
                    hasManagedBill: true,
                    managedBillSuspended: false,
                    triggerActive: true
                )
            )
            .Is.EqualTo(ProductionBillDecision.None);

    [Test]
    public static void ActiveManagedBillWithInactiveTriggerIsSuspended() =>
        Assert
            .That(
                DecideBillAction(
                    hasManagedBill: true,
                    managedBillSuspended: false,
                    triggerActive: false
                )
            )
            .Is.EqualTo(ProductionBillDecision.Suspend);

    [Test]
    public static void SuspendedManagedBillWithInactiveTriggerIsLeftAlone() =>
        Assert
            .That(
                DecideBillAction(
                    hasManagedBill: true,
                    managedBillSuspended: true,
                    triggerActive: false
                )
            )
            .Is.EqualTo(ProductionBillDecision.None);
}
