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
    readonly CachedValue<bool> _noManager;

    public Alert_NoManager()
    {
        defaultLabel = "ColonyManagerRedux.Alerts.NoManagerLabel".Translate();
        defaultExplanation = "ColonyManagerRedux.Alerts.NoManager".Translate();

        _noManager = new(() =>
            Manager.For(Find.CurrentMap).JobTracker.JobsOfType<ManagerJob>().Any()
                && !AnyConsciousManagerPawn());
    }

    public override AlertPriority Priority => AlertPriority.Medium;

    public override AlertReport GetReport()
    {
        return _noManager.Value;
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

    protected override void OnClick()
    {
        Find.MainTabsRoot.SetCurrentTab(ManagerMainButtonDefOf.Work);
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Microsoft.Performance",
    "CA1812:AvoidUninstantiatedInternalClasses",
    Justification = "Class is instantiated via reflection")]
internal sealed class Alert_NoTable : Alert
{
    readonly CachedValue<bool> _noTable;

    public Alert_NoTable()
    {
        defaultLabel = "ColonyManagerRedux.Alerts.NoTableLabel".Translate();

        _noTable = new(() =>
            Manager.For(Find.CurrentMap).JobTracker.JobsOfType<ManagerJob>().Any()
            && !AnyManagerTable());
    }

    public override AlertPriority Priority => AlertPriority.Medium;

    public override AlertReport GetReport()
    {
        return _noTable.Value;
    }

    public override TaggedString GetExplanation()
    {
        return "ColonyManagerRedux.Alerts.NoTable".Translate(
            BestBuildingResearchedThatCanBeBuilt.label);
    }

    private static bool AnyManagerTable()
    {
        ListerBuildings listerBuildings = Find.CurrentMap.listerBuildings;
        return listerBuildings.AllBuildingsColonistOfClass<Building_ManagerStation>().Any() ||
            listerBuildings.ColonistsHaveBuilding(ManagerThingDefOf.CM_AIManager);
    }

    protected override void OnClick()
    {
        Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Architect);
        var architectTabWindow = (MainTabWindow_Architect)MainButtonDefOf.Architect.TabWindow;

        var bestBuildingDef = BestBuildingResearchedThatCanBeBuilt;

        var desPanels = architectTabWindow.desPanelsCached;
        architectTabWindow.selectedDesPanel = desPanels
            .Find(p => p.def == DesignationCategoryDefOf.Production);
        architectTabWindow.forceActivatedCommand
            = DesignationCategoryDefOf.Production.AllResolvedDesignators
                .SingleOrDefault(d => d is Designator_Build build
                    && build.PlacingDef == bestBuildingDef);
    }

    private static ThingDef BestBuildingResearchedThatCanBeBuilt
    {
        get
        {
            if (ManagerResearchProjectDefOf.AdvancedManagingSoftware.IsFinished)
            {
                return ManagerThingDefOf.CM_AIManager;
            }
            else if (ManagerResearchProjectDefOf.ManagingSoftware.IsFinished)
            {
                return ManagerThingDefOf.CM_ManagerStation;
            }
            else
            {
                return ManagerThingDefOf.CM_BasicManagerStation;
            }
        }
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Microsoft.Performance",
    "CA1812:AvoidUninstantiatedInternalClasses",
    Justification = "Class is instantiated via reflection")]
internal sealed class Alert_TableAndAI : Alert
{
    readonly CachedValue<bool> _hasAIManager;
    readonly CachedValue<List<Thing>> _managerStations;

    public Alert_TableAndAI()
    {
        defaultLabel = "ColonyManagerRedux.Alerts.ManagerDeskAndAIManagerLabel".Translate();
        defaultExplanation = "ColonyManagerRedux.Alerts.ManagerDeskAndAIManager".Translate();

        _hasAIManager = new(updater: () =>
            Find.CurrentMap.listerBuildings.ColonistsHaveBuilding(ManagerThingDefOf.CM_AIManager));
        _managerStations = new(() => ManagerStations);
    }

    public override AlertPriority Priority => AlertPriority.Medium;

    public override AlertReport GetReport()
    {
        if (!_hasAIManager.Value)
        {
            return false;
        }
        return AlertReport.CulpritsAre(_managerStations.Value);
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
