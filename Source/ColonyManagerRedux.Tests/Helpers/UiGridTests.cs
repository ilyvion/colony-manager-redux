// UiGridTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The grid layout maths, swatch-cache check and multi-select toggling behind
/// <see cref="StockpileGUI"/> and <see cref="AreaAllowedGUI"/>.
/// </summary>
[TestFixture(TestType.MainMenu)]
internal sealed class UiGridTests
{
    // StockpileGUI.ComputeGrid: the count includes the 'any stockpile' cell.

    [Test]
    public static void StockpileGridRowsRoundUpAndScrollPastThreeRows()
    {
        foreach (
            var (count, rows, scroll) in new[]
            {
                (0, 0, false),
                (1, 1, false),
                (2, 1, false),
                (3, 2, false),
                (6, 3, false),
                (7, 4, true),
                (8, 4, true),
            }
        )
        {
            var result = StockpileGUI.ComputeGrid(count, 2, 3);

            Expect.AreEqual(rows, result.rows, $"rows for {count}");
            Expect.AreEqual(scroll, result.needsScroll, $"scroll for {count}");
        }
    }

    [Test]
    public static void CellOriginsWrapAfterEachRow()
    {
        Expect.AreEqual(new Vector2(0f, 0f), StockpileGUI.CellOrigin(0, 2, 100f, 30f));
        Expect.AreEqual(new Vector2(100f, 0f), StockpileGUI.CellOrigin(1, 2, 100f, 30f));
        Expect.AreEqual(new Vector2(0f, 30f), StockpileGUI.CellOrigin(2, 2, 100f, 30f));
        Expect.AreEqual(new Vector2(100f, 60f), StockpileGUI.CellOrigin(5, 2, 100f, 30f));
    }

    // StockpileGUI.ShouldRebuildTextures

    private static readonly Color Red = Color.red;
    private static readonly Color Blue = Color.blue;

    [Test]
    public static void SwatchesAreKeptWhenTheColoursMatch() =>
        Expect.IsFalse(StockpileGUI.ShouldRebuildTextures([Red, Blue], [Red, Blue]));

    [Test]
    public static void SwatchesAreRebuiltWhenTheCountChanges()
    {
        Expect.IsTrue(StockpileGUI.ShouldRebuildTextures([Red], [Red, Blue]));
        Expect.IsTrue(StockpileGUI.ShouldRebuildTextures([Red, Blue], [Red]));
    }

    [Test]
    public static void SwatchesAreRebuiltWhenAColourChangesAtTheSameCount() =>
        Expect.IsTrue(StockpileGUI.ShouldRebuildTextures([Red, Blue], [Red, Color.green]));

    [Test]
    public static void SwatchesAreRebuiltWhenTheOrderChanges() =>
        Expect.IsTrue(StockpileGUI.ShouldRebuildTextures([Red, Blue], [Blue, Red]));

    [Test]
    public static void NoSwatchesAndNoStockpilesNeedNoRebuild() =>
        Expect.IsFalse(StockpileGUI.ShouldRebuildTextures([], []));

    // AreaAllowedGUI.ComputeAreaGrid: the count includes the 'any area' cell.

    [Test]
    public static void AreaGridClampsPerRowToTheCellCount()
    {
        var (rows, perRow) = AreaAllowedGUI.ComputeAreaGrid(2, 4);

        Expect.AreEqual(1, rows);
        Expect.AreEqual(2, perRow);
    }

    [Test]
    public static void AreaGridWithASingleAreaIsOneCell()
    {
        var (rows, perRow) = AreaAllowedGUI.ComputeAreaGrid(1, 4);

        Expect.AreEqual(1, rows);
        Expect.AreEqual(1, perRow);
    }

    [Test]
    public static void AreaGridFillingExactlyOneRowStaysOnOneRow()
    {
        var (rows, perRow) = AreaAllowedGUI.ComputeAreaGrid(4, 4);

        Expect.AreEqual(1, rows);
        Expect.AreEqual(4, perRow);
    }

    [Test]
    public static void AreaGridWrapsPartialRows()
    {
        var (rows, perRow) = AreaAllowedGUI.ComputeAreaGrid(5, 4);

        Expect.AreEqual(2, rows);
        Expect.AreEqual(4, perRow);
    }

    [Test]
    public static void AreaGridTreatsAZeroOrNegativePerRowAsOne()
    {
        foreach (var requested in new[] { 0, -3 })
        {
            var (rows, perRow) = AreaAllowedGUI.ComputeAreaGrid(3, requested);

            Expect.AreEqual(3, rows, $"rows for {requested}");
            Expect.AreEqual(1, perRow, $"perRow for {requested}");
        }
    }

    // AreaAllowedGUI.ApplyToggle

    [Test]
    public static void ToggleOnAddsTheItem()
    {
        var set = new HashSet<int>();

        AreaAllowedGUI.ApplyToggle(set, 1, wasOn: false, nowOn: true);

        Expect.IsTrue(set.Contains(1));
    }

    [Test]
    public static void ToggleOffRemovesTheItem()
    {
        var set = new HashSet<int> { 1, 2 };

        AreaAllowedGUI.ApplyToggle(set, 1, wasOn: true, nowOn: false);

        Expect.IsFalse(set.Contains(1));
        Expect.IsTrue(set.Contains(2));
    }

    [Test]
    public static void UnchangedStateLeavesTheSetAlone()
    {
        var set = new HashSet<int> { 2 };

        AreaAllowedGUI.ApplyToggle(set, 1, wasOn: false, nowOn: false);
        AreaAllowedGUI.ApplyToggle(set, 2, wasOn: true, nowOn: true);

        Expect.AreEqual(1, set.Count);
        Expect.IsTrue(set.Contains(2));
    }
}
