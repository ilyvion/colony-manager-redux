// TriggerTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[HotSwappable]
[TestFixture(TestType.MainMenu)]
internal sealed class TriggerTests
{
    [Test]
    public static void ProgressBarMetricsUsesTwentyPercentPastTargetWhenItsTheLargestCandidate()
    {
        // maxValue=10 -> (int)(10*1.2)=12 beats maxValue+1=11 and currentValue=5.
        var (max, unit, markPosition, barSpan) = Trigger.ComputeProgressBarMetrics(5f, 10f, 120f);
        Expect.AreEqual(12f, max);
        Expect.AreEqual(10f, unit);
        Expect.AreEqual(100f, markPosition);
        Expect.AreEqual(50f, barSpan);
    }

    [Test]
    public static void ProgressBarMetricsFallsBackToTargetPlusOneWhenThatsLargerThanTheTwentyPercentMargin()
    {
        // maxValue=2 -> (int)(2*1.2)=2, but maxValue+1=3 is larger, and currentValue=1
        // doesn't exceed either.
        var (max, unit, markPosition, barSpan) = Trigger.ComputeProgressBarMetrics(1f, 2f, 30f);
        Expect.AreEqual(3f, max);
        Expect.AreEqual(10f, unit);
        Expect.AreEqual(20f, markPosition);
        Expect.AreEqual(10f, barSpan);
    }

    [Test]
    public static void ProgressBarMetricsUsesCurrentValueWhenItOvershootsBothOtherCandidates()
    {
        // currentValue=100 exceeds both maxValue*1.2 (12) and maxValue+1 (11), so it
        // becomes the logical max, keeping the bar from clipping an overshot value.
        var (max, unit, markPosition, barSpan) = Trigger.ComputeProgressBarMetrics(100f, 10f, 100f);
        Expect.AreEqual(100f, max);
        Expect.AreEqual(1f, unit);
        Expect.AreEqual(10f, markPosition);
        Expect.AreEqual(100f, barSpan);
    }

    [Test]
    public static void ProgressBarMetricsHandlesZeroMaxValue()
    {
        // maxValue=0 -> (int)(0*1.2)=0, so maxValue+1=1 is the only positive candidate.
        var (max, unit, markPosition, barSpan) = Trigger.ComputeProgressBarMetrics(0f, 0f, 50f);
        Expect.AreEqual(1f, max);
        Expect.AreEqual(50f, unit);
        Expect.AreEqual(0f, markPosition);
        Expect.AreEqual(0f, barSpan);
    }

    [Test]
    public static void ProgressBarMetricsProducesNegativeBarSpanForNegativeCurrentValue()
    {
        // Negative currentValue (reachable via "not equals"/overshoot threshold types)
        // isn't clamped by this formula -- it flows straight through into a negative
        // barSpan, exactly matching the pre-extraction behavior. Documented here as a
        // known quirk, not fixed, since fixing it is out of scope for this extraction.
        var (max, _, _, barSpan) = Trigger.ComputeProgressBarMetrics(-5f, 10f, 120f);
        Expect.AreEqual(12f, max);
        Expect.LessThan(barSpan, 0f);
    }

    [Test]
    public static void ProgressBarMetricsWithZeroExpectedValueMatchesThreeArgOverload()
    {
        // The 3-arg overload used to be the only formula; it now delegates to the 4-arg
        // one with expectedValue=0, so the two must keep producing identical results.
        var withoutExpected = Trigger.ComputeProgressBarMetrics(5f, 10f, 120f);
        var (max, unit, markPosition, barSpan, expectedBarSpan) = Trigger.ComputeProgressBarMetrics(
            5f,
            0f,
            10f,
            120f
        );
        Expect.AreEqual(withoutExpected.max, max);
        Expect.AreEqual(withoutExpected.unit, unit);
        Expect.AreEqual(withoutExpected.markPosition, markPosition);
        Expect.AreEqual(withoutExpected.barSpan, barSpan);
        Expect.AreEqual(barSpan, expectedBarSpan);
    }

    [Test]
    public static void ProgressBarMetricsExpectedBarSpanCoversCurrentPlusExpected()
    {
        // currentValue=5, expectedValue=3 -> totalValue=8 is still under maxValue*1.2 (12),
        // so max stays anchored to the 20%-past-target margin and expectedBarSpan reflects
        // the full current+expected total.
        var (max, unit, markPosition, barSpan, expectedBarSpan) = Trigger.ComputeProgressBarMetrics(
            5f,
            3f,
            10f,
            120f
        );
        Expect.AreEqual(12f, max);
        Expect.AreEqual(10f, unit);
        Expect.AreEqual(100f, markPosition);
        Expect.AreEqual(50f, barSpan);
        Expect.AreEqual(80f, expectedBarSpan);
    }

    [Test]
    public static void ProgressBarMetricsExpandsMaxWhenCurrentPlusExpectedOvershootsTarget()
    {
        // currentValue=8, expectedValue=10 -> totalValue=18 exceeds maxValue*1.2 (12) and
        // maxValue+1 (11), so the logical max grows to fit the expected overlay without
        // clipping it, just like an overshot currentValue would on its own.
        var (max, unit, _, _, expectedBarSpan) = Trigger.ComputeProgressBarMetrics(
            8f,
            10f,
            10f,
            120f
        );
        Expect.AreEqual(18f, max);
        Expect.AreEqual(120f / 18f, unit);
        Expect.AreEqual(120f, expectedBarSpan);
    }
}
