// TriggerThresholdTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class TriggerThresholdTests
{
    [Test]
    public static void LowerThanPassesWhenCountBelowTarget() =>
        Expect.AreEqual(false, Trigger_Threshold.Evaluate(Trigger_Threshold.Ops.LowerThan, 5, 10));

    [Test]
    public static void LowerThanFailsWhenCountAtTarget() =>
        Expect.AreEqual(true, Trigger_Threshold.Evaluate(Trigger_Threshold.Ops.LowerThan, 10, 10));

    [Test]
    public static void LowerThanFailsWhenCountAboveTarget() =>
        Expect.AreEqual(true, Trigger_Threshold.Evaluate(Trigger_Threshold.Ops.LowerThan, 15, 10));

    [Test]
    public static void EqualsPassesWhenCountMatchesTarget() =>
        Expect.AreEqual(true, Trigger_Threshold.Evaluate(Trigger_Threshold.Ops.Equals, 10, 10));

    [Test]
    public static void EqualsFailsWhenCountDiffersFromTarget() =>
        Expect.AreEqual(false, Trigger_Threshold.Evaluate(Trigger_Threshold.Ops.Equals, 9, 10));

    [Test]
    public static void HigherThanPassesWhenCountAboveTarget() =>
        Expect.AreEqual(
            false,
            Trigger_Threshold.Evaluate(Trigger_Threshold.Ops.HigherThan, 15, 10)
        );

    [Test]
    public static void HigherThanFailsWhenCountAtTarget() =>
        Expect.AreEqual(true, Trigger_Threshold.Evaluate(Trigger_Threshold.Ops.HigherThan, 10, 10));

    [Test]
    public static void HigherThanFailsWhenCountBelowTarget() =>
        Expect.AreEqual(true, Trigger_Threshold.Evaluate(Trigger_Threshold.Ops.HigherThan, 5, 10));

    [Test]
    public static void NotEqualsPassesWhenCountDiffersFromTarget() =>
        Expect.AreEqual(true, Trigger_Threshold.Evaluate(Trigger_Threshold.Ops.NotEquals, 9, 10));

    [Test]
    public static void NotEqualsFailsWhenCountMatchesTarget() =>
        Expect.AreEqual(false, Trigger_Threshold.Evaluate(Trigger_Threshold.Ops.NotEquals, 10, 10));

    [Test]
    public static void UnrecognizedOperatorReturnsNull() =>
        Expect.IsTrue(Trigger_Threshold.Evaluate((Trigger_Threshold.Ops)999, 10, 10) == null);

    [Test]
    public static void LowerThanDirectiveIsIncreaseWhenCountBelowTarget() =>
        Expect.AreEqual(
            Trigger_Threshold.Directive.Increase,
            Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.LowerThan, 5, 10)
        );

    [Test]
    public static void LowerThanDirectiveIsHoldWhenCountAtTarget() =>
        Expect.AreEqual(
            Trigger_Threshold.Directive.Hold,
            Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.LowerThan, 10, 10)
        );

    [Test]
    public static void LowerThanDirectiveIsHoldWhenCountAboveTarget() =>
        Expect.AreEqual(
            Trigger_Threshold.Directive.Hold,
            Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.LowerThan, 15, 10)
        );

    [Test]
    public static void EqualsDirectiveIsIncreaseWhenCountBelowTarget() =>
        Expect.AreEqual(
            Trigger_Threshold.Directive.Increase,
            Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.Equals, 9, 10)
        );

    [Test]
    public static void EqualsDirectiveIsHoldWhenCountAtTarget() =>
        Expect.AreEqual(
            Trigger_Threshold.Directive.Hold,
            Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.Equals, 10, 10)
        );

    [Test]
    public static void EqualsDirectiveIsDecreaseWhenCountAboveTarget() =>
        Expect.AreEqual(
            Trigger_Threshold.Directive.Decrease,
            Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.Equals, 11, 10)
        );

    [Test]
    public static void HigherThanDirectiveIsHoldWhenCountBelowTarget() =>
        Expect.AreEqual(
            Trigger_Threshold.Directive.Hold,
            Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.HigherThan, 5, 10)
        );

    [Test]
    public static void HigherThanDirectiveIsHoldWhenCountAtTarget() =>
        Expect.AreEqual(
            Trigger_Threshold.Directive.Hold,
            Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.HigherThan, 10, 10)
        );

    [Test]
    public static void HigherThanDirectiveIsDecreaseWhenCountAboveTarget() =>
        Expect.AreEqual(
            Trigger_Threshold.Directive.Decrease,
            Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.HigherThan, 15, 10)
        );

    [Test]
    public static void NotEqualsDirectiveIsHoldWhenCountBelowTarget() =>
        Expect.AreEqual(
            Trigger_Threshold.Directive.Hold,
            Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.NotEquals, 9, 10)
        );

    [Test]
    public static void NotEqualsDirectiveIsIncreaseWhenCountAtTarget() =>
        Expect.AreEqual(
            Trigger_Threshold.Directive.Increase,
            Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.NotEquals, 10, 10)
        );

    [Test]
    public static void NotEqualsDirectiveIsHoldWhenCountAboveTarget() =>
        Expect.AreEqual(
            Trigger_Threshold.Directive.Hold,
            Trigger_Threshold.EvaluateDirective(Trigger_Threshold.Ops.NotEquals, 11, 10)
        );

    [Test]
    public static void UnrecognizedOperatorDirectiveReturnsNull() =>
        Expect.IsTrue(
            Trigger_Threshold.EvaluateDirective((Trigger_Threshold.Ops)999, 10, 10) == null
        );

    [Test]
    public static void MigrateUnsupportedOpKeepsSupportedOp() =>
        Expect.AreEqual(
            Trigger_Threshold.Ops.Equals,
            Trigger_Threshold.MigrateUnsupportedOp(
                Trigger_Threshold.Ops.Equals,
                Trigger_Threshold.AccumulationOnlyOps,
                Trigger_Threshold.Ops.LowerThan
            )
        );

    [Test]
    public static void MigrateUnsupportedOpFallsBackToGivenFallback() =>
        Expect.AreEqual(
            Trigger_Threshold.Ops.LowerThan,
            Trigger_Threshold.MigrateUnsupportedOp(
                Trigger_Threshold.Ops.HigherThan,
                Trigger_Threshold.AccumulationOnlyOps,
                Trigger_Threshold.Ops.LowerThan
            )
        );

    // Regression guard: TargetCount's setter used to accept negative values unchanged, letting a
    // typed "-5" in the manager tab's target-count field flow straight into shortfall/scheduling
    // math that assumes a non-negative count.
    [Test]
    public static void ClampTargetCountClampsNegativeValueToZero() =>
        Expect.AreEqual(0, Trigger_Threshold.ClampTargetCount(-5));

    [Test]
    public static void ClampTargetCountLeavesZeroUnchanged() =>
        Expect.AreEqual(0, Trigger_Threshold.ClampTargetCount(0));

    [Test]
    public static void ClampTargetCountLeavesPositiveValueUnchanged() =>
        Expect.AreEqual(42, Trigger_Threshold.ClampTargetCount(42));

    // Regression guard: a freshly constructed ThingFilter defaults AllowedHitPointsPercents and
    // AllowedQualityLevels to their zeroed struct value (0%-0% hitpoints, no quality levels
    // allowed) rather than "allow everything".
    [Test]
    public static void CreateThresholdFilterAllowsFullHitPointsRange() =>
        Expect.IsTrue(
            Trigger_Threshold.CreateThresholdFilter(() => { }).AllowedHitPointsPercents
                == FloatRange.ZeroToOne
        );

    [Test]
    public static void CreateThresholdFilterAllowsFullQualityRange() =>
        Expect.IsTrue(
            Trigger_Threshold.CreateThresholdFilter(() => { }).AllowedQualityLevels
                == QualityRange.All
        );

    [Test]
    public static void CreateThresholdFilterProducesFilterThatCountsFullHitPointsThing() =>
        Expect.IsTrue(
            Utilities.ShouldCountThing(
                false,
                default,
                true,
                1f,
                Trigger_Threshold.CreateThresholdFilter(() => { })
            )
        );
}
