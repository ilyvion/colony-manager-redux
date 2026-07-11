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
}
