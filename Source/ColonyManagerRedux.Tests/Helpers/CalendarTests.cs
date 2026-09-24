// CalendarTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[HotSwappable]
[TestFixture(TestType.MainMenu)]
internal sealed class CalendarTests
{
    [Test]
    public static void ComputeSquareSizeFitsExactlyForPerfectSquareCanvasAndCount() =>
        // 9 cells laid out 3x3 across a 90x90 canvas fit exactly at size 30.
        Expect.AreEqual(30, Calendar.ComputeSquareSize(90f, 90f, 9));

    [Test]
    public static void ComputeSquareSizeFitsExactlyForAsymmetricCanvas() =>
        // 8 cells laid out 4x2 across a 200x100 canvas fit exactly at size 50.
        Expect.AreEqual(50, Calendar.ComputeSquareSize(200f, 100f, 8));

    [Test]
    public static void ComputeSquareSizeRoundsDownForNonPerfectSquareCount() =>
        // 8 cells in a 100x100 canvas need a 3x3 grid (9 slots), so size is floor(100/3).
        Expect.AreEqual(33, Calendar.ComputeSquareSize(100f, 100f, 8));

    [Test]
    public static void ComputeSquareSizeTakesAlternateBranchWhenCeilingOverfits() =>
        // With x=100, y=30, n=7 the straightforward px/py candidates overshoot the
        // available cells, so the algorithm falls back to its alternate (ceiling-based)
        // branch for at least one axis. Pin the resulting size down explicitly.
        Expect.AreEqual(15, Calendar.ComputeSquareSize(100f, 30f, 7));

    [Test]
    public static void ComputeSquareSizeDoesNotSilentlyCollapseToZeroForZeroDays() =>
        // n=0 drives both px and py to 0, so the fallback division (x/px) divides by
        // zero. Casting the resulting infinity to int is runtime-defined, but it must
        // not silently produce 0 (which would draw invisible day cells) and must not throw.
        Expect.AreNotEqual(0, Calendar.ComputeSquareSize(100f, 100f, 0));

    [Test]
    public static void BuildSizeCacheKeyCanCollideOnNearIdenticalCoordinates()
    {
        // The cache key rounds x/y to 3 decimal digits, so two distinct floats within
        // that tolerance produce an identical key -- a latent cache-collision risk.
        var keyA = Calendar.BuildSizeCacheKey(100.0001f, 50f, 12);
        var keyB = Calendar.BuildSizeCacheKey(100.0002f, 50f, 12);
        Expect.AreEqual(keyB, keyA);
    }
}
