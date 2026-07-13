// UtilitiesAreaTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class UtilitiesAreaTests
{
    [Test]
    public static void NoAreaIsAlwaysAllowedRegardlessOfInvert()
    {
        Assert
            .That(
                Utilities.IsInAllowedArea(
                    hasArea: false,
                    areaContainsPosition: false,
                    invert: false
                )
            )
            .Is.True();
        Assert
            .That(
                Utilities.IsInAllowedArea(hasArea: false, areaContainsPosition: false, invert: true)
            )
            .Is.True();
        Assert
            .That(
                Utilities.IsInAllowedArea(hasArea: false, areaContainsPosition: true, invert: false)
            )
            .Is.True();
        Assert
            .That(
                Utilities.IsInAllowedArea(hasArea: false, areaContainsPosition: true, invert: true)
            )
            .Is.True();
    }

    [Test]
    public static void InsideAreaIsAllowedWhenNotInverted() =>
        Assert
            .That(
                Utilities.IsInAllowedArea(hasArea: true, areaContainsPosition: true, invert: false)
            )
            .Is.True();

    [Test]
    public static void InsideAreaIsDisallowedWhenInverted() =>
        Assert
            .That(
                Utilities.IsInAllowedArea(hasArea: true, areaContainsPosition: true, invert: true)
            )
            .Is.False();

    [Test]
    public static void OutsideAreaIsDisallowedWhenNotInverted() =>
        Assert
            .That(
                Utilities.IsInAllowedArea(hasArea: true, areaContainsPosition: false, invert: false)
            )
            .Is.False();

    [Test]
    public static void OutsideAreaIsAllowedWhenInverted() =>
        Assert
            .That(
                Utilities.IsInAllowedArea(hasArea: true, areaContainsPosition: false, invert: true)
            )
            .Is.True();
}
