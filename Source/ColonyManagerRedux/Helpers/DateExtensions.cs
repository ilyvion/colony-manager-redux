// DateExtensions.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Provides extension methods for converting ticks to human-readable period strings.
/// </summary>
public static class DateExtensions
{
    /// <summary>
    /// Converts a number of ticks to a verbose, human-readable period string, similar to
    /// <see cref="GenDate.ToStringTicksToPeriodVerbose"/>, except that it never drops smaller
    /// units once a larger one is present (e.g. it can say "1 year, 2 quadrums, 3 days, 4 hours"
    /// instead of silently truncating to "1 year, 2 quadrums").
    /// </summary>
    public static string ToStringTicksToPeriodVerboseFull(this int numTicks, bool allowHours = true)
    {
        if (numTicks < 0)
        {
            return "0";
        }

        numTicks.TicksToPeriod(out var years, out var quadrums, out var days, out var hoursFloat);

        var parts = new List<string>();
        if (years > 0)
        {
            parts.Add(years == 1 ? "Period1Year".Translate() : "PeriodYears".Translate(years));
        }
        if (quadrums > 0)
        {
            parts.Add(
                quadrums == 1 ? "Period1Quadrum".Translate() : "PeriodQuadrums".Translate(quadrums)
            );
        }
        if (days > 0)
        {
            parts.Add(days == 1 ? "Period1Day".Translate() : "PeriodDays".Translate(days));
        }

        if (allowHours)
        {
            var (show, wholeNumber, whole, fractional) = DescribeHoursPart(
                hoursFloat,
                years > 0 || quadrums > 0 || days > 0
            );
            if (show)
            {
                parts.Add(
                    wholeNumber
                        ? whole == 1
                            ? "Period1Hour".Translate()
                            : "PeriodHours".Translate(whole)
                        : "PeriodHours".Translate(
                            fractional.ToString("0.#", CultureInfo.InvariantCulture)
                        )
                );
            }
        }

        return parts.Count == 0
            ? allowHours
                ? "PeriodHours".Translate(0)
                : "PeriodDays".Translate(0)
            : string.Join(", ", parts);
    }

    /// <summary>
    /// Pure decision behind the hours-formatting branch of
    /// <see cref="ToStringTicksToPeriodVerboseFull"/>: decides whether an hours component should
    /// be shown at all, and if so, whether it should render as a whole number (<paramref
    /// name="hasLargerUnit"/> present, or a rounded/near-1.0 value) or as a fractional value with
    /// one decimal place.
    /// </summary>
    internal static (bool show, bool wholeNumber, int whole, float fractional) DescribeHoursPart(
        float hoursFloat,
        bool hasLargerUnit
    )
    {
        if (hasLargerUnit)
        {
            var hours = (int)hoursFloat;
            return hours > 0 ? (true, true, hours, 0f) : (false, false, 0, 0f);
        }

        if (hoursFloat <= 0f)
        {
            return (false, false, 0, 0f);
        }

        if (hoursFloat > 1f)
        {
            return (true, true, Mathf.RoundToInt(hoursFloat), 0f);
        }

        return Math.Round(hoursFloat, 1) == 1.0
            ? (true, true, 1, 0f)
            : (true, false, 0, hoursFloat);
    }
}
