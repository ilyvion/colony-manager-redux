// ManagerJob_Foraging.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerJob_Foraging : ManagerJob<ManagerSettings_Foraging>
{
    public sealed class History : HistoryWorker<ManagerJob_Foraging>
    {
        public override Coroutine GetCountForHistoryChapterCoroutine(
            ManagerJob_Foraging managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> count)
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryStock)
            {
                yield return managerJob.TriggerThreshold.GetCurrentCountCoroutine(count)
                    .ResumeWhenOtherCoroutineIsCompleted();
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryDesignated)
            {
                yield return managerJob._cachedCurrentDesignatedCount.DoUpdateIfNeeded(force: true)
                    .ResumeWhenOtherCoroutineIsCompleted();
                count.Value = managerJob._cachedCurrentDesignatedCount.Value;
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
            Boxed<int> target)
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryStock)
            {
                target.Value = managerJob.TriggerThreshold.TargetCount;
            }
            else
            {
                target.Value = 0;
            }
            yield break;
        }
    }


    public HashSet<ThingDef> AllowedPlants = [];
    public Area? ForagingArea;
    public bool ForceFullyMature;
    public Utilities.SyncDirection Sync = Utilities.SyncDirection.AllowedToFilter;
    public bool SyncFilterAndAllowed = true;

    private List<Designation> _designations = [];

    private MultiTickCachedValue<int> _cachedCurrentDesignatedCount;
    internal MultiTickCachedValue<int> CachedCurrentDesignatedCount
        => _cachedCurrentDesignatedCount;

    private List<ThingDef>? _allPlants;
    public List<ThingDef> AllPlants
    {
        get
        {
            _allPlants ??= Utilities_Plants.GetForagingPlants(Manager).ToList();
            return _allPlants;
        }
    }

    public Trigger_Threshold TriggerThreshold => (Trigger_Threshold)Trigger!;

    public List<Designation> Designations => new(_designations);

    public override bool IsValid => base.IsValid && TriggerThreshold != null;

    public override IEnumerable<string> Targets => AllowedPlants
        .Select(plant => plant.LabelCap.Resolve());

    public override WorkTypeDef WorkTypeDef => WorkTypeDefOf.Growing;

    public ManagerJob_Foraging(Manager manager) : base(manager)
    {
        _cachedCurrentDesignatedCount = new(0, GetCurrentDesignatedCountCoroutine);

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

        AllowedPlants.RemoveWhere(p => !AllPlants.Contains(p));
    }

    private Coroutine GetCurrentDesignatedCountCoroutine(AnyBoxed<int> count)
    {
        for (int i = 0; i < _designations.Count; i++)
        {
            if (i > 0 && i % Constants.CoroutineBreakAfter == 0)
            {
                yield return ResumeImmediately.Singleton;
            }

            Designation? des = _designations[i];

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

            count.Value += plant.YieldNow();
        }
    }

    public void AddRelevantGameDesignations(ManagerLog jobLog)
    {
        // get list of game designations not managed by this job that could have been assigned
        // by this job.
        int addedCount = 0;
        List<LocalTargetInfo> newTargets = [];
        foreach (
            var des in Manager.map.designationManager
                .SpawnedDesignationsOfDef(DesignationDefOf.HarvestPlant)
                .Except(_designations)
                .Where(des => IsValidDesignatedForagingTarget(des.target)))
        {
            addedCount++;
            AddDesignation(des, false);
            newTargets.Add(des.target);
        }
        if (addedCount > 0)
        {
            jobLog.AddDetail("ColonyManagerRedux.Logs.AddRelevantGameDesignations"
                .Translate(addedCount, Def.label), newTargets);
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
        return "ColonyManagerRedux.Job.DesignationLabel".Translate(
            plant.LabelCap,
            Distance(plant, Manager.map.GetBaseCenter()).ToString("F0"),
            plant.YieldNow(),
            plant.def.plant.harvestedThingDef.LabelCap);
    }

    public override void ExposeData()
    {
        // scribe base things
        base.ExposeData();

        // settings, references first!
        Scribe_Collections.Look(ref AllowedPlants, "allowedPlants", LookMode.Def);
        Scribe_Values.Look(ref ForceFullyMature, "forceFullyMature");

        if (Manager.ScribeGameSpecificData)
        {
            Scribe_References.Look(ref ForagingArea, "foragingArea");

            Utilities.Scribe_Designations(ref _designations, Manager);
        }

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            TriggerThreshold.SettingsChanged = Notify_ThresholdFilterChanged;
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
            if (TriggerThreshold.ThresholdFilter.Allows(plant.plant.harvestedThingDef))
            {
                AllowedPlants.Add(plant);
            }
            else
            {
                AllowedPlants.Remove(plant);
            }
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
                AllowedPlants.Remove(plant);
            }
        }
        ConfigureThresholdTriggerParentFilter();
    }

    public void SetPlantAllowed(ThingDef plant, bool allow, bool sync = true)
    {
        if (allow)
        {
            AllowedPlants.Add(plant);
        }
        else
        {
            AllowedPlants.Remove(plant);
        }

        if (SyncFilterAndAllowed && sync)
        {
            Sync = Utilities.SyncDirection.AllowedToFilter;

            ThingDef harvestedThingDef = plant.plant.harvestedThingDef;
            var setAllow = AllowedPlants.Any(p => p.plant.harvestedThingDef == harvestedThingDef);
            TriggerThreshold.ThresholdFilter.SetAllow(harvestedThingDef, setAllow);
        }
    }

    public override Coroutine TryDoJobCoroutine(
        ManagerLog jobLog,
        Boxed<bool> workDone)
    {
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
        yield return ResumeImmediately.Singleton;

        // clean up designations that are (now) in the wrong area.
        CleanAreaDesignations(jobLog);
        yield return ResumeImmediately.Singleton;

        // add designations in the game that could have been handled by this job
        AddRelevantGameDesignations(jobLog);
        yield return ResumeImmediately.Singleton;

        // designate plants until trigger is met.
        yield return _cachedCurrentDesignatedCount.DoUpdateIfNeeded(force: true)
            .ResumeWhenOtherCoroutineIsCompleted();
        var count = TriggerThreshold.GetCurrentCount() + _cachedCurrentDesignatedCount.Value;

        if (count >= TriggerThreshold.TargetCount
            || ColonyManagerReduxMod.Settings.ShouldRemoveMoreDesignations(_designations.Count))
        {
            List<Designation> sortedDesignations = [];
            yield return GetThingsSorted(
                _designations.Where(d => d.target.HasThing),
                sortedDesignations,
                _ => true,
                (p, d) => p.YieldNow() / d,
                d => (Plant)d.target.Thing)
                .ResumeWhenOtherCoroutineIsCompleted();

            // reduce designations until we're just above target
            foreach (var (designation, i) in sortedDesignations.Select((d, i) => (d, i)).Reverse())
            {
                var plant = (Plant)designation.target.Thing;
                int yield = plant.YieldNow();
                count -= yield;
                if (count >= TriggerThreshold.TargetCount
                    || ColonyManagerReduxMod.Settings
                        .ShouldRemoveMoreDesignations(_designations.Count))
                {
                    designation.Delete();
                    _designations.Remove(designation);
                    jobLog.AddDetail("ColonyManagerRedux.Logs.RemoveDesignation"
                        .Translate(
                            DesignationDefOf.HarvestPlant.ActionText(),
                            "ColonyManagerRedux.Foraging.Logs.Plant".Translate(),
                            plant.Label,
                            yield,
                            count,
                            TriggerThreshold.TargetCount),
                        plant);
                    workDone.Value = true;
                }
                else
                {
                    break;
                }

                if (i > 0 && i % Constants.CoroutineBreakAfter == 0)
                {
                    yield return ResumeImmediately.Singleton;
                }
            }

            if (!workDone)
            {
                jobLog.AddDetail("ColonyManagerRedux.Logs.TargetsAlreadySatisfied".Translate(
                    "ColonyManagerRedux.Foraging.Logs.Plants".Translate(),
                    Def.label
                ));
            }

            yield break;
        }

        jobLog.AddDetail("ColonyManagerRedux.Logs.CurrentCount"
            .Translate(count, TriggerThreshold.TargetCount));

        if (!ColonyManagerReduxMod.Settings.CanAddMoreDesignations(_designations.Count))
        {
            jobLog.AddDetail("ColonyManagerRedux.Logs.CantAddMoreDesignations".Translate(
                "ColonyManagerRedux.Foraging.Logs.Plants".Translate(),
                Def.label
            ));
            yield break;
        }

        List<Plant> sortedPlants = [];
        yield return GetTargetsSorted(
            sortedPlants,
            IsValidUndesignatedForagingTarget,
            (p, d) => p.YieldNow() / d)
            .ResumeWhenOtherCoroutineIsCompleted();

        if (sortedPlants.Count == 0)
        {
            jobLog.AddDetail("ColonyManagerRedux.Logs.NoValidTargets".Translate(
                "ColonyManagerRedux.Foraging.Logs.Plants".Translate(),
                Def.label
            ));
            yield break;
        }

        foreach (var (plant, i) in sortedPlants.Select((t, i) => (t, i)))
        {
            if (count >= TriggerThreshold.TargetCount
                || !ColonyManagerReduxMod.Settings.CanAddMoreDesignations(_designations.Count))
            {
                break;
            }

            int yield = plant.YieldNow();
            count += yield;
            AddDesignation(new(plant, DesignationDefOf.HarvestPlant));
            jobLog.AddDetail("ColonyManagerRedux.Logs.AddDesignation"
                .Translate(
                    DesignationDefOf.HarvestPlant.ActionText(),
                    "ColonyManagerRedux.Foraging.Logs.Plant".Translate(),
                    plant.Label,
                    yield,
                    count,
                    TriggerThreshold.TargetCount),
                plant);
            workDone.Value = true;
            if (i > 0 && i % Constants.CoroutineBreakAfter == 0)
            {
                yield return ResumeImmediately.Singleton;
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
        int missingThingCount = 0;
        int incorrectAreaCount = 0;
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
            jobLog.AddDetail("ColonyManagerRedux.Logs.CleanAreaDesignations"
                .Translate(
                    missingThingCount + incorrectAreaCount,
                    missingThingCount,
                    incorrectAreaCount,
                    Def.label));
        }
    }

    private bool IsValidUndesignatedForagingTarget(LocalTargetInfo t)
    {
        return t.HasThing
            && IsValidUndesignatedForagingTarget(t.Thing);
    }

    private bool IsValidUndesignatedForagingTarget(Thing t)
    {
        return t is Plant plant && IsValidUndesignatedForagingTarget(plant);
    }

    private bool IsValidUndesignatedForagingTarget(Plant target)
    {
        return target.def.plant != null
            && target.Map == Manager.map

            && AllowedPlants.Contains(target.def)
            && target.Spawned
            && Manager.map.designationManager.DesignationOn(target) == null

            // cut only mature plants, or non-mature that yield something right now.
            && ((!ForceFullyMature && target.YieldNow() > 1)
                || target.LifeStage == PlantLifeStage.Mature)

            && (ForagingArea == null || ForagingArea.ActiveCells.Contains(target.Position))

            && IsReachable(target);
    }

    private bool IsValidDesignatedForagingTarget(LocalTargetInfo t)
    {
        return t.HasThing
            && IsValidDesignatedForagingTarget(t.Thing);
    }

    private bool IsValidDesignatedForagingTarget(Thing t)
    {
        return t is Plant plant && IsValidDesignatedForagingTarget(plant);
    }

    private bool IsValidDesignatedForagingTarget(Plant target)
    {
        return target.def.plant != null
            && target.Map == Manager.map

            && AllowedPlants.Contains(target.def)
            && target.Spawned

            && (ForagingArea == null || ForagingArea.ActiveCells.Contains(target.Position));
    }

    private void ConfigureThresholdTrigger()
    {
        TriggerThreshold.SettingsChanged = Notify_ThresholdFilterChanged;
        TriggerThreshold.ThresholdFilter.SetDisallowAll();
        if (Scribe.mode == LoadSaveMode.Inactive)
        {
            ConfigureThresholdTriggerParentFilter();
        }
    }

    private void ConfigureThresholdTriggerParentFilter()
    {
        ThingFilter parentFilter = TriggerThreshold.ParentFilter;
        parentFilter.SetDisallowAll();
        foreach (var harvestedThingDef in Utilities_Plants.GetForagingPlants(Manager).Select(p => p.plant.harvestedThingDef))
        {
            parentFilter.SetAllow(harvestedThingDef, true);
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
