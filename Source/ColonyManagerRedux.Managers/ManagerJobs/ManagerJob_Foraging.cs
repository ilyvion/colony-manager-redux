// ManagerJob_Foraging.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerJob_Foraging : ManagerJob<ManagerSettings_Foraging>
{
    public sealed class History : HistoryWorker<ManagerJob_Foraging>
    {
        public override int GetCountForHistoryChapter(ManagerJob_Foraging managerJob, int tick, ManagerJobHistoryChapterDef chapterDef)
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryStock)
            {
                return managerJob.TriggerThreshold.GetCurrentCount(cached: false);
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryDesignated)
            {
                return managerJob.GetCurrentDesignatedCount();
            }
            else
            {
                throw new ArgumentException($"Unexpected chapterDef value {chapterDef.defName}");
            }
        }

        public override int GetTargetForHistoryChapter(ManagerJob_Foraging managerJob, int tick, ManagerJobHistoryChapterDef chapterDef)
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryStock)
            {
                return managerJob.TriggerThreshold.TargetCount;
            }
            return 0;
        }
    }

    private readonly CachedValue<int> _cachedCurrentDesignatedCount = new(0);

    public HashSet<ThingDef> AllowedPlants = [];
    public Area? ForagingArea;
    public bool ForceFullyMature;
    public Utilities.SyncDirection Sync = Utilities.SyncDirection.AllowedToFilter;
    public bool SyncFilterAndAllowed = true;

    private List<Designation> _designations = [];

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

    public ManagerJob_Foraging(Manager manager) : base(manager)
    {
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

    private int CurrentDesignatedCountRaw
    {
        get
        {
            var count = 0;
            foreach (var des in _designations)
            {
                if (!des.target.HasThing)
                {
                    continue;
                }


                if (des.target.Thing is not Plant plant)
                {
                    continue;
                }

                count += plant.YieldNow();
            }
            return count;
        }
    }

    public int GetCurrentDesignatedCount(bool cached = true)
    {
        return cached && _cachedCurrentDesignatedCount.TryGetValue(out int count)
            ? count
            : _cachedCurrentDesignatedCount.Update(CurrentDesignatedCountRaw);
    }

    public List<Designation> Designations => new(_designations);

    public override bool IsValid => base.IsValid && TriggerThreshold != null;

    public override IEnumerable<string> Targets => AllowedPlants
        .Select(plant => plant.LabelCap.Resolve());

    public override WorkTypeDef WorkTypeDef => WorkTypeDefOf.Growing;


    public void AddRelevantGameDesignations(ManagerLog jobLog)
    {
        // get list of game designations not managed by this job that could have been assigned by this job.
        int addedCount = 0;
        List<LocalTargetInfo> newTargets = [];
        foreach (
            var des in Manager.map.designationManager
                .SpawnedDesignationsOfDef(DesignationDefOf.HarvestPlant)
                .Except(_designations)
                .Where(des => IsValidForagingTarget(des.target)))
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

    public static List<ThingDef> GetMaterialsInPlant(ThingDef plantDef)
    {
        var plant = (plantDef?.plant) ?? throw new ArgumentNullException(nameof(plantDef));
        return new List<ThingDef>([plant.harvestedThingDef]);
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
            if (GetMaterialsInPlant(plant).Any(TriggerThreshold.ThresholdFilter.Allows))
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

            foreach (var material in GetMaterialsInPlant(plant))
            {
                if (TriggerThreshold.ParentFilter.Allows(material))
                {
                    TriggerThreshold.ThresholdFilter.SetAllow(material, allow);
                }
            }
        }
    }

    public override bool TryDoJob(ManagerLog jobLog)
    {
        // keep track of work done
        var workDone = false;

        if (!TriggerThreshold.State)
        {
            if (JobState != ManagerJobState.Completed)
            {
                JobState = ManagerJobState.Completed;
                jobLog.AddDetail("ColonyManagerRedux.Logs.JobCompleted".Translate());

                CleanUp(jobLog);
            }
            return workDone;
        }
        else
        {
            JobState = ManagerJobState.Active;
        }

        // clean up designations that were completed.
        CleanDeadDesignations(_designations, DesignationDefOf.HarvestPlant, jobLog);

        // clean up designations that are (now) in the wrong area.
        CleanAreaDesignations(jobLog);

        // add designations in the game that could have been handled by this job
        AddRelevantGameDesignations(jobLog);

        // designate plants until trigger is met.
        var count = TriggerThreshold.GetCurrentCount() + GetCurrentDesignatedCount();

        if (count >= TriggerThreshold.TargetCount)
        {
            return workDone;
        }

        jobLog.AddDetail("ColonyManagerRedux.Logs.CurrentCount".Translate(count,
            TriggerThreshold.TargetCount));
        var targets = GetValidForagingTargetsSorted();

        if (targets.Count == 0)
        {
            jobLog.AddDetail("ColonyManagerRedux.Logs.NoValidTargets".Translate(
                "ColonyManagerRedux.Foraging.Logs.Plants".Translate(),
                Def.label
            ));
        }

        for (var i = 0; i < targets.Count && count < TriggerThreshold.TargetCount; i++)
        {
            Plant target = targets[i];
            int yield = target.YieldNow();
            count += yield;
            AddDesignation(new(target, DesignationDefOf.HarvestPlant));
            jobLog.AddDetail("ColonyManagerRedux.Logs.AddDesignation"
                .Translate(
                    DesignationDefOf.HarvestPlant.ActionText(),
                    "ColonyManagerRedux.Foraging.Logs.Plant".Translate(),
                    target.Label,
                    yield,
                    count,
                    TriggerThreshold.TargetCount),
                target);
            workDone = true;
        }

        return workDone;
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

    private List<Plant> GetValidForagingTargetsSorted()
    {
        var position = Manager.map.GetBaseCenter();

        return Manager.map.listerThings.AllThings
                      .Where(IsValidForagingTarget)

                      .Select(p => (Plant)p)
                      .OrderByDescending(p => p.YieldNow() / Distance(p, position))
                      .ToList();
    }

    private bool IsValidForagingTarget(LocalTargetInfo t)
    {
        return t.HasThing
            && IsValidForagingTarget(t.Thing);
    }

    private bool IsValidForagingTarget(Thing t)
    {
        return t is Plant plant && IsValidForagingTarget(plant);
    }

    private bool IsValidForagingTarget(Plant target)
    {
        // should be a plant, and be on the same map as this job
        return target.def.plant != null
            && target.Map == Manager.map

            // non-biome plants won't be on the list, also filters non-yield or wood plants
            && AllowedPlants.Contains(target.def)
            && target.Spawned
            && Manager.map.designationManager.DesignationOn(target) == null

            // cut only mature plants, or non-mature that yield something right now.
            && (!ForceFullyMature && target.YieldNow() > 1
              || target.LifeStage == PlantLifeStage.Mature)

            // limit to area of interest
            && (ForagingArea == null
              || ForagingArea.ActiveCells.Contains(target.Position))

            // reachable
            && IsReachable(target);
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
        foreach (var harvestedThingDef in Utilities_Plants.GetForagingPlants(Manager).Select(p => p.plant.harvestedThingDef))
        {
            parentFilter.SetAllow(harvestedThingDef, true);
        }
    }
}
