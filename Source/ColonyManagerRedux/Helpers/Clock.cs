// Clock.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public class ClockHandle(float hours, Color? color = null, float length = 1f, float thickness = 1.5f) : HourTick(
    color ?? Color.white, length, thickness)
{
    public float Hours { get; } = hours;
}

public class HourTick(Color? color = null, float length = .2f, float thickness = 1)
{
    public Color Color { get; } = color ?? Color.grey;

    public float Length { get; } = length;
    public float Thickness { get; } = thickness;
}

[HotSwappable]
public static class Clock
{
    public static void Draw(Rect canvas, params ClockHandle[] clockHandles)
    {
        Draw(canvas, clockHandles, new HourTick(length: .5f), new HourTick(length: .3f));
    }

    public static void Draw(Rect canvas, IEnumerable<ClockHandle> clockHandles, HourTick major, HourTick minor)
    {
        if (clockHandles == null)
        {
            throw new ArgumentNullException(nameof(clockHandles));
        }

        for (var h = 0; h < 12; h++)
        {
            if (h % 3 == 0)
            {
                if (major != null)
                {
                    // NOTE: This tiny subtraction aligns the ticks
                    DrawTick(canvas, major, h - 0.01f);
                }
            }
            else
            {
                if (minor != null)
                {
                    // NOTE: This tiny subtraction aligns the ticks
                    DrawTick(canvas, minor, h - 0.01f);
                }
            }
        }

        foreach (var handle in clockHandles)
        {
            DrawHandle(canvas, handle);
        }
    }

    public static void DrawHandle(Rect canvas, ClockHandle handle)
    {
        if (handle == null)
        {
            throw new ArgumentNullException(nameof(handle));
        }

        DrawMarker(canvas, handle.Hours, handle.Thickness, handle.Color, 0f, handle.Length);
    }

    public static void DrawMarker(Rect canvas, float hour, float thickness, Color color, float start, float end)
    {
        var angle = (hour / 6 - .5f) * Mathf.PI; // should start at top...
        var radius = Mathf.Min(canvas.width, canvas.height) / 2f;
        var vector = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        var from = radius * start * vector + canvas.center;
        var to = radius * end * vector + canvas.center;
        //            Logger.Debug( $"{canvas}, {from}, {to}" );
        Widgets.DrawLine(from, to, color, thickness);
    }

    public static void DrawTick(Rect canvas, HourTick tick, float hour)
    {
        if (tick == null)
        {
            throw new ArgumentNullException(nameof(tick));
        }

        DrawMarker(canvas, hour, tick.Thickness, tick.Color, 1f - tick.Length, 1f);
    }
}
