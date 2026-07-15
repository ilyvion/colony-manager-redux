// AreaAllowedGUI.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using Verse.Sound;

namespace ColonyManagerRedux;

/// <summary>
/// Provides UI helpers for selecting and displaying allowed areas in the manager interface.
/// </summary>
[HotSwappable]
public static class AreaAllowedGUI
{
    /// <summary>
    /// Draws the allowed area selector UI and returns the selected area.
    /// </summary>
    /// <param name="rect">Reference to the rectangle in which to draw.</param>
    /// <param name="currentArea">The currently selected area.</param>
    /// <param name="countPerRow">Number of areas per row.</param>
    /// <param name="map">The map containing the areas.</param>
    /// <param name="margin">Optional margin for the selector area.</param>
    /// <returns>The newly selected area, or the current area if unchanged.</returns>
    public static Area? DoAllowedAreaSelectors(
        ref Rect rect,
        Area? currentArea,
        int countPerRow,
        Map map,
        float margin = 0
    )
    {
        var newArea = currentArea;
        DoAllowedAreaSelectors(ref rect, ref newArea, countPerRow, map, margin);
        return newArea;
    }

    // RimWorld.AreaAllowedGUI
    /// <summary>
    /// Draws the allowed area selector UI at the specified position and width.
    /// </summary>
    /// <param name="pos">Reference to the position to start drawing.</param>
    /// <param name="width">The width of the selector area.</param>
    /// <param name="area">Reference to the currently selected area.</param>
    /// <param name="countPerRow">Number of areas per row.</param>
    /// <param name="map">The map containing the areas.</param>
    /// <param name="margin">Optional margin for the selector area.</param>
    public static void DoAllowedAreaSelectors(
        ref Vector2 pos,
        float width,
        ref Area? area,
        int countPerRow,
        Map map,
        float margin = 0
    )
    {
        var rect = new Rect(pos.x, pos.y, width, Constants.ListEntryHeight);
        DoAllowedAreaSelectors(ref rect, ref area, countPerRow, map, margin);
        pos.y += rect.height;
    }

    /// <summary>
    /// Draws the allowed area selector UI in the specified rectangle.
    /// </summary>
    /// <param name="rect">Reference to the rectangle in which to draw.</param>
    /// <param name="allowedArea">Reference to the currently allowed area.</param>
    /// <param name="countPerRow">Number of areas per row.</param>
    /// <param name="map">The map containing the areas.</param>
    /// <param name="lrMargin">Optional left/right margin for the selector area.</param>
    public static void DoAllowedAreaSelectors(
        ref Rect rect,
        ref Area? allowedArea,
        int countPerRow,
        Map map,
        float lrMargin = 0
    )
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

        var assignableAreas = map.areaManager.AllAreas.Where(a => a.AssignableAsAllowed()).ToList();
        var areaCount = 1 + assignableAreas.Count;

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
        foreach (var area in assignableAreas)
        {
            var xOffset = areaIndex % countPerRow * widthPerArea;
            var yOffset = areaIndex / countPerRow * Constants.ListEntryHeight;
            var areaRect = new Rect(
                rect.x + xOffset,
                rect.y + yOffset,
                widthPerArea,
                rect.height / areaRows
            );
            DoAreaSelector(areaRect, ref allowedArea, area);
            areaIndex++;
        }

        Text.WordWrap = true;
        Text.Font = GameFont.Small;
    }

    /// <summary>
    /// Draws the allowed area selector UI followed by an "invert area" toggle below it.
    /// </summary>
    /// <param name="pos">Reference to the position to start drawing.</param>
    /// <param name="width">The width of the selector area.</param>
    /// <param name="area">Reference to the currently selected area.</param>
    /// <param name="invert">Reference to whether the area should be inverted.</param>
    /// <param name="countPerRow">Number of areas per row.</param>
    /// <param name="map">The map containing the areas.</param>
    /// <param name="margin">Optional margin for the selector area.</param>
    /// <returns>The total height drawn.</returns>
    public static float DoAllowedAreaSelectorsWithInvert(
        ref Vector2 pos,
        float width,
        ref Area? area,
        ref bool invert,
        int countPerRow,
        Map map,
        float margin = 0
    )
    {
        var start = pos;
        DoAllowedAreaSelectors(ref pos, width, ref area, countPerRow, map, margin);
        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.InvertArea".Translate(),
            "ColonyManagerRedux.InvertArea.Tip".Translate(),
            ref invert
        );
        return pos.y - start.y;
    }

    /// <summary>
    /// Draws the multi-select allowed area selector UI.
    /// </summary>
    /// <param name="rect">The rectangle in which to draw.</param>
    /// <param name="allowedAreas">Reference to the set of allowed areas.</param>
    /// <param name="map">The map containing the areas.</param>
    /// <param name="lrMargin">Optional left/right margin for the selector area.</param>
    public static void DoAllowedAreaSelectorsMC(
        Rect rect,
        ref HashSet<Area> allowedAreas,
        Map map,
        float lrMargin = 0
    )
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

        var assignableAreas = map.areaManager.AllAreas.Where(a => a.AssignableAsAllowed()).ToList();
        var areaCount = assignableAreas.Count;

        var widthPerArea = rect.width / areaCount;
        Text.WordWrap = false;
        Text.Font = GameFont.Tiny;
        var areaIndex = 0;
        foreach (var area in assignableAreas)
        {
            var xOffset = areaIndex * widthPerArea;
            var areaRect = new Rect(rect.x + xOffset, rect.y, widthPerArea, rect.height);
            var status = allowedAreas.Contains(area);
            var newStatus = DoAreaSelector(areaRect, area, status);
            if (status != newStatus)
            {
                // Selection changed
                if (newStatus)
                {
                    // Area should be added
                    _ = allowedAreas.Add(area);
                }
                else
                {
                    // Area should be removed
                    _ = allowedAreas.Remove(area);
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

            if (Input.GetMouseButton(0) && areaAllowed != area)
            {
                areaAllowed = area;
                SoundDefOf.Designate_DragStandard_Changed.PlayOneShotOnCamera();
            }
        }

        TooltipHandler.TipRegion(rect, text);
    }
}
