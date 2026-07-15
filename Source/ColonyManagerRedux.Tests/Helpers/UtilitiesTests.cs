// UtilitiesTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class UtilitiesTests
{
    private static ThingFilter NewFilter(
        QualityCategory minQuality,
        QualityCategory maxQuality,
        float minHitPointsPercent,
        float maxHitPointsPercent = 1f
    ) =>
        new()
        {
            AllowedQualityLevels = new QualityRange(minQuality, maxQuality),
            AllowedHitPointsPercents = new FloatRange(minHitPointsPercent, maxHitPointsPercent),
        };

    [Test]
    public static void CountsThingWithoutQualityRegardlessOfQualityRange()
    {
        // Regression guard: things without a quality category (e.g. most resources) must not be
        // excluded just because they don't fall inside the filter's quality range.
        var filter = NewFilter(QualityCategory.Legendary, QualityCategory.Legendary, 0f);
        Assert
            .That(Utilities.ShouldCountThing(false, QualityCategory.Awful, 0.5f, filter))
            .Is.True();
    }

    [Test]
    public static void ExcludesThingWithQualityOutsideAllowedRange()
    {
        var filter = NewFilter(QualityCategory.Good, QualityCategory.Legendary, 0f);
        Assert.That(Utilities.ShouldCountThing(true, QualityCategory.Poor, 1f, filter)).Is.False();
    }

    [Test]
    public static void CountsThingWithQualityInsideAllowedRange()
    {
        var filter = NewFilter(QualityCategory.Good, QualityCategory.Legendary, 0f);
        Assert
            .That(Utilities.ShouldCountThing(true, QualityCategory.Excellent, 1f, filter))
            .Is.True();
    }

    [Test]
    public static void ExcludesDamagedThingBelowAllowedHitPointsPercent()
    {
        // Regression guard for the bug where damaged items matching the filter were skipped
        // while items that didn't match were counted (i.e. the check was inverted).
        var filter = NewFilter(QualityCategory.Awful, QualityCategory.Legendary, 0.75f);
        Assert.That(Utilities.ShouldCountThing(false, default, 0.5f, filter)).Is.False();
    }

    [Test]
    public static void CountsThingAtOrAboveAllowedHitPointsPercent()
    {
        var filter = NewFilter(QualityCategory.Awful, QualityCategory.Legendary, 0.75f);
        Assert.That(Utilities.ShouldCountThing(false, default, 0.9f, filter)).Is.True();
    }

    [Test]
    public static void SaturatingIntSumOfEmptySequenceIsZero() =>
        Assert.That(Utilities.SaturatingIntSum([])).Is.EqualTo(0);

    [Test]
    public static void SaturatingIntSumAddsNormally() =>
        Assert.That(Utilities.SaturatingIntSum([1, 2, 3])).Is.EqualTo(6);

    [Test]
    public static void SaturatingIntSumClampsAtMaxValueOnOverflow() =>
        // Born from commit 9b8ae26 ("feat: add SaturatingIntSum utility method to prevent
        // integer overflow in power calculations", issue #21): summing near int.MaxValue must
        // saturate rather than wrap around into negative territory.
        Assert.That(Utilities.SaturatingIntSum([int.MaxValue, 1])).Is.EqualTo(int.MaxValue);

    [Test]
    public static void SaturatingIntSumClampsAtMinValueOnUnderflow() =>
        Assert.That(Utilities.SaturatingIntSum([int.MinValue, -1])).Is.EqualTo(int.MinValue);

    [Test]
    public static void SaturatingIntSumReachingExactlyMaxValueDoesNotSaturate() =>
        // Landing exactly on int.MaxValue is not itself an overflow; only exceeding it should
        // trigger the saturating clamp.
        Assert.That(Utilities.SaturatingIntSum([int.MaxValue - 1, 1])).Is.EqualTo(int.MaxValue);

    [Test]
    public static void SaturatingIntSumReachingExactlyMinValueDoesNotSaturate() =>
        Assert.That(Utilities.SaturatingIntSum([int.MinValue + 1, -1])).Is.EqualTo(int.MinValue);

    [Test]
    public static void SaturatingIntSumThrowsOnNullSequence() =>
        Assert.ThatFunc(() => Utilities.SaturatingIntSum(null!)).Does.Throw();

    [Test]
    public static void SafeAbsOfPositiveValueIsUnchanged() =>
        Assert.That(Utilities.SafeAbs(5)).Is.EqualTo(5);

    [Test]
    public static void SafeAbsOfNegativeValueIsPositive() =>
        Assert.That(Utilities.SafeAbs(-5)).Is.EqualTo(5);

    [Test]
    public static void SafeAbsOfZeroIsZero() => Assert.That(Utilities.SafeAbs(0)).Is.EqualTo(0);

    [Test]
    public static void SafeAbsOfMinValueDoesNotOverflow() =>
        // The motivation for commit 9b8ae26: naive -value on int.MinValue overflows back to
        // int.MinValue (still negative) instead of throwing or crashing.
        Assert.That(Utilities.SafeAbs(int.MinValue)).Is.EqualTo(int.MaxValue);

    private static void NamedStaticMethod() { }

    private sealed class InstanceMethodHolder
    {
#pragma warning disable CA1822 // Mark members as static
        public void NamedInstanceMethod() { }
#pragma warning restore CA1822 // Mark members as static
    }

    [Test]
    public static void IsLikelyAnonymousIsFalseForNamedStaticMethod() =>
        Assert.That(Utilities.IsLikelyAnonymous(NamedStaticMethod)).Is.False();

    [Test]
    public static void IsLikelyAnonymousIsFalseForNamedInstanceMethod() =>
        Assert
            .That(Utilities.IsLikelyAnonymous(new InstanceMethodHolder().NamedInstanceMethod))
            .Is.False();

    [Test]
    public static void IsLikelyAnonymousIsTrueForLambda()
    {
        Action lambda = () => { };
        Assert.That(Utilities.IsLikelyAnonymous(lambda)).Is.True();
    }

    [Test]
    public static void IsLikelyAnonymousIsTrueForLocalFunction()
    {
        static void LocalFunction() { }
        Action local = LocalFunction;
        Assert.That(Utilities.IsLikelyAnonymous(local)).Is.True();
    }

    [Test]
    public static void DrawReorderButtonDoesNotInvokeCallbackAtBoundary()
    {
        // Regression guard: a reorder button at its boundary (e.g. already at the top of the
        // list) must not fire its click callback, even if it somehow received a click.
        var invoked = false;

        var result = Utilities.DrawReorderButton(
            default,
            null!,
            default,
            atBoundary: true,
            () => invoked = true
        );

        Assert.That(result).Is.False();
        Assert.That(invoked).Is.False();
    }

    [Test]
    [ShouldThrow(typeof(ArgumentNullException))]
    public static void DrawReorderButtonThrowsOnNullCallback() =>
        Utilities.DrawReorderButton(default, null!, default, true, null!);
}
