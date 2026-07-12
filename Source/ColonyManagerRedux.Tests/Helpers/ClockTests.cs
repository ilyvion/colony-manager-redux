// ClockTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[HotSwappable]
[TestSuite]
internal static class ClockTests
{
    private static readonly Rect SquareCanvas = new(0f, 0f, 100f, 100f);

    private static Vector2 Round(Vector2 v) => new(MathF.Round(v.x), MathF.Round(v.y));

    [Test]
    public static void HourZeroPointsToTopOfCanvas()
    {
        var (from, to) = Clock.ComputeHandlePoints(SquareCanvas, 0f, 0f, 1f);
        Assert.That(Round(from).x).Is.EqualTo(50f);
        Assert.That(Round(from).y).Is.EqualTo(50f);
        Assert.That(Round(to).x).Is.EqualTo(50f);
        Assert.That(Round(to).y).Is.EqualTo(0f);
    }

    [Test]
    public static void HourThreePointsRight()
    {
        var (_, to) = Clock.ComputeHandlePoints(SquareCanvas, 3f, 0f, 1f);
        Assert.That(Round(to).x).Is.EqualTo(100f);
        Assert.That(Round(to).y).Is.EqualTo(50f);
    }

    [Test]
    public static void HourSixPointsToBottomOfCanvas()
    {
        var (_, to) = Clock.ComputeHandlePoints(SquareCanvas, 6f, 0f, 1f);
        Assert.That(Round(to).x).Is.EqualTo(50f);
        Assert.That(Round(to).y).Is.EqualTo(100f);
    }

    [Test]
    public static void HourNinePointsLeft()
    {
        var (_, to) = Clock.ComputeHandlePoints(SquareCanvas, 9f, 0f, 1f);
        Assert.That(Round(to).x).Is.EqualTo(0f);
        Assert.That(Round(to).y).Is.EqualTo(50f);
    }

    [Test]
    public static void HourTwelveWrapsConsistentlyWithHourZero()
    {
        var (_, hourZeroTo) = Clock.ComputeHandlePoints(SquareCanvas, 0f, 0f, 1f);
        var (_, hourTwelveTo) = Clock.ComputeHandlePoints(SquareCanvas, 12f, 0f, 1f);
        Assert.That(Round(hourZeroTo).x).Is.EqualTo(Round(hourTwelveTo).x);
        Assert.That(Round(hourZeroTo).y).Is.EqualTo(Round(hourTwelveTo).y);
    }

    [Test]
    public static void StartAndEndScaleAlongTheHandleDirection()
    {
        var (from, to) = Clock.ComputeHandlePoints(SquareCanvas, 3f, 0.2f, 0.8f);
        Assert.That(Round(from).x).Is.EqualTo(60f);
        Assert.That(Round(from).y).Is.EqualTo(50f);
        Assert.That(Round(to).x).Is.EqualTo(90f);
        Assert.That(Round(to).y).Is.EqualTo(50f);
    }

    [Test]
    public static void RadiusUsesTheShorterCanvasDimension()
    {
        // A 200x100 canvas must use a radius of 50 (half the shorter dimension),
        // not 100 (half the width) -- an easy mistake if width were used directly.
        var wideCanvas = new Rect(0f, 0f, 200f, 100f);
        var (_, to) = Clock.ComputeHandlePoints(wideCanvas, 3f, 0f, 1f);
        Assert.That(Round(to).x).Is.EqualTo(150f);
        Assert.That(Round(to).y).Is.EqualTo(50f);
    }
}
