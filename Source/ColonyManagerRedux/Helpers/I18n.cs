// I18n.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HotSwappable]
public static class I18n
{
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
        return $"{"ColonyManagerRedux.Info.Yield".Translate()}\n{labels.ToLineList(" - ")}";
    }

    public static string YieldOne(float yield, ThingDef def)
    {
        if (def == null)
        {
            throw new ArgumentNullException(nameof(def));
        }
        return YieldOne($"{def.LabelCap} x{yield:F0}");
    }

    public static string ActionText(this DesignationDef designationDef)
    {
        if (designationDef == null)
        {
            throw new ArgumentNullException(nameof(designationDef));
        }

        // Of course these can't just all be named the same as their defName. 🙄
        return ((string)(designationDef.defName switch
        {
            "CutPlant" => "DesignatorCutPlants".Translate(),
            "HarvestPlant" => "DesignatorHarvest".Translate(),
            "Haul" => "DesignatorHaulThings".Translate(),
            _ => $"Designator{designationDef.defName}".Translate(),
        })).UncapitalizeFirst();
    }
}
