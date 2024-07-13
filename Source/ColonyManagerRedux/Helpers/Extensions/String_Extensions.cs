// String_Extensions.cs
// Copyright Karel Kroeze, 2020-2020

using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ColonyManagerRedux;

public static class String_Extensions
{
    private static readonly Dictionary<Pair<string, Rect>, bool> _fitsCache =
        [];

    public static string Bold(this TaggedString text)
    {
        return text.Resolve().Bold();
    }

    public static string Bold(this string text)
    {
        return $"<b>{text}</b>";
    }

    public static bool Fits(this string text, Rect rect)
    {
        var key = new Pair<string, Rect>(text, rect);
        if (_fitsCache.TryGetValue(key, out bool result))
        {
            return result;
        }

        // make sure WW is temporarily turned off.
        var WW = Text.WordWrap;
        Text.WordWrap = false;
        result = Text.CalcSize(text).x < rect.width;
        Text.WordWrap = WW;

        _fitsCache.Add(key, result);
        return result;
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
