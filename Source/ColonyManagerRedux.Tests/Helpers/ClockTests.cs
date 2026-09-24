// ClockTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[HotSwappable]
[TestFixture(TestType.MainMenu)]
internal sealed class ClockTests
{
    private static readonly Rect SquareCanvas = new(0f, 0f, 100f, 100f);

    private static Vector2 Round(Vector2 v) => new(Mathf.Round(v.x), Mathf.Round(v.y));

    [Test]
    public static void HourZeroPointsToTopOfCanvas()
    {
        var (from, to) = Clock.ComputeHandlePoints(SquareCanvas, 0f, 0f, 1f);
        Expect.AreEqual(50f, Round(from).x);
        Expect.AreEqual(50f, Round(from).y);
        Expect.AreEqual(50f, Round(to).x);
        Expect.AreEqual(0f, Round(to).y);
    }

    [Test]
    public static void HourThreePointsRight()
    {
        var (_, to) = Clock.ComputeHandlePoints(SquareCanvas, 3f, 0f, 1f);
        Expect.AreEqual(100f, Round(to).x);
        Expect.AreEqual(50f, Round(to).y);
    }

    [Test]
    public static void HourSixPointsToBottomOfCanvas()
    {
        var (_, to) = Clock.ComputeHandlePoints(SquareCanvas, 6f, 0f, 1f);
        Expect.AreEqual(50f, Round(to).x);
        Expect.AreEqual(100f, Round(to).y);
    }

    [Test]
    public static void HourNinePointsLeft()
    {
        var (_, to) = Clock.ComputeHandlePoints(SquareCanvas, 9f, 0f, 1f);
        Expect.AreEqual(0f, Round(to).x);
        Expect.AreEqual(50f, Round(to).y);
    }

    [Test]
    public static void HourTwelveWrapsConsistentlyWithHourZero()
    {
        var (_, hourZeroTo) = Clock.ComputeHandlePoints(SquareCanvas, 0f, 0f, 1f);
        var (_, hourTwelveTo) = Clock.ComputeHandlePoints(SquareCanvas, 12f, 0f, 1f);
        Expect.AreEqual(Round(hourTwelveTo).x, Round(hourZeroTo).x);
        Expect.AreEqual(Round(hourTwelveTo).y, Round(hourZeroTo).y);
    }

    [Test]
    public static void StartAndEndScaleAlongTheHandleDirection()
    {
        var (from, to) = Clock.ComputeHandlePoints(SquareCanvas, 3f, 0.2f, 0.8f);
        Expect.AreEqual(60f, Round(from).x);
        Expect.AreEqual(50f, Round(from).y);
        Expect.AreEqual(90f, Round(to).x);
        Expect.AreEqual(50f, Round(to).y);
    }

    [Test]
    public static void RadiusUsesTheShorterCanvasDimension()
    {
        // A 200x100 canvas must use a radius of 50 (half the shorter dimension),
        // not 100 (half the width) -- an easy mistake if width were used directly.
        var wideCanvas = new Rect(0f, 0f, 200f, 100f);
        var (_, to) = Clock.ComputeHandlePoints(wideCanvas, 3f, 0f, 1f);
        Expect.AreEqual(150f, Round(to).x);
        Expect.AreEqual(50f, Round(to).y);
    }
}
