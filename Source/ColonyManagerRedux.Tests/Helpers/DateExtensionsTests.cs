// DateExtensionsTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[HotSwappable]
[TestSuite]
internal static class DateExtensionsTests
{
    [Test]
    public static void NegativeTicksReturnZeroLiteral() =>
        Assert.That((-1).ToStringTicksToPeriodVerboseFull()).Is.EqualTo("0");

    [Test]
    public static void NegativeTicksReturnZeroLiteralEvenWithHoursDisallowed() =>
        Assert.That((-1).ToStringTicksToPeriodVerboseFull(allowHours: false)).Is.EqualTo("0");

    [Test]
    public static void DescribeHoursPartHidesZeroHoursWhenLargerUnitPresent() =>
        Assert.That(DateExtensions.DescribeHoursPart(0f, hasLargerUnit: true).show).Is.False();

    [Test]
    public static void DescribeHoursPartShowsWholeHoursWhenLargerUnitPresent()
    {
        var (show, wholeNumber, whole, _) = DateExtensions.DescribeHoursPart(
            4.9f,
            hasLargerUnit: true
        );
        Assert.That(show).Is.True();
        Assert.That(wholeNumber).Is.True();
        // Truncates (via (int) cast), doesn't round, once a larger unit already absorbed the rest.
        Assert.That(whole).Is.EqualTo(4);
    }

    [Test]
    public static void DescribeHoursPartHidesNonPositiveHoursWithoutLargerUnit() =>
        Assert.That(DateExtensions.DescribeHoursPart(0f, hasLargerUnit: false).show).Is.False();

    [Test]
    public static void DescribeHoursPartRoundsWholeHoursAboveOneWithoutLargerUnit()
    {
        // 2.6 rounds unambiguously up to 3.
        var (show, wholeNumber, whole, _) = DateExtensions.DescribeHoursPart(
            2.6f,
            hasLargerUnit: false
        );
        Assert.That(show).Is.True();
        Assert.That(wholeNumber).Is.True();
        Assert.That(whole).Is.EqualTo(3);
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
        Assert.That(show).Is.True();
        Assert.That(wholeNumber).Is.True();
        Assert.That(whole).Is.EqualTo(1);
    }

    [Test]
    public static void DescribeHoursPartShowsFractionalHoursBelowRoundingThreshold()
    {
        var (show, wholeNumber, _, fractional) = DateExtensions.DescribeHoursPart(
            0.5f,
            hasLargerUnit: false
        );
        Assert.That(show).Is.True();
        Assert.That(wholeNumber).Is.False();
        Assert.That(fractional).Is.EqualTo(0.5f);
    }
}
