// I18n.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

internal static class I18n
{
    public static readonly TranslationHistoryLabel HistoryProduction = "ColonyManagerRedux.ColonyManager.HistoryProduction";
    public static readonly TranslationHistoryLabel HistoryConsumption = "ColonyManagerRedux.ColonyManager.HistoryConsumption";
    public static readonly TranslationHistoryLabel HistoryBatteries = "ColonyManagerRedux.ColonyManager.HistoryBatteries";


    public static string Aggressiveness(float aggression)
    {
        return Translate("ColonyManager.Aggressiveness",
                          aggression >= .1f
                              ? aggression.ToStringPercent().Colorize(Color.red)
                              : aggression.ToStringPercent());
    }

    public static string Key(string key)
    {
        return $"ColonyManagerRedux.{key}";
    }

    public static string Translate(string key, params NamedArgument[] args)
    {
        return Key(key).Translate(args);
    }

    public static string YieldOne(string label)
    {
        return $"{Translate("ColonyManager.Yield")} {label}";
    }

    public static string YieldMany(IEnumerable<string> labels)
    {
        return $"{Translate("ColonyManager.Yield")}\n - {labels.ToLineList(" - ")}";
    }

    public static string YieldOne(float yield, ThingDef def)
    {
        return YieldOne($"{def.LabelCap} x{yield:F0} ");
    }

    public static string Gender(Gender gender)
    {
        return Translate($"ColonyManager.Gender.{gender}");
    }

    public static string ChanceToDrop(float chance)
    {
        return Translate("ColonyManager.ChanceToDrop", chance.ToStringPercent());
    }
}
