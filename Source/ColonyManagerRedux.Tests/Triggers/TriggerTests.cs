// TriggerTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[HotSwappable]
[TestSuite]
internal static class TriggerTests
{
    [Test]
    public static void ProgressBarMetricsUsesTwentyPercentPastTargetWhenItsTheLargestCandidate()
    {
        // maxValue=10 -> (int)(10*1.2)=12 beats maxValue+1=11 and currentValue=5.
        var (max, unit, markPosition, barSpan) = Trigger.ComputeProgressBarMetrics(5f, 10f, 120f);
        Assert.That(max).Is.EqualTo(12f);
        Assert.That(unit).Is.EqualTo(10f);
        Assert.That(markPosition).Is.EqualTo(100f);
        Assert.That(barSpan).Is.EqualTo(50f);
    }

    [Test]
    public static void ProgressBarMetricsFallsBackToTargetPlusOneWhenThatsLargerThanTheTwentyPercentMargin()
    {
        // maxValue=2 -> (int)(2*1.2)=2, but maxValue+1=3 is larger, and currentValue=1
        // doesn't exceed either.
        var (max, unit, markPosition, barSpan) = Trigger.ComputeProgressBarMetrics(1f, 2f, 30f);
        Assert.That(max).Is.EqualTo(3f);
        Assert.That(unit).Is.EqualTo(10f);
        Assert.That(markPosition).Is.EqualTo(20f);
        Assert.That(barSpan).Is.EqualTo(10f);
    }

    [Test]
    public static void ProgressBarMetricsUsesCurrentValueWhenItOvershootsBothOtherCandidates()
    {
        // currentValue=100 exceeds both maxValue*1.2 (12) and maxValue+1 (11), so it
        // becomes the logical max, keeping the bar from clipping an overshot value.
        var (max, unit, markPosition, barSpan) = Trigger.ComputeProgressBarMetrics(100f, 10f, 100f);
        Assert.That(max).Is.EqualTo(100f);
        Assert.That(unit).Is.EqualTo(1f);
        Assert.That(markPosition).Is.EqualTo(10f);
        Assert.That(barSpan).Is.EqualTo(100f);
    }

    [Test]
    public static void ProgressBarMetricsHandlesZeroMaxValue()
    {
        // maxValue=0 -> (int)(0*1.2)=0, so maxValue+1=1 is the only positive candidate.
        var (max, unit, markPosition, barSpan) = Trigger.ComputeProgressBarMetrics(0f, 0f, 50f);
        Assert.That(max).Is.EqualTo(1f);
        Assert.That(unit).Is.EqualTo(50f);
        Assert.That(markPosition).Is.EqualTo(0f);
        Assert.That(barSpan).Is.EqualTo(0f);
    }

    [Test]
    public static void ProgressBarMetricsProducesNegativeBarSpanForNegativeCurrentValue()
    {
        // Negative currentValue (reachable via "not equals"/overshoot threshold types)
        // isn't clamped by this formula -- it flows straight through into a negative
        // barSpan, exactly matching the pre-extraction behavior. Documented here as a
        // known quirk, not fixed, since fixing it is out of scope for this extraction.
        var (max, _, _, barSpan) = Trigger.ComputeProgressBarMetrics(-5f, 10f, 120f);
        Assert.That(max).Is.EqualTo(12f);
        Assert.That(barSpan).Is.LessThan(0f);
    }
}
