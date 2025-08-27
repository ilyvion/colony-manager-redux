// I18n.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Provides internationalization helpers for translating and formatting strings in the manager UI.
/// </summary>
[HotSwappable]
public static class I18n
{
    /// <summary>
    /// Gets a translated string for aggressiveness, colorizing if above threshold.
    /// </summary>
    /// <param name="aggression">The aggression value.</param>
    /// <returns>The formatted and translated aggressiveness string.</returns>
    public static string Aggressiveness(float aggression) => "ColonyManagerRedux.Info.Aggressiveness".Translate(
            aggression >= .1f
                ? aggression.ToStringPercent().Colorize(Color.red)
                : aggression.ToStringPercent());

    /// <summary>
    /// Gets a translated yield string for a single label.
    /// </summary>
    /// <param name="label">The label to include in the yield string.</param>
    /// <returns>The formatted and translated yield string.</returns>
    public static string YieldOne(string label) => $"{"ColonyManagerRedux.Info.Yield".Translate()} {label}";

    /// <summary>
    /// Gets a translated yield string for multiple labels.
    /// </summary>
    /// <param name="labels">The labels to include in the yield string.</param>
    /// <returns>The formatted and translated yield string.</returns>
    public static string YieldMany(IEnumerable<string> labels) => $"{"ColonyManagerRedux.Info.Yield".Translate()}\n{labels.ToLineList(" - ")}";

    /// <summary>
    /// Gets a translated yield string for a ThingDef and yield value.
    /// </summary>
    /// <param name="yield">The yield value.</param>
    /// <param name="def">The ThingDef to include in the yield string.</param>
    /// <returns>The formatted and translated yield string.</returns>
    public static string YieldOne(float yield, ThingDef def) => def == null ? throw new ArgumentNullException(nameof(def)) : YieldOne($"{def.LabelCap} x{yield:F0}");

    /// <summary>
    /// Gets the translated action text for a designation definition.
    /// </summary>
    /// <param name="designationDef">The designation definition.</param>
    /// <returns>The translated and formatted action text.</returns>
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
