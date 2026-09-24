// DateExtensionsTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[HotSwappable]
[TestFixture(TestType.MainMenu)]
internal sealed class DateExtensionsTests
{
    [Test]
    public static void NegativeTicksReturnZeroLiteral() =>
        Expect.AreEqual("0", (-1).ToStringTicksToPeriodVerboseFull());

    [Test]
    public static void NegativeTicksReturnZeroLiteralEvenWithHoursDisallowed() =>
        Expect.AreEqual("0", (-1).ToStringTicksToPeriodVerboseFull(allowHours: false));

    [Test]
    public static void DescribeHoursPartHidesZeroHoursWhenLargerUnitPresent() =>
        Expect.IsFalse(DateExtensions.DescribeHoursPart(0f, hasLargerUnit: true).show);

    [Test]
    public static void DescribeHoursPartShowsWholeHoursWhenLargerUnitPresent()
    {
        var (show, wholeNumber, whole, _) = DateExtensions.DescribeHoursPart(
            4.9f,
            hasLargerUnit: true
        );
        Expect.IsTrue(show);
        Expect.IsTrue(wholeNumber);
        // Truncates (via (int) cast), doesn't round, once a larger unit already absorbed the rest.
        Expect.AreEqual(4, whole);
    }

    [Test]
    public static void DescribeHoursPartHidesNonPositiveHoursWithoutLargerUnit() =>
        Expect.IsFalse(DateExtensions.DescribeHoursPart(0f, hasLargerUnit: false).show);

    [Test]
    public static void DescribeHoursPartRoundsWholeHoursAboveOneWithoutLargerUnit()
    {
        // 2.6 rounds unambiguously up to 3.
        var (show, wholeNumber, whole, _) = DateExtensions.DescribeHoursPart(
            2.6f,
            hasLargerUnit: false
        );
        Expect.IsTrue(show);
        Expect.IsTrue(wholeNumber);
        Expect.AreEqual(3, whole);
    }

    [Test]
    public static void DescribeHoursPartTreatsValueRoundingToOneAsWholeHour()
    {
        // 0.96 rounds to 1.0 at one decimal place, so it must render as
        // the singular "1 hour" rather than the fractional "1.0 hours" branch.
        var (show, wholeNumber, whole, _) = DateExtensions.DescribeHoursPart(
            0.96f,
            hasLargerUnit: false
        );
        Expect.IsTrue(show);
        Expect.IsTrue(wholeNumber);
        Expect.AreEqual(1, whole);
    }

    [Test]
    public static void DescribeHoursPartShowsFractionalHoursBelowRoundingThreshold()
    {
        var (show, wholeNumber, _, fractional) = DateExtensions.DescribeHoursPart(
            0.5f,
            hasLargerUnit: false
        );
        Expect.IsTrue(show);
        Expect.IsFalse(wholeNumber);
        Expect.AreEqual(0.5f, fractional);
    }
}
