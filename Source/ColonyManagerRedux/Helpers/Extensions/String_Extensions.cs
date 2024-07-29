// String_Extensions.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.UI;

namespace ColonyManagerRedux;

[HotSwappable]
public static class String_Extensions
{
    private static readonly Dictionary<Pair<string, float>, (bool fits, Vector2 textSize)> _fitsCache =
        [];

    public static string Bold(this TaggedString text)
    {
        return text.Resolve().Bold();
    }

    public static string Bold(this string text)
    {
        return $"<b>{text}</b>";
    }

    public static bool Fits(this string text, float width, out Vector2 textSize)
    {
        var key = new Pair<string, float>(text, width);
        if (_fitsCache.TryGetValue(key, out var value))
        {
            textSize = value.textSize;
            return value.fits;
        }

        if (_fitsCache.Count >= 100)
        {
            _fitsCache.Clear();
        }

        using (GUIScope.WordWrap(false))
        {
            textSize = Text.CalcSize(text);
            value = (textSize.x < width, textSize);
        }

        _fitsCache.Add(key, value);
        return value.fits;
    }

    public static string Italic(this TaggedString text)
    {
        return text.Resolve().Italic();
    }

    public static string Italic(this string text)
    {
        return $"<i>{text}</i>";
    }
}
