// ManagerJob_Foraging.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Diagnostics.CodeAnalysis;
using Verse.AI;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
[CoroutineSettingsType]
internal sealed class ManagerJob_Foraging
    : ManagerJob<ManagerSettings_Foraging, ManagerJob_Foraging.ForagingWorkData>
{
    // What GatherJobDataCoroutine decided needs to happen; ExecuteJobDataCoroutine applies it.
    // Gathering only ever decides on one of these paths per run (mirroring the branches that
    // used to early-return/yield break in the old single-phase TryDoJobCoroutine).
    internal enum ForagingWorkKind
    {
        // Trigger is disabled: clean up all existing designations and stop foraging.
        CleanUp,

        // Trigger is already satisfied (or too many designations exist): remove some.
        ReduceDesignations,

        // Trigger is not yet satisfied: add new designations for the allowed plants.
        AddDesignations,
    }

    /// <summary>
    /// Carries the decisions made by <see cref="GatherJobDataCoroutine"/> (which doesn't touch
    /// the game) to <see cref="ExecuteJobDataCoroutine"/> (which applies them). No field here
    /// should ever be read as a signal that a change has already happened.
    /// </summary>
    internal sealed class ForagingWorkData
    {
        public ForagingWorkKind Kind;

        // Always considered, regardless of Kind (except CleanUp, where CleanUp() deletes
        // every outstanding designation anyway).
        public List<Designation> AreaDesignationsToRemove = [];

        // Kind == ReduceDesignations
        public List<(Designation Designation, int Yield, int CountAfter)> DesignationsToRemove = [];

        // Kind == AddDesignations
        public List<(Plant Target, int Yield, int CountAfter)> DesignationsToAdd = [];
    }

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
    public bool InvertForagingArea;
    public bool ForceFullyMature;
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
                .. Utilities_Plants.GetForagingPlants(_plantsLockedToMap ? Manager.map : null),
            ];
            return field;
        }
        private set;
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
        Trigger = new Trigger_Threshold(this, Trigger_Threshold.AccumulationOnlyOps);
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
        Scribe_Values.Look(ref InvertForagingArea, "invertForagingArea");

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
            TriggerThreshold.RestrictSupportedOps(Trigger_Threshold.AccumulationOnlyOps);
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
        Notify_TargetsChanged();
    }

    public void RefreshAllPlants()
    {
        ColonyManagerReduxMod.Instance.LogDebug("Refreshing all plants");

        // all plants that yield something, and it isn't wood.
        AllPlants = null;
        var options = AllPlants;

        // remove stuff not in new list
        foreach (var plant in AllowedPlants.ToList())
        {
            if (!options.Contains(plant))
            {
                _ = AllowedPlants.Remove(plant);
            }
        }
        Notify_TargetsChanged();
        ConfigureThresholdTriggerParentFilter();
    }

    public void SetPlantAllowed(ThingDef plant, bool allow, bool sync = true)
    {
        _ = allow ? AllowedPlants.Add(plant) : AllowedPlants.Remove(plant);
        Notify_TargetsChanged();

        if (SyncFilterAndAllowed && sync)
        {
            Sync = Utilities.SyncDirection.AllowedToFilter;

            if (!plant.TrySpecialAllowedSync(AllowedPlants, TriggerThreshold.ThresholdFilter))
            {
                var harvestedThingDef = plant.plant.harvestedThingDef;
                var setAllow = Utilities_ResourceSync.ShouldResourceStayAllowed(
                    AllowedPlants,
                    p => p.plant.harvestedThingDef,
                    harvestedThingDef
                );
                TriggerThreshold.ThresholdFilter.SetAllow(harvestedThingDef, setAllow);
            }
        }
    }

    [CoroutineSettingsMethod]
    protected override Coroutine GatherJobDataCoroutine(
        ManagerLog jobLog,
        AnyBoxed<ForagingWorkData?> data
    )
    {
        if (!TriggerThreshold.State)
        {
            if (JobState != ManagerJobState.Completed)
            {
                JobState = ManagerJobState.Completed;
                jobLog.AddDetail("ColonyManagerRedux.Logs.JobCompleted".Translate());

                data.Value = new ForagingWorkData { Kind = ForagingWorkKind.CleanUp };
            }
            yield break;
        }
        else
        {
            JobState = ManagerJobState.Active;
        }

        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            (Func<ManagerLog, AnyBoxed<ForagingWorkData?>, Coroutine>)GatherJobDataCoroutine
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                (Func<ManagerLog, AnyBoxed<ForagingWorkData?>, Coroutine>)GatherJobDataCoroutine
            );

        // Resync our own bookkeeping against designations that have disappeared or that
        // already exist in the game unbeknownst to us; this doesn't change anything in the
        // game itself, so it's safe to do while gathering.
        CleanDeadDesignations(_designations, DesignationDefOf.HarvestPlant, jobLog);
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        AddRelevantGameDesignations(jobLog);
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        var workData = new ForagingWorkData();
        workData.AreaDesignationsToRemove.AddRange(PlanAreaCleanupDesignations());
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        // designate plants until trigger is met.
        yield return CachedCurrentDesignatedCount
            .DoUpdateIfNeeded(force: true)
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);
        var count = new Boxed<int>(
            TriggerThreshold.GetCurrentCount() + CachedCurrentDesignatedCount.Value
        );

        var directive = TriggerThreshold.GetDirective(count.Value);
        if (
            directive != Trigger_Threshold.Directive.Increase
            || ColonyManagerReduxMod.Settings.ShouldRemoveMoreDesignations(_designations.Count)
        )
        {
            workData.Kind = ForagingWorkKind.ReduceDesignations;

            yield return PlanReduceDesignations(
                    operationsPerTick,
                    ticksBetweenOperations,
                    count,
                    workData
                )
                .ResumeWhenOtherCoroutineIsCompleted();

            data.Value = workData;
            yield break;
        }

        workData.Kind = ForagingWorkKind.AddDesignations;

        jobLog.AddDetail(
            "ColonyManagerRedux.Logs.CurrentCount".Translate(
                count.Value,
                TriggerThreshold.TargetCount
            )
        );

        yield return PlanAddDesignations(
                jobLog,
                operationsPerTick,
                ticksBetweenOperations,
                count,
                workData
            )
            .ResumeWhenOtherCoroutineIsCompleted();

        data.Value = workData;
    }

    /// <summary>
    /// Decides whether a designation whose target either has no thing, or whose thing is no
    /// longer inside the configured foraging area, should be removed. Pure function, kept
    /// separate so it's unit-testable without a live <see cref="Map"/>.
    /// </summary>
    internal static bool ShouldRemoveForAreaCleanup(bool hasThing, bool inAllowedArea) =>
        !hasThing || !inAllowedArea;

    /// <summary>
    /// Decides which designations need to be removed because their target has vanished or has
    /// left the configured foraging area, without deleting anything yet.
    /// </summary>
    private List<Designation> PlanAreaCleanupDesignations()
    {
        List<Designation> toRemove = [];
        foreach (var des in _designations)
        {
            var inAllowedArea =
                des.target.HasThing
                && Utilities.IsInAllowedArea(
                    ForagingArea,
                    des.target.Thing.Position,
                    InvertForagingArea
                );
            if (ShouldRemoveForAreaCleanup(des.target.HasThing, inAllowedArea))
            {
                toRemove.Add(des);
            }
        }
        return toRemove;
    }

    private Coroutine PlanReduceDesignations(
        int operationsPerTick,
        int ticksBetweenOperations,
        Boxed<int> count,
        ForagingWorkData data
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

    private Coroutine PlanAddDesignations(
        ManagerLog jobLog,
        int operationsPerTick,
        int ticksBetweenOperations,
        Boxed<int> count,
        ForagingWorkData data
    )
    {
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

        var sortedPlantYields = sortedPlants.Select(p => p.YieldNow()).ToList();
        var designateCount = Utilities_Plants.ComputeNumberToDesignate(
            count.Value,
            sortedPlantYields,
            _designations.Count,
            TriggerThreshold.DoesCountMeetTarget,
            ColonyManagerReduxMod.Settings.CanAddMoreDesignations
        );
        for (var i = 0; i < designateCount; i++)
        {
            var plant = sortedPlants[i];
            var yield = sortedPlantYields[i];
            count.Value += yield;
            data.DesignationsToAdd.Add((plant, yield, count.Value));

            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    [CoroutineSettingsMethod]
    protected override Coroutine ExecuteJobDataCoroutine(
        ManagerLog jobLog,
        ForagingWorkData data,
        Boxed<bool> workDone
    )
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            (Func<ManagerLog, ForagingWorkData, Boxed<bool>, Coroutine>)ExecuteJobDataCoroutine
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                (Func<ManagerLog, ForagingWorkData, Boxed<bool>, Coroutine>)ExecuteJobDataCoroutine
            );

        if (data.Kind == ForagingWorkKind.CleanUp)
        {
            // Matches the old behavior of not counting a completion cleanup as "work done"
            // for this cycle.
            CleanUp(jobLog);
            yield break;
        }

        ExecuteAreaCleanupDesignations(jobLog, data);
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        yield return data.Kind == ForagingWorkKind.ReduceDesignations
            ? ExecuteReduceDesignations(
                    jobLog,
                    data,
                    operationsPerTick,
                    ticksBetweenOperations,
                    workDone
                )
                .ResumeWhenOtherCoroutineIsCompleted()
            : ExecuteAddDesignations(
                    jobLog,
                    data,
                    operationsPerTick,
                    ticksBetweenOperations,
                    workDone
                )
                .ResumeWhenOtherCoroutineIsCompleted();
    }

    private void ExecuteAreaCleanupDesignations(ManagerLog jobLog, ForagingWorkData data)
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
                    ForagingArea,
                    des.target.Thing.Position,
                    InvertForagingArea
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

    private Coroutine ExecuteReduceDesignations(
        ManagerLog jobLog,
        ForagingWorkData data,
        int operationsPerTick,
        int ticksBetweenOperations,
        Boxed<bool> workDone
    )
    {
        var i = 0;
        foreach (var (designation, yieldAmount, countAfter) in data.DesignationsToRemove)
        {
            var plant = (Plant)designation.target.Thing;
            designation.Delete();
            _ = _designations.Remove(designation);

            // The target planned for removal during gather may already be gone by the time
            // the (now much later) execute phase actually runs; the designation is still
            // cleaned up above, but there's nothing to report in that case.
            if (!plant.DestroyedOrNull())
            {
                jobLog.AddDetail(
                    "ColonyManagerRedux.Logs.RemoveDesignation".Translate(
                        DesignationDefOf.HarvestPlant.ActionText(),
                        "ColonyManagerRedux.Foraging.Logs.Plant".Translate(),
                        plant.Label,
                        yieldAmount,
                        countAfter,
                        TriggerThreshold.TargetLabel
                    ),
                    plant
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
                    "ColonyManagerRedux.Foraging.Logs.Plants".Translate(),
                    Def.label
                )
            );
        }
    }

    private Coroutine ExecuteAddDesignations(
        ManagerLog jobLog,
        ForagingWorkData data,
        int operationsPerTick,
        int ticksBetweenOperations,
        Boxed<bool> workDone
    )
    {
        var i = 0;
        foreach (var (plant, yieldAmount, countAfter) in data.DesignationsToAdd)
        {
            // The target planned for designation during gather may have been destroyed,
            // despawned, or hauled away by the time the (now much later) execute phase
            // actually runs; re-validate before designating it.
            if (plant.DestroyedOrNull())
            {
                continue;
            }

            AddDesignation(new(plant, DesignationDefOf.HarvestPlant));
            workDone.Value = true;

            jobLog.AddDetail(
                "ColonyManagerRedux.Logs.AddDesignation".Translate(
                    DesignationDefOf.HarvestPlant.ActionText(),
                    "ColonyManagerRedux.Foraging.Logs.Plant".Translate(),
                    plant.Label,
                    yieldAmount,
                    countAfter,
                    TriggerThreshold.TargetLabel
                ),
                plant
            );

            i++;
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
        && Utilities.IsInAllowedArea(ForagingArea, target.Position, InvertForagingArea)
        && IsReachable(target, PathEndMode.Touch);

    private bool IsValidDesignatedForagingTarget(LocalTargetInfo t) =>
        t.HasThing && IsValidDesignatedForagingTarget(t.Thing);

    private bool IsValidDesignatedForagingTarget(Thing t) =>
        t is Plant plant && IsValidDesignatedForagingTarget(plant);

    private bool IsValidDesignatedForagingTarget(Plant target) =>
        target.def.plant != null
        && target.Map == Manager.map
        && AllowedPlants.Contains(target.def)
        && target.Spawned
        && Utilities.IsInAllowedArea(ForagingArea, target.Position, InvertForagingArea);

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
