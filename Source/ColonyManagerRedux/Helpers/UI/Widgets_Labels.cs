// Widgets_Labels.cs
// Copyright Karel Kroeze, 2017-2020

using ilyvion.Laboratory.UI;

namespace ColonyManagerRedux;

// TODO: Move this to ilyvion.Laboratory
public static class Widgets_Labels
{
    public static void Label(Rect rect, string label, TextAnchor anchor = TextAnchor.UpperLeft,
                              GameFont font = GameFont.Small, Color? color = null, float margin = 0f,
                              bool wrap = true)
    {
        rect.xMin += margin;
        using var _ = GUIScope.Multiple(null, font, color ?? Color.white, wrap, anchor);
        Widgets.Label(rect, label);
    }

    public static void Label(Rect rect, string label, string? tooltip, TextAnchor anchor = TextAnchor.UpperLeft,
                              GameFont font = GameFont.Small, Color? color = null, float margin = 0f,
                              bool wrap = true)
    {
        if (!tooltip.NullOrEmpty())
        {
            TooltipHandler.TipRegion(rect, tooltip);
        }

        Label(rect, label, anchor, font, color, margin);
    }


    public static void Label(ref Vector2 position, float width, float height, string label,
                              TextAnchor anchor = TextAnchor.UpperLeft,
                              GameFont font = GameFont.Small, Color? color = null, float margin = 0f,
                              bool wrap = true)
    {
        var labelRect = new Rect(position.x, position.y, width, height);
        position.y += height;
        Label(labelRect, label, anchor, font, color, margin, wrap);
    }
}
