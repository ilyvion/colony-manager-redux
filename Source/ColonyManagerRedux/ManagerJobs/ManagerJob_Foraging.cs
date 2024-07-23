// ManagerJob_Foraging.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HotSwappable]
internal sealed class ManagerJob_Foraging : ManagerJob
{
    public sealed class History : HistoryWorker<ManagerJob_Foraging>
    {
        public override int GetCountForHistoryChapter(ManagerJob_Foraging managerJob, ManagerJobHistoryChapterDef chapterDef)
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryStock)
            {
                return managerJob.TriggerThreshold.CurrentCount;
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryDesignated)
            {
                return managerJob.CurrentDesignatedCount;
            }
            else
            {
                throw new ArgumentException($"Unexpected chapterDef value {chapterDef.defName}");
            }
        }

        public override int GetTargetForHistoryChapter(ManagerJob_Foraging managerJob, ManagerJobHistoryChapterDef chapterDef)
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
        var foragingSettings = ColonyManagerReduxMod.Settings.ManagerJobSettingsFor<ManagerJobSettings_Foraging>(def);
        if (foragingSettings != null)
        {
            SyncFilterAndAllowed = foragingSettings.DefaultSyncFilterAndAllowed;
            ForceFullyMature = foragingSettings.DefaultForceFullyMature;
        }
    }

    public override void PostImport()
    {
        base.PostImport();
        TriggerThreshold.job = this;

        AllowedPlants.RemoveWhere(p => !AllPlants.Contains(p));
    }

    public override bool IsCompleted => !TriggerThreshold.State;

    public int CurrentDesignatedCount
    {
        get
        {

            // see if we have a cached count
            if (_cachedCurrentDesignatedCount.TryGetValue(out int count))
            {
                return count;
            }

            // fetch count
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

            _cachedCurrentDesignatedCount.Update(count);
            return count;
        }
    }

    public List<Designation> Designations => new(_designations);

    public override bool IsValid => base.IsValid && TriggerThreshold != null;

    public override string Label => "ColonyManagerRedux.Foraging.Foraging".Translate();

    public override IEnumerable<string> Targets => AllowedPlants
        .Select(plant => plant.LabelCap.Resolve());

    public override WorkTypeDef WorkTypeDef => WorkTypeDefOf.Growing;


    public void AddRelevantGameDesignations()
    {
        // get list of game designations not managed by this job that could have been assigned by this job.
        foreach (
            var des in Manager.map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.HarvestPlant)
                .Except(_designations)
                .Where(des => IsValidForagingTarget(des.target)))
        {
            AddDesignation(des);
        }
    }

    /// <summary>
    ///     Remove designations in our managed list that are not in the game's designation manager.
    /// </summary>
    public void CleanDeadDesignations()
    {
        var _gameDesignations =
            Manager.map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.HarvestPlant);
        _designations = _designations.Intersect(_gameDesignations).ToList();
    }

    /// <summary>
    ///     Clean up all outstanding designations
    /// </summary>
    public override void CleanUp()
    {
        CleanDeadDesignations();
        foreach (var des in _designations)
        {
            des.Delete();
        }

        _designations.Clear();
    }

    public string DesignationLabel(Designation designation)
    {
        // label, dist, yield.
        var plant = (Plant)designation.target.Thing;
        return "ColonyManagerRedux.Manager.DesignationLabel".Translate(
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

        if (Manager.Mode == Manager.ScribingMode.Normal)
        {
            Scribe_References.Look(ref ForagingArea, "foragingArea");

            Utilities.Scribe_Designations(ref _designations, Manager);
        }

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
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
        Logger.Debug("Threshold changed.");
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
        Logger.Debug("Refreshing all plants");

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

    public override bool TryDoJob()
    {
        // keep track of work done
        var workDone = false;

        // clean up designations that were completed.
        CleanDeadDesignations();

        // clean up designations that are (now) in the wrong area.
        CleanAreaDesignations();

        // add designations in the game that could have been handled by this job
        AddRelevantGameDesignations();

        // designate plants until trigger is met.
        var count = TriggerThreshold.CurrentCount + CurrentDesignatedCount;
        if (count < TriggerThreshold.TargetCount)
        {
            var targets = GetValidForagingTargetsSorted();

            for (var i = 0; i < targets.Count && count < TriggerThreshold.TargetCount; i++)
            {
                var des = new Designation(targets[i], DesignationDefOf.HarvestPlant);
                count += targets[i].YieldNow();
                AddDesignation(des);
                workDone = true;
            }
        }

        return workDone;
    }

    private void AddDesignation(Designation des)
    {
        // add to game
        Manager.map.designationManager.AddDesignation(des);

        // add to internal list
        _designations.Add(des);
    }

    private void CleanAreaDesignations()
    {
        foreach (var des in _designations)
        {
            if (!des.target.HasThing)
            {
                des.Delete();
            }

            // if area is not null and does not contain designate location, remove designation.
            else if (!ForagingArea?.ActiveCells.Contains(des.target.Thing.Position) ?? false)
            {
                des.Delete();
            }
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
        TriggerThreshold.ThresholdFilter = new ThingFilter(Notify_ThresholdFilterChanged);
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
