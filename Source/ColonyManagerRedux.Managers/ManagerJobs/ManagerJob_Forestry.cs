// ManagerJob_Forestry.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using Verse.AI;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
[CoroutineSettingsType]
internal sealed class ManagerJob_Forestry : ManagerJob<ManagerSettings_Forestry>
{
    [CoroutineSettingsType]
    public sealed class History : HistoryWorker<ManagerJob_Forestry>
    {
        [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
        public override Coroutine GetCountForHistoryChapterCoroutine(
            ManagerJob_Forestry managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> count
        )
        {
            var ticksBetweenOperations =
                ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                    (Func<
                        ManagerJob_Forestry,
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
            ManagerJob_Forestry managerJob,
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

    public enum ForestryJobType
    {
        ClearArea,
        Logging,
    }

    public HashSet<ThingDef> AllowedTrees = [];
    public bool AllowSaplings;
    public HashSet<Area> ClearAreas = [];
    public Area? LoggingArea;
    public bool InvertLoggingArea;
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
                .. Utilities_Plants.GetForestryPlants(
                    _plantsLockedToMap ? Manager.map : null,
                    Type == ForestryJobType.ClearArea
                ),
            ];
            return _allPlants;
        }
    }

    private ForestryJobType _type = ForestryJobType.Logging;

    public Trigger_Threshold TriggerThreshold => (Trigger_Threshold)Trigger!;

    public ManagerJob_Forestry(Manager manager)
        : base(manager)
    {
        CachedCurrentDesignatedCount = new(0, GetCurrentDesignatedCountCoroutine);

        // populate the trigger field, set the root category to wood.
        Trigger = new Trigger_Threshold(this)
        {
            AllowAnyThresholdChanged = ConfigureThresholdTriggerParentFilter,
        };
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

        _ = AllowedTrees.RemoveWhere(t => !AllPlants.Contains(t));
    }

    public List<Designation> Designations => [.. _designations];

    public override bool IsValid => base.IsValid && TriggerThreshold != null;

    private bool _hasLoggedInvalidTypeInTargets;
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
                    ColonyManagerReduxMod.Instance.LogErrorOnce(
                        $"Invalid ForestryJobType value: {Type}",
                        ref _hasLoggedInvalidTypeInTargets
                    );
                    return [];
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
            ConfigureThresholdTriggerParentFilter();
        }
    }

    public override WorkTypeDef WorkTypeDef => WorkTypeDefOf.PlantCutting;

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
                .Where(des => IsValidDesignatedForestryTarget(des.target))
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
            Distance(plant, Manager.map.GetBaseCenter())
                .ToString("F0", CultureInfo.InvariantCulture),
            plant.YieldNow(),
            plant.def.plant.harvestedThingDef.LabelCap
        );
    }

    [CoroutineSettingsMethod]
    public Coroutine DoClearAreaDesignations(ManagerLog jobLog, Area area, Boxed<bool> workDone)
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            DoClearAreaDesignations
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                DoClearAreaDesignations
            );

        var map = Manager.map;
        var designationManager = map.designationManager;

        var designationsAdded = false;
        foreach (var (cell, i) in area.ActiveCells.Select((c, i) => (c, i)))
        {
            // This is at the start so that it also includes loops that were `continue`d.
            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
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
            jobLog.AddDetail(
                "ColonyManagerRedux.Forestry.Logs.AddClearingDesignation".Translate(
                    DesignationDefOf.CutPlant.ActionText(),
                    "ColonyManagerRedux.Foraging.Logs.Plant".Translate(),
                    plant.Label,
                    area.Label
                ),
                plant
            );
            workDone.Value = true;
            designationsAdded = true;
        }

        if (!designationsAdded)
        {
            jobLog.AddDetail(
                "ColonyManagerRedux.Logs.NoValidTargets".Translate(
                    "ColonyManagerRedux.Foraging.Logs.Plants".Translate(),
                    Def.label
                )
            );
        }
    }

    private string? _tmpLoggingAreaLabel;
    private List<string>? _tmpClearAreasLabels;

    public override void ExposeData()
    {
        // scribe base things
        base.ExposeData();

        // settings, references first!
        Scribe_Collections.Look(ref AllowedTrees, "allowedTrees", LookMode.Def);
        Scribe_Values.Look(ref SyncFilterAndAllowed, "syncFilterAndAllowed", true);
        Scribe_Values.Look(ref _type, "type", ForestryJobType.Logging);
        Scribe_Values.Look(ref AllowSaplings, "allowSaplings");
        Scribe_Values.Look(
            ref _plantsLockedToMap,
            "plantsLockedToMap",
            ColonyManagerReduxMod.Settings.NewJobsShouldBeResourceLocked
        );
        Scribe_Values.Look(ref InvertLoggingArea, "invertLoggingArea");

        // clearing areas list
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            // make sure areas list doesn't contain deleted areas
            UpdateClearAreas();
        }

        if (Manager.ScribeSameMapData)
        {
            Scribe_References.Look(ref LoggingArea, "loggingArea");

            Scribe_Collections.Look(ref ClearAreas, "clearAreas", LookMode.Reference);
            Utilities.Scribe_Designations(ref _designations, Manager);
        }
        else
        {
            Utilities.Scribe_AreaByLabel(
                ref LoggingArea,
                ref _tmpLoggingAreaLabel,
                "loggingArea",
                Manager.map.areaManager
            );
            Utilities.Scribe_AreasByLabel(
                ref ClearAreas,
                ref _tmpClearAreasLabels,
                "clearAreas",
                Manager.map.areaManager
            );
        }

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            ConfigureThresholdTriggerParentFilter();
            TriggerThreshold.SettingsChanged = Notify_ThresholdFilterChanged;
            TriggerThreshold.AllowAnyThresholdChanged = ConfigureThresholdTriggerParentFilter;
        }
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

            count.Value += plant.YieldNow();
        }
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
            _ = TriggerThreshold.ThresholdFilter.Allows(thingDef.plant.harvestedThingDef)
                ? AllowedTrees.Add(thingDef)
                : AllowedTrees.Remove(thingDef);
        }
        Notify_TargetsChanged();
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
                _ = AllowedTrees.Remove(tree);
            }
        }
        Notify_TargetsChanged();
        ConfigureThresholdTriggerParentFilter();
    }

    public void SetTreeAllowed(ThingDef tree, bool allow, bool sync = true)
    {
        _ = allow ? AllowedTrees.Add(tree) : AllowedTrees.Remove(tree);
        Notify_TargetsChanged();

        if (SyncFilterAndAllowed && sync)
        {
            Sync = Utilities.SyncDirection.AllowedToFilter;

            var harvestedThingDef = tree.plant.harvestedThingDef;
            var setAllow = ComputeSetAllow(
                harvestedThingDef,
                AllowedTrees.Select(t => t.plant.harvestedThingDef)
            );
            if (setAllow == null)
            {
                return;
            }

            TriggerThreshold.ThresholdFilter.SetAllow(harvestedThingDef!, setAllow.Value);
        }
    }

    /// <summary>
    /// Decides whether the threshold filter's "allow" flag for <paramref name="harvestedThingDef"/>
    /// should be set, and to what. Returns null when there's nothing to sync (a null
    /// <paramref name="harvestedThingDef"/> means the caller should bail out without touching the
    /// filter — this is the guard added for the null-<c>harvestedThingDef</c> crash fixed by
    /// commit 04a87f5).
    /// </summary>
    internal static bool? ComputeSetAllow<T>(
        T? harvestedThingDef,
        IEnumerable<T?> allowedTreesHarvestedDefs
    )
        where T : class =>
        harvestedThingDef == null
            ? null
            : allowedTreesHarvestedDefs.Any(d => d == harvestedThingDef);

    [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
#pragma warning disable CS0672, CS0618 // overrides obsolete member; not yet migrated to two-phase API
    public override Coroutine TryDoJobCoroutine(ManagerLog jobLog, Boxed<bool> workDone)
    {
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(TryDoJobCoroutine);

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
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        CoroutineHandle? handle = null;
        switch (Type)
        {
            case ForestryJobType.Logging:
                handle = MultiTickCoroutineManager.StartCoroutine(
                    DoLoggingJob(jobLog, workDone),
                    debugHandle: "DoLoggingJob"
                );
                break;
            case ForestryJobType.ClearArea:
                if (ClearAreas.Any())
                {
                    handle = MultiTickCoroutineManager.StartCoroutine(
                        DoClearAreas(jobLog, workDone),
                        debugHandle: "DoClearAreas"
                    );
                }

                break;
            default:
                ColonyManagerReduxMod.Instance.LogError(
                    $"Invalid/unhandled ForestryJobType value: {Type}"
                );
                break;
        }

        if (handle != null)
        {
            yield return handle.ResumeWhenOtherCoroutineIsCompleted();
            yield return new ResumeAfterTicks(ticksBetweenOperations);
        }
    }
#pragma warning restore CS0672, CS0618

    internal void UpdateClearAreas()
    {
        // iterate over existing areas, remove deleted areas.
        var Areas = new List<Area>(ClearAreas);
        foreach (var area in Areas)
        {
            if (!Manager.map.areaManager.AllAreas.Contains(area))
            {
                _ = ClearAreas.Remove(area);
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
            else if (
                !Utilities.IsInAllowedArea(
                    LoggingArea,
                    des.target.Thing.Position,
                    InvertLoggingArea
                )
            )
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

    private Coroutine DoClearAreas(ManagerLog jobLog, Boxed<bool> workDone)
    {
        foreach (var area in ClearAreas)
        {
            yield return DoClearAreaDesignations(jobLog, area, workDone)
                .ResumeWhenOtherCoroutineIsCompleted(debugHandle: nameof(DoClearAreaDesignations));
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine DoLoggingJob(ManagerLog jobLog, Boxed<bool> workDone)
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            DoLoggingJob
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(DoLoggingJob);

        // remove designations not in zone.
        if (LoggingArea != null)
        {
            CleanAreaDesignations(jobLog);
            yield return new ResumeAfterTicks(ticksBetweenOperations);
        }

        // add external designations
        AddRelevantGameDesignations(jobLog);
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        // get current lumber count
        yield return CachedCurrentDesignatedCount
            .DoUpdateIfNeeded(force: true)
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);
        var count = TriggerThreshold.GetCurrentCount() + CachedCurrentDesignatedCount.Value;
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        // designate until we're either out of trees or we have enough designated.
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
            var sortedYields = sortedDesignations
                .Select(d => ((Plant)d.target.Thing).YieldNow())
                .ToList();
            var removeCount = Utilities_Plants.ComputeReduceCount(
                count,
                sortedYields,
                _designations.Count,
                TriggerThreshold.DoesCountMeetTarget,
                ColonyManagerReduxMod.Settings.ShouldRemoveMoreDesignations
            );
            for (var i = 0; i < removeCount; i++)
            {
                var designation = sortedDesignations[i];

                var tree = (Plant)designation.target.Thing;
                var yield = sortedYields[i];
                count -= yield;
                designation.Delete();
                _ = _designations.Remove(designation);
                jobLog.AddDetail(
                    "ColonyManagerRedux.Logs.RemoveDesignation".Translate(
                        DesignationDefOf.HarvestPlant.ActionText(),
                        "ColonyManagerRedux.Forestry.Logs.Tree".Translate(),
                        tree.Label,
                        yield,
                        count,
                        TriggerThreshold.TargetLabel
                    ),
                    tree
                );
                workDone.Value = true;

                if (i > 0 && i % operationsPerTick == 0)
                {
                    yield return new ResumeAfterTicks(ticksBetweenOperations);
                }
            }

            if (!workDone)
            {
                jobLog.AddDetail(
                    "ColonyManagerRedux.Logs.TargetsAlreadySatisfied".Translate(
                        "ColonyManagerRedux.Forestry.Logs.Trees".Translate(),
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
                    "ColonyManagerRedux.Forestry.Logs.Trees".Translate(),
                    Def.label
                )
            );
            yield break;
        }

        List<Plant> sortedTrees = [];
        yield return GetTargetsSorted(
                sortedTrees,
                IsValidUndesignatedForestryTarget,
                (p, d) => p.YieldNow() / d
            )
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        if (sortedTrees.Count == 0)
        {
            jobLog.AddDetail(
                "ColonyManagerRedux.Logs.NoValidTargets".Translate(
                    "ColonyManagerRedux.Forestry.Logs.Trees".Translate(),
                    Def.label
                )
            );
            yield break;
        }

        var sortedTreeYields = sortedTrees.Select(t => t.YieldNow()).ToList();
        var designateCount = Utilities_Plants.ComputeNumberToDesignate(
            count,
            sortedTreeYields,
            _designations.Count,
            TriggerThreshold.DoesCountMeetTarget,
            ColonyManagerReduxMod.Settings.CanAddMoreDesignations
        );
        foreach (var (tree, i) in sortedTrees.Take(designateCount).Select((t, i) => (t, i)))
        {
            var yield = sortedTreeYields[i];
            count += yield;
            AddDesignation(new(tree, DesignationDefOf.HarvestPlant));
            jobLog.AddDetail(
                "ColonyManagerRedux.Logs.AddDesignation".Translate(
                    DesignationDefOf.HarvestPlant.ActionText(),
                    "ColonyManagerRedux.Forestry.Logs.Tree".Translate(),
                    tree.Label,
                    yield,
                    count,
                    TriggerThreshold.TargetLabel
                ),
                tree
            );
            workDone.Value = true;
            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
                ;
            }
        }
    }

    private bool IsValidUndesignatedForestryTarget(Plant target) =>
        target.def.plant != null
        && target.Map == Manager.map
        && AllowedTrees.Contains(target.def)
        && target.Spawned
        && Manager.map.designationManager.DesignationOn(target) == null
        // cut only mature trees, or saplings that yield something right now.
        && ((AllowSaplings && target.YieldNow() > 1) || target.LifeStage == PlantLifeStage.Mature)
        && Utilities.IsInAllowedArea(LoggingArea, target.Position, InvertLoggingArea)
        && IsReachable(target, PathEndMode.Touch);

    private bool IsValidDesignatedForestryTarget(LocalTargetInfo t) =>
        t.HasThing && IsValidDesignatedForestryTarget(t.Thing);

    private bool IsValidDesignatedForestryTarget(Thing t) =>
        t is Plant plant && IsValidDesignatedForestryTarget(plant);

    private bool IsValidDesignatedForestryTarget(Plant target) =>
        target.def.plant != null
        && target.Map == Manager.map
        && AllowedTrees.Contains(target.def)
        && target.Spawned
        && Utilities.IsInAllowedArea(LoggingArea, target.Position, InvertLoggingArea);

    private void ConfigureThresholdTriggerParentFilter()
    {
        if (!TriggerThreshold.AllowAnyThreshold)
        {
            if (Type == ForestryJobType.Logging)
            {
                TriggerThreshold.ParentFilter.SetDisallowAll();
                foreach (var item in AllPlants)
                {
                    TriggerThreshold.ParentFilter.SetAllow(item.plant.harvestedThingDef, true);
                }
            }
        }
    }

    protected override void Notify_AreaRemoved(Area area)
    {
        if (LoggingArea == area)
        {
            LoggingArea = null;
        }
        _ = ClearAreas.Remove(area);
    }
}
