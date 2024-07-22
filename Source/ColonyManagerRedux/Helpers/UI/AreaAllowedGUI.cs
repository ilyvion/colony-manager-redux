// AreaAllowedGUI.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using Verse.Sound;

namespace ColonyManagerRedux;

public static class AreaAllowedGUI
{
    public static Area? DoAllowedAreaSelectors(Rect rect,
                                               Area? areaIn,
                                               Map map,
                                               float lrMargin = 0)
    {
        var areaIO = areaIn;
        DoAllowedAreaSelectors(rect, ref areaIO, map, lrMargin);
        return areaIO;
    }

    // RimWorld.AreaAllowedGUI
    public static void DoAllowedAreaSelectors(
        ref Vector2 pos,
        float width,
        ref Area? area,
        Map map,
        float margin = 0)
    {
        var rect = new Rect(
            pos.x,
            pos.y,
            width,
            Constants.ListEntryHeight);
        pos.y += Constants.ListEntryHeight;
        DoAllowedAreaSelectors(rect, ref area, map, margin);
    }

    public static void DoAllowedAreaSelectors(
        Rect rect,
        ref Area? allowedArea,
        Map map,
        float lrMargin = 0)
    {
        if (map == null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        if (lrMargin > 0)
        {
            rect.xMin += lrMargin;
            rect.width -= lrMargin * 2;
        }

        var allAreas = map.areaManager.AllAreas;
        var areaCount = 1 + allAreas.Where(a => a.AssignableAsAllowed()).Count();

        var widthPerArea = rect.width / areaCount;
        Text.WordWrap = false;
        Text.Font = GameFont.Tiny;
        var nullAreaRect = new Rect(rect.x, rect.y, widthPerArea, rect.height);
        DoAreaSelector(nullAreaRect, ref allowedArea, null);
        var areaIndex = 1;
        foreach (Area area in allAreas.Where(a => a.AssignableAsAllowed()))
        {
            var xOffset = areaIndex * widthPerArea;
            var areaRect = new Rect(rect.x + xOffset, rect.y, widthPerArea, rect.height);
            DoAreaSelector(areaRect, ref allowedArea, area);
            areaIndex++;
        }

        Text.WordWrap = true;
        Text.Font = GameFont.Small;
    }

    public static void DoAllowedAreaSelectorsMC(
        Rect rect,
        ref HashSet<Area> allowedAreas,
        Map map,
        float lrMargin = 0)
    {
        if (map == null)
        {
            throw new ArgumentNullException(nameof(map));
        }
        if (allowedAreas == null)
        {
            throw new ArgumentNullException(nameof(allowedAreas));
        }

        if (lrMargin > 0)
        {
            rect.xMin += lrMargin;
            rect.width -= lrMargin * 2;
        }

        var allAreas = map.areaManager.AllAreas;
        var areaCount = allAreas.Where(a => a.AssignableAsAllowed()).Count();

        var widthPerArea = rect.width / areaCount;
        Text.WordWrap = false;
        Text.Font = GameFont.Tiny;
        var areaIndex = 0;
        foreach (Area area in allAreas.Where(a => a.AssignableAsAllowed()))
        {
            var xOffset = areaIndex * widthPerArea;
            var areaRect = new Rect(rect.x + xOffset, rect.y, widthPerArea, rect.height);
            bool status = allowedAreas.Contains(area);
            bool newStatus = DoAreaSelector(areaRect, area, status);
            if (status != newStatus)
            {
                // Selection changed
                if (newStatus)
                {
                    // Area should be added
                    allowedAreas.Add(area);
                }
                else
                {
                    // Area should be removed
                    allowedAreas.Remove(area);
                }
            }
            areaIndex++;
        }

        Text.WordWrap = true;
        Text.Font = GameFont.Small;
    }

    private static bool DoAreaSelector(Rect rect, Area area, bool status)
    {
        rect = rect.ContractedBy(1f);
        GUI.DrawTexture(rect, area == null ? BaseContent.GreyTex : area.ColorTexture);
        Text.Anchor = TextAnchor.MiddleLeft;
        var text = AreaUtility.AreaAllowedLabel_Area(area);
        var rect2 = rect;
        rect2.xMin += 3f;
        rect2.yMin += 2f;
        Widgets.Label(rect2, text);
        if (status)
        {
            Widgets.DrawBox(rect, 2);
        }

        if (Mouse.IsOver(rect))
        {
            area?.MarkForDraw();

            if (Widgets.ButtonInvisible(rect))
            {
                SoundDefOf.Designate_DragStandard_Changed.PlayOneShotOnCamera();
                return !status;
            }
        }

        TooltipHandler.TipRegion(rect, text);
        return status;
    }

    // RimWorld.AreaAllowedGUI
    private static void DoAreaSelector(Rect rect, ref Area? areaAllowed, Area? area)
    {
        rect = rect.ContractedBy(1f);
        GUI.DrawTexture(rect, area == null ? BaseContent.GreyTex : area.ColorTexture);
        Text.Anchor = TextAnchor.MiddleLeft;
        var text = AreaUtility.AreaAllowedLabel_Area(area);
        var rect2 = rect;
        rect2.xMin += 3f;
        rect2.yMin += 2f;
        Widgets.Label(rect2, text);
        if (areaAllowed == area)
        {
            Widgets.DrawBox(rect, 2);
        }

        if (Mouse.IsOver(rect))
        {
            area?.MarkForDraw();

            if (Input.GetMouseButton(0) &&
                 areaAllowed != area)
            {
                areaAllowed = area;
                SoundDefOf.Designate_DragStandard_Changed.PlayOneShotOnCamera();
            }
        }

        TooltipHandler.TipRegion(rect, text);
    }
}
