// StockpileGUI.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024–2025 Alexander Krivács Schrøder

using System.Diagnostics.CodeAnalysis;
using ilyvion.Laboratory.Extensions;
using Verse.Sound;

namespace ColonyManagerRedux;

/// <summary>
/// Provides UI helpers for selecting and displaying stockpiles in the manager interface.
/// </summary>
[HotSwappable]
public static class StockpileGUI
{
    /// <summary>
    /// Number of stockpiles displayed per row in the selector UI.
    /// </summary>
    public const int StockPilesPerRow = 2;

    private const int MaxRowsWithoutScroll = 3;

    private static List<Texture2D>? textures;
    private static readonly List<Color> _textureColors = [];
    private static readonly List<Color> _tmpColors = [];

    private static Vector2 _scrollPosition;
    private static readonly List<Zone_Stockpile> _tmpStockpiles = [];

    /// <summary>
    /// Draws the stockpile selector UI and updates the active stockpile.
    /// </summary>
    /// <param name="position">The position to start drawing.</param>
    /// <param name="width">The width of the selector area.</param>
    /// <param name="activeStockpile">Reference to the currently active stockpile.</param>
    /// <param name="map">The map containing the stockpiles.</param>
    /// <returns>The height of the drawn selector area.</returns>
    public static float DoStockpileSelectors(
        Vector2 position,
        float width,
        ref Zone_Stockpile? activeStockpile,
        Map map
    )
    {
        if (map == null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        // get all stockpiles
        _tmpStockpiles.AddRange(map.zoneManager.AllZones.OfType<Zone_Stockpile>());
        using var _ = new DoOnDispose(_tmpStockpiles.Clear);

        // count + 1 for all stockpiles
        var stockPileCount = _tmpStockpiles.Count + 1;
        var (rowCount, needsScrollbars) = ComputeGrid(
            stockPileCount,
            StockPilesPerRow,
            MaxRowsWithoutScroll
        );
        var viewWidth = needsScrollbars ? width - 16f : width;
        var widthPerCell = viewWidth / StockPilesPerRow;

        // create colour swatch
        _tmpColors.Clear();
        foreach (var stockpile in _tmpStockpiles)
        {
            _tmpColors.Add(stockpile.color);
        }
        if (textures == null || ShouldRebuildTextures(_textureColors, _tmpColors))
        {
            CreateTextures(_tmpStockpiles);
        }

        mouseOverZone = null;

        Text.WordWrap = false;
        Text.Font = GameFont.Tiny;

        Widgets.BeginScrollView(
            new Rect(position.x, position.y, width, 3 * Constants.ListEntryHeight),
            ref _scrollPosition,
            new Rect(position.x, position.y, viewWidth, rowCount * Constants.ListEntryHeight),
            needsScrollbars
        );

        for (var j = 0; j < stockPileCount; j++)
        {
            if (j == 0)
            {
                var nullAreaRect = new Rect(
                    position.x,
                    position.y,
                    widthPerCell,
                    Constants.ListEntryHeight
                );
                DoZoneSelector(nullAreaRect, ref activeStockpile, null, BaseContent.GreyTex);
            }
            else
            {
                var origin = CellOrigin(
                    j,
                    StockPilesPerRow,
                    widthPerCell,
                    Constants.ListEntryHeight
                );
                var stockpileRect = new Rect(
                    position.x + origin.x,
                    position.y + origin.y,
                    widthPerCell,
                    Constants.ListEntryHeight
                );
                DoZoneSelector(
                    stockpileRect,
                    ref activeStockpile,
                    _tmpStockpiles[j - 1],
                    textures[j - 1]
                );
            }
        }

        Widgets.EndScrollView();

        Text.WordWrap = true;
        Text.Font = GameFont.Small;

        return rowCount * Constants.ListEntryHeight;
    }

    /// <summary>
    /// The number of rows needed to lay out <paramref name="itemCount"/> cells
    /// <paramref name="perRow"/> to a row, and whether that exceeds
    /// <paramref name="maxRowsWithoutScroll"/>.
    /// </summary>
    internal static (int rows, bool needsScroll) ComputeGrid(
        int itemCount,
        int perRow,
        int maxRowsWithoutScroll
    )
    {
        var rows = (int)Math.Ceiling((double)itemCount / perRow);
        return (rows, rows > maxRowsWithoutScroll);
    }

    /// <summary>
    /// The top-left corner of cell <paramref name="index"/> in a grid of
    /// <paramref name="perRow"/> cells per row.
    /// </summary>
    internal static Vector2 CellOrigin(int index, int perRow, float cellWidth, float rowHeight) =>
        new(index % perRow * cellWidth, index / perRow * rowHeight);

    /// <summary>
    /// Whether the cached swatch colours no longer match the current stockpiles' colours, in count
    /// or in order.
    /// </summary>
    internal static bool ShouldRebuildTextures(
        IReadOnlyList<Color> cached,
        IReadOnlyList<Color> current
    )
    {
        if (cached.Count != current.Count)
        {
            return true;
        }
        for (var i = 0; i < current.Count; i++)
        {
            if (cached[i] != current[i])
            {
                return true;
            }
        }
        return false;
    }

    private static bool addedPostDrawSelectionOverlaysAction;
    private static Zone? mouseOverZone;

    private static readonly Color MouseOverColor = new(.75f, .75f, .75f);

    private static void PostDrawSelectionOverlays()
    {
        if (ShouldDrawMouseOverZone(mouseOverZone))
        {
            GenDraw.DrawFieldEdges(mouseOverZone!.Cells, MouseOverColor);
        }
    }

    internal static bool ShouldDrawMouseOverZone(Zone? zone) =>
        zone != null && !Find.Selector.IsSelected(zone);

    [MemberNotNull(nameof(textures))]
    private static void CreateTextures(List<Zone_Stockpile> zones)
    {
        if (textures != null)
        {
            foreach (var tex in textures)
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }

            textures.Clear();
        }
        else
        {
            textures = [];
        }

        _textureColors.Clear();
        foreach (var zone in zones)
        {
            textures.Add(SolidColorMaterials.NewSolidColorTexture(zone.color));
            _textureColors.Add(zone.color);
        }
    }

    // RimWorld.AreaAllowedGUI
    private static void DoZoneSelector(
        Rect rect,
        ref Zone_Stockpile? activeStockpile,
        Zone_Stockpile? zone,
        Texture2D tex
    )
    {
        if (!addedPostDrawSelectionOverlaysAction)
        {
            RimWorld_SelectionDrawer_DrawSelectionOverlays.PostDrawSelectionOverlaysActions.Add(
                PostDrawSelectionOverlays
            );
            addedPostDrawSelectionOverlaysAction = true;
        }

        rect = rect.ContractedBy(1f);
        GUI.DrawTexture(rect, tex);
        Text.Anchor = TextAnchor.MiddleLeft;
        var label = zone?.label ?? "ColonyManagerRedux.AnyStockpile".Translate();
        var innerRect = rect;
        innerRect.xMin += 4f;
        innerRect.xMax -= 4f;
        innerRect.yMin += 2f;
        Widgets.Label(innerRect, label.Truncate(innerRect.width));
        if (activeStockpile == zone)
        {
            Widgets.DrawBox(rect, 2);
        }

        if (Mouse.IsOver(rect))
        {
            if (zone != null)
            {
                mouseOverZone = zone;
                if (zone.AllSlotCellsList() != null && zone.AllSlotCellsList().Count > 0)
                {
                    if (!Find.CameraDriver.IsPanning())
                    {
                        CameraJumper.TryJump(zone.Cells.First(), zone.Map);
                    }
                }
            }

            if (Input.GetMouseButton(0) && activeStockpile != zone)
            {
                activeStockpile = zone;
                SoundDefOf.Designate_DragStandard_Changed.PlayOneShotOnCamera();
            }
        }

        TooltipHandler.TipRegion(rect, label);
        Text.Anchor = TextAnchor.UpperLeft;
    }
}
