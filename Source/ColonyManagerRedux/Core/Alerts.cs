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
        defaultLabel = "ColonyManagerRedux.Alerts.NoManagerLabel".Translate();
        defaultExplanation = "ColonyManagerRedux.Alerts.NoManager".Translate();
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
        defaultLabel = "ColonyManagerRedux.Alerts.NoTableLabel".Translate();
        defaultExplanation = "ColonyManagerRedux.Alerts.NoTable".Translate();
    }

    public override AlertPriority Priority => AlertPriority.Medium;

    public override AlertReport GetReport()
    {
        return Manager.For(Find.CurrentMap).JobTracker.JobsOfType<ManagerJob>().Any() && !AnyManagerTable();
    }

    private static bool AnyManagerTable()
    {
        ListerBuildings listerBuildings = Find.CurrentMap.listerBuildings;
        return listerBuildings.AllBuildingsColonistOfClass<Building_ManagerStation>().Any() ||
            listerBuildings.ColonistsHaveBuilding(ManagerThingDefOf.CM_AIManager);
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Microsoft.Performance",
    "CA1812:AvoidUninstantiatedInternalClasses",
    Justification = "Class is instantiated via reflection")]
internal sealed class Alert_TableAndAI : Alert
{
    public Alert_TableAndAI()
    {
        defaultLabel = "ColonyManagerRedux.Alerts.ManagerDeskAndAIManagerLabel".Translate();
        defaultExplanation = "ColonyManagerRedux.Alerts.ManagerDeskAndAIManager".Translate();
    }

    public override AlertPriority Priority => AlertPriority.Medium;

    public override AlertReport GetReport()
    {
        return AlertReport.CulpritsAre(ManagerStations);
    }

    private readonly List<Thing> managerStations = [];
    private List<Thing> ManagerStations
    {
        get
        {
            ListerBuildings listerBuildings = Find.CurrentMap.listerBuildings;

            managerStations.Clear();
            if (listerBuildings.ColonistsHaveBuilding(ManagerThingDefOf.CM_AIManager))
            {
                managerStations.AddRange(listerBuildings.AllBuildingsColonistOfClass<Building_ManagerStation>());
            }
            return managerStations;
        }
    }
}
