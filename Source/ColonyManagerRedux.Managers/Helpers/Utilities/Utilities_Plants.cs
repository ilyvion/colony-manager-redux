// Utilities_Plants.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024–2026 Alexander Krivács Schrøder

using System.Diagnostics.CodeAnalysis;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal static class Utilities_Plants
{
    public static IEnumerable<ThingDef> GetForestryPlants(Map? map, bool clearArea)
    {
        bool IsValid(ThingDef td)
        {
            return IsValidForestryPlant(
                clearArea,
                td.plant.harvestTag,
                td.plant.harvestedThingDef,
                ThingDefOf.WoodLog,
                td.plant.harvestYield
            );
        }

        return GetAllPlants(map).Where(IsValid).Distinct().OrderBy(pk => pk.label);
    }

    /// <summary>
    /// Decides whether a plant def is a valid forestry (logging) target: any def is valid when
    /// <paramref name="clearArea"/> is true; otherwise only wood-yielding defs with a positive
    /// yield are valid.
    /// </summary>
    internal static bool IsValidForestryPlant<T>(
        bool clearArea,
        string? harvestTag,
        T? harvestedThingDef,
        T woodLogDef,
        float harvestYield
    )
        where T : class =>
        clearArea
        || (
            (harvestTag == "Wood" || harvestedThingDef == woodLogDef)
            && harvestedThingDef != null
            && harvestYield > 0
        );

    /// <summary>
    /// Shared by Forestry and Foraging's "reduce designations until we're just above target"
    /// loop. Given yields in removal order, walks forward removing (subtracting) each one as
    /// long as either the running count still meets the threshold once removed, or the
    /// designation count is high enough that more should be removed regardless — stopping at the
    /// first item that fails both checks. Since removal always proceeds from the front and stops
    /// at the first failure, the result is always a contiguous prefix, so only its length is
    /// returned.
    /// </summary>
    internal static int ComputeReduceCount(
        int startingCount,
        IReadOnlyList<int> sortedYields,
        int startingDesignationCount,
        Func<int, bool> countMeetsTarget,
        Func<int, bool> shouldRemoveMoreDesignations
    )
    {
        var count = startingCount;
        var designationCount = startingDesignationCount;
        var removeCount = 0;
        for (; removeCount < sortedYields.Count; removeCount++)
        {
            count -= sortedYields[removeCount];
            if (!countMeetsTarget(count) && !shouldRemoveMoreDesignations(designationCount))
            {
                break;
            }
            designationCount--;
        }
        return removeCount;
    }

    /// <summary>
    /// Shared by Forestry and Foraging's "add designations until target met" loop. Given yields
    /// in designation order, walks forward adding (accumulating) each one until the running count
    /// meets the threshold or no more designations may be added — stopping before the first item
    /// that would violate either check. The result is always a contiguous prefix, so only its
    /// length is returned.
    /// </summary>
    internal static int ComputeNumberToDesignate(
        int startingCount,
        IReadOnlyList<int> sortedYields,
        int startingDesignationCount,
        Func<int, bool> countMeetsTarget,
        Func<int, bool> canAddMoreDesignations
    )
    {
        var count = startingCount;
        var designationCount = startingDesignationCount;
        var designateCount = 0;
        for (; designateCount < sortedYields.Count; designateCount++)
        {
            if (countMeetsTarget(count) || !canAddMoreDesignations(designationCount))
            {
                break;
            }
            count += sortedYields[designateCount];
            designationCount++;
        }
        return designateCount;
    }

    public static IEnumerable<ThingDef> GetForagingPlants(Map? map) =>
        GetAllPlants(map)
            // that yield something that is not wood
            .Where(plant =>
                plant.plant.harvestYield > 0
                && plant.plant.harvestedThingDef != null
                && plant.plant.harvestTag != "Wood"
            )
            .Distinct()
            .OrderBy(pk => pk.label);

    private static IEnumerable<ThingDef> GetAllPlants(Map? map) =>
        map != null
            ? map
                .Biome.AllWildPlants
                // cave plants (shrooms)
                .Concat(
                    DefDatabase<ThingDef>.AllDefsListForReading.Where(td =>
                        td.plant?.cavePlant ?? false
                    )
                )
                // ambrosia
                .Concat(ThingDefOf.Plant_Ambrosia)
                // and anything on the map that is not in a plant zone/planter
                .Concat(
                    map.listerThings.AllThings.OfType<Plant>()
                        .Where(p =>
                            p.Spawned
                            && map.zoneManager.ZoneAt(p.Position) is not IPlantToGrowSettable
                            && map.thingGrid.ThingsAt(p.Position)
                                .FirstOrDefault(t => t is Building_PlantGrower) == null
                        )
                        .Select(p => p.def)
                )
            : DefDatabase<ThingDef>.AllDefsListForReading.Where(td => td.IsPlant);

    public static bool TrySpecialAllowedSync(
        this ThingDef plantDef,
        HashSet<ThingDef> allowedPlants,
        ThingFilter thresholdFilter
    )
    {
        if (
            ModsConfig.IsActive(Constants.SurvivalistsAdditionsModId)
            && plantDef == ManagerThingDefOf.SRV_PlantTurnip
        )
        {
            var setAllow = allowedPlants.Contains(ManagerThingDefOf.SRV_PlantTurnip);
            thresholdFilter.SetAllow(ManagerThingDefOf.SRV_Turnip, setAllow);
            thresholdFilter.SetAllow(ManagerThingDefOf.SRV_Turnip_Green, setAllow);

            return true;
        }

        return false;
    }

    public static bool TrySpecialFilterSync(
        this ThingDef plantDef,
        ThingFilter thresholdFilter,
        ref bool shouldAllowPlant
    )
    {
        if (
            ModsConfig.IsActive(Constants.SurvivalistsAdditionsModId)
            && plantDef == ManagerThingDefOf.SRV_PlantTurnip
        )
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
        if (
            ModsConfig.IsActive(Constants.SurvivalistsAdditionsModId)
            && plantDef == ManagerThingDefOf.SRV_PlantTurnip
        )
        {
            var yield = plantDef.plant.harvestYield * 1.5;
            var yield2 = plantDef.plant.harvestYield * 2.5;
            count.Value += (int)(yield + yield2);

            return true;
        }

        return false;
    }

    public static bool TrySpecialYieldTooltip(
        this ThingDef plantDef,
        [NotNullWhen(true)] out string? tooltip
    )
    {
        if (
            ModsConfig.IsActive(Constants.SurvivalistsAdditionsModId)
            && plantDef == ManagerThingDefOf.SRV_PlantTurnip
        )
        {
            var yield = plantDef.plant.harvestYield * 1.5;
            var yield2 = plantDef.plant.harvestYield * 2.5;
            tooltip = I18n.YieldMany(
                Gen.YieldSingle($"{ManagerThingDefOf.SRV_Turnip.LabelCap} x{yield:F0}")
                    .Concat(
                        Gen.YieldSingle(
                            $"{ManagerThingDefOf.SRV_Turnip_Green.LabelCap} x{yield2:F0}"
                        )
                    )
            );

            return true;
        }

        tooltip = null;
        return false;
    }

    public static bool TrySpecialDesignationYieldTooltip(
        this ThingDef plantDef,
        [NotNullWhen(true)] out string? tooltip
    )
    {
        if (
            ModsConfig.IsActive(Constants.SurvivalistsAdditionsModId)
            && plantDef == ManagerThingDefOf.SRV_PlantTurnip
        )
        {
            var yield = plantDef.plant.harvestYield * 1.5;
            var yield2 = plantDef.plant.harvestYield * 2.5;
            tooltip = Gen.YieldSingle($"{ManagerThingDefOf.SRV_Turnip.LabelCap} x{yield:F0}")
                .Concat(
                    Gen.YieldSingle($"{ManagerThingDefOf.SRV_Turnip_Green.LabelCap} x{yield2:F0}")
                )
                .Join(null, "\n- ");

            return true;
        }

        tooltip = null;
        return false;
    }
}
