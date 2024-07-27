// I18n.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

internal static class I18n
{
    public static readonly TranslationHistoryLabel HistoryProduction = "ColonyManagerRedux.History.Production";
    public static readonly TranslationHistoryLabel HistoryConsumption = "ColonyManagerRedux.History.Consumption";
    public static readonly TranslationHistoryLabel HistoryBatteries = "ColonyManagerRedux.History.Batteries";


    public static string Aggressiveness(float aggression)
    {
        return "ColonyManagerRedux.Info.Aggressiveness".Translate(
            aggression >= .1f
                ? aggression.ToStringPercent().Colorize(Color.red)
                : aggression.ToStringPercent());
    }

    public static string YieldOne(string label)
    {
        return $"{"ColonyManagerRedux.Info.Yield".Translate()} {label}";
    }

    public static string YieldMany(IEnumerable<string> labels)
    {
        return $"{"ColonyManagerRedux.Info.Yield".Translate()}\n - {labels.ToLineList(" - ")}";
    }

    public static string YieldOne(float yield, ThingDef def)
    {
        return YieldOne($"{def.LabelCap} x{yield:F0} ");
    }
}
