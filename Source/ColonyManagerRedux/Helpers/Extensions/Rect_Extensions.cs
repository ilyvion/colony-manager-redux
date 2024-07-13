﻿// Rect_Extensions.cs
// Copyright Karel Kroeze, 2018-2020

using UnityEngine;
using Verse;

namespace ColonyManagerRedux
{
    public static class Rect_Extensions
    {
        public static Rect CenteredIn(this Rect inner, Rect outer, float x = 0f, float y = 0f)
        {
            inner = inner.CenteredOnXIn(outer).CenteredOnYIn(outer);
            inner.x += x;
            inner.y += y;
            return inner;
        }

        public static Rect RoundToInt(this Rect rect)
        {
            return new Rect(
                Mathf.RoundToInt(rect.xMin),
                Mathf.RoundToInt(rect.yMin),
                Mathf.RoundToInt(rect.width),
                Mathf.RoundToInt(rect.height));
        }
    }
}
