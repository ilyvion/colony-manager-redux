// TriggerThresholdTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class TriggerThresholdTests
{
    [Test]
    public static void LowerThanPassesWhenCountBelowTarget() =>
        Assert.That(Trigger_Threshold.Evaluate(Trigger_Threshold.Ops.LowerThan, 5, 10)).Is.False();

    [Test]
    public static void LowerThanFailsWhenCountAtTarget() =>
        Assert.That(Trigger_Threshold.Evaluate(Trigger_Threshold.Ops.LowerThan, 10, 10)).Is.True();

    [Test]
    public static void LowerThanFailsWhenCountAboveTarget() =>
        Assert.That(Trigger_Threshold.Evaluate(Trigger_Threshold.Ops.LowerThan, 15, 10)).Is.True();

    [Test]
    public static void EqualsPassesWhenCountMatchesTarget() =>
        Assert.That(Trigger_Threshold.Evaluate(Trigger_Threshold.Ops.Equals, 10, 10)).Is.True();

    [Test]
    public static void EqualsFailsWhenCountDiffersFromTarget() =>
        Assert.That(Trigger_Threshold.Evaluate(Trigger_Threshold.Ops.Equals, 9, 10)).Is.False();

    [Test]
    public static void HigherThanPassesWhenCountAboveTarget() =>
        Assert
            .That(Trigger_Threshold.Evaluate(Trigger_Threshold.Ops.HigherThan, 15, 10))
            .Is.False();

    [Test]
    public static void HigherThanFailsWhenCountAtTarget() =>
        Assert.That(Trigger_Threshold.Evaluate(Trigger_Threshold.Ops.HigherThan, 10, 10)).Is.True();

    [Test]
    public static void HigherThanFailsWhenCountBelowTarget() =>
        Assert.That(Trigger_Threshold.Evaluate(Trigger_Threshold.Ops.HigherThan, 5, 10)).Is.True();

    [Test]
    public static void NotEqualsPassesWhenCountDiffersFromTarget() =>
        Assert.That(Trigger_Threshold.Evaluate(Trigger_Threshold.Ops.NotEquals, 9, 10)).Is.True();

    [Test]
    public static void NotEqualsFailsWhenCountMatchesTarget() =>
        Assert.That(Trigger_Threshold.Evaluate(Trigger_Threshold.Ops.NotEquals, 10, 10)).Is.False();

    [Test]
    public static void UnrecognizedOperatorReturnsNull() =>
        Assert.That(Trigger_Threshold.Evaluate((Trigger_Threshold.Ops)999, 10, 10)).Is.Null();

    [Test]
    public static void LowerThanDirectiveIsIncreaseWhenCountBelowTarget() =>
        Assert
            .That(Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.LowerThan, 5, 10))
            .Is.EqualTo(Trigger_Threshold.Directive.Increase);

    [Test]
    public static void LowerThanDirectiveIsHoldWhenCountAtTarget() =>
        Assert
            .That(Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.LowerThan, 10, 10))
            .Is.EqualTo(Trigger_Threshold.Directive.Hold);

    [Test]
    public static void LowerThanDirectiveIsHoldWhenCountAboveTarget() =>
        Assert
            .That(Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.LowerThan, 15, 10))
            .Is.EqualTo(Trigger_Threshold.Directive.Hold);

    [Test]
    public static void EqualsDirectiveIsIncreaseWhenCountBelowTarget() =>
        Assert
            .That(Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.Equals, 9, 10))
            .Is.EqualTo(Trigger_Threshold.Directive.Increase);

    [Test]
    public static void EqualsDirectiveIsHoldWhenCountAtTarget() =>
        Assert
            .That(Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.Equals, 10, 10))
            .Is.EqualTo(Trigger_Threshold.Directive.Hold);

    [Test]
    public static void EqualsDirectiveIsDecreaseWhenCountAboveTarget() =>
        Assert
            .That(Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.Equals, 11, 10))
            .Is.EqualTo(Trigger_Threshold.Directive.Decrease);

    [Test]
    public static void HigherThanDirectiveIsHoldWhenCountBelowTarget() =>
        Assert
            .That(Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.HigherThan, 5, 10))
            .Is.EqualTo(Trigger_Threshold.Directive.Hold);

    [Test]
    public static void HigherThanDirectiveIsHoldWhenCountAtTarget() =>
        Assert
            .That(Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.HigherThan, 10, 10))
            .Is.EqualTo(Trigger_Threshold.Directive.Hold);

    [Test]
    public static void HigherThanDirectiveIsDecreaseWhenCountAboveTarget() =>
        Assert
            .That(Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.HigherThan, 15, 10))
            .Is.EqualTo(Trigger_Threshold.Directive.Decrease);

    [Test]
    public static void NotEqualsDirectiveIsHoldWhenCountBelowTarget() =>
        Assert
            .That(Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.NotEquals, 9, 10))
            .Is.EqualTo(Trigger_Threshold.Directive.Hold);

    [Test]
    public static void NotEqualsDirectiveIsIncreaseWhenCountAtTarget() =>
        Assert
            .That(Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.NotEquals, 10, 10))
            .Is.EqualTo(Trigger_Threshold.Directive.Increase);

    [Test]
    public static void NotEqualsDirectiveIsHoldWhenCountAboveTarget() =>
        Assert
            .That(Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.NotEquals, 11, 10))
            .Is.EqualTo(Trigger_Threshold.Directive.Hold);

    [Test]
    public static void UnrecognizedOperatorDirectiveReturnsNull() =>
        Assert
            .That(Trigger_Threshold.EvaluateDirective((Trigger_Threshold.Ops)999, 10, 10))
            .Is.Null();

    [Test]
    public static void MigrateUnsupportedOpKeepsSupportedOp() =>
        Assert
            .That(
                Trigger_Threshold.MigrateUnsupportedOp(
                    Trigger_Threshold.Ops.Equals,
                    Trigger_Threshold.AccumulationOnlyOps,
                    Trigger_Threshold.Ops.LowerThan
                )
            )
            .Is.EqualTo(Trigger_Threshold.Ops.Equals);

    [Test]
    public static void MigrateUnsupportedOpFallsBackToGivenFallback() =>
        Assert
            .That(
                Trigger_Threshold.MigrateUnsupportedOp(
                    Trigger_Threshold.Ops.HigherThan,
                    Trigger_Threshold.AccumulationOnlyOps,
                    Trigger_Threshold.Ops.LowerThan
                )
            )
            .Is.EqualTo(Trigger_Threshold.Ops.LowerThan);

    // Regression guard: TargetCount's setter used to accept negative values unchanged, letting a
    // typed "-5" in the manager tab's target-count field flow straight into shortfall/scheduling
    // math that assumes a non-negative count.
    [Test]
    public static void ClampTargetCountClampsNegativeValueToZero() =>
        Assert.That(Trigger_Threshold.ClampTargetCount(-5)).Is.EqualTo(0);

    [Test]
    public static void ClampTargetCountLeavesZeroUnchanged() =>
        Assert.That(Trigger_Threshold.ClampTargetCount(0)).Is.EqualTo(0);

    [Test]
    public static void ClampTargetCountLeavesPositiveValueUnchanged() =>
        Assert.That(Trigger_Threshold.ClampTargetCount(42)).Is.EqualTo(42);
}
