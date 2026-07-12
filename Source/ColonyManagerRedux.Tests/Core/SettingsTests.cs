// SettingsTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class SettingsTests
{
    [Test]
    public static void CanAddMoreDesignationsIsAlwaysTrueWhenLimitIsZero() =>
        Assert.That(Settings.CanAddMoreDesignations(0, 1_000_000)).Is.True();

    [Test]
    public static void CanAddMoreDesignationsIsTrueWhenBelowLimit() =>
        Assert.That(Settings.CanAddMoreDesignations(10, 5)).Is.True();

    [Test]
    public static void CanAddMoreDesignationsIsFalseWhenAtLimit() =>
        Assert.That(Settings.CanAddMoreDesignations(10, 10)).Is.False();

    [Test]
    public static void CanAddMoreDesignationsIsFalseWhenAboveLimit() =>
        Assert.That(Settings.CanAddMoreDesignations(10, 11)).Is.False();

    [Test]
    public static void ShouldRemoveMoreDesignationsIsAlwaysFalseWhenLimitIsZero() =>
        Assert.That(Settings.ShouldRemoveMoreDesignations(0, 1_000_000)).Is.False();

    [Test]
    public static void ShouldRemoveMoreDesignationsIsFalseWhenBelowLimit() =>
        Assert.That(Settings.ShouldRemoveMoreDesignations(10, 5)).Is.False();

    [Test]
    public static void ShouldRemoveMoreDesignationsIsFalseWhenAtLimit() =>
        Assert.That(Settings.ShouldRemoveMoreDesignations(10, 10)).Is.False();

    [Test]
    public static void ShouldRemoveMoreDesignationsIsTrueWhenAboveLimit() =>
        Assert.That(Settings.ShouldRemoveMoreDesignations(10, 11)).Is.True();

    [Test]
    public static void ShouldKeepManagerSettingsEntryIsTrueWhenDefStillValid() =>
        Assert
            .That(Settings.ShouldKeepManagerSettingsEntry("Hunting", ["Hunting", "Mining"]))
            .Is.True();

    [Test]
    public static void ShouldKeepManagerSettingsEntryIsFalseWhenDefNoLongerValid() =>
        Assert.That(Settings.ShouldKeepManagerSettingsEntry("Hunting", ["Mining"])).Is.False();

    [Test]
    public static void FindMissingManagerDefsIsEmptyWhenAllPresent() =>
        Assert
            .ThatCollection(
                Settings.FindMissingManagerDefs(["Hunting", "Mining"], ["Hunting", "Mining"])
            )
            .Is.Empty();

    [Test]
    public static void FindMissingManagerDefsReturnsDefsWithoutAnEntry() =>
        Assert
            .ThatCollection(Settings.FindMissingManagerDefs(["Hunting", "Mining"], ["Hunting"]))
            .Does.Contain("Mining");

    [Test]
    public static void ScaleUpMaxDesignationsPerJobMultipliesByTen() =>
        Assert.That(Settings.ScaleUpMaxDesignationsPerJob(15)).Is.EqualTo(150);

    [Test]
    public static void ScaleDownMaxDesignationsPerJobDividesByTen() =>
        Assert.That(Settings.ScaleDownMaxDesignationsPerJob(150)).Is.EqualTo(15);

    [Test]
    public static void MaxDesignationsPerJobZeroRoundTripsToZero() =>
        Assert
            .That(Settings.ScaleUpMaxDesignationsPerJob(Settings.ScaleDownMaxDesignationsPerJob(0)))
            .Is.EqualTo(0);

    [Test]
    public static void MaxDesignationsPerJobTruncatesNonMultipleOfTenOnRoundTrip() =>
        // 25 / 10 == 2 (integer division), then 2 * 10 == 20: the raw slider step loses precision
        // for values that aren't already a multiple of 10.
        Assert
            .That(
                Settings.ScaleUpMaxDesignationsPerJob(Settings.ScaleDownMaxDesignationsPerJob(25))
            )
            .Is.EqualTo(20);

    [Test]
    public static void MaxDesignationsPerJobRoundTripsExactlyForMultipleOfTen() =>
        Assert
            .That(
                Settings.ScaleUpMaxDesignationsPerJob(Settings.ScaleDownMaxDesignationsPerJob(150))
            )
            .Is.EqualTo(150);

    [Test]
    public static void ClampAlertTiersLeavesAlreadyOrderedValuesUnchanged()
    {
        var (high, critical) = Settings.ClampAlertTiers(0.5f, 1f, 2f);
        Assert.That(high).Is.EqualTo(1f);
        Assert.That(critical).Is.EqualTo(2f);
    }

    [Test]
    public static void ClampAlertTiersRaisesHighToAlertWhenBelowIt()
    {
        var (high, critical) = Settings.ClampAlertTiers(1.5f, 1f, 2f);
        Assert.That(high).Is.EqualTo(1.5f);
        Assert.That(critical).Is.EqualTo(2f);
    }

    [Test]
    public static void ClampAlertTiersRaisesCriticalToHighWhenBelowIt()
    {
        var (high, critical) = Settings.ClampAlertTiers(0.5f, 1f, 0.75f);
        Assert.That(high).Is.EqualTo(1f);
        Assert.That(critical).Is.EqualTo(1f);
    }

    [Test]
    public static void ClampAlertTiersRaisesCriticalTransitivelyThroughRaisedHigh()
    {
        var (high, critical) = Settings.ClampAlertTiers(3f, 1f, 2f);
        Assert.That(high).Is.EqualTo(3f);
        Assert.That(critical).Is.EqualTo(3f);
    }

    [Test]
    public static void ClampAlertTiersKeepsEqualValuesStable()
    {
        var (high, critical) = Settings.ClampAlertTiers(1f, 1f, 1f);
        Assert.That(high).Is.EqualTo(1f);
        Assert.That(critical).Is.EqualTo(1f);
    }

    [Test]
    public static void FindMatchingTicksReturnsExactMatch() =>
        Assert.That(Settings.FindMatchingTicks([100, 200, 300], 200)!.Value).Is.EqualTo(200);

    [Test]
    public static void FindMatchingTicksFallsBackToNullWhenNoMatch() =>
        Assert.That(Settings.FindMatchingTicks([100, 200, 300], 250)).Is.Null();

    [Test]
    public static void FindMatchingTicksFallsBackToNullOnEmptyList() =>
        Assert.That(Settings.FindMatchingTicks([], 100)).Is.Null();

    [Test]
    public static void FindMatchingTicksReturnsFirstOfDuplicateMatches() =>
        Assert.That(Settings.FindMatchingTicks([100, 100, 200], 100)!.Value).Is.EqualTo(100);
}
