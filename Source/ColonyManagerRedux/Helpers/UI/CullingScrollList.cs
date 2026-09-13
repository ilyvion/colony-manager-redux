// CullingScrollList.cs
// Copyright (c) 2024–2026 Alexander Krivács Schrøder

using ilyvion.Laboratory.UI;

namespace ColonyManagerRedux;

/// <summary>
/// Draws rows of a variable-height scrolling list, culling those that are off-screen without
/// ever culling a row whose height hasn't been measured yet.
/// </summary>
public static class CullingScrollList
{
    /// <summary>
    /// Draws a single row's content at <paramref name="cur"/>, advancing it by the row's height.
    /// </summary>
    public delegate void RowDrawer<in T>(T item, ref Vector2 cur, float width);

    /// <summary>
    /// Draws one row of a <see cref="GUIScope.ScrollView(Rect, ScrollViewStatus, bool)"/>, using
    /// <paramref name="rowHeights"/> to remember each row's real height once it has been drawn.
    /// A row's height can only be measured by actually drawing it, so a row with no cached height
    /// is always drawn regardless of where it would otherwise fall relative to the viewport;
    /// caching its own height then lets later frames cull it once it's actually off-screen.
    /// </summary>
    /// <param name="scrollView">The scroll view the row belongs to.</param>
    /// <param name="cur">The current draw position; advanced by the row's height either way.</param>
    /// <param name="rowHeights">Cache of previously measured row heights, keyed by <paramref name="key"/>.</param>
    /// <param name="key">The row's cache key.</param>
    /// <param name="item">The row's data, passed through to <paramref name="drawEntry"/>.</param>
    /// <param name="drawEntry">Draws the row's content when it isn't culled.</param>
    /// <returns>
    /// The row's <see cref="Rect"/> if it was drawn this frame, so the caller can apply
    /// selection/hover/alternating-row decoration to it, or <see langword="null"/> if it was
    /// culled.
    /// </returns>
#pragma warning disable CA1062 // Validate arguments of public methods
    public static Rect? DrawRow<T, TKey>(
        ScrollViewScope scrollView,
        ref Vector2 cur,
        Dictionary<TKey, float> rowHeights,
        TKey key,
        T item,
        RowDrawer<T> drawEntry
    )
        where TKey : notnull
    {
        var hasCachedHeight = rowHeights.TryGetValue(key, out var cachedHeight);
        var row = new Rect(cur.x, cur.y, scrollView.ViewRect.width, cachedHeight);

        if (!hasCachedHeight || !scrollView.CanCull(row.height, cur.y))
        {
            drawEntry(item, ref cur, scrollView.ViewRect.width);
            row.height = cur.y - row.y;
            rowHeights[key] = row.height;
            return row;
        }

        cur.y += cachedHeight;
        return null;
    }
#pragma warning restore CA1062 // Validate arguments of public methods
}
