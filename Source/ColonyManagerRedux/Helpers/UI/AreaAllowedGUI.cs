// AreaAllowedGUI.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using Verse.Sound;

namespace ColonyManagerRedux;

[HotSwappable]
public static class AreaAllowedGUI
{
    public static Area? DoAllowedAreaSelectors(ref Rect rect,
        Area? currentArea,
        int countPerRow,
        Map map,
        float margin = 0)
    {
        var newArea = currentArea;
        DoAllowedAreaSelectors(ref rect, ref newArea, countPerRow, map, margin);
        return newArea;
    }

    // RimWorld.AreaAllowedGUI
    public static void DoAllowedAreaSelectors(
        ref Vector2 pos,
        float width,
        ref Area? area,
        int countPerRow,
        Map map,
        float margin = 0)
    {
        var rect = new Rect(
            pos.x,
            pos.y,
            width,
            Constants.ListEntryHeight);
        DoAllowedAreaSelectors(ref rect, ref area, countPerRow, map, margin);
        pos.y += rect.height;
    }

    public static void DoAllowedAreaSelectors(
        ref Rect rect,
        ref Area? allowedArea,
        int countPerRow,
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

        if (areaCount < countPerRow)
        {
            countPerRow = areaCount;
        }

        var areaRows = Mathf.CeilToInt((float)areaCount / countPerRow);
        rect.height += (areaRows - 1) * Constants.ListEntryHeight;
        var widthPerArea = rect.width / countPerRow;

        Text.WordWrap = false;
        Text.Font = GameFont.Tiny;
        var nullAreaRect = new Rect(rect.x, rect.y, widthPerArea, rect.height / areaRows);
        DoAreaSelector(nullAreaRect, ref allowedArea, null);
        var areaIndex = 1;
        foreach (Area area in allAreas.Where(a => a.AssignableAsAllowed()))
        {
            var xOffset = (areaIndex % countPerRow) * widthPerArea;
            var yOffset = (areaIndex / countPerRow) * Constants.ListEntryHeight;
            var areaRect = new Rect(rect.x + xOffset, rect.y + yOffset, widthPerArea, rect.height / areaRows);
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
