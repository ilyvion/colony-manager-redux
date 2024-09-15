// Utilities_Plants.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Diagnostics.CodeAnalysis;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal static class Utilities_Plants
{
    public static IEnumerable<ThingDef> GetForestryPlants(Map map, bool clearArea)
    {
        return GetAllPlants(map)

            // if !clearArea, remove things that do not yield wood
            .Where(td => clearArea || (td.plant.harvestTag == "Wood" ||
                td.plant.harvestedThingDef == ThingDefOf.WoodLog) &&
                td.plant.harvestedThingDef != null &&
                td.plant.harvestYield > 0)
            .Distinct()
            .OrderBy(pk => pk.label);
    }

    public static IEnumerable<ThingDef> GetForagingPlants(Map map)
    {
        return GetAllPlants(map)

            // that yield something that is not wood
            .Where(plant => plant.plant.harvestYield > 0 &&
                plant.plant.harvestedThingDef != null &&
                plant.plant.harvestTag != "Wood")
            .Distinct()
            .OrderBy(pk => pk.label);
    }

    private static IEnumerable<ThingDef> GetAllPlants(Map map)
    {
        return map.Biome.AllWildPlants

            // cave plants (shrooms)
            .Concat(DefDatabase<ThingDef>.AllDefsListForReading
                .Where(td => td.plant?.cavePlant ?? false))

            // ambrosia
            .Concat(ThingDefOf.Plant_Ambrosia)

            // and anything on the map that is not in a plant zone/planter
            .Concat(map.listerThings.AllThings.OfType<Plant>()
                .Where(p => p.Spawned &&
                    map.zoneManager.ZoneAt(p.Position) is not IPlantToGrowSettable &&
                    map.thingGrid.ThingsAt(p.Position)
                        .FirstOrDefault(t => t is Building_PlantGrower) == null)
                .Select(p => p.def));
    }

    public static bool TrySpecialAllowedSync(
        this ThingDef plantDef, HashSet<ThingDef> allowedPlants, ThingFilter thresholdFilter)
    {
        if (ModsConfig.IsActive(Constants.SurvivalistsAdditionsModId)
            && plantDef == ManagerThingDefOf.SRV_PlantTurnip)
        {
            var setAllow = allowedPlants.Contains(ManagerThingDefOf.SRV_PlantTurnip);
            thresholdFilter.SetAllow(ManagerThingDefOf.SRV_Turnip, setAllow);
            thresholdFilter.SetAllow(ManagerThingDefOf.SRV_Turnip_Green, setAllow);

            return true;
        }

        return false;
    }

    public static bool TrySpecialFilterSync(
        this ThingDef plantDef, ThingFilter thresholdFilter, ref bool shouldAllowPlant)
    {
        if (ModsConfig.IsActive(Constants.SurvivalistsAdditionsModId)
            && plantDef == ManagerThingDefOf.SRV_PlantTurnip)
        {
            shouldAllowPlant =
                thresholdFilter.Allows(ManagerThingDefOf.SRV_Turnip)
                || thresholdFilter.Allows(ManagerThingDefOf.SRV_Turnip_Green);

            return true;
        }

        return false;
    }

    public static bool TrySpecialDesigationCount(this ThingDef plantDef, AnyBoxed<int> count)
    {
        if (ModsConfig.IsActive(Constants.SurvivalistsAdditionsModId)
                && plantDef == ManagerThingDefOf.SRV_PlantTurnip)
        {
            var yield = plantDef.plant.harvestYield * 1.5;
            var yield2 = plantDef.plant.harvestYield * 2.5;
            count.Value += (int)(yield + yield2);

            return true;
        }

        return false;
    }

    public static bool TrySpecialYieldTooltip(
        this ThingDef plantDef, [NotNullWhen(true)] out string? tooltip)
    {
        if (ModsConfig.IsActive(Constants.SurvivalistsAdditionsModId)
                && plantDef == ManagerThingDefOf.SRV_PlantTurnip)
        {
            var yield = plantDef.plant.harvestYield * 1.5;
            var yield2 = plantDef.plant.harvestYield * 2.5;
            tooltip = I18n.YieldMany(
                Gen.YieldSingle($"{ManagerThingDefOf.SRV_Turnip.LabelCap} x{yield:F0}").Concat(
                    Gen.YieldSingle($"{ManagerThingDefOf.SRV_Turnip_Green.LabelCap} x{yield2:F0}")
                ));

            return true;
        }

        tooltip = null;
        return false;
    }

    public static bool TrySpecialDesignationYieldTooltip(
        this ThingDef plantDef, [NotNullWhen(true)] out string? tooltip)
    {
        if (ModsConfig.IsActive(Constants.SurvivalistsAdditionsModId)
                && plantDef == ManagerThingDefOf.SRV_PlantTurnip)
        {
            var yield = plantDef.plant.harvestYield * 1.5;
            var yield2 = plantDef.plant.harvestYield * 2.5;
            tooltip =
                Gen.YieldSingle($"{ManagerThingDefOf.SRV_Turnip.LabelCap} x{yield:F0}").Concat(
                    Gen.YieldSingle($"{ManagerThingDefOf.SRV_Turnip_Green.LabelCap} x{yield2:F0}")
                ).Join(null, "\n- ");

            return true;
        }

        tooltip = null;
        return false;
    }
}
