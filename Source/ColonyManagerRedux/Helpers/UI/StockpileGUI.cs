﻿// StockpileGUI.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Diagnostics.CodeAnalysis;
using Verse.Sound;

namespace ColonyManagerRedux;

[HotSwappable]
public static class StockpileGUI
{
    private static List<Texture2D>? textures;

    public static void DoStockpileSelectors(Rect rect, ref Zone_Stockpile? stockpile, Map map)
    {
        if (map == null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        // get all stockpiles
        var allStockpiles = map.zoneManager.AllZones.OfType<Zone_Stockpile>().ToList();

        // count + 1 for all stockpiles
        var areaCount = allStockpiles.Count + 1;

        // create colour swatch
        if (textures == null || textures.Count != areaCount - 1)
        {
            CreateTextures(allStockpiles);
        }

        var widthPerCell = rect.width / areaCount;
        Text.WordWrap = false;
        Text.Font = GameFont.Tiny;
        var nullAreaRect = new Rect(rect.x, rect.y, widthPerCell, rect.height);
        DoZoneSelector(nullAreaRect, ref stockpile, null, BaseContent.GreyTex);
        var areaIndex = 1;
        for (var j = 0; j < allStockpiles.Count; j++)
        {
            var xOffset = areaIndex * widthPerCell;
            var stockpileRect = new Rect(rect.x + xOffset, rect.y, widthPerCell, rect.height);
            DoZoneSelector(stockpileRect, ref stockpile, allStockpiles[j], textures[j]);
            areaIndex++;
        }

        Text.WordWrap = true;
        Text.Font = GameFont.Small;
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
    private static void DoZoneSelector(Rect rect, ref Zone_Stockpile? zoneAllowed, Zone_Stockpile? zone,
                                        Texture2D tex)
    {
        rect = rect.ContractedBy(1f);
        GUI.DrawTexture(rect, tex);
        Text.Anchor = TextAnchor.MiddleLeft;
        var label = zone?.label ?? "Any stockpile";
        var innerRect = rect;
        innerRect.xMin += 3f;
        innerRect.yMin += 2f;
        Widgets.Label(innerRect, label);
        if (zoneAllowed == zone)
        {
            Widgets.DrawBox(rect, 2);
        }

        if (Mouse.IsOver(rect))
        {
            if (zone != null)
            {
                if (zone.AllSlotCellsList() != null && zone.AllSlotCellsList().Count > 0 && Input.GetMouseButton(0))
                {
                    CameraJumper.TryJump(zone.Cells.First(), zone.Map);
                }
            }

            if (Input.GetMouseButton(0) &&
                 zoneAllowed != zone)
            {
                zoneAllowed = zone;
                SoundDefOf.Designate_DragStandard_Changed.PlayOneShotOnCamera();
            }
        }

        TooltipHandler.TipRegion(rect, label);
        Text.Anchor = TextAnchor.UpperLeft;
    }
}
