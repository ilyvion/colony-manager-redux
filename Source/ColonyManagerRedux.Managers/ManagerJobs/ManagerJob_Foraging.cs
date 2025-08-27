// ManagerJob_Foraging.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

[HotSwappable]
[CoroutineSettingsType]
internal sealed class ManagerJob_Foraging : ManagerJob<ManagerSettings_Foraging>
{
    [CoroutineSettingsType]
    public sealed class History : HistoryWorker<ManagerJob_Foraging>
    {
        [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
        public override Coroutine GetCountForHistoryChapterCoroutine(
            ManagerJob_Foraging managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> count
        )
        {
            var ticksBetweenOperations =
                ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                    (Func<
                        ManagerJob_Foraging,
                        int,
                        ManagerJobHistoryChapterDef,
                        Boxed<int>,
                        Coroutine
                    >)
                        GetCountForHistoryChapterCoroutine
                );

            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryStock)
            {
                yield return managerJob
                    .TriggerThreshold.GetCurrentCountCoroutine(count)
                    .ResumeWhenOtherCoroutineIsCompleted();
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryDesignated)
            {
                yield return managerJob
                    .CachedCurrentDesignatedCount.DoUpdateIfNeeded(force: true)
                    .ResumeWhenOtherCoroutineIsCompleted();
                yield return new ResumeAfterTicks(ticksBetweenOperations);
                count.Value = managerJob.CachedCurrentDesignatedCount.Value;
            }
            else
            {
                throw new ArgumentException($"Unexpected chapterDef value {chapterDef.defName}");
            }
        }

        public override Coroutine GetTargetForHistoryChapterCoroutine(
            ManagerJob_Foraging managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> target
        )
        {
            target.Value =
                chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryStock
                    ? managerJob.TriggerThreshold.TargetCount
                    : 0;
            yield break;
        }
    }

    public HashSet<ThingDef> AllowedPlants = [];
    public Area? ForagingArea;
    public bool ForceFullyMature;
    public Utilities.SyncDirection Sync = Utilities.SyncDirection.AllowedToFilter;
    public bool SyncFilterAndAllowed = true;

    private List<Designation> _designations = [];

    internal MultiTickCachedValue<int> CachedCurrentDesignatedCount { get; }

    private bool _plantsLockedToMap = ColonyManagerReduxMod.Settings.NewJobsShouldBeResourceLocked;
    public bool PlantsLockedToMap
    {
        get => _plantsLockedToMap;
        set
        {
            if (_plantsLockedToMap != value)
            {
                _plantsLockedToMap = value;
                _allPlants = null; // reset cached plants
            }
        }
    }

    private List<ThingDef>? _allPlants;
    public List<ThingDef> AllPlants
    {
        get
        {
            _allPlants ??=
            [
                .. Utilities_Plants.GetForagingPlants(_plantsLockedToMap ? Manager.map : null),
            ];
            return _allPlants;
        }
    }

    public Trigger_Threshold TriggerThreshold => (Trigger_Threshold)Trigger!;

    public List<Designation> Designations => [.. _designations];

    public override bool IsValid => base.IsValid && TriggerThreshold != null;

    public override IEnumerable<string> Targets =>
        AllowedPlants.Select(plant => plant.LabelCap.Resolve());

    public override WorkTypeDef WorkTypeDef => WorkTypeDefOf.Growing;

    public ManagerJob_Foraging(Manager manager)
        : base(manager)
    {
        CachedCurrentDesignatedCount = new(0, GetCurrentDesignatedCountCoroutine);

        // populate the trigger field, count all harvested thingdefs from the allowed plant list
        Trigger = new Trigger_Threshold(this);
        ConfigureThresholdTrigger();
    }

    public override void PostMake()
    {
        var foragingSettings = ManagerSettings;
        if (foragingSettings != null)
        {
            SyncFilterAndAllowed = foragingSettings.DefaultSyncFilterAndAllowed;
            ForceFullyMature = foragingSettings.DefaultForceFullyMature;
        }
    }

    public override void PostImport()
    {
        base.PostImport();

        _ = AllowedPlants.RemoveWhere(p => !AllPlants.Contains(p));
    }

    [CoroutineSettingsMethod]
    private Coroutine GetCurrentDesignatedCountCoroutine(AnyBoxed<int> count)
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            GetCurrentDesignatedCountCoroutine
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                GetCurrentDesignatedCountCoroutine
            );

        for (var i = 0; i < _designations.Count; i++)
        {
            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }

            var des = _designations[i];

            if (!des.target.HasThing)
            {
                continue;
            }

            if (des.target.Thing is not Plant plant)
            {
                continue;
            }

            if (!plant.Spawned)
            {
                continue;
            }

            if (!plant.def.TrySpecialDesigationCount(count))
            {
                count.Value += plant.YieldNow();
            }
        }
    }

    public void AddRelevantGameDesignations(ManagerLog jobLog)
    {
        // get list of game designations not managed by this job that could have been assigned
        // by this job.
        var addedCount = 0;
        List<LocalTargetInfo> newTargets = [];
        foreach (
            var des in Manager
                .map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.HarvestPlant)
                .Except(_designations)
                .Where(des => IsValidDesignatedForagingTarget(des.target))
        )
        {
            addedCount++;
            AddDesignation(des, false);
            newTargets.Add(des.target);
        }
        if (addedCount > 0)
        {
            jobLog.AddDetail(
                "ColonyManagerRedux.Logs.AddRelevantGameDesignations".Translate(
                    addedCount,
                    Def.label
                ),
                newTargets
            );
        }
    }

    /// <summary>
    ///     Clean up all outstanding designations
    /// </summary>
    public override void CleanUp(ManagerLog? jobLog)
    {
        CleanDeadDesignations(_designations, DesignationDefOf.HarvestPlant, jobLog);
        CleanUpDesignations(_designations, jobLog);
    }

    public string? DesignationLabel(Designation designation)
    {
        if (!designation.target.HasThing)
        {
            return null;
        }

        // label, dist, yield.
        var plant = (Plant)designation.target.Thing;
        return plant.def.TrySpecialDesignationYieldTooltip(out var tooltip)
            ? (string)
                "ColonyManagerRedux.Job.DesignationLabelMulti".Translate(
                    plant.LabelCap,
                    Distance(plant, Manager.map.GetBaseCenter())
                        .ToString("F0", CultureInfo.InvariantCulture),
                    tooltip
                )
            : (string)
                "ColonyManagerRedux.Job.DesignationLabel".Translate(
                    plant.LabelCap,
                    Distance(plant, Manager.map.GetBaseCenter())
                        .ToString("F0", CultureInfo.InvariantCulture),
                    plant.YieldNow(),
                    plant.def.plant.harvestedThingDef.LabelCap
                );
    }

    private string? _tmpForagingAreaLabel;

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Collections.Look(ref AllowedPlants, "allowedPlants", LookMode.Def);
        Scribe_Values.Look(ref ForceFullyMature, "forceFullyMature");
        Scribe_Values.Look(
            ref _plantsLockedToMap,
            "plantsLockedToMap",
            ColonyManagerReduxMod.Settings.NewJobsShouldBeResourceLocked
        );

        if (Manager.ScribeSameMapData)
        {
            Scribe_References.Look(ref ForagingArea, "foragingArea");

            Utilities.Scribe_Designations(ref _designations, Manager);
        }
        else
        {
            Utilities.Scribe_AreaByLabel(
                ref ForagingArea,
                ref _tmpForagingAreaLabel,
                "foragingArea",
                Manager.map.areaManager
            );
        }

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            TriggerThreshold.SettingsChanged = Notify_ThresholdFilterChanged;
            TriggerThreshold.AllowAnyThresholdChanged = ConfigureThresholdTriggerParentFilter;
            ConfigureThresholdTriggerParentFilter();
        }
    }

    public void Notify_ThresholdFilterChanged()
    {
        ColonyManagerReduxMod.Instance.LogDebug("Threshold changed.");
        if (!SyncFilterAndAllowed || Sync == Utilities.SyncDirection.AllowedToFilter)
        {
            return;
        }

        foreach (var plant in AllPlants)
        {
            var shouldAllowPlant = false;
            if (!plant.TrySpecialFilterSync(TriggerThreshold.ThresholdFilter, ref shouldAllowPlant))
            {
                shouldAllowPlant = TriggerThreshold.ThresholdFilter.Allows(
                    plant.plant.harvestedThingDef
                );
            }

            _ = shouldAllowPlant ? AllowedPlants.Add(plant) : AllowedPlants.Remove(plant);
        }
    }

    public void RefreshAllPlants()
    {
        ColonyManagerReduxMod.Instance.LogDebug("Refreshing all plants");

        // all plants that yield something, and it isn't wood.
        _allPlants = null;
        var options = AllPlants;

        // remove stuff not in new list
        foreach (var plant in AllowedPlants.ToList())
        {
            if (!options.Contains(plant))
            {
                _ = AllowedPlants.Remove(plant);
            }
        }
        ConfigureThresholdTriggerParentFilter();
    }

    public void SetPlantAllowed(ThingDef plant, bool allow, bool sync = true)
    {
        _ = allow ? AllowedPlants.Add(plant) : AllowedPlants.Remove(plant);

        if (SyncFilterAndAllowed && sync)
        {
            Sync = Utilities.SyncDirection.AllowedToFilter;

            if (!plant.TrySpecialAllowedSync(AllowedPlants, TriggerThreshold.ThresholdFilter))
            {
                var harvestedThingDef = plant.plant.harvestedThingDef;
                var setAllow = AllowedPlants.Any(p =>
                    p.plant.harvestedThingDef == harvestedThingDef
                );
                TriggerThreshold.ThresholdFilter.SetAllow(harvestedThingDef, setAllow);
            }
        }
    }

    [CoroutineSettingsMethod]
    public override Coroutine TryDoJobCoroutine(ManagerLog jobLog, Boxed<bool> workDone)
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            TryDoJobCoroutine
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(TryDoJobCoroutine);

        if (!TriggerThreshold.State)
        {
            if (JobState != ManagerJobState.Completed)
            {
                JobState = ManagerJobState.Completed;
                jobLog.AddDetail("ColonyManagerRedux.Logs.JobCompleted".Translate());

                CleanUp(jobLog);
            }
            yield break;
        }
        else
        {
            JobState = ManagerJobState.Active;
        }

        // clean up designations that were completed.
        CleanDeadDesignations(_designations, DesignationDefOf.HarvestPlant, jobLog);
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        // clean up designations that are (now) in the wrong area.
        CleanAreaDesignations(jobLog);
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        // add designations in the game that could have been handled by this job
        AddRelevantGameDesignations(jobLog);
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        // designate plants until trigger is met.
        yield return CachedCurrentDesignatedCount
            .DoUpdateIfNeeded(force: true)
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);
        var count = TriggerThreshold.GetCurrentCount() + CachedCurrentDesignatedCount.Value;

        if (
            TriggerThreshold.DoesCountMeetTarget(count)
            || ColonyManagerReduxMod.Settings.ShouldRemoveMoreDesignations(_designations.Count)
        )
        {
            List<Designation> sortedDesignations = [];
            yield return GetThingsSorted(
                    _designations.Where(d => d.target.HasThing),
                    sortedDesignations,
                    _ => true,
                    (p, d) => -p.YieldNow() / d,
                    d => (Plant)d.target.Thing
                )
                .ResumeWhenOtherCoroutineIsCompleted();
            yield return new ResumeAfterTicks(ticksBetweenOperations);

            // reduce designations until we're just above target
            for (var i = 0; i < sortedDesignations.Count; i++)
            {
                var designation = sortedDesignations[i];

                var plant = (Plant)designation.target.Thing;
                var yield = plant.YieldNow();
                count -= yield;
                if (
                    TriggerThreshold.DoesCountMeetTarget(count)
                    || ColonyManagerReduxMod.Settings.ShouldRemoveMoreDesignations(
                        _designations.Count
                    )
                )
                {
                    designation.Delete();
                    _ = _designations.Remove(designation);
                    jobLog.AddDetail(
                        "ColonyManagerRedux.Logs.RemoveDesignation".Translate(
                            DesignationDefOf.HarvestPlant.ActionText(),
                            "ColonyManagerRedux.Foraging.Logs.Plant".Translate(),
                            plant.Label,
                            yield,
                            count,
                            TriggerThreshold.TargetLabel
                        ),
                        plant
                    );
                    workDone.Value = true;
                }
                else
                {
                    break;
                }

                if (i > 0 && i % operationsPerTick == 0)
                {
                    yield return new ResumeAfterTicks(ticksBetweenOperations);
                }
            }

            if (!workDone)
            {
                jobLog.AddDetail(
                    "ColonyManagerRedux.Logs.TargetsAlreadySatisfied".Translate(
                        "ColonyManagerRedux.Foraging.Logs.Plants".Translate(),
                        Def.label
                    )
                );
            }

            yield break;
        }

        jobLog.AddDetail(
            "ColonyManagerRedux.Logs.CurrentCount".Translate(count, TriggerThreshold.TargetCount)
        );

        if (!ColonyManagerReduxMod.Settings.CanAddMoreDesignations(_designations.Count))
        {
            jobLog.AddDetail(
                "ColonyManagerRedux.Logs.CantAddMoreDesignations".Translate(
                    "ColonyManagerRedux.Foraging.Logs.Plants".Translate(),
                    Def.label
                )
            );
            yield break;
        }

        List<Plant> sortedPlants = [];
        yield return GetTargetsSorted(
                sortedPlants,
                IsValidUndesignatedForagingTarget,
                (p, d) => p.YieldNow() / d
            )
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        if (sortedPlants.Count == 0)
        {
            jobLog.AddDetail(
                "ColonyManagerRedux.Logs.NoValidTargets".Translate(
                    "ColonyManagerRedux.Foraging.Logs.Plants".Translate(),
                    Def.label
                )
            );
            yield break;
        }

        foreach (var (plant, i) in sortedPlants.Select((t, i) => (t, i)))
        {
            if (
                TriggerThreshold.DoesCountMeetTarget(count)
                || !ColonyManagerReduxMod.Settings.CanAddMoreDesignations(_designations.Count)
            )
            {
                break;
            }

            var yield = plant.YieldNow();
            count += yield;
            AddDesignation(new(plant, DesignationDefOf.HarvestPlant));
            jobLog.AddDetail(
                "ColonyManagerRedux.Logs.AddDesignation".Translate(
                    DesignationDefOf.HarvestPlant.ActionText(),
                    "ColonyManagerRedux.Foraging.Logs.Plant".Translate(),
                    plant.Label,
                    yield,
                    count,
                    TriggerThreshold.TargetLabel
                ),
                plant
            );
            workDone.Value = true;
            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    private void AddDesignation(Designation des, bool addToGame = true)
    {
        // add to game
        if (addToGame)
        {
            Manager.map.designationManager.AddDesignation(des);
        }

        // add to internal list
        _designations.Add(des);
    }

    private void CleanAreaDesignations(ManagerLog jobLog)
    {
        var missingThingCount = 0;
        var incorrectAreaCount = 0;
        foreach (var des in _designations)
        {
            if (!des.target.HasThing)
            {
                missingThingCount++;
                des.Delete();
            }
            // if area is not null and does not contain designate location, remove designation.
            else if (!ForagingArea?.ActiveCells.Contains(des.target.Thing.Position) ?? false)
            {
                incorrectAreaCount++;
                des.Delete();
            }
        }
        if (missingThingCount != 0 || incorrectAreaCount != 0)
        {
            jobLog.AddDetail(
                "ColonyManagerRedux.Logs.CleanAreaDesignations".Translate(
                    missingThingCount + incorrectAreaCount,
                    missingThingCount,
                    incorrectAreaCount,
                    Def.label
                )
            );
        }
    }

    private bool IsValidUndesignatedForagingTarget(Plant target) =>
        target.def.plant != null
        && target.Map == Manager.map
        && AllowedPlants.Contains(target.def)
        && target.Spawned
        && Manager.map.designationManager.DesignationOn(target) == null
        // cut only mature plants, or non-mature that yield something right now.
        && (
            (!ForceFullyMature && target.YieldNow() > 1)
            || target.LifeStage == PlantLifeStage.Mature
        )
        && (ForagingArea == null || ForagingArea.ActiveCells.Contains(target.Position))
        && IsReachable(target);

    private bool IsValidDesignatedForagingTarget(LocalTargetInfo t) =>
        t.HasThing && IsValidDesignatedForagingTarget(t.Thing);

    private bool IsValidDesignatedForagingTarget(Thing t) =>
        t is Plant plant && IsValidDesignatedForagingTarget(plant);

    private bool IsValidDesignatedForagingTarget(Plant target) =>
        target.def.plant != null
        && target.Map == Manager.map
        && AllowedPlants.Contains(target.def)
        && target.Spawned
        && (ForagingArea == null || ForagingArea.ActiveCells.Contains(target.Position));

    private void ConfigureThresholdTrigger()
    {
        TriggerThreshold.AllowAnyThresholdChanged = ConfigureThresholdTriggerParentFilter;
        TriggerThreshold.SettingsChanged = Notify_ThresholdFilterChanged;
        TriggerThreshold.ThresholdFilter.SetDisallowAll();
        if (Scribe.mode == LoadSaveMode.Inactive)
        {
            ConfigureThresholdTriggerParentFilter();
        }
    }

    private void ConfigureThresholdTriggerParentFilter()
    {
        if (!TriggerThreshold.AllowAnyThreshold)
        {
            var parentFilter = TriggerThreshold.ParentFilter;
            parentFilter.SetDisallowAll();
            foreach (
                var harvestedThingDef in Utilities_Plants
                    .GetForagingPlants(Manager)
                    .Select(p => p.plant.harvestedThingDef)
            )
            {
                parentFilter.SetAllow(harvestedThingDef, true);
            }

            if (ModsConfig.IsActive(Constants.SurvivalistsAdditionsModId))
            {
                parentFilter.SetAllow(ManagerThingDefOf.SRV_Turnip, true);
                parentFilter.SetAllow(ManagerThingDefOf.SRV_Turnip_Green, true);
            }
        }
    }

    protected override void Notify_AreaRemoved(Area area)
    {
        if (ForagingArea == area)
        {
            ForagingArea = null;
        }
    }
}
