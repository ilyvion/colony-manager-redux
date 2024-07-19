// Widgets_Buttons.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

internal static class Widgets_Buttons
{
    public static bool DisableableButtonText(Rect rect, string label, bool drawBackground = true, bool doMouseoverSound = true, Color? textColor = null, bool enabled = true, TextAnchor? overrideTextAnchor = null)
    {
        Color realizedTextColor = textColor ?? Widgets.NormalOptionColor;

        var color = GUI.color;
        if (!enabled)
        {
            if (drawBackground)
            {
                GUI.color = Color.gray;
                Texture2D atlas = Widgets.ButtonBGAtlas;
                Widgets.DrawAtlas(rect, atlas);
                GUI.color = color;
            }
            else
            {
                GUI.color = realizedTextColor;
                if (Mouse.IsOver(rect))
                {
                    GUI.color = Widgets.MouseoverOptionColor;
                }
            }

            TextAnchor anchor = Text.Anchor;
            if (overrideTextAnchor.HasValue)
            {
                Text.Anchor = overrideTextAnchor.Value;
            }
            else if (drawBackground)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleLeft;
            }
            bool wordWrap = Text.WordWrap;
            if (rect.height < Text.LineHeight * 2f)
            {
                Text.WordWrap = false;
            }
            Widgets.Label(rect, label);
            Text.Anchor = anchor;
            GUI.color = color;
            Text.WordWrap = wordWrap;

            GUI.color = color;

            return false;
        }
        else
        {
            return Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, realizedTextColor, enabled, overrideTextAnchor);
        }
    }
}
