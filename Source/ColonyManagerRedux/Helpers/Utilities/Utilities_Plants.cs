// Utilities_Mining.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public static class Utilities_Plants
{
    public static IEnumerable<ThingDef> GetForestryPlants(Map map, bool clearArea)
    {
        return GetAllPlants(map)

            // if !clearArea, remove things that do not yield wood
            .Where(td => clearArea || (td.plant.harvestTag == "Wood" ||
                                    td.plant.harvestedThingDef == ThingDefOf.WoodLog) &&
                                    td.plant.harvestYield > 0)
            .Distinct();
    }

    public static IEnumerable<ThingDef> GetForagingPlants(Map map)
    {
        return GetAllPlants(map)

            // that yield something that is not wood
            .Where(plant => plant.plant.harvestYield > 0 &&
                            plant.plant.harvestedThingDef != null &&
                            plant.plant.harvestTag != "Wood")
            .Distinct();
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
}
