// ManagerJob_Forestry.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerJob_Forestry : ManagerJob<ManagerSettings_Forestry>
{
    public sealed class History : HistoryWorker<ManagerJob_Forestry>
    {
        public override int GetCountForHistoryChapter(ManagerJob_Forestry managerJob, int tick, ManagerJobHistoryChapterDef chapterDef)
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryStock)
            {
                return managerJob.TriggerThreshold.GetCurrentCount(cached: false);
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryDesignated)
            {
                return managerJob.GetCurrentDesignatedCount(cached: false);
            }
            else
            {
                throw new ArgumentException($"Unexpected chapterDef value {chapterDef.defName}");
            }
        }

        public override int GetTargetForHistoryChapter(ManagerJob_Forestry managerJob, int tick, ManagerJobHistoryChapterDef chapterDef)
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryStock)
            {
                return managerJob.TriggerThreshold.TargetCount;
            }
            return 0;
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
        TriggerThreshold.ThresholdFilter.SetAllow(ThingDefOf.WoodLog, true);
        ConfigureThresholdTriggerParentFilter();
    }

    public override void PostMake()
    {
        var forestrySettings = ManagerSettings;
        if (forestrySettings != null)
        {
            _type = forestrySettings.DefaultForestryJobType;
            AllowSaplings = forestrySettings.DefaultAllowSaplings;
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

    /// <summary>
    ///     Remove obsolete designations from the list.
    /// </summary>
    public void CleanDesignations(ManagerLog? jobLog = null)
    {
        var originalCount = _designations.Count;
        var gameDesignations =
            Manager.map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.HarvestPlant);
        _designations = _designations.Intersect(gameDesignations).ToList();
        var newCount = _designations.Count;

        if (originalCount != newCount)
        {
            jobLog?.AddDetail("ColonyManagerRedux.Logs.CleanDesignations"
                .Translate(originalCount - newCount, originalCount, newCount));
        }
    }

    public override void CleanUp()
    {
        // clear the list of obsolete designations
        CleanDesignations();

        // cancel outstanding designation
        foreach (var des in _designations)
        {
            des.Delete();
        }

        // clear the list completely
        _designations.Clear();
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

    public void DoClearAreaDesignations(ManagerLog jobLog, Area area, ref bool workDone)
    {
        var map = Manager.map;
        var designationManager = map.designationManager;

        bool designationsAdded = false;
        foreach (var cell in area.ActiveCells)
        {
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
            workDone = true;
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

    public void SetTreeAllowed(ThingDef tree, bool allow)
    {
        if (allow)
        {
            AllowedTrees.Add(tree);
        }
        else
        {
            AllowedTrees.Remove(tree);
        }
    }

    public override bool TryDoJob(ManagerLog jobLog)
    {
        // keep track if any actual work was done.
        var workDone = false;

        if (Type == ForestryJobType.Logging && !TriggerThreshold.State)
        {
            if (JobState != ManagerJobState.Completed)
            {
                JobState = ManagerJobState.Completed;
                CleanUp();
            }
            return workDone;
        }
        else
        {
            JobState = ManagerJobState.Active;
        }

        // clean dead designations
        CleanDesignations(jobLog);

        switch (Type)
        {
            case ForestryJobType.Logging:
                DoLoggingJob(jobLog, ref workDone);
                break;
            case ForestryJobType.ClearArea:
                if (ClearAreas.Any())
                {
                    DoClearAreas(jobLog, ref workDone);
                }

                break;
        }

        return workDone;
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

    private void DoClearAreas(ManagerLog jobLog, ref bool workDone)
    {
        foreach (var area in ClearAreas)
        {
            DoClearAreaDesignations(jobLog, area, ref workDone);
        }
    }

    private void DoLoggingJob(ManagerLog jobLog, ref bool workDone)
    {
        // remove designations not in zone.
        if (LoggingArea != null)
        {
            CleanAreaDesignations(jobLog);
        }

        // add external designations
        AddRelevantGameDesignations(jobLog);

        // get current lumber count
        var count = TriggerThreshold.GetCurrentCount() + GetCurrentDesignatedCount();

        // designate until we're either out of trees or we have enough designated.
        if (count >= TriggerThreshold.TargetCount)
        {
            return;
        }

        jobLog.AddDetail("ColonyManagerRedux.Logs.CurrentCount".Translate(count, TriggerThreshold.TargetCount));
        var trees = GetLoggableTreesSorted();

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
            workDone = true;
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
        TriggerThreshold.ParentFilter.SetAllow(ThingDefOf.WoodLog, true);
    }
}
