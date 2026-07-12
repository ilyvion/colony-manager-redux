// Alerts.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024–2025 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

internal sealed class Alert_NoManager : Alert
{
    private readonly CachedValue<bool> _noManager;

    public Alert_NoManager()
    {
        defaultLabel = "ColonyManagerRedux.Alerts.NoManagerLabel".Translate();
        defaultExplanation = "ColonyManagerRedux.Alerts.NoManager".Translate();

        _noManager = new(() =>
        {
            var currentMap = Find.CurrentMap;
            if (currentMap == null)
            {
                return false;
            }
            var manager = Manager.For(currentMap);
            return AnyUnsuspendedJobs(manager.JobTracker.JobList.Select(j => j.IsSuspended))
                && !AnyConsciousManagerPawn();
        });
    }

    public override AlertPriority Priority => AlertPriority.Medium;

    public override AlertReport GetReport() =>
        ColonyManagerReduxMod.Settings.ShowNoManagerAlert && _noManager.Value;

    /// <summary>
    /// Pure filter behind the "are there jobs that actually need a manager" checks in
    /// <see cref="Alert_NoManager"/> and <see cref="Alert_NoTable"/>: a suspended job doesn't
    /// need anyone (or anything) to work on it, so it shouldn't be able to trigger either alert.
    /// </summary>
    internal static bool AnyUnsuspendedJobs(IEnumerable<bool> jobsIsSuspended) =>
        jobsIsSuspended.Any(isSuspended => !isSuspended);

    private static bool AnyConsciousManagerPawn() =>
        Find.CurrentMap.mapPawns.FreeColonistsSpawned.Any(pawn =>
            !pawn.health.Dead
            && !pawn.Downed
            && pawn.workSettings.WorkIsActive(ManagerWorkTypeDefOf.Managing)
        ) || Find.CurrentMap.listerBuildings.ColonistsHaveBuilding(ManagerThingDefOf.CM_AIManager);

    protected override void OnClick() =>
        Find.MainTabsRoot.SetCurrentTab(ManagerMainButtonDefOf.Work);
}

[HotSwappable]
internal sealed class Alert_JobsNotUpdating : Alert
{
    private readonly CachedValue<int> _mostOutdatedJobTicks;

    public Alert_JobsNotUpdating()
    {
        defaultLabel = "ColonyManagerRedux.Alerts.JobsNotUpdatingLabel".Translate();
        defaultExplanation = "ColonyManagerRedux.Alerts.JobsNotUpdating".Translate();

        _mostOutdatedJobTicks = new(() =>
        {
            var currentMap = Find.CurrentMap;
            if (currentMap == null)
            {
                return 0;
            }
            var manager = Manager.For(currentMap);
            return MostOutdatedTicks(
                manager.JobTracker.JobList.Select(j =>
                    (j.IsSuspended, j.ShouldDoNow, j.TicksSinceShouldUpdate)
                )
            );
        });
    }

    /// <summary>
    /// Pure filter/max behind <see cref="_mostOutdatedJobTicks"/>: finds the largest
    /// <c>ticksSinceShouldUpdate</c> among jobs that are active (not suspended) and currently due,
    /// defaulting to 0 when there are none.
    /// </summary>
    internal static int MostOutdatedTicks(
        IEnumerable<(bool isSuspended, bool shouldDoNow, int ticksSinceShouldUpdate)> jobs
    ) =>
        jobs.Where(j => !j.isSuspended && j.shouldDoNow).Max(j => (int?)j.ticksSinceShouldUpdate)
        ?? 0;

    public override AlertPriority Priority =>
        ClassifyOutdatedPriority(
            _mostOutdatedJobTicks.Value,
            ColonyManagerReduxMod.Settings.DaysBeforeShowingCriticalAlert,
            ColonyManagerReduxMod.Settings.DaysBeforeShowingHighAlert
        );

    /// <summary>
    /// Pure threshold classification behind <see cref="Priority"/>: compares
    /// <paramref name="mostOutdatedTicks"/> against the critical/high day thresholds (converted
    /// to ticks) to pick the alert tier. Critical is checked first, so classification stays sane
    /// even if the critical/high day-threshold ordering invariant normally enforced by
    /// <see cref="Settings.ClampAlertTiers"/> were ever violated.
    /// </summary>
    internal static AlertPriority ClassifyOutdatedPriority(
        int mostOutdatedTicks,
        float criticalDays,
        float highDays
    )
    {
        if (mostOutdatedTicks >= GenDate.TicksPerDay * criticalDays)
        {
            return AlertPriority.Critical;
        }
        else if (mostOutdatedTicks >= GenDate.TicksPerDay * highDays)
        {
            return AlertPriority.High;
        }
        return AlertPriority.Medium;
    }

    private const float PulseFreq = 0.5f;

    private const float PulseAmpCritical = 0.6f;

    protected override Color BGColor
    {
        get
        {
            var num = Pulser.PulseBrightness(
                0.5f,
                Pulser.PulseBrightness(PulseFreq, PulseAmpCritical)
            );
            return new Color(num, num, num)
                * (
                    Priority switch
                    {
                        AlertPriority.High => Color.yellow.ToTransparent(.5f),
                        AlertPriority.Critical => Color.red.ToTransparent(.5f),
                        AlertPriority.Medium => Color.clear,
                        _ => throw new NotImplementedException(),
                    }
                );
        }
    }

    public override AlertReport GetReport() =>
        // No need to report jobs not being updated if there's no manager to update them
        ColonyManagerReduxMod.Settings.ShowJobsNotUpdatingAlert
        && !Find.Alerts.activeAlerts.Any(a => a is Alert_NoManager)
        && (
            _mostOutdatedJobTicks.Value
            >= GenDate.TicksPerDay * ColonyManagerReduxMod.Settings.DaysBeforeShowingAlert
        );

    public override TaggedString GetExplanation() =>
        "ColonyManagerRedux.Alerts.JobsNotUpdating".Translate(
            _mostOutdatedJobTicks.Value.ToStringTicksToPeriod()
        );

    protected override void OnClick() =>
        Find.MainTabsRoot.SetCurrentTab(ManagerMainButtonDefOf.Work);
}

[HotSwappable]
internal sealed class Alert_NoTable : Alert
{
    private readonly CachedValue<bool> _noTable;

    public Alert_NoTable()
    {
        defaultLabel = "ColonyManagerRedux.Alerts.NoTableLabel".Translate();

        _noTable = new(() =>
        {
            var currentMap = Find.CurrentMap;
            if (currentMap == null)
            {
                return false;
            }
            var manager = Manager.For(currentMap);
            return Alert_NoManager.AnyUnsuspendedJobs(
                    manager.JobTracker.JobsOfType<ManagerJob>().Select(j => j.IsSuspended)
                ) && !AnyManagerWorkspace();
        });
    }

    public override AlertPriority Priority => AlertPriority.Medium;

    public override AlertReport GetReport() =>
        ColonyManagerReduxMod.Settings.ShowNoTableAlert && _noTable.Value;

    public override TaggedString GetExplanation() =>
        "ColonyManagerRedux.Alerts.NoTable".Translate()
        + "\n\n"
        + (
            BestBuildingResearchedThatCanBeBuilt == null
                ? "ColonyManagerRedux.Alerts.NoTable.NoTableResearched".Translate()
                : "ColonyManagerRedux.Alerts.NoTable.ClickToBuild".Translate(
                    BestBuildingResearchedThatCanBeBuilt!.label
                )
        );

    private static bool AnyManagerWorkspace()
    {
        var listerBuildings = Find.CurrentMap.listerBuildings;
        return listerBuildings.AllBuildingsColonistOfClass<Building_ManagerStation>().Any()
            || listerBuildings.ColonistsHaveBuilding(ManagerThingDefOf.CM_AIManager);
    }

    protected override void OnClick()
    {
        var bestBuildingDef = BestBuildingResearchedThatCanBeBuilt;

        if (bestBuildingDef != null)
        {
            Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Architect);
            var architectTabWindow = (MainTabWindow_Architect)MainButtonDefOf.Architect.TabWindow;
            var desPanels = architectTabWindow.desPanelsCached;

            architectTabWindow.selectedDesPanel = desPanels.Find(p =>
                p.def == DesignationCategoryDefOf.Production
            );
            architectTabWindow.forceActivatedCommand =
                DesignationCategoryDefOf.Production.AllResolvedDesignators.SingleOrDefault(d =>
                    d is Designator_Build build && build.PlacingDef == bestBuildingDef
                );
        }
        else
        {
            Find.MainTabsRoot.SetCurrentTab(MainButtonDefOf.Research);
            var researchTabWindow = (MainTabWindow_Research)MainButtonDefOf.Research.TabWindow;
            researchTabWindow.CurTab = ManagerResearchTabDefOf.CMR_ResearchTab;
        }
    }

    private static ThingDef? BestBuildingResearchedThatCanBeBuilt =>
        ManagerResearchProjectDefOf.AdvancedManagingSoftware.IsFinished
            ? ManagerThingDefOf.CM_AIManager
        : ManagerResearchProjectDefOf.ManagingSoftware.IsFinished
            ? ManagerThingDefOf.CM_ManagerStation
        : ManagerResearchProjectDefOf.CMR_ManagingDesk.IsFinished
            ? ManagerThingDefOf.CM_BasicManagerStation
        : ManagerResearchProjectDefOf.CMR_ManagingSpot.IsFinished
            ? ManagerThingDefOf.CMR_ManagingSpot
        : null;
}

internal sealed class Alert_TableAndAI : Alert
{
    private readonly CachedValue<bool> _hasAIManager;
    private readonly CachedValue<List<Thing>> _managerStations;

    public Alert_TableAndAI()
    {
        defaultLabel = "ColonyManagerRedux.Alerts.ManagerDeskAndAIManagerLabel".Translate();
        defaultExplanation = "ColonyManagerRedux.Alerts.ManagerDeskAndAIManager".Translate();

        _hasAIManager = new(updater: () =>
        {
            var currentMap = Find.CurrentMap;
            return currentMap != null
                && currentMap.listerBuildings.ColonistsHaveBuilding(ManagerThingDefOf.CM_AIManager);
        });
        _managerStations = new(() => ManagerStations);
    }

    public override AlertPriority Priority => AlertPriority.Medium;

    public override AlertReport GetReport() =>
        !ColonyManagerReduxMod.Settings.ShowNoTableNeededAlert || !_hasAIManager.Value
            ? (AlertReport)false
            : AlertReport.CulpritsAre(_managerStations.Value);

    private readonly List<Thing> managerStations = [];
    private List<Thing> ManagerStations
    {
        get
        {
            var listerBuildings = Find.CurrentMap.listerBuildings;

            managerStations.Clear();
            if (listerBuildings.ColonistsHaveBuilding(ManagerThingDefOf.CM_AIManager))
            {
                managerStations.AddRange(
                    listerBuildings.AllBuildingsColonistOfClass<Building_ManagerStation>()
                );
            }
            return managerStations;
        }
    }
}
