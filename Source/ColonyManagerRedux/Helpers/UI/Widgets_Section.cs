﻿// Widgets_Section.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.Extensions;
using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[HotSwappable]
public static class Widgets_Section
{
    private static readonly Dictionary<string, float> _columnHeights = [];
    private static readonly Dictionary<string, Vector2> _columnScrollPositions = [];

    private static readonly Dictionary<int, float> _heights = [];

    public static void BeginSectionColumn(Rect canvas, string identifier, out Vector2 position, out float width)
    {
        var height = GetHeight(identifier);
        var scrollPosition = GetScrollPosition(identifier);
        var outRect = canvas.ContractedBy(Margin).RoundToInt();
        var viewRect = new Rect(outRect.xMin, outRect.yMin, outRect.width, height);
        if (viewRect.height > outRect.height)
        {
            viewRect.width -= GenUI.ScrollBarWidth + Margin / 2f;
        }

        viewRect = viewRect.RoundToInt();

        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
        GUI.BeginGroup(viewRect);
        viewRect = viewRect.AtZero();

        position = Vector2.zero;
        width = viewRect.width;

        _columnScrollPositions[identifier] = scrollPosition;
    }

    public static void EndSectionColumn(string identifier, Vector2 position)
    {
        GUI.EndGroup();
        Widgets.EndScrollView();

        _columnHeights[identifier] = position.y;
    }

    public static void Section<T>(
        T data,
        ref Vector2 position,
        float width,
        Func<T, Vector2, float, float> drawerFunc,
        string header = "",
        int id = 0)
    {
        if (drawerFunc == null)
        {
            throw new ArgumentNullException(nameof(drawerFunc));
        }

        id = id != 0 ? id : drawerFunc.GetHashCode();
        Section(ref position, width, (p, w) => drawerFunc(data, p, w), header, id);
    }

    public static void Section(
        ref Vector2 position,
        float width,
        Func<Vector2, float, float> drawerFunc,
        string header = "",
        int id = 0)
    {
        if (drawerFunc == null)
        {
            throw new ArgumentNullException(nameof(drawerFunc));
        }

        var hasHeader = !header.NullOrEmpty();
        id = id != 0 ? id : drawerFunc.GetHashCode();

        // header
        if (hasHeader)
        {
            using var _ = GUIScope.Font(GameFont.Tiny);
            var headerSize = Text.CalcSize(header);
            var headerRect = new Rect(
                position.x,
                position.y,
                headerSize.x + Margin,
                SectionHeaderHeight).RoundToInt();
            IlyvionWidgets.Label(
                headerRect,
                header,
                TextAnchor.LowerLeft,
                GameFont.Tiny,
                leftMargin: Margin);
            position.y += SectionHeaderHeight;
        }

        // draw content
        var contentRect = new Rect(
            position.x,
            position.y,
            width,
            GetHeight(id) + 2 * Margin).RoundToInt();

        // NOTE: we're updating height _after_ drawing, so the background is technically always one frame behind.
        GUI.DrawTexture(contentRect, Resources.SlightlyDarkBackground);
        var height = drawerFunc(position + new Vector2(Margin, Margin), width - 2 * Margin);
        position.y += height + 3 * Margin;
        _heights[id] = height;
    }

    private static float GetHeight(string identifier)
    {
        if (_columnHeights.TryGetValue(identifier, out float height))
        {
            return height;
        }

        height = 0f;
        _columnHeights[identifier] = height;
        return height;
    }

    private static float GetHeight(int id)
    {
        _heights.TryGetValue(id, out float height);
        return height;
    }

    private static Vector2 GetScrollPosition(string identifier)
    {
        if (_columnScrollPositions.TryGetValue(identifier, out Vector2 scrollposition))
        {
            return scrollposition;
        }

        scrollposition = Vector2.zero;
        _columnScrollPositions[identifier] = scrollposition;
        return scrollposition;
    }
}
