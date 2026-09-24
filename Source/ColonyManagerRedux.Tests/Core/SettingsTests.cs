// SettingsTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class SettingsTests
{
    [Test]
    public static void CanAddMoreDesignationsIsAlwaysTrueWhenLimitIsZero() =>
        Expect.IsTrue(Settings.CanAddMoreDesignations(0, 1_000_000));

    [Test]
    public static void CanAddMoreDesignationsIsTrueWhenBelowLimit() =>
        Expect.IsTrue(Settings.CanAddMoreDesignations(10, 5));

    [Test]
    public static void CanAddMoreDesignationsIsFalseWhenAtLimit() =>
        Expect.IsFalse(Settings.CanAddMoreDesignations(10, 10));

    [Test]
    public static void CanAddMoreDesignationsIsFalseWhenAboveLimit() =>
        Expect.IsFalse(Settings.CanAddMoreDesignations(10, 11));

    [Test]
    public static void ShouldRemoveMoreDesignationsIsAlwaysFalseWhenLimitIsZero() =>
        Expect.IsFalse(Settings.ShouldRemoveMoreDesignations(0, 1_000_000));

    [Test]
    public static void ShouldRemoveMoreDesignationsIsFalseWhenBelowLimit() =>
        Expect.IsFalse(Settings.ShouldRemoveMoreDesignations(10, 5));

    [Test]
    public static void ShouldRemoveMoreDesignationsIsFalseWhenAtLimit() =>
        Expect.IsFalse(Settings.ShouldRemoveMoreDesignations(10, 10));

    [Test]
    public static void ShouldRemoveMoreDesignationsIsTrueWhenAboveLimit() =>
        Expect.IsTrue(Settings.ShouldRemoveMoreDesignations(10, 11));

    [Test]
    public static void ShouldKeepManagerSettingsEntryIsTrueWhenDefStillValid() =>
        Expect.IsTrue(Settings.ShouldKeepManagerSettingsEntry("Hunting", ["Hunting", "Mining"]));

    [Test]
    public static void ShouldKeepManagerSettingsEntryIsFalseWhenDefNoLongerValid() =>
        Expect.IsFalse(Settings.ShouldKeepManagerSettingsEntry("Hunting", ["Mining"]));

    [Test]
    public static void FindMissingManagerDefsIsEmptyWhenAllPresent() =>
        Expect.IsEmpty(
            Settings.FindMissingManagerDefs(["Hunting", "Mining"], ["Hunting", "Mining"])
        );

    [Test]
    public static void FindMissingManagerDefsReturnsDefsWithoutAnEntry() =>
        Expect.IsTrue(
            Settings.FindMissingManagerDefs(["Hunting", "Mining"], ["Hunting"]).Contains("Mining")
        );

    [Test]
    public static void ScaleUpMaxDesignationsPerJobMultipliesByTen() =>
        Expect.AreEqual(150, Settings.ScaleUpMaxDesignationsPerJob(15));

    [Test]
    public static void ScaleDownMaxDesignationsPerJobDividesByTen() =>
        Expect.AreEqual(15, Settings.ScaleDownMaxDesignationsPerJob(150));

    [Test]
    public static void MaxDesignationsPerJobZeroRoundTripsToZero() =>
        Expect.AreEqual(
            0,
            Settings.ScaleUpMaxDesignationsPerJob(Settings.ScaleDownMaxDesignationsPerJob(0))
        );

    [Test]
    public static void MaxDesignationsPerJobTruncatesNonMultipleOfTenOnRoundTrip() =>
        // 25 / 10 == 2 (integer division), then 2 * 10 == 20: the raw slider step loses precision
        // for values that aren't already a multiple of 10.
        Expect.AreEqual(
            20,
            Settings.ScaleUpMaxDesignationsPerJob(Settings.ScaleDownMaxDesignationsPerJob(25))
        );

    [Test]
    public static void MaxDesignationsPerJobRoundTripsExactlyForMultipleOfTen() =>
        Expect.AreEqual(
            150,
            Settings.ScaleUpMaxDesignationsPerJob(Settings.ScaleDownMaxDesignationsPerJob(150))
        );

    [Test]
    public static void ClampAlertTiersLeavesAlreadyOrderedValuesUnchanged()
    {
        var (high, critical) = Settings.ClampAlertTiers(0.5f, 1f, 2f);
        Expect.AreEqual(1f, high);
        Expect.AreEqual(2f, critical);
    }

    [Test]
    public static void ClampAlertTiersRaisesHighToAlertWhenBelowIt()
    {
        var (high, critical) = Settings.ClampAlertTiers(1.5f, 1f, 2f);
        Expect.AreEqual(1.5f, high);
        Expect.AreEqual(2f, critical);
    }

    [Test]
    public static void ClampAlertTiersRaisesCriticalToHighWhenBelowIt()
    {
        var (high, critical) = Settings.ClampAlertTiers(0.5f, 1f, 0.75f);
        Expect.AreEqual(1f, high);
        Expect.AreEqual(1f, critical);
    }

    [Test]
    public static void ClampAlertTiersRaisesCriticalTransitivelyThroughRaisedHigh()
    {
        var (high, critical) = Settings.ClampAlertTiers(3f, 1f, 2f);
        Expect.AreEqual(3f, high);
        Expect.AreEqual(3f, critical);
    }

    [Test]
    public static void ClampAlertTiersKeepsEqualValuesStable()
    {
        var (high, critical) = Settings.ClampAlertTiers(1f, 1f, 1f);
        Expect.AreEqual(1f, high);
        Expect.AreEqual(1f, critical);
    }

    [Test]
    public static void FindMatchingTicksReturnsExactMatch() =>
        Expect.AreEqual(200, Settings.FindMatchingTicks([100, 200, 300], 200)!.Value);

    [Test]
    public static void FindMatchingTicksFallsBackToNullWhenNoMatch() =>
        Expect.IsTrue(Settings.FindMatchingTicks([100, 200, 300], 250) == null);

    [Test]
    public static void FindMatchingTicksFallsBackToNullOnEmptyList() =>
        Expect.IsTrue(Settings.FindMatchingTicks([], 100) == null);

    [Test]
    public static void FindMatchingTicksReturnsFirstOfDuplicateMatches() =>
        Expect.AreEqual(100, Settings.FindMatchingTicks([100, 100, 200], 100)!.Value);

    [Test]
    public static void AdjustCustomIntervalTicksClampsAtZeroOnDecrement() =>
        Expect.AreEqual(0, Settings.AdjustCustomIntervalTicks(100, -1000));

    [Test]
    public static void AdjustCustomIntervalTicksDecrementsNormally() =>
        Expect.AreEqual(60, Settings.AdjustCustomIntervalTicks(100, -40));

    [Test]
    public static void AdjustCustomIntervalTicksIncrementsNormally() =>
        Expect.AreEqual(140, Settings.AdjustCustomIntervalTicks(100, 40));

    [Test]
    public static void ResolveCoroutineSettingUsesGlobalWhenAdvancedSettingsDisabled() =>
        // Even a valid, present override must be ignored when the advanced-settings toggle is
        // off.
        Expect.AreEqual(
            5,
            Settings.ResolveCoroutineSetting(
                showAdvanced: false,
                globalValue: 5,
                hasOverride: true,
                overrideValue: 99,
                minValidOverride: 1
            )
        );

    [Test]
    public static void ResolveCoroutineSettingUsesGlobalWhenNoOverrideStored() =>
        Expect.AreEqual(
            5,
            Settings.ResolveCoroutineSetting(
                showAdvanced: true,
                globalValue: 5,
                hasOverride: false,
                overrideValue: 0,
                minValidOverride: 1
            )
        );

    [Test]
    public static void ResolveCoroutineSettingUsesGlobalWhenOverrideBelowValidityThreshold() =>
        Expect.AreEqual(
            5,
            Settings.ResolveCoroutineSetting(
                showAdvanced: true,
                globalValue: 5,
                hasOverride: true,
                overrideValue: 0,
                minValidOverride: 1
            )
        );

    [Test]
    public static void ResolveCoroutineSettingUsesValidOverrideWhenAdvancedSettingsEnabled() =>
        Expect.AreEqual(
            99,
            Settings.ResolveCoroutineSetting(
                showAdvanced: true,
                globalValue: 5,
                hasOverride: true,
                overrideValue: 99,
                minValidOverride: 1
            )
        );

    [Test]
    public static void ResolveCoroutineSettingTreatsZeroAsValidOverrideForTicksBetweenOperations() =>
        // The asymmetry between the two coroutine settings: 0 is a valid explicit override for
        // ticks-between-operations (minValidOverride 0), unlike operations-per-tick
        // (minValidOverride 1).
        Expect.AreEqual(
            0,
            Settings.ResolveCoroutineSetting(
                showAdvanced: true,
                globalValue: 5,
                hasOverride: true,
                overrideValue: 0,
                minValidOverride: 0
            )
        );
}
