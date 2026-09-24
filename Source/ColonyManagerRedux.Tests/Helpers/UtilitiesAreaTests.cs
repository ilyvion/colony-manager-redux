// UtilitiesAreaTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class UtilitiesAreaTests
{
    [Test]
    public static void NoAreaIsAlwaysAllowedRegardlessOfInvert()
    {
        Expect.IsTrue(
            Utilities.IsInAllowedArea(hasArea: false, areaContainsPosition: false, invert: false)
        );
        Expect.IsTrue(
            Utilities.IsInAllowedArea(hasArea: false, areaContainsPosition: false, invert: true)
        );
        Expect.IsTrue(
            Utilities.IsInAllowedArea(hasArea: false, areaContainsPosition: true, invert: false)
        );
        Expect.IsTrue(
            Utilities.IsInAllowedArea(hasArea: false, areaContainsPosition: true, invert: true)
        );
    }

    [Test]
    public static void InsideAreaIsAllowedWhenNotInverted() =>
        Expect.IsTrue(
            Utilities.IsInAllowedArea(hasArea: true, areaContainsPosition: true, invert: false)
        );

    [Test]
    public static void InsideAreaIsDisallowedWhenInverted() =>
        Expect.IsFalse(
            Utilities.IsInAllowedArea(hasArea: true, areaContainsPosition: true, invert: true)
        );

    [Test]
    public static void OutsideAreaIsDisallowedWhenNotInverted() =>
        Expect.IsFalse(
            Utilities.IsInAllowedArea(hasArea: true, areaContainsPosition: false, invert: false)
        );

    [Test]
    public static void OutsideAreaIsAllowedWhenInverted() =>
        Expect.IsTrue(
            Utilities.IsInAllowedArea(hasArea: true, areaContainsPosition: false, invert: true)
        );
}
