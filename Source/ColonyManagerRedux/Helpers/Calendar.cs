// Calendar.cs
// Copyright Karel Kroeze, 2019-2020

namespace ColonyManagerRedux;

internal sealed class CalendarMarker(float days, Color color, bool fill, bool debug = false)
{
    public Color Color { get; } = color;

    public float Days { get; } = days;

    public bool Debug { get; } = debug;
    public bool Fill { get; } = fill;
}

internal static class Calendar
{
    private static readonly Dictionary<string, int> _sizeCache = [];

    public static void Draw(Rect canvas, params CalendarMarker[] markers) =>
        Draw(canvas, Resources.SlightlyDarkBackgroundColour, markers);

    public static void Draw(Rect canvas, Color color, CalendarMarker[] markers)
    {
        if (markers == null)
        {
            throw new ArgumentNullException(nameof(markers));
        }

        var days = markers.NullOrEmpty()
            ? GenDate.DaysPerTwelfth
            : Mathf.CeilToInt(markers.Max(m => m.Days) / GenDate.DaysPerTwelfth)
                * GenDate.DaysPerTwelfth;

        var size = SquareSize(canvas.width, canvas.height, days);
        var cols = Mathf.FloorToInt(canvas.width / size);

        for (var d = 0; d < days; d++)
        {
            // draw day background
            DrawDay(d % cols, d / cols, size, canvas.min, 1f, color);

            foreach (var marker in markers)
            {
                if (marker.Days < d)
                {
                    continue;
                }

                if (marker.Fill)
                {
                    DrawDay(d % cols, d / cols, size, canvas.min, marker.Days - d, marker.Color);
                }

                if (!marker.Fill && marker.Days > d && marker.Days <= d + 1)
                {
                    DrawMarker(d % cols, d / cols, size, canvas.min, marker.Days - d, marker.Color);
                }
            }
        }
    }

    private static void DrawDay(
        int col,
        int row,
        int size,
        Vector2 pos,
        float progress,
        Color color
    )
    {
        var canvas = new Rect(
            (int)((col * size) + pos.x),
            (int)((row * size) + pos.y),
            Mathf.Clamp01(progress) * (size - 1),
            size - 1
        );
        Widgets.DrawBoxSolid(canvas, color);
    }

    private static void DrawMarker(
        int col,
        int row,
        int size,
        Vector2 pos,
        float progress,
        Color color
    )
    {
        var start = new Vector2((col + Mathf.Clamp01(progress)) * size, (row * size) - 2) + pos;
        var end = new Vector2((col + Mathf.Clamp01(progress)) * size, ((row + 1) * size) + 1) + pos;
        Widgets.DrawLine(start, end, color, 1);
    }

    private static int SquareSize(float x, float y, int n)
    {
        var key = BuildSizeCacheKey(x, y, n);
        if (_sizeCache.TryGetValue(key, out var size))
        {
            return size;
        }

        size = ComputeSquareSize(x, y, n);
        _sizeCache.Add(key, size);
        return size;
    }

    internal static string BuildSizeCacheKey(float x, float y, int n) =>
        $"x:{x:F3}, y:{y:F3}, n:{n}";

    //https://math.stackexchange.com/a/466248/176741
    internal static int ComputeSquareSize(float x, float y, int n)
    {
        float sx,
            sy;

        var px = Mathf.CeilToInt(Mathf.Sqrt(n * x / y));
        sx = Mathf.Floor(px * y / x) * px < n ? y / Mathf.CeilToInt(px * y / x) : x / px;

        var py = Mathf.Ceil(Mathf.Sqrt(n * y / x));
        sy = Mathf.Floor(py * x / y) * py < n ? x / Mathf.CeilToInt(x * py / y) : y / py;

        return (int)Mathf.Max(sx, sy);
    }
}
