// UtilitiesBuildGridRectsTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class UtilitiesBuildGridRectsTests
{
    [Test]
    public static void GridRectsAreIndexedByRowThenColumn()
    {
        float[] widths = [10f, 20f, 30f];
        float[] heights = [5f, 15f];

        var rects = Utilities.BuildGridRects(new Vector2(100f, 200f), widths, heights);

        Assert.That(rects.GetLength(0)).Is.EqualTo(2);
        Assert.That(rects.GetLength(1)).Is.EqualTo(3);
    }

    [Test]
    public static void GridRectsAccumulateOffsetsFromPosition()
    {
        float[] widths = [10f, 20f, 30f];
        float[] heights = [5f, 15f];

        var rects = Utilities.BuildGridRects(new Vector2(100f, 200f), widths, heights);

        // row 0, col 0 sits exactly at pos.
        Assert.That(rects[0, 0].x).Is.EqualTo(100f);
        Assert.That(rects[0, 0].y).Is.EqualTo(200f);

        // col 2 is offset by the running sum of the preceding column widths (10 + 20).
        Assert.That(rects[0, 2].x).Is.EqualTo(130f);

        // row 1 is offset by the running sum of the preceding row heights (5).
        Assert.That(rects[1, 0].y).Is.EqualTo(205f);
    }

    [Test]
    public static void GridRectsUseTheirColumnWidthAndRowHeightUnlessMarginIsGiven()
    {
        float[] widths = [10f, 20f, 30f];
        float[] heights = [5f, 15f];

        var rects = Utilities.BuildGridRects(Vector2.zero, widths, heights);

        Assert.That(rects[0, 1].width).Is.EqualTo(20f);
        Assert.That(rects[1, 2].height).Is.EqualTo(15f);
    }

    // DrawAreaRestrictionsSection relies on columnMargin to leave a gap between adjacent
    // area-selector cells; a regression here would make selectors visually overlap.
    [Test]
    public static void ColumnMarginIsSubtractedFromEachRectsWidthOnly()
    {
        float[] widths = [10f, 20f];
        float[] heights = [5f];

        var rects = Utilities.BuildGridRects(Vector2.zero, widths, heights, columnMargin: 3f);

        Assert.That(rects[0, 0].width).Is.EqualTo(7f);
        Assert.That(rects[0, 1].width).Is.EqualTo(17f);
        Assert.That(rects[0, 0].height).Is.EqualTo(5f);
    }
}
