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

        _noManager = new(false, updater: () =>
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
        defaultExplanation = "ColonyManagerRedux.Alerts.NoTable".Translate(
            BestBuildingResearchedThatCanBeBuilt.label);

        _noTable = new(false, updater: () =>
            Manager.For(Find.CurrentMap).JobTracker.JobsOfType<ManagerJob>().Any()
            && !AnyManagerTable());
    }

    public override AlertPriority Priority => AlertPriority.Medium;

    public override AlertReport GetReport()
    {
        return _noTable.Value;
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

        var architectTabWindowTraverse = Traverse.Create(architectTabWindow);
        var desPanels = architectTabWindowTraverse
            .Field<List<ArchitectCategoryTab>>("desPanelsCached").Value;
        architectTabWindow.selectedDesPanel = desPanels
            .Find(p => p.def == DesignationCategoryDefOf.Production);
        architectTabWindowTraverse.Field<Designator>("forceActivatedCommand")
            .Value = DesignationCategoryDefOf.Production.AllResolvedDesignators
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

        _hasAIManager = new(false, updater: () =>
            Find.CurrentMap.listerBuildings.ColonistsHaveBuilding(ManagerThingDefOf.CM_AIManager));
        _managerStations = new([], updater: () => ManagerStations);
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

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Microsoft.Performance",
    "CA1812:AvoidUninstantiatedInternalClasses",
    Justification = "Class is instantiated via reflection")]
[HotSwappable]
internal sealed class Alert_AutoslaughterOverlap : Alert
{
    private readonly CachedValue<List<ThingDef>> _overlappingAnimals;

    public Alert_AutoslaughterOverlap()
    {
        defaultLabel = "ColonyManagerRedux.Alerts.AutoslaughterOverlapLabel".Translate();
        defaultExplanation = "ColonyManagerRedux.Alerts.AutoslaughterOverlap".Translate();

        _overlappingAnimals = new CachedValue<List<ThingDef>>([], updater: () =>
        {
            var autoSlaughterVanillaAnimals = AutoSlaughterVanillaAnimals().ToList();
            var autoSlaugherLivestockAnimals = AutoSlaugherLivestockAnimals().ToList();

            return autoSlaughterVanillaAnimals.Intersect(autoSlaugherLivestockAnimals).ToList();
        });
    }

    public override AlertPriority Priority => AlertPriority.Medium;

    public override AlertReport GetReport()
    {
        return _overlappingAnimals.Value.Count > 0;
    }

    public override TaggedString GetExplanation()
    {
        return "ColonyManagerRedux.Alerts.AutoslaughterOverlap".Translate(
            "ColonyManagerRedux.Livestock.ButcherExcess".Translate(),
            "- " + _overlappingAnimals.Value.Join(a => a.race.AnyPawnKind.GetLabelPlural(), "\n- "));
    }

    private static IEnumerable<ThingDef> AutoSlaughterVanillaAnimals()
    {
        foreach (AutoSlaughterConfig config in Find.CurrentMap.autoSlaughterManager.configs)
        {
            if (config.maxTotal != -1 || config.maxFemales != -1 || config.maxFemalesYoung != -1 || config.maxMales != -1 || config.maxMalesYoung != -1)
            {
                yield return config.animal;
            }
        }
    }

    private static IEnumerable<ThingDef> AutoSlaugherLivestockAnimals()
    {
        foreach (var managerJobLivestock in Manager.For(Find.CurrentMap).JobTracker.JobsOfType<ManagerJob_Livestock>())
        {
            if (managerJobLivestock.ButcherExcess)
            {
                yield return managerJobLivestock.TriggerPawnKind.pawnKind.race;
            }
        }
    }
}
