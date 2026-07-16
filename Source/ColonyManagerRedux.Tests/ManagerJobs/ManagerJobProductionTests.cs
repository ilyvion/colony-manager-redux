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

    [Test]
    public static void AllModeIsAlwaysInScope() =>
        Assert
            .That(
                IsWorkTableInScope(
                    WorkbenchAssignmentMode.All,
                    inArea: false,
                    isSpecificallySelected: false
                )
            )
            .Is.True();

    [Test]
    public static void AreaModeFollowsAreaMembership()
    {
        Assert
            .That(
                IsWorkTableInScope(
                    WorkbenchAssignmentMode.Area,
                    inArea: true,
                    isSpecificallySelected: false
                )
            )
            .Is.True();
        Assert
            .That(
                IsWorkTableInScope(
                    WorkbenchAssignmentMode.Area,
                    inArea: false,
                    isSpecificallySelected: true
                )
            )
            .Is.False();
    }

    [Test]
    public static void SpecificModeFollowsExplicitSelection()
    {
        Assert
            .That(
                IsWorkTableInScope(
                    WorkbenchAssignmentMode.Specific,
                    inArea: true,
                    isSpecificallySelected: false
                )
            )
            .Is.False();
        Assert
            .That(
                IsWorkTableInScope(
                    WorkbenchAssignmentMode.Specific,
                    inArea: false,
                    isSpecificallySelected: true
                )
            )
            .Is.True();
    }

    [Test]
    public static void MatchingSkillRangeNeedsNoUpdate() =>
        Assert.That(BillNeedsSkillRangeUpdate(new IntRange(0, 20), new IntRange(0, 20))).Is.False();

    [Test]
    public static void MismatchedSkillRangeNeedsUpdate() =>
        Assert.That(BillNeedsSkillRangeUpdate(new IntRange(0, 20), new IntRange(5, 15))).Is.True();

    [Test]
    public static void MatchingIngredientRadiusNeedsNoUpdate() =>
        Assert.That(BillNeedsIngredientRadiusUpdate(999f, 999f)).Is.False();

    [Test]
    public static void MismatchedIngredientRadiusNeedsUpdate() =>
        Assert.That(BillNeedsIngredientRadiusUpdate(999f, 12f)).Is.True();

    [Test]
    public static void MatchingStoreModeNeedsNoUpdate() =>
        Assert
            .That(
                BillNeedsStoreModeUpdate(
                    BillStoreModeDefOf.BestStockpile,
                    BillStoreModeDefOf.BestStockpile
                )
            )
            .Is.False();

    [Test]
    public static void MismatchedStoreModeNeedsUpdate() =>
        Assert
            .That(
                BillNeedsStoreModeUpdate(
                    BillStoreModeDefOf.DropOnFloor,
                    BillStoreModeDefOf.BestStockpile
                )
            )
            .Is.True();

    [Test]
    public static void MaintainStockModeOnlySupportsAccumulationOps() =>
        Assert
            .ThatCollection(SupportedOpsForMode(ProductionMode.MaintainStock))
            .Does.Not.Contain(Trigger_Threshold.Ops.HigherThan);

    [Test]
    public static void ConsumeSurplusModeSupportsAllOps() =>
        Assert
            .ThatCollection(SupportedOpsForMode(ProductionMode.ConsumeSurplus))
            .Does.Contain(Trigger_Threshold.Ops.HigherThan);
}
