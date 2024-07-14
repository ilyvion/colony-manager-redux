// Utilities_Mining.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ColonyManagerRedux;

public static class Utilities_Forestry
{
    public static IEnumerable<ThingDef> GetPlants(Map map, bool clearArea)
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
                .Select(p => p.def))

            // if type == logging, remove things that do not yield wood
            .Where(td => clearArea || (td.plant.harvestTag == "Wood" ||
                                    td.plant.harvestedThingDef == ThingDefOf.WoodLog) &&
                                    td.plant.harvestYield > 0)
            .Distinct();
    }
}
