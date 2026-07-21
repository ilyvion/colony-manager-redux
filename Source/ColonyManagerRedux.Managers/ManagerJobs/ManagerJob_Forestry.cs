// ManagerJob_Forestry.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024–2026 Alexander Krivács Schrøder

using System.Diagnostics.CodeAnalysis;
using Verse.AI;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
[CoroutineSettingsType]
internal sealed class ManagerJob_Forestry
    : ManagerJob<ManagerSettings_Forestry, ManagerJob_Forestry.ForestryWorkData>
{
    // What GatherJobDataCoroutine decided needs to happen; ExecuteJobDataCoroutine applies it.
    // Gathering only ever decides on one of these paths per run (mirroring the branches that
    // used to early-return/yield break in the old single-phase TryDoJobCoroutine).
    internal enum ForestryWorkKind
    {
        // Nothing to execute this cycle (e.g. Type == ClearArea with no clear areas
        // configured, or an invalid/unhandled ForestryJobType value).
        None,

        // Type == Logging, trigger is disabled: clean up existing designations and stop.
        CleanUp,

        // Type == ClearArea: designate matching trees within the configured clear areas.
        ClearArea,

        // Type == Logging, trigger already satisfied (or too many designations exist):
        // remove some.
        ReduceDesignations,

        // Type == Logging, trigger not yet satisfied: add new designations for the
        // configured trees.
        AddDesignations,
    }

    /// <summary>
    /// Carries the decisions made by <see cref="GatherJobDataCoroutine"/> (which doesn't touch
    /// the game) to <see cref="ExecuteJobDataCoroutine"/> (which applies them). No field here
    /// should ever be read as a signal that a change has already happened.
    /// </summary>
    internal sealed class ForestryWorkData
    {
        public ForestryWorkKind Kind;

        // Type == Logging, considered regardless of Kind (except CleanUp, where CleanUp()
        // deletes every outstanding designation anyway).
        public List<Designation> AreaDesignationsToRemove = [];

        // Kind == ClearArea
        public List<(Plant Target, Area Area)> ClearAreaDesignationsToAdd = [];

        // Kind == ReduceDesignations
        public List<(Designation Designation, int Yield, int CountAfter)> DesignationsToRemove = [];

        // Kind == AddDesignations
        public List<(Plant Target, int Yield, int CountAfter)> DesignationsToAdd = [];
    }

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

    /// <inheritdoc/>
    public override int ExpectedAdditionalCount
    {
        get
        {
            _ = CachedCurrentDesignatedCount.DoUpdateIfNeeded();
            return CachedCurrentDesignatedCount.Value;
        }
    }

    private bool _plantsLockedToMap = ColonyManagerReduxMod.Settings.NewJobsShouldBeResourceLocked;
    public bool PlantsLockedToMap
    {
        get => _plantsLockedToMap;
        set
        {
            if (_plantsLockedToMap != value)
            {
                _plantsLockedToMap = value;
                AllPlants = null; // reset cached plants
            }
        }
    }

    [AllowNull]
    public List<ThingDef> AllPlants
    {
        get
        {
            field ??=
            [
                .. Utilities_Plants.GetForestryPlants(
                    _plantsLockedToMap ? Manager.map : null,
                    Type == ForestryJobType.ClearArea
                ),
            ];
            return field;
        }
        private set;
    }

    private ForestryJobType _type = ForestryJobType.Logging;

    public Trigger_Threshold TriggerThreshold => (Trigger_Threshold)Trigger!;

    public ManagerJob_Forestry(Manager manager)
        : base(manager)
    {
        CachedCurrentDesignatedCount = new(0, GetCurrentDesignatedCountCoroutine);

        // populate the trigger field, set the root category to wood.
        Trigger = new Trigger_Threshold(this, Trigger_Threshold.AccumulationOnlyOps)
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
            TriggerThreshold.RestrictSupportedOps(Trigger_Threshold.AccumulationOnlyOps);
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
        AllPlants = null;
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

    /// <summary>
    /// Decides whether a designation whose target either has no thing, or whose thing is no
    /// longer inside the configured logging area, should be removed. Pure function, kept
    /// separate so it's unit-testable without a live <see cref="Map"/>.
    /// </summary>
    internal static bool ShouldRemoveForAreaCleanup(bool hasThing, bool inAllowedArea) =>
        !hasThing || !inAllowedArea;

    /// <summary>
    /// Decides which designations need to be removed because their target has vanished or has
    /// left the configured logging area, without deleting anything yet.
    /// </summary>
    private List<Designation> PlanAreaCleanupDesignations()
    {
        List<Designation> toRemove = [];
        if (LoggingArea == null)
        {
            return toRemove;
        }

        foreach (var des in _designations)
        {
            var inAllowedArea =
                des.target.HasThing
                && Utilities.IsInAllowedArea(
                    LoggingArea,
                    des.target.Thing.Position,
                    InvertLoggingArea
                );
            if (ShouldRemoveForAreaCleanup(des.target.HasThing, inAllowedArea))
            {
                toRemove.Add(des);
            }
        }
        return toRemove;
    }

    private void ExecuteAreaCleanupDesignations(ManagerLog jobLog, ForestryWorkData data)
    {
        var missingThingCount = 0;
        var incorrectAreaCount = 0;
        foreach (var des in data.AreaDesignationsToRemove)
        {
            // The execute phase is gated behind ~95% of the job's work timer, so re-check
            // that the condition which flagged this designation during gather still holds
            // before deleting it.
            var hasThing = des.target.HasThing;
            var inAllowedArea =
                hasThing
                && Utilities.IsInAllowedArea(
                    LoggingArea,
                    des.target.Thing.Position,
                    InvertLoggingArea
                );
            if (!ShouldRemoveForAreaCleanup(hasThing, inAllowedArea))
            {
                continue;
            }

            if (!hasThing)
            {
                missingThingCount++;
            }
            else
            {
                incorrectAreaCount++;
            }
            des.Delete();
            _ = _designations.Remove(des);
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

    [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
    protected override Coroutine GatherJobDataCoroutine(
        ManagerLog jobLog,
        AnyBoxed<ForestryWorkData?> data
    )
    {
        if (Type == ForestryJobType.Logging && !TriggerThreshold.State)
        {
            if (JobState != ManagerJobState.Completed)
            {
                JobState = ManagerJobState.Completed;
                jobLog.AddDetail("ColonyManagerRedux.Logs.JobCompleted".Translate());

                data.Value = new ForestryWorkData { Kind = ForestryWorkKind.CleanUp };
            }
            yield break;
        }
        else
        {
            JobState = ManagerJobState.Active;
        }

        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                (Func<ManagerLog, AnyBoxed<ForestryWorkData?>, Coroutine>)GatherJobDataCoroutine
            );

        // Resync our own bookkeeping against designations that have disappeared or that
        // already exist in the game unbeknownst to us; this doesn't change anything in the
        // game itself, so it's safe to do while gathering.
        CleanDeadDesignations(_designations, DesignationDefOf.HarvestPlant, jobLog);
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        var workData = new ForestryWorkData();

        switch (Type)
        {
            case ForestryJobType.Logging:
                yield return PlanLoggingJob(jobLog, workData).ResumeWhenOtherCoroutineIsCompleted();
                break;
            case ForestryJobType.ClearArea:
                if (ClearAreas.Any())
                {
                    workData.Kind = ForestryWorkKind.ClearArea;
                    yield return PlanClearAreas(jobLog, workData)
                        .ResumeWhenOtherCoroutineIsCompleted();
                }
                break;
            default:
                ColonyManagerReduxMod.Instance.LogError(
                    $"Invalid/unhandled ForestryJobType value: {Type}"
                );
                break;
        }

        data.Value = workData;
    }

    [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
    private Coroutine PlanLoggingJob(ManagerLog jobLog, ForestryWorkData data)
    {
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(PlanLoggingJob);

        // plan removing designations not in zone.
        data.AreaDesignationsToRemove.AddRange(PlanAreaCleanupDesignations());
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        // add external designations
        AddRelevantGameDesignations(jobLog);
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        // get current lumber count
        yield return CachedCurrentDesignatedCount
            .DoUpdateIfNeeded(force: true)
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);
        var count = new Boxed<int>(
            TriggerThreshold.GetCurrentCount() + CachedCurrentDesignatedCount.Value
        );
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        // designate until we're either out of trees or we have enough designated.
        var directive = TriggerThreshold.GetDirective(count.Value);
        if (
            directive != Trigger_Threshold.Directive.Increase
            || ColonyManagerReduxMod.Settings.ShouldRemoveMoreDesignations(_designations.Count)
        )
        {
            data.Kind = ForestryWorkKind.ReduceDesignations;

            yield return PlanReduceDesignations(count, data).ResumeWhenOtherCoroutineIsCompleted();

            yield break;
        }

        data.Kind = ForestryWorkKind.AddDesignations;

        jobLog.AddDetail(
            "ColonyManagerRedux.Logs.CurrentCount".Translate(
                count.Value,
                TriggerThreshold.TargetCount
            )
        );

        yield return PlanAddDesignations(jobLog, count, data).ResumeWhenOtherCoroutineIsCompleted();
    }

    [CoroutineSettingsMethod]
    private Coroutine PlanReduceDesignations(Boxed<int> count, ForestryWorkData data)
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            PlanReduceDesignations
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                PlanReduceDesignations
            );

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

        // plan reducing designations until we're just above target
        var sortedYields = sortedDesignations
            .Select(d => ((Plant)d.target.Thing).YieldNow())
            .ToList();
        var removeCount = Utilities_Plants.ComputeReduceCount(
            count.Value,
            sortedYields,
            _designations.Count,
            TriggerThreshold.DoesCountMeetTarget,
            ColonyManagerReduxMod.Settings.ShouldRemoveMoreDesignations
        );
        for (var i = 0; i < removeCount; i++)
        {
            var designation = sortedDesignations[i];
            var yield = sortedYields[i];
            count.Value -= yield;
            data.DesignationsToRemove.Add((designation, yield, count.Value));

            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine PlanAddDesignations(
        ManagerLog jobLog,
        Boxed<int> count,
        ForestryWorkData data
    )
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            PlanAddDesignations
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                PlanAddDesignations
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
            count.Value,
            sortedTreeYields,
            _designations.Count,
            TriggerThreshold.DoesCountMeetTarget,
            ColonyManagerReduxMod.Settings.CanAddMoreDesignations
        );
        for (var i = 0; i < designateCount; i++)
        {
            var tree = sortedTrees[i];
            var yield = sortedTreeYields[i];
            count.Value += yield;
            data.DesignationsToAdd.Add((tree, yield, count.Value));

            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    private Coroutine PlanClearAreas(ManagerLog jobLog, ForestryWorkData data)
    {
        foreach (var area in ClearAreas)
        {
            yield return PlanClearAreaDesignations(jobLog, area, data)
                .ResumeWhenOtherCoroutineIsCompleted(
                    debugHandle: nameof(PlanClearAreaDesignations)
                );
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine PlanClearAreaDesignations(ManagerLog jobLog, Area area, ForestryWorkData data)
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            PlanClearAreaDesignations
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                PlanClearAreaDesignations
            );

        var map = Manager.map;
        var designationManager = map.designationManager;

        var designationsPlanned = false;
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

            // there's no reason not to cut it down, so plan to cut it down.
            data.ClearAreaDesignationsToAdd.Add((plant, area));
            designationsPlanned = true;
        }

        if (!designationsPlanned)
        {
            jobLog.AddDetail(
                "ColonyManagerRedux.Logs.NoValidTargets".Translate(
                    "ColonyManagerRedux.Foraging.Logs.Plants".Translate(),
                    Def.label
                )
            );
        }
    }

    [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
    protected override Coroutine ExecuteJobDataCoroutine(
        ManagerLog jobLog,
        ForestryWorkData data,
        Boxed<bool> workDone
    )
    {
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                (Func<ManagerLog, ForestryWorkData, Boxed<bool>, Coroutine>)ExecuteJobDataCoroutine
            );

        if (data.Kind == ForestryWorkKind.None)
        {
            yield break;
        }

        if (data.Kind == ForestryWorkKind.CleanUp)
        {
            // Matches the old behavior of not counting a completion cleanup as "work done"
            // for this cycle.
            CleanUp(jobLog);
            yield break;
        }

        if (data.Kind == ForestryWorkKind.ClearArea)
        {
            yield return ExecuteClearAreaDesignations(jobLog, data, workDone)
                .ResumeWhenOtherCoroutineIsCompleted();
            yield break;
        }

        ExecuteAreaCleanupDesignations(jobLog, data);
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        yield return data.Kind == ForestryWorkKind.ReduceDesignations
            ? ExecuteReduceDesignations(jobLog, data, workDone)
                .ResumeWhenOtherCoroutineIsCompleted()
            : ExecuteAddDesignations(jobLog, data, workDone).ResumeWhenOtherCoroutineIsCompleted();
    }

    [CoroutineSettingsMethod]
    private Coroutine ExecuteReduceDesignations(
        ManagerLog jobLog,
        ForestryWorkData data,
        Boxed<bool> workDone
    )
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            ExecuteReduceDesignations
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                ExecuteReduceDesignations
            );

        var i = 0;
        foreach (var (designation, yieldAmount, countAfter) in data.DesignationsToRemove)
        {
            var tree = (Plant)designation.target.Thing;
            designation.Delete();
            _ = _designations.Remove(designation);

            // The execute phase is gated behind ~95% of the job's work timer, so the tree
            // planned for removal during gather may already be gone by the time we get here;
            // the designation is still cleaned up above, but there's nothing to report.
            if (!tree.DestroyedOrNull())
            {
                jobLog.AddDetail(
                    "ColonyManagerRedux.Logs.RemoveDesignation".Translate(
                        DesignationDefOf.HarvestPlant.ActionText(),
                        "ColonyManagerRedux.Forestry.Logs.Tree".Translate(),
                        tree.Label,
                        yieldAmount,
                        countAfter,
                        TriggerThreshold.TargetLabel
                    ),
                    tree
                );
                workDone.Value = true;
            }

            i++;
            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }

        if (!workDone.Value)
        {
            jobLog.AddDetail(
                "ColonyManagerRedux.Logs.TargetsAlreadySatisfied".Translate(
                    "ColonyManagerRedux.Forestry.Logs.Trees".Translate(),
                    Def.label
                )
            );
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine ExecuteAddDesignations(
        ManagerLog jobLog,
        ForestryWorkData data,
        Boxed<bool> workDone
    )
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            ExecuteAddDesignations
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                ExecuteAddDesignations
            );

        var i = 0;
        foreach (var (tree, yieldAmount, countAfter) in data.DesignationsToAdd)
        {
            // The execute phase is gated behind ~95% of the job's work timer, so the target
            // planned during gather may have been destroyed, despawned, or hauled away by the
            // time we get here; re-validate before designating it.
            if (tree.DestroyedOrNull())
            {
                continue;
            }

            AddDesignation(new(tree, DesignationDefOf.HarvestPlant));
            workDone.Value = true;

            jobLog.AddDetail(
                "ColonyManagerRedux.Logs.AddDesignation".Translate(
                    DesignationDefOf.HarvestPlant.ActionText(),
                    "ColonyManagerRedux.Forestry.Logs.Tree".Translate(),
                    tree.Label,
                    yieldAmount,
                    countAfter,
                    TriggerThreshold.TargetLabel
                ),
                tree
            );

            i++;
            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine ExecuteClearAreaDesignations(
        ManagerLog jobLog,
        ForestryWorkData data,
        Boxed<bool> workDone
    )
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            ExecuteClearAreaDesignations
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                ExecuteClearAreaDesignations
            );

        var designationManager = Manager.map.designationManager;

        var i = 0;
        foreach (var (plant, area) in data.ClearAreaDesignationsToAdd)
        {
            // The execute phase is gated behind ~95% of the job's work timer, so the plant
            // planned for designation during gather may have been destroyed, harvested, or
            // already designated by another actor by the time we get here; re-validate.
            if (plant.DestroyedOrNull() || designationManager.AllDesignationsOn(plant).Any())
            {
                continue;
            }

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

            i++;
            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
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
