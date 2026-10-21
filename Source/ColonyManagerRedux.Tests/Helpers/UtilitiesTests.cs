// UtilitiesTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class UtilitiesTests
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
        Expect.IsTrue(Utilities.ShouldCountThing(false, QualityCategory.Awful, true, 0.5f, filter));
    }

    [Test]
    public static void ExcludesThingWithQualityOutsideAllowedRange()
    {
        var filter = NewFilter(QualityCategory.Good, QualityCategory.Legendary, 0f);
        Expect.IsFalse(Utilities.ShouldCountThing(true, QualityCategory.Poor, true, 1f, filter));
    }

    [Test]
    public static void CountsThingWithQualityInsideAllowedRange()
    {
        var filter = NewFilter(QualityCategory.Good, QualityCategory.Legendary, 0f);
        Expect.IsTrue(
            Utilities.ShouldCountThing(true, QualityCategory.Excellent, true, 1f, filter)
        );
    }

    [Test]
    public static void ExcludesDamagedThingBelowAllowedHitPointsPercent()
    {
        // Regression guard for the bug where damaged items matching the filter were skipped
        // while items that didn't match were counted (i.e. the check was inverted).
        var filter = NewFilter(QualityCategory.Awful, QualityCategory.Legendary, 0.75f);
        Expect.IsFalse(Utilities.ShouldCountThing(false, default, true, 0.5f, filter));
    }

    [Test]
    public static void CountsThingAtOrAboveAllowedHitPointsPercent()
    {
        var filter = NewFilter(QualityCategory.Awful, QualityCategory.Legendary, 0.75f);
        Expect.IsTrue(Utilities.ShouldCountThing(false, default, true, 0.9f, filter));
    }

    [Test]
    public static void CountsThingWithUntrackedHitPointsWhenFilterRangeIsUnrestricted()
    {
        // Regression guard for countAllOnMap undercounting: a thing whose hit points were never
        // saved (e.g. scattered map-gen resources) loads with HitPoints == -1, producing a
        // negative hitPointsPercent. Mirroring ThingFilter.Allows, the default 0%-100% range
        // must not care about hit points at all, so this must still be counted.
        var filter = NewFilter(QualityCategory.Awful, QualityCategory.Legendary, 0f, 1f);
        Expect.IsTrue(Utilities.ShouldCountThing(false, default, true, -0.01f, filter));
    }

    [Test]
    public static void CountsThingRegardlessOfHitPointsPercentWhenDefDoesNotUseHitPoints()
    {
        var filter = NewFilter(QualityCategory.Awful, QualityCategory.Legendary, 0.75f);
        Expect.IsTrue(Utilities.ShouldCountThing(false, default, false, 0f, filter));
    }

    [Test]
    public static void DisabledCountAllOnMapCountsStorageOnlyResourceViaCounter() =>
        // Regression guard for the bug where enabling countAllOnMap could report FEWER
        // items than leaving it disabled: for a resource whose stored portion is already
        // covered by the resource counter, the map-wide scan must skip storage (it's
        // already counted) regardless of countAllOnMap.
        Expect.IsTrue(
            Utilities.ShouldSkipDueToStorageState(
                usedResourceCounter: true,
                countAllOnMap: false,
                isInAnyStorage: true
            )
        );

    [Test]
    public static void EnabledCountAllOnMapStillSkipsStoredResourceToAvoidDoubleCounting() =>
        Expect.IsTrue(
            Utilities.ShouldSkipDueToStorageState(
                usedResourceCounter: true,
                countAllOnMap: true,
                isInAnyStorage: true
            )
        );

    [Test]
    public static void EnabledCountAllOnMapCountsResourceOutsideStorage() =>
        // The whole point of countAllOnMap: things outside storage are added on top of
        // whatever the resource counter already reported for the stored portion.
        Expect.IsFalse(
            Utilities.ShouldSkipDueToStorageState(
                usedResourceCounter: true,
                countAllOnMap: true,
                isInAnyStorage: false
            )
        );

    [Test]
    public static void DisabledCountAllOnMapSkipsNonResourceThingOutsideStorage() =>
        Expect.IsTrue(
            Utilities.ShouldSkipDueToStorageState(
                usedResourceCounter: false,
                countAllOnMap: false,
                isInAnyStorage: false
            )
        );

    [Test]
    public static void DisabledCountAllOnMapCountsNonResourceThingInStorage() =>
        Expect.IsFalse(
            Utilities.ShouldSkipDueToStorageState(
                usedResourceCounter: false,
                countAllOnMap: false,
                isInAnyStorage: true
            )
        );

    [Test]
    public static void EnabledCountAllOnMapCountsNonResourceThingRegardlessOfStorage() =>
        Expect.IsFalse(
            Utilities.ShouldSkipDueToStorageState(
                usedResourceCounter: false,
                countAllOnMap: true,
                isInAnyStorage: false
            )
        );

    [Test]
    public static void SaturatingIntSumOfEmptySequenceIsZero() =>
        Expect.AreEqual(0, Utilities.SaturatingIntSum([]));

    [Test]
    public static void SaturatingIntSumAddsNormally() =>
        Expect.AreEqual(6, Utilities.SaturatingIntSum([1, 2, 3]));

    [Test]
    public static void SaturatingIntSumClampsAtMaxValueOnOverflow() =>
        // Born from commit 9b8ae26 ("feat: add SaturatingIntSum utility method to prevent
        // integer overflow in power calculations", issue #21): summing near int.MaxValue must
        // saturate rather than wrap around into negative territory.
        Expect.AreEqual(int.MaxValue, Utilities.SaturatingIntSum([int.MaxValue, 1]));

    [Test]
    public static void SaturatingIntSumClampsAtMinValueOnUnderflow() =>
        Expect.AreEqual(int.MinValue, Utilities.SaturatingIntSum([int.MinValue, -1]));

    [Test]
    public static void SaturatingIntSumReachingExactlyMaxValueDoesNotSaturate() =>
        // Landing exactly on int.MaxValue is not itself an overflow; only exceeding it should
        // trigger the saturating clamp.
        Expect.AreEqual(int.MaxValue, Utilities.SaturatingIntSum([int.MaxValue - 1, 1]));

    [Test]
    public static void SaturatingIntSumReachingExactlyMinValueDoesNotSaturate() =>
        Expect.AreEqual(int.MinValue, Utilities.SaturatingIntSum([int.MinValue + 1, -1]));

    [Test]
    public static void SaturatingIntSumThrowsOnNullSequence() =>
        Expect.Throws<ArgumentNullException>(() => Utilities.SaturatingIntSum(null!));

    [Test]
    public static void SafeAbsOfPositiveValueIsUnchanged() =>
        Expect.AreEqual(5, Utilities.SafeAbs(5));

    [Test]
    public static void SafeAbsOfNegativeValueIsPositive() =>
        Expect.AreEqual(5, Utilities.SafeAbs(-5));

    [Test]
    public static void SafeAbsOfZeroIsZero() => Expect.AreEqual(0, Utilities.SafeAbs(0));

    [Test]
    public static void SafeAbsOfMinValueDoesNotOverflow() =>
        // The motivation for commit 9b8ae26: naive -value on int.MinValue overflows back to
        // int.MinValue (still negative) instead of throwing or crashing.
        Expect.AreEqual(int.MaxValue, Utilities.SafeAbs(int.MinValue));

    private static void NamedStaticMethod() { }

    private static void OtherNamedStaticMethod() { }

    private sealed class InstanceMethodHolder
    {
#pragma warning disable CA1822 // Mark members as static
        public void NamedInstanceMethod() { }

        public void OtherNamedInstanceMethod() { }
#pragma warning restore CA1822 // Mark members as static
    }

    [Test]
    public static void GetDelegateIdDiffersForDifferentInstanceMethodsOnSameTarget()
    {
        var holder = new InstanceMethodHolder();
        Expect.AreNotEqual(
            Utilities.GetDelegateId(holder.NamedInstanceMethod),
            Utilities.GetDelegateId(holder.OtherNamedInstanceMethod)
        );
    }

    [Test]
    public static void GetDelegateIdDiffersForDifferentStaticMethods() =>
        Expect.AreNotEqual(
            Utilities.GetDelegateId(NamedStaticMethod),
            Utilities.GetDelegateId(OtherNamedStaticMethod)
        );

    [Test]
    public static void GetDelegateIdIsStableForSameMethodAndTarget()
    {
        var holder = new InstanceMethodHolder();
        Expect.AreEqual(
            Utilities.GetDelegateId(holder.NamedInstanceMethod),
            Utilities.GetDelegateId(holder.NamedInstanceMethod)
        );
    }

    [Test]
    public static void IsLikelyAnonymousIsFalseForNamedStaticMethod() =>
        Expect.IsFalse(Utilities.IsLikelyAnonymous(NamedStaticMethod));

    [Test]
    public static void IsLikelyAnonymousIsFalseForNamedInstanceMethod() =>
        Expect.IsFalse(Utilities.IsLikelyAnonymous(new InstanceMethodHolder().NamedInstanceMethod));

    [Test]
    public static void IsLikelyAnonymousIsTrueForLambda()
    {
        Action lambda = () => { };
        Expect.IsTrue(Utilities.IsLikelyAnonymous(lambda));
    }

    [Test]
    public static void IsLikelyAnonymousIsTrueForLocalFunction()
    {
        static void LocalFunction() { }
        Action local = LocalFunction;
        Expect.IsTrue(Utilities.IsLikelyAnonymous(local));
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

        Expect.IsFalse(result);
        Expect.IsFalse(invoked);
    }

    [Test]
    public static void DrawReorderButtonThrowsOnNullCallback() =>
        Expect.Throws<ArgumentNullException>(() =>
            Utilities.DrawReorderButton(default, null!, default, true, null!)
        );
}
