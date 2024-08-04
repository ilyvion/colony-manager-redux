// HSV_Helper.cs
// Copyright Karel Kroeze, 2016-2020

namespace ColonyManagerRedux;

internal static class HSV_Helper
{
    public static Color[] Range(int n)
    {
        var cols = new Color[n];
        for (var i = 0; i < n; i++)
        {
            cols[i] = Color.HSVToRGB(i / (float)n, 1f, 1f);
        }

        return cols;
    }
}
