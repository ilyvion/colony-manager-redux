// Widgets_Section.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.Extensions;
using ilyvion.Laboratory.UI;

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

/// <summary>
/// Provides UI helpers for drawing sectioned panels with headers, scrolling, and dynamic sizing.
/// </summary>
[HotSwappable]
public static class Widgets_Section
{
    private static readonly Dictionary<string, float> _columnHeights = [];
    private static readonly Dictionary<string, Vector2> _columnScrollPositions = [];

    private static readonly Dictionary<int, float> _heights = [];

    /// <summary>
    /// Begins a new section column with scrolling support.
    /// </summary>
    /// <param name="canvas">The canvas rectangle.</param>
    /// <param name="identifier">A unique identifier for the column.</param>
    /// <param name="position">Outputs the starting position for drawing.</param>
    /// <param name="width">Outputs the width of the column.</param>
    public static void BeginSectionColumn(Rect canvas, string identifier, out Vector2 position, out float width)
    {
        var height = GetHeight(identifier);
        var scrollPosition = GetScrollPosition(identifier);
        var outRect = canvas.ContractedBy(Margin).RoundToInt();
        var viewRect = new Rect(outRect.xMin, outRect.yMin, outRect.width, height);
        if (viewRect.height > outRect.height)
        {
            viewRect.width -= GenUI.ScrollBarWidth + (Margin / 2f);
        }

        viewRect = viewRect.RoundToInt();

        Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
        GUI.BeginGroup(viewRect);
        viewRect = viewRect.AtZero();

        position = Vector2.zero;
        width = viewRect.width;

        _columnScrollPositions[identifier] = scrollPosition;
    }

    /// <summary>
    /// Ends the current section column and updates its height.
    /// </summary>
    /// <param name="identifier">The unique identifier for the column.</param>
    /// <param name="position">The final position after drawing.</param>
    public static void EndSectionColumn(string identifier, Vector2 position)
    {
        GUI.EndGroup();
        Widgets.EndScrollView();

        _columnHeights[identifier] = position.y;
    }

    /// <summary>
    /// Draws a section with a header and content using a generic data source.
    /// </summary>
    /// <typeparam name="T">The type of data for the section.</typeparam>
    /// <param name="data">The data to pass to the drawer function.</param>
    /// <param name="position">Reference to the current drawing position.</param>
    /// <param name="width">The width of the section.</param>
    /// <param name="drawerFunc">The function to draw the section content.</param>
    /// <param name="header">Optional header text.</param>
    /// <param name="id">Optional unique ID for the section.</param>
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

    /// <summary>
    /// Draws a section with a header and content.
    /// </summary>
    /// <param name="position">Reference to the current drawing position.</param>
    /// <param name="width">The width of the section.</param>
    /// <param name="drawerFunc">The function to draw the section content.</param>
    /// <param name="header">Optional header text.</param>
    /// <param name="id">Optional unique ID for the section.</param>
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
        if (id == 0 && IsLikelyAnonymous(drawerFunc))
        {
            ColonyManagerReduxMod.Instance.LogWarning(
                $"Section drawerFunc seems to be an anonymous function; not providing a manual value for id " +
                "may lead to unexpected behavior as these don't have a stable hash value. " +
                $"Auto-generated id for {drawerFunc.Method.Name} is {drawerFunc.GetHashCode()}");
        }
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
            GetHeight(id) + (2 * Margin)).RoundToInt();

        // NOTE: we're updating height _after_ drawing, so the background is technically always one frame behind.
        GUI.DrawTexture(contentRect, Resources.SlightlyDarkBackground);
        var height = drawerFunc(position + new Vector2(Margin, Margin), width - (2 * Margin));
        position.y += height + (3 * Margin);
        _heights[id] = height;

        static bool IsLikelyAnonymous(Delegate delegat)
        {
            var method = delegat.Method;
            var declaringType = method.DeclaringType;

            // Lambdas and local functions usually have generated names containing things like:
            // "<>c__DisplayClass", "<>c", "<SomeMethodName>b__..."
#if v1_5
            return method.Name.Contains('<')
#else
            return method.Name.Contains('<', StringComparison.Ordinal)
#endif
                || declaringType.GetCustomAttributes(
                    typeof(CompilerGeneratedAttribute),
                    false).Length != 0;
        }
    }

    private static float GetHeight(string identifier)
    {
        if (_columnHeights.TryGetValue(identifier, out var height))
        {
            return height;
        }

        height = 0f;
        _columnHeights[identifier] = height;
        return height;
    }

    private static float GetHeight(int id)
    {
        _ = _heights.TryGetValue(id, out var height);
        return height;
    }

    private static Vector2 GetScrollPosition(string identifier)
    {
        if (_columnScrollPositions.TryGetValue(identifier, out var scrollposition))
        {
            return scrollposition;
        }

        scrollposition = Vector2.zero;
        _columnScrollPositions[identifier] = scrollposition;
        return scrollposition;
    }
}
