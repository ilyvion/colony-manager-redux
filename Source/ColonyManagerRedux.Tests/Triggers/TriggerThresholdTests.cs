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
}
