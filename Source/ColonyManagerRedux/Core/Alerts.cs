// Alerts.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Microsoft.Performance",
    "CA1812:AvoidUninstantiatedInternalClasses",
    Justification = "Class is instantiated via reflection")]
internal sealed class Alert_NoManager : Alert
{
    public Alert_NoManager()
    {
        defaultLabel = "ColonyManagerRedux.ManagerAlertNoManagerLabel".Translate();
        defaultExplanation = "ColonyManagerRedux.ManagerAlertNoManager".Translate();
    }

    public override AlertPriority Priority => AlertPriority.Medium;

    public override AlertReport GetReport()
    {
        return Manager.For(Find.CurrentMap).JobTracker.JobsOfType<ManagerJob>().Any() && !AnyConsciousManagerPawn();
    }

    private static bool AnyConsciousManagerPawn()
    {
        return
            Find.CurrentMap.mapPawns.FreeColonistsSpawned.Any(
                pawn => !pawn.health.Dead && !pawn.Downed &&
                    pawn.workSettings.WorkIsActive(
                        ManagerWorkTypeDefOf.Managing)) ||
                    Find.CurrentMap.listerBuildings.ColonistsHaveBuilding(
                        ManagerThingDefOf.CM_AIManager);
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Microsoft.Performance",
    "CA1812:AvoidUninstantiatedInternalClasses",
    Justification = "Class is instantiated via reflection")]
internal sealed class Alert_NoTable : Alert
{
    public Alert_NoTable()
    {
        defaultLabel = "ColonyManagerRedux.ManagerAlertNoTableLabel".Translate();
        defaultExplanation = "ColonyManagerRedux.ManagerAlertNoTable".Translate();
    }

    public override AlertPriority Priority => AlertPriority.Medium;

    public override AlertReport GetReport()
    {
        return Manager.For(Find.CurrentMap).JobTracker.JobsOfType<ManagerJob>().Any() && !AnyManagerTable();
    }

    private static bool AnyManagerTable()
    {
        return Find.CurrentMap.listerBuildings.AllBuildingsColonistOfClass<Building_ManagerStation>().Any();
    }
}
