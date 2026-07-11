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
}
