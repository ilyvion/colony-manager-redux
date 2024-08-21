// ManagerJob_Forestry.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerJob_Forestry : ManagerJob<ManagerSettings_Forestry>
{
    public sealed class History : HistoryWorker<ManagerJob_Forestry>
    {
        public override Coroutine GetCountForHistoryChapterCoroutine(
            ManagerJob_Forestry managerJob,
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
                count.Value = managerJob.GetCurrentDesignatedCount(cached: false);
            }
            else
            {
                throw new ArgumentException($"Unexpected chapterDef value {chapterDef.defName}");
            }
        }

        public override Coroutine GetTargetForHistoryChapterCoroutine(
            ManagerJob_Forestry managerJob,
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

    public enum ForestryJobType
    {
        ClearArea,
        Logging
    }

    private readonly CachedValue<int>
        _designatedWoodCachedValue = new(0);

    public HashSet<ThingDef> AllowedTrees = [];
    public bool AllowSaplings;
    public HashSet<Area> ClearAreas = [];
    public Area? LoggingArea;
    public Utilities.SyncDirection Sync = Utilities.SyncDirection.AllowedToFilter;

    public bool SyncFilterAndAllowed = true;

    private List<Designation> _designations = [];

    private List<ThingDef>? _allPlants;
    public List<ThingDef> AllPlants
    {
        get
        {
            _allPlants ??= Utilities_Plants.GetForestryPlants(Manager, Type == ForestryJobType.ClearArea).ToList();
            return _allPlants;
        }
    }

    private ForestryJobType _type = ForestryJobType.Logging;

    public Trigger_Threshold TriggerThreshold => (Trigger_Threshold)Trigger!;

    public ManagerJob_Forestry(Manager manager) : base(manager)
    {
        // populate the trigger field, set the root category to wood.
        Trigger = new Trigger_Threshold(this);
        if (Scribe.mode == LoadSaveMode.Inactive)
        {
            ConfigureThresholdTriggerParentFilter();
        }
        TriggerThreshold.SettingsChanged = Notify_ThresholdFilterChanged;
    }

    public override void PostMake()
    {
        var forestrySettings = ManagerSettings;
        if (forestrySettings != null)
        {
            _type = forestrySettings.DefaultForestryJobType;
            AllowSaplings = forestrySettings.DefaultAllowSaplings;
            SyncFilterAndAllowed = forestrySettings.DefaultSyncFilterAndAllowed;
        }
    }

    public override void PostImport()
    {
        base.PostImport();

        AllowedTrees.RemoveWhere(t => !AllPlants.Contains(t));
    }

    public List<Designation> Designations => new(_designations);

    public override bool IsValid => base.IsValid && TriggerThreshold != null;

    public override IEnumerable<string> Targets
    {
        get
        {
            switch (Type)
            {
                case ForestryJobType.Logging:
                    return AllowedTrees.Select(tree => tree.LabelCap.Resolve());
                case ForestryJobType.ClearArea:
                    if (ClearAreas.Count == 0)
                    {
                        return ["ColonyManagerRedux.Common.None".Translate().RawText];
                    }

                    return ClearAreas.Select(ca => ca.Label);

                default:
                    throw new Exception($"Invalid ForestryJobType value: {Type}");
            }
        }
    }

    public ForestryJobType Type
    {
        get => _type;
        set
        {
            _type = value;
            RefreshAllTrees();
        }
    }

    public override WorkTypeDef WorkTypeDef => WorkTypeDefOf.PlantCutting;

    public void AddRelevantGameDesignations(ManagerLog jobLog)
    {
        // get list of game designations not managed by this job that could have been assigned by this job.
        int addedCount = 0;
        List<LocalTargetInfo> newTargets = [];
        foreach (var des in Manager.map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.CutPlant)
            .Except(_designations)
            .Where(des => IsValidForestryTarget(des.target)))
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

    public override void CleanUp(ManagerLog? jobLog)
    {
        // clear the list of obsolete designations
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

    public Coroutine DoClearAreaDesignations(ManagerLog jobLog, Area area, Boxed<bool> workDone)
    {
        var map = Manager.map;
        var designationManager = map.designationManager;

        bool designationsAdded = false;
        foreach (var (cell, i) in area.ActiveCells.Select((c, i) => (c, i)))
        {
            // This is at the start so that it also includes loops that were `continue`d.
            if (i > 0 && i % Constants.CoroutineBreakAfter == 0)
            {
                yield return ResumeImmediately.Singleton;
            }

            // confirm there is a plant here that it is a tree and that it has no current designation
            var plant = cell.GetPlant(map);

            // if there is no plant, or there is already a designation here, bail out
            if (plant == null || designationManager.AllDesignationsOn(plant).Any())
            {
                continue;
            }

            // if the plant is not in the allowed filter
            if (!AllowedTrees.Contains(plant.def))
            {
                continue;
            }

            // we don't cut stuff in growing zones
            if (map.zoneManager.ZoneAt(cell) is IPlantToGrowSettable)
            {
                continue;
            }

            // nor in plant pots (or hydroponics)
            if (map.thingGrid.ThingsListAt(cell).Any(t => t is Building_PlantGrower))
            {
                continue;
            }

            // there's no reason not to cut it down, so cut it down.
            designationManager.AddDesignation(new Designation(plant, DesignationDefOf.CutPlant));
            jobLog.AddDetail("ColonyManagerRedux.Forestry.Logs.AddClearingDesignation"
                .Translate(
                    DesignationDefOf.CutPlant.ActionText(),
                    "ColonyManagerRedux.Foraging.Logs.Plant".Translate(),
                    plant.Label,
                    area.Label),
                plant);
            workDone.Value = true;
            designationsAdded = true;
        }

        if (!designationsAdded)
        {
            jobLog.AddDetail("ColonyManagerRedux.Logs.NoValidTargets".Translate(
                "ColonyManagerRedux.Foraging.Logs.Plants".Translate(),
                Def.label
            ));
        }
    }

    public override void ExposeData()
    {
        // scribe base things
        base.ExposeData();

        // settings, references first!
        Scribe_Collections.Look(ref AllowedTrees, "allowedTrees", LookMode.Def);
        Scribe_Values.Look(ref SyncFilterAndAllowed, "syncFilterAndAllowed", true);
        Scribe_Values.Look(ref _type, "type", ForestryJobType.Logging);
        Scribe_Values.Look(ref AllowSaplings, "allowSaplings");

        if (Manager.ScribeGameSpecificData)
        {
            Scribe_References.Look(ref LoggingArea, "loggingArea");

            // clearing areas list
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                // make sure areas list doesn't contain deleted areas
                UpdateClearAreas();
            }

            // scribe that stuff
            Scribe_Collections.Look(ref ClearAreas, "clearAreas", LookMode.Reference);

            Utilities.Scribe_Designations(ref _designations, Manager);
        }

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            ConfigureThresholdTriggerParentFilter();
            TriggerThreshold.SettingsChanged = Notify_ThresholdFilterChanged;
        }
    }

    private int CurrentDesignatedCountRaw
    {
        get
        {
            var count = 0;
            foreach (var des in _designations)
            {
                if (des.target.HasThing &&
                     des.target.Thing is Plant plant)
                {
                    count += plant.YieldNow();
                }
            }
            return count;
        }
    }

    public int GetCurrentDesignatedCount(bool cached = true)
    {
        return cached && _designatedWoodCachedValue.TryGetValue(out int count)
            ? count
            : _designatedWoodCachedValue.Update(CurrentDesignatedCountRaw);
    }

    public void Notify_ThresholdFilterChanged()
    {
        ColonyManagerReduxMod.Instance.LogDebug("Threshold changed.");

        if (!SyncFilterAndAllowed || Sync == Utilities.SyncDirection.AllowedToFilter)
        {
            return;
        }

        foreach (var thingDef in AllPlants)
        {
            if (TriggerThreshold.ThresholdFilter.Allows(thingDef.plant.harvestedThingDef))
            {
                AllowedTrees.Add(thingDef);
            }
            else
            {
                AllowedTrees.Remove(thingDef);
            }
        }
    }

    public void RefreshAllTrees()
    {
        ColonyManagerReduxMod.Instance.LogDebug("Refreshing all trees");

        // all plants
        _allPlants = null;
        var options = AllPlants;

        // remove stuff not in new list
        foreach (var tree in AllowedTrees.ToList())
        {
            if (!options.Contains(tree))
            {
                AllowedTrees.Remove(tree);
            }
        }
    }

    public void SetTreeAllowed(ThingDef tree, bool allow, bool sync = true)
    {
        if (allow)
        {
            AllowedTrees.Add(tree);
        }
        else
        {
            AllowedTrees.Remove(tree);
        }

        if (SyncFilterAndAllowed && sync)
        {
            Sync = Utilities.SyncDirection.AllowedToFilter;

            ThingDef harvestedThingDef = tree.plant.harvestedThingDef;
            var setAllow = AllowedTrees.Any(t => t.plant.harvestedThingDef == harvestedThingDef);
            TriggerThreshold.ThresholdFilter.SetAllow(harvestedThingDef, setAllow);
        }
    }

    public override Coroutine TryDoJobCoroutine(
        ManagerLog jobLog,
        Boxed<bool> workDone)
    {
        if (Type == ForestryJobType.Logging && !TriggerThreshold.State)
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

        // clean dead designations
        CleanDeadDesignations(_designations, DesignationDefOf.HarvestPlant, jobLog);
        yield return ResumeImmediately.Singleton;

        CoroutineHandle? handle = null;
        switch (Type)
        {
            case ForestryJobType.Logging:
                handle = MultiTickCoroutineManager.StartCoroutine(
                    DoLoggingJob(jobLog, workDone),
                    debugHandle: "DoLoggingJob");
                break;
            case ForestryJobType.ClearArea:
                if (ClearAreas.Any())
                {
                    handle = MultiTickCoroutineManager.StartCoroutine(
                        DoClearAreas(jobLog, workDone),
                        debugHandle: "DoClearAreas");
                }

                break;
        }

        if (handle != null)
        {
            yield return handle.ResumeWhenOtherCoroutineIsCompleted();
        }
    }

    internal void UpdateClearAreas()
    {
        // iterate over existing areas, remove deleted areas.
        var Areas = new List<Area>(ClearAreas);
        foreach (var area in Areas)
        {
            if (!Manager.map.areaManager.AllAreas.Contains(area))
            {
                ClearAreas.Remove(area);
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
            else if ((!LoggingArea?.ActiveCells.Contains(des.target.Thing.Position)) ?? false)
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

    private Coroutine DoClearAreas(ManagerLog jobLog, Boxed<bool> workDone)
    {
        foreach (var area in ClearAreas)
        {
            yield return DoClearAreaDesignations(jobLog, area, workDone)
                .ResumeWhenOtherCoroutineIsCompleted(debugHandle: nameof(DoClearAreaDesignations));
        }
    }

    private Coroutine DoLoggingJob(ManagerLog jobLog, Boxed<bool> workDone)
    {
        // remove designations not in zone.
        if (LoggingArea != null)
        {
            CleanAreaDesignations(jobLog);
            yield return ResumeImmediately.Singleton;
        }

        // add external designations
        AddRelevantGameDesignations(jobLog);
        yield return ResumeImmediately.Singleton;

        // get current lumber count
        var count = TriggerThreshold.GetCurrentCount() + GetCurrentDesignatedCount();
        yield return ResumeImmediately.Singleton;

        // designate until we're either out of trees or we have enough designated.
        if (count >= TriggerThreshold.TargetCount)
        {
            yield break;
        }

        jobLog.AddDetail("ColonyManagerRedux.Logs.CurrentCount".Translate(count, TriggerThreshold.TargetCount));
        var trees = GetLoggableTreesSorted();
        yield return ResumeImmediately.Singleton;

        if (trees.Count == 0)
        {
            jobLog.AddDetail("ColonyManagerRedux.Logs.NoValidTargets".Translate(
                "ColonyManagerRedux.Forestry.Logs.Trees".Translate(),
                Def.label
            ));
        }

        for (var i = 0; i < trees.Count && count < TriggerThreshold.TargetCount; i++)
        {
            Plant tree = trees[i];
            int yield = tree.YieldNow();
            count += yield;
            AddDesignation(new(tree, DesignationDefOf.HarvestPlant));
            jobLog.AddDetail("ColonyManagerRedux.Logs.AddDesignation"
                .Translate(
                    DesignationDefOf.HarvestPlant.ActionText(),
                    string.Empty,
                    tree.Label,
                    yield,
                    count,
                    TriggerThreshold.TargetCount),
                tree);
            workDone.Value = true;
            if (i > 0 && i % Constants.CoroutineBreakAfter == 0)
            {
                yield return ResumeImmediately.Singleton;
            }
        }
    }

    private List<Plant> GetLoggableTreesSorted()
    {
        var position = Manager.map.GetBaseCenter();

#if DEBUG_PERFORMANCE
        DeepProfiler.Start( "GetLoggableTreesSorted" );
#endif
        var list = Manager.map.listerThings.AllThings.Where(IsValidForestryTarget)
                          .Select(p => (Plant)p)
                          .OrderByDescending(p => p.YieldNow() / Distance(p, position))
                          .ToList();

#if DEBUG_PERFORMANCE
        DeepProfiler.End();
#endif

        return list;
    }

    private List<IntVec3> GetWindCells()
    {
        return Manager.map.listerBuildings
                      .allBuildingsColonist
                      .Where(b => b.GetComp<CompPowerPlantWind>() != null)
                      .SelectMany(turbine => WindTurbineUtility.CalculateWindCells(turbine.Position,
                                                                                     turbine.Rotation,
                                                                                     turbine.RotatedSize))
                      .ToList();
    }

    private bool IsInWindTurbineArea(IntVec3 position)
    {
        return GetWindCells().Contains(position);
    }

    private bool IsValidForestryTarget(LocalTargetInfo t)
    {
        return t.HasThing
            && IsValidForestryTarget(t.Thing);
    }

    private bool IsValidForestryTarget(Thing t)
    {
        return t is Plant plant
            && IsValidForestryTarget(plant);
    }

    private bool IsValidForestryTarget(Plant target)
    {
        return target.def.plant != null

            && AllowedTrees.Contains(target.def)

            && target.Spawned
            && Manager.map.designationManager.DesignationOn(target) == null

            // cut only mature trees, or saplings that yield something right now.
            && (AllowSaplings || target.LifeStage == PlantLifeStage.Mature) && target.YieldNow() > 1
            && (LoggingArea == null || LoggingArea.ActiveCells.Contains(target.Position))

            // reachable
            && IsReachable(target);
    }

    private void ConfigureThresholdTriggerParentFilter()
    {
        TriggerThreshold.ParentFilter.SetDisallowAll();
        foreach (var item in AllPlants)
        {
            TriggerThreshold.ParentFilter.SetAllow(item.plant.harvestedThingDef, true);
        }
    }

    protected override void Notify_AreaRemoved(Area area)
    {
        if (LoggingArea == area)
        {
            LoggingArea = null;
        }
        ClearAreas.Remove(area);
    }
}
