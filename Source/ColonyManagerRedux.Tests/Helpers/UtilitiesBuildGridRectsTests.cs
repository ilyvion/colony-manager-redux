// UtilitiesBuildGridRectsTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class UtilitiesBuildGridRectsTests
{
    [Test]
    public static void GridRectsAreIndexedByRowThenColumn()
    {
        float[] widths = [10f, 20f, 30f];
        float[] heights = [5f, 15f];

        var rects = Utilities.BuildGridRects(new Vector2(100f, 200f), widths, heights);

        Expect.AreEqual(2, rects.GetLength(0));
        Expect.AreEqual(3, rects.GetLength(1));
    }

    [Test]
    public static void GridRectsAccumulateOffsetsFromPosition()
    {
        float[] widths = [10f, 20f, 30f];
        float[] heights = [5f, 15f];

        var rects = Utilities.BuildGridRects(new Vector2(100f, 200f), widths, heights);

        // row 0, col 0 sits exactly at pos.
        Expect.AreEqual(100f, rects[0, 0].x);
        Expect.AreEqual(200f, rects[0, 0].y);

        // col 2 is offset by the running sum of the preceding column widths (10 + 20).
        Expect.AreEqual(130f, rects[0, 2].x);

        // row 1 is offset by the running sum of the preceding row heights (5).
        Expect.AreEqual(205f, rects[1, 0].y);
    }

    [Test]
    public static void GridRectsUseTheirColumnWidthAndRowHeightUnlessMarginIsGiven()
    {
        float[] widths = [10f, 20f, 30f];
        float[] heights = [5f, 15f];

        var rects = Utilities.BuildGridRects(Vector2.zero, widths, heights);

        Expect.AreEqual(20f, rects[0, 1].width);
        Expect.AreEqual(15f, rects[1, 2].height);
    }

    // DrawAreaRestrictionsSection relies on columnMargin to leave a gap between adjacent
    // area-selector cells; a regression here would make selectors visually overlap.
    [Test]
    public static void ColumnMarginIsSubtractedFromEachRectsWidthOnly()
    {
        float[] widths = [10f, 20f];
        float[] heights = [5f];

        var rects = Utilities.BuildGridRects(Vector2.zero, widths, heights, columnMargin: 3f);

        Expect.AreEqual(7f, rects[0, 0].width);
        Expect.AreEqual(17f, rects[0, 1].width);
        Expect.AreEqual(5f, rects[0, 0].height);
    }
}
