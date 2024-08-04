// StockpileGUI.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Diagnostics.CodeAnalysis;
using ilyvion.Laboratory.Extensions;
using Verse.Sound;

namespace ColonyManagerRedux;

[HotSwappable]
public static class StockpileGUI
{
    public const int StockPilesPerRow = 2;

    private static List<Texture2D>? textures;

    private static Vector2 _scrollPosition;
    public static float DoStockpileSelectors(Vector2 position, float width, ref Zone_Stockpile? activeStockpile, Map map)
    {
        if (map == null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        // get all stockpiles
        var allStockpiles = map.zoneManager.AllZones.OfType<Zone_Stockpile>().ToList();

        // count + 1 for all stockpiles
        var stockPileCount = allStockpiles.Count + 1;
        int rowCount = (int)Math.Ceiling((double)stockPileCount / StockPilesPerRow);
        bool needsScrollbars = rowCount > 3;
        float viewWidth = needsScrollbars ? width - 16f : width;
        var widthPerCell = viewWidth / StockPilesPerRow;

        // create colour swatch
        if (textures == null || textures.Count != stockPileCount - 1)
        {
            CreateTextures(allStockpiles);
        }

        mouseOverZone = null;

        Text.WordWrap = false;
        Text.Font = GameFont.Tiny;

        Widgets.BeginScrollView(
            new Rect(position.x, position.y, width, 3 * Constants.ListEntryHeight),
            ref _scrollPosition,
            new Rect(position.x, position.y, viewWidth, rowCount * Constants.ListEntryHeight),
            needsScrollbars);

        for (var j = 0; j < stockPileCount; j++)
        {
            if (j == 0)
            {
                var nullAreaRect = new Rect(position.x, position.y, widthPerCell, Constants.ListEntryHeight);
                DoZoneSelector(nullAreaRect, ref activeStockpile, null, BaseContent.GreyTex);
            }
            else
            {
                var stockpileRect = new Rect(
                    position.x + j % StockPilesPerRow * widthPerCell,
                    position.y + j / StockPilesPerRow * Constants.ListEntryHeight,
                    widthPerCell, Constants.ListEntryHeight);
                DoZoneSelector(stockpileRect, ref activeStockpile, allStockpiles[j - 1], textures[j - 1]);
            }
        }

        Widgets.EndScrollView();

        Text.WordWrap = true;
        Text.Font = GameFont.Small;

        return rowCount * Constants.ListEntryHeight;
    }


    private static bool addedPostDrawSelectionOverlaysAction;
    private static Zone? mouseOverZone;

    private static readonly Color MouseOverColor = new(.75f, .75f, .75f);
    private static void PostDrawSelectionOverlays()
    {
        if (mouseOverZone != null && !Find.Selector.IsSelected(mouseOverZone))
        {
            GenDraw.DrawFieldEdges(mouseOverZone.Cells, MouseOverColor);
        }
    }

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

        foreach (var zone in zones)
        {
            textures.Add(SolidColorMaterials.NewSolidColorTexture(zone.color));
        }
    }

    // RimWorld.AreaAllowedGUI
    private static void DoZoneSelector(Rect rect, ref Zone_Stockpile? activeStockpile, Zone_Stockpile? zone,
                                        Texture2D tex)
    {
        if (!addedPostDrawSelectionOverlaysAction)
        {
            RimWorld_SelectionDrawer_DrawSelectionOverlays.PostDrawSelectionOverlaysActions.Add(PostDrawSelectionOverlays);
            addedPostDrawSelectionOverlaysAction = true;
        }

        rect = rect.ContractedBy(1f);
        GUI.DrawTexture(rect, tex);
        Text.Anchor = TextAnchor.MiddleLeft;
        var label = zone?.label ?? "Any stockpile";
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

            if (Input.GetMouseButton(0) &&
                 activeStockpile != zone)
            {
                activeStockpile = zone;
                SoundDefOf.Designate_DragStandard_Changed.PlayOneShotOnCamera();
            }
        }

        TooltipHandler.TipRegion(rect, label);
        Text.Anchor = TextAnchor.UpperLeft;
    }
}
