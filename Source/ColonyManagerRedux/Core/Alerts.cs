﻿// Alerts.cs
// Copyright Karel Kroeze, 2018-2020

using System.Linq;
using RimWorld;
using Verse;

namespace ColonyManagerRedux;

internal class Alert_NoManager : Alert
{
    public Alert_NoManager()
    {
        defaultLabel = "ColonyManagerRedux.ManagerAlertNoManagerLabel".Translate();
        defaultExplanation = "ColonyManagerRedux.ManagerAlertNoManager".Translate();
    }

    public override AlertPriority Priority => AlertPriority.Medium;

    public override AlertReport GetReport()
    {
        return Manager.For(Find.CurrentMap).JobStack.FullStack().Count > 0 && !AnyConsciousManagerPawn();
    }

    private bool AnyConsciousManagerPawn()
    {
        return
            Find.CurrentMap.mapPawns.FreeColonistsSpawned.Any(
                pawn => !pawn.health.Dead && !pawn.Downed &&
                    pawn.workSettings.WorkIsActive(
                        Utilities.WorkTypeDefOf_Managing)) ||
                    Find.CurrentMap.listerBuildings.ColonistsHaveBuilding(
                        DefDatabase<ThingDef>.GetNamed("FM_AIManager"));
    }
}

internal class Alert_NoTable : Alert
{
    public Alert_NoTable()
    {
        defaultLabel = "ColonyManagerRedux.ManagerAlertNoTableLabel".Translate();
        defaultExplanation = "ColonyManagerRedux.ManagerAlertNoTable".Translate();
    }

    public override AlertPriority Priority => AlertPriority.Medium;

    public override AlertReport GetReport()
    {
        return Manager.For(Find.CurrentMap).JobStack.FullStack().Count > 0 && !AnyManagerTable();
    }

    private bool AnyManagerTable()
    {
        return Find.CurrentMap.listerBuildings.AllBuildingsColonistOfClass<Building_ManagerStation>().Any();
    }
}
