// ManagerJob.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024–2025 Alexander Krivács Schrøder

using System.Buffers;
using System.Text;
using ilyvion.Laboratory.Extensions;
using Verse.AI;

namespace ColonyManagerRedux;

/// <summary>
/// Represents a manager job with specific settings of type <typeparamref name="TSettings"/>.
/// </summary>
/// <typeparam name="TSettings">The type of settings associated with this manager job.</typeparam>
[HotSwappable]
public abstract class ManagerJob<TSettings>(Manager manager) : ManagerJob(manager)
    where TSettings : ManagerSettings
{
    /// <summary>
    /// Gets the manager settings for this job.
    /// </summary>
    public TSettings ManagerSettings =>
        ColonyManagerReduxMod.Settings.ManagerSettingsFor<TSettings>(Def)
        ?? throw new InvalidOperationException(
            $"Type {GetType().Name} claims to have a "
                + $"manager settings type of {typeof(TSettings).Name}, but no such type has been "
                + "registered. Did you remember to add your settings type to your ManagerDef with a "
                + "managerSettingsClass value?"
        );
}

/// <summary>
/// Represents a manager job with specific settings of type <typeparamref name="TSettings"/>,
/// whose asynchronous work is split into a gather phase (which decides what needs to be
/// done, without changing anything in the game) and an execute phase (which applies those
/// decisions), communicating between the two via a <typeparamref name="TWorkData"/> value.
/// </summary>
/// <typeparam name="TSettings">The type of settings associated with this manager job.</typeparam>
/// <typeparam name="TWorkData">The type used to carry the gather phase's decisions to the execute phase.</typeparam>
[HotSwappable]
public abstract class ManagerJob<TSettings, TWorkData>(Manager manager)
    : ManagerJob<TSettings>(manager)
    where TSettings : ManagerSettings
{
    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the concrete job type derives from this class without overriding
    /// <see cref="GatherJobDataCoroutine(ManagerLog, AnyBoxed{TWorkData})"/> — every job built on
    /// this class must implement the gather phase.
    /// </exception>
    public sealed override Coroutine GatherJobDataCoroutine(
        ManagerLog jobLog,
        AnyBoxed<object?> data
    )
    {
        if (data == null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        AnyBoxed<TWorkData?> typedData = new(default);
        yield return GatherJobDataCoroutine(jobLog, typedData)
            .ResumeWhenOtherCoroutineIsCompleted();
        data.Value = typedData.Value;
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">
    /// Thrown if called without gathered data of type <typeparamref name="TWorkData"/>; this
    /// would indicate a bug in the gather/execute sequencing that calls this method, not a
    /// legitimate "no work" state.
    /// </exception>
    public sealed override Coroutine ExecuteJobDataCoroutine(
        ManagerLog jobLog,
        object? data,
        Boxed<bool> workDone
    ) =>
        data is TWorkData typedData
            ? ExecuteJobDataCoroutine(jobLog, typedData, workDone)
            : throw new InvalidOperationException(
                $"{GetType().Name}.{nameof(ExecuteJobDataCoroutine)} was invoked without "
                    + $"gathered data of type {typeof(TWorkData).Name}. This indicates a bug in "
                    + "the caller's gather/execute sequencing."
            );

    /// <summary>
    /// Gathers the information needed to perform the job, without making any changes to
    /// the game world (e.g. finding and sorting candidate targets, but not designating
    /// them). The resulting data is passed to
    /// <see cref="ExecuteJobDataCoroutine(ManagerLog, TWorkData, Boxed{bool})"/>.
    /// </summary>
    /// <param name="jobLog">The log to record job actions and results.</param>
    /// <param name="data">A boxed value to store the gathered data in.</param>
    /// <returns>A coroutine representing the asynchronous gather operation.</returns>
    protected abstract Coroutine GatherJobDataCoroutine(
        ManagerLog jobLog,
        AnyBoxed<TWorkData?> data
    );

    /// <summary>
    /// Executes the job using the data gathered by
    /// <see cref="GatherJobDataCoroutine(ManagerLog, AnyBoxed{TWorkData})"/>.
    /// </summary>
    /// <param name="jobLog">The log to record job actions and results.</param>
    /// <param name="data">The data gathered by the gather phase.</param>
    /// <param name="workDone">A boxed boolean indicating whether work was done.</param>
    /// <returns>A coroutine representing the asynchronous execute operation.</returns>
    protected abstract Coroutine ExecuteJobDataCoroutine(
        ManagerLog jobLog,
        TWorkData data,
        Boxed<bool> workDone
    );
}

/// <summary>
/// Represents a base class for all manager jobs, providing core functionality for job management,
/// serialization, and interaction with the manager system.
/// </summary>
[HotSwappable]
[CoroutineSettingsType]
public abstract class ManagerJob : ILoadReferenceable, IExposable
{
    internal ManagerDef _def;

    /// <summary>
    /// Gets the <see cref="ManagerDef"/> associated with this manager job.
    /// </summary>
    public ManagerDef Def => _def;

    private List<ManagerJobComp> _comps = [];

    private bool _shouldCheckReachable;

    /// <summary>
    /// Gets or sets a value indicating whether the job should check if targets are reachable.
    /// </summary>
    public ref bool ShouldCheckReachable => ref _shouldCheckReachable;

    private int _jobCreatedTick = Find.TickManager.TicksGame;
    private int _lastActionTick = -1;

    /// <summary>
    /// Gets the number of ticks since the job was last updated.
    /// </summary>
    public int TicksSinceLastUpdate =>
        _lastActionTick < 0
            ? Find.TickManager.TicksGame - _jobCreatedTick
            : Find.TickManager.TicksGame - _lastActionTick;

    /// <summary>
    /// Gets the number of ticks since the job should have last been updated.
    /// </summary>
    public int TicksSinceShouldUpdate => TicksSinceLastUpdate - UpdateInterval.Ticks;

    /// <summary>
    /// Gets a value indicating whether the job has been updated at least once.
    /// </summary>
    public bool HasBeenUpdated => _lastActionTick != -1;

    internal Manager _manager;

    /// <summary>
    /// Gets the <see cref="ColonyManagerRedux.Manager"/> instance associated with this job.
    /// </summary>
    public Manager Manager => _manager;

    private bool _usePathBasedDistance;

    /// <summary>
    /// Gets or sets a value indicating whether to use path-based distance calculations for this job.
    /// </summary>
    public ref bool UsePathBasedDistance => ref _usePathBasedDistance;

    internal int Priority;

    private bool _isSuspended;

    private UpdateInterval? _updateInterval;
    private int _updateIntervalScribe;

    private int _loadID = -1;
    internal int LoadID => _loadID;
    private bool isManaged;

    private Trigger? _trigger;

    /// <summary>
    /// Gets the trigger associated with this manager job.
    /// </summary>
    public Trigger? Trigger
    {
        get => _trigger;
        protected set => _trigger = value;
    }

    /// <summary>
    /// Gets a value indicating whether this manager job can be imported and exported.
    /// </summary>
    public virtual bool IsTransferable => true;

    /// <summary>
    /// Gets the amount expected to be added on top of the trigger's current count once this
    /// job's pending designations are completed (e.g. wood on trees already marked for
    /// felling), for use as a "pending" overlay on top of the current-stock progress bar.
    /// </summary>
    public virtual int ExpectedAdditionalCount => 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManagerJob"/> class with the specified manager.
    /// </summary>
    /// <param name="manager">The manager instance associated with this job.</param>
#pragma warning disable CS8618 // Set by ManagerDefMaker.MakeManagerJob
    protected ManagerJob(Manager manager)
#pragma warning restore CS8618
    {
        var settings = ColonyManagerReduxMod.Settings;
        ShouldCheckReachable = settings.DefaultShouldCheckReachable;
        UsePathBasedDistance = settings.DefaultUsePathBasedDistance;
        if (!settings.NewJobsAreImmediatelyOutdated)
        {
            // set last updated to current time
            Touch();
        }

        _manager = manager;
    }

    private ManagerJobState _jobState;

    /// <summary>
    /// Gets the current job state of the manager job.
    /// </summary>
    public ManagerJobState JobState
    {
        get => _jobState;
        protected set => _jobState = value;
    }

    /// <summary>
    /// Gets a value indicating whether the manager job has been completed.
    /// </summary>
    public bool IsCompleted => JobState == ManagerJobState.Completed;

    /// <summary>
    /// Gets the tooltip text displayed when the manager job has been completed.
    /// </summary>
    public virtual string IsCompletedTooltip =>
        "ColonyManagerRedux.Job.JobHasbeenCompletedTooltip".Translate();

    /// <summary>
    /// Gets a value indicating whether this manager job is valid.
    /// </summary>
    public virtual bool IsValid => Manager != null;

    /// <summary>
    /// Gets the display label for this manager job.
    /// </summary>
    public virtual string Label => _def.label.CapitalizeFirst();

    /// <summary>
    /// Gets or sets a value indicating whether this manager job is currently managed.
    /// </summary>
    public virtual bool IsManaged
    {
        get => isManaged;
        set
        {
            isManaged = value;
            if (isManaged && _loadID == -1)
            {
                _loadID = Manager.GetNextManagerJobID();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the job should be performed now, based on whether it is managed and should be updated.
    /// </summary>
    public bool ShouldDoNow => IsManaged && ShouldUpdate;

    private bool ShouldUpdate =>
        _lastActionTick < 0
        || ((_lastActionTick + UpdateInterval.Ticks) < Find.TickManager.TicksGame);

    /// <summary>
    /// Gets or sets a value indicating whether this manager job is currently suspended.
    /// </summary>
    public bool IsSuspended
    {
        get => _isSuspended;
        set
        {
            _isSuspended = value;
            CausedException = null;
        }
    }

    /// <summary>
    /// Gets the tooltip text displayed when the manager job has been suspended.
    /// </summary>
    public virtual string IsSuspendedTooltip =>
        "ColonyManagerRedux.Job.JobHasBeenSuspendedTooltip".Translate();

    /// <summary>
    /// Gets the tooltip text displayed when the manager job has been suspended due to an exception.
    /// </summary>
    public virtual string IsSuspendedDueToExceptionTooltip =>
        "ColonyManagerRedux.Job.JobHasBeenSuspendedDueToExceptionTooltip".Translate();

    /// <summary>
    /// Gets the <see cref="ManagerTab"/> associated with this manager job.
    /// </summary>
    public ManagerTab Tab => Manager.Tabs.First(tab => tab.GetType() == _def.managerTabClass);

    /// <summary>
    /// Gets an enumerable collection of target identifiers associated with this manager job.
    /// </summary>
    public abstract IEnumerable<string> Targets { get; }

    private CachedValue<string>? _cachedTargetsLabel;

    /// <summary>
    /// Gets a comma-separated label of <see cref="Targets"/>, or a "none" placeholder if empty.
    /// </summary>
    /// <remarks>
    /// Building this can be expensive for jobs with large allow-lists (it resolves a label per
    /// target), so it's cached until <see cref="Notify_TargetsChanged"/> is called.
    /// </remarks>
    public string TargetsLabel =>
        (
            _cachedTargetsLabel ??= new CachedValue<string>(() =>
                Targets.Any()
                    ? string.Join(", ", Targets)
                    : (string)"ColonyManagerRedux.Common.None".Translate()
            )
        ).TryGetValue(out var label)
            ? label
            : "";

    /// <summary>
    /// Invalidates the cached <see cref="TargetsLabel"/>. Call this whenever the set of allowed
    /// targets for this job changes.
    /// </summary>
    public void Notify_TargetsChanged() => _cachedTargetsLabel?.Invalidate();

    /// <summary>
    /// Gets or sets the update interval for this manager job.
    /// </summary>
    public virtual UpdateInterval UpdateInterval
    {
        get => _updateInterval ?? ColonyManagerReduxMod.Settings.DefaultUpdateInterval;
        set => _updateInterval = value;
    }

    /// <summary>
    /// Gets the <see cref="WorkTypeDef"/> associated with this manager job, or null if none.
    /// </summary>
    public abstract WorkTypeDef? WorkTypeDef { get; }

    /// <summary>
    /// Gets the maximum upper threshold value for this manager job.
    /// </summary>
    public virtual int MaxUpperThreshold { get; } = Constants.DefaultMaxUpperThreshold;

    /// <summary>
    /// Gets or sets the exception that caused this manager job to be suspended, if any.
    /// </summary>
    public Exception? CausedException
    {
        get;
        set
        {
            field = value;
            CausedExceptionText = null;
        }
    }

    /// <summary>
    /// Gets the cached string representation of the exception that caused this manager job to be suspended, if any.
    /// </summary>
    public string? CausedExceptionText
    {
        get
        {
            if (field == null && CausedException != null)
            {
                ref var noStacktraceCaching = ref AccessTools.StaticFieldRefAccess<bool>(
                    "HarmonyMod.HarmonyMain:noStacktraceCaching"
                );

                var originalValue = noStacktraceCaching;
                noStacktraceCaching = true;
                field = CausedException.ToString();
                noStacktraceCaching = originalValue;
            }
            return field;
        }
        private set;
    }

    internal void Initialize()
    {
        _comps = [];
        foreach (var compProperties in _def.jobComps)
        {
            ManagerJobComp? managerJobComp = null;
            try
            {
                managerJobComp = (ManagerJobComp)Activator.CreateInstance(compProperties.compClass);
                managerJobComp.Parent = this;
                _comps.Add(managerJobComp);
                managerJobComp.InitializeInt(compProperties);
            }
            catch (Exception ex)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    "Could not instantiate or initialize a ManagerJobComp: " + ex
                );
                if (managerJobComp != null)
                {
                    _ = _comps.Remove(managerJobComp);
                }
            }
        }
    }

    /// <summary>
    /// Called after the manager job has been created to perform any additional initialization.
    /// </summary>
    public virtual void PostMake() { }

    /// <summary>
    /// Called before exporting this manager job, allowing for any necessary pre-export logic.
    /// </summary>
    public virtual void PreExport() { }

    /// <summary>
    /// Called after exporting this manager job, allowing for any necessary post-export logic.
    /// </summary>
    public virtual void PostExport() { }

    /// <summary>
    /// Called before importing this manager job, allowing for any necessary pre-import logic.
    /// </summary>
    public virtual void PreImport() { }

    /// <summary>
    /// Called after importing this manager job to perform internal post-import logic, such as setting management state and updating triggers.
    /// </summary>
    public void PostImportInt()
    {
        IsManaged = true;
        if (Trigger is Trigger trigger)
        {
            trigger.Job = this;
        }
        if (!ColonyManagerReduxMod.Settings.NewJobsAreImmediatelyOutdated)
        {
            // set last updated to current time
            Touch();
        }
        PostImport();
    }

    /// <summary>
    /// Called after importing this manager job, allowing for any necessary post-import logic.
    /// </summary>
    public virtual void PostImport() { }

    /// <inheritdoc/>
    public virtual void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            _updateIntervalScribe = UpdateInterval.Ticks;
        }

        Scribe_Defs.Look(ref _def, "def");
        if (_def == null)
        {
            return;
        }

        Scribe_Deep.Look(ref _trigger, "trigger", this);
        Scribe_Values.Look(ref _updateIntervalScribe, "updateInterval");
        Scribe_Values.Look(ref ShouldCheckReachable, "shouldCheckReachable", true);
        Scribe_Values.Look(ref UsePathBasedDistance, "usePathBasedDistance");

        if (Manager.ScribeSameGameData)
        {
            Scribe_Values.Look(ref _lastActionTick, "lastActionTick");
            Scribe_Values.Look(
                ref _jobCreatedTick,
                "jobCreatedTick",
                _lastActionTick < 0 ? Find.TickManager.TicksGame : _lastActionTick
            );
            Scribe_Values.Look(ref Priority, "priority");
            Scribe_Values.Look(ref _isSuspended, "isSuspended");
            Scribe_Values.Look(ref _jobState, "jobState");
        }
        if (Manager.ScribeSameMapData)
        {
            Scribe_Values.Look(ref _loadID, "loadID", 0);
            Scribe_References.Look(ref _manager, "manager");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // must be true if it was saved.
                IsManaged = true;
            }
        }
        else
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                _loadID = Manager.GetNextManagerJobID();
            }
        }

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            _updateInterval =
                Utilities.UpdateIntervalOptions.FirstOrDefault(ui =>
                    ui.Ticks == _updateIntervalScribe
                ) ?? ColonyManagerReduxMod.Settings.DefaultUpdateInterval;
        }

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            Initialize();
        }

        foreach (var comp in _comps)
        {
            comp.PostExposeData();
        }
    }

    /// <summary>
    /// Called to finalize initialization of the manager job (called from MapComponent).
    /// </summary>
    protected internal virtual void FinalizeInit() { }

    /// <summary>
    /// Attempts to perform the manager job asynchronously using a coroutine.
    /// </summary>
    /// <remarks>
    /// This is a temporary fallback for jobs that don't yet implement the two-phase
    /// <see cref="GatherJobDataCoroutine(ManagerLog, AnyBoxed{object})"/>/
    /// <see cref="ExecuteJobDataCoroutine(ManagerLog, object, Boxed{bool})"/> API, and will be
    /// removed once every job has been migrated. Because this method's whole body typically
    /// mutates game state, the caller runs it as the job's <i>execute</i> phase (not gather) —
    /// it does not get the early-start benefit two-phase jobs get, only the "finish instantly
    /// once done" one.
    /// </remarks>
    /// <param name="jobLog">The log to record job actions and results.</param>
    /// <param name="workDone">A boxed boolean indicating whether work was done.</param>
    /// <returns>A coroutine representing the asynchronous job operation.</returns>
    [Obsolete(
        "Implement the two-phase GatherJobDataCoroutine/ExecuteJobDataCoroutine API instead; "
            + "this fallback will be removed in a future version."
    )]
    public virtual Coroutine? TryDoJobCoroutine(ManagerLog jobLog, Boxed<bool> workDone) => null;

    /// <summary>
    /// Gathers the information needed to perform the job, without making any changes to
    /// the game world. The resulting data is passed to
    /// <see cref="ExecuteJobDataCoroutine(ManagerLog, object, Boxed{bool})"/>.
    /// </summary>
    /// <remarks>
    /// Returns <see langword="null"/> by default to signal that this job hasn't
    /// implemented the two-phase API, in which case the obsolete <see cref="TryDoJobCoroutine"/>
    /// is used as a fallback instead — run in full during the execute phase, since (unlike
    /// gathering) its body isn't guaranteed to be free of game-state mutation.
    /// </remarks>
    /// <param name="jobLog">The log to record job actions and results.</param>
    /// <param name="data">A boxed value to store the gathered data in.</param>
    /// <returns>A coroutine representing the asynchronous gather operation, or <see langword="null"/> if not implemented.</returns>
    public virtual Coroutine? GatherJobDataCoroutine(ManagerLog jobLog, AnyBoxed<object?> data) =>
        null;

    /// <summary>
    /// Executes the job using the data gathered by
    /// <see cref="GatherJobDataCoroutine(ManagerLog, AnyBoxed{object})"/>.
    /// </summary>
    /// <param name="jobLog">The log to record job actions and results.</param>
    /// <param name="data">The data gathered by the gather phase.</param>
    /// <param name="workDone">A boxed boolean indicating whether work was done.</param>
    /// <returns>A coroutine representing the asynchronous execute operation.</returns>
    public virtual Coroutine? ExecuteJobDataCoroutine(
        ManagerLog jobLog,
        object? data,
        Boxed<bool> workDone
    ) => null;

    /// <summary>
    /// Cleans up any resources or state associated with this manager job.
    /// </summary>
    /// <param name="jobLog">The log to record cleanup actions and results, or null if not needed.</param>
    public abstract void CleanUp(ManagerLog? jobLog = null);

    /// <summary>
    /// Cleans up and removes all designations in the provided list, logging the operation if a job log is provided.
    /// </summary>
    /// <param name="designations">The list of designations to clean up and remove.</param>
    /// <param name="jobLog">The log to record cleanup actions and results, or null if not needed.</param>
    protected static void CleanUpDesignations(
        List<Designation> designations,
        ManagerLog? jobLog = null
    )
    {
        if (designations == null)
        {
            throw new ArgumentNullException(nameof(designations));
        }

        var originalCount = designations.Count;

        // cancel outstanding designation
        foreach (var designation in designations)
        {
            designation.Delete();
        }

        // clear the list completely
        designations.Clear();

        var newCount = designations.Count;
        if (originalCount != newCount)
        {
            jobLog?.AddDetail(
                "ColonyManagerRedux.Logs.CleanJobCompletedDesignations".Translate(
                    originalCount - newCount,
                    originalCount,
                    newCount
                )
            );
        }
    }

    /// <summary>
    /// Gets the intersection of designations based on the specified <paramref name="designationDef"/>.
    /// </summary>
    /// <param name="designationDef">The designation definition to filter by, or null to return all designations.</param>
    /// <returns>An enumerable collection of <see cref="Designation"/> objects matching the criteria.</returns>
    protected virtual IEnumerable<Designation> GetIntersectionDesignations(
        DesignationDef? designationDef
    ) =>
        designationDef != null
            ? Manager.map.designationManager.SpawnedDesignationsOfDef(designationDef)
            : Manager.map.designationManager.AllDesignations;

    /// <summary>
    /// Cleans up the list of designations by retaining only those that still exist in the game, based on the specified designation definition.
    /// </summary>
    /// <param name="designations">The list of designations to clean up.</param>
    /// <param name="designationDef">The designation definition to filter by, or null to include all designations.</param>
    /// <param name="jobLog">The log to record cleanup actions and results, or null if not needed.</param>
    protected void CleanDeadDesignations(
        List<Designation> designations,
        DesignationDef? designationDef,
        ManagerLog? jobLog = null
    )
    {
        if (designations == null)
        {
            throw new ArgumentNullException(nameof(designations));
        }

        var originalCount = designations.Count;
        var gameDesignations = GetIntersectionDesignations(designationDef);
        using var designationsIntersection = ArrayPool<Designation>.Shared.RentWithSelfReturn(
            designations.Count
        );
        var newCount = 0;
        foreach (var (d, i) in designations.Intersect(gameDesignations).Select((d, i) => (d, i)))
        {
            designationsIntersection[i] = d;
            newCount++;
        }
        designations.Clear();
        designations.AddRange(designationsIntersection.Arr.Take(newCount));

        if (originalCount != newCount)
        {
            jobLog?.AddDetail(
                "ColonyManagerRedux.Logs.CleanDeadDesignations".Translate(
                    originalCount - newCount,
                    originalCount,
                    newCount
                )
            );
        }
    }

    /// <summary>
    /// Deletes this manager job, optionally performing cleanup before removal.
    /// </summary>
    /// <param name="cleanup">If true, performs cleanup before deleting the job.</param>
    public virtual void Delete(bool cleanup = true)
    {
        if (cleanup)
        {
            CleanUp();
        }

        Manager.JobTracker.Delete(this, false);
    }

    /// <summary>
    /// Calculates the distance between the specified target and source position, optionally using path-based distance.
    /// </summary>
    /// <param name="target">The target <see cref="Thing"/> to measure distance to.</param>
    /// <param name="source">The source <see cref="IntVec3"/> position to measure distance from.</param>
    /// <returns>The calculated distance as a float value.</returns>
    public virtual float Distance(Thing target, IntVec3 source)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (UsePathBasedDistance)
        {
            if (target.Map == null)
            {
                ColonyManagerReduxMod.Instance.LogWarning(
                    $"{target} does not have a valid Map; " + "cannot use path based distance."
                );
            }
            else
            {
                var path = target.Map.pathFinder.FindPathCmr(
                    source,
                    target,
                    TraverseParms.For(TraverseMode.PassDoors, Danger.Some),
                    PathEndMode.Touch
                );
                var cost = path.Found ? path.TotalCost : int.MaxValue;
                path.ReleaseToPool();
                return cost * 2;
            }
        }

        return Mathf.Sqrt(source.DistanceToSquared(target.Position)) * 2;
    }

    /// <summary>
    /// Calculates the distances from a source position to a collection of target <see cref="Thing"/> objects,
    /// optionally yielding after a configurable number of operations for coroutine support.
    /// </summary>
    /// <param name="targets">The collection of target <see cref="Thing"/> objects to measure distances to.</param>
    /// <param name="source">The source <see cref="IntVec3"/> position from which distances are measured.</param>
    /// <param name="distances">The list to which calculated distances will be added.</param>
    /// <returns>A coroutine that yields after a configurable number of operations.</returns>
    [CoroutineSettingsMethod]
    public virtual Coroutine DistancesCoroutine(
        IEnumerable<Thing> targets,
        IntVec3 source,
        List<float> distances
    )
    {
        if (targets == null)
        {
            throw new ArgumentNullException(nameof(targets));
        }
        if (distances == null)
        {
            throw new ArgumentNullException(nameof(distances));
        }

        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            DistancesCoroutine
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                DistancesCoroutine
            );

        foreach (var (target, i) in targets.Select((t, i) => (t, i)))
        {
            distances.Add(Distance(target, source));

            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    private readonly Queue<List<(object thing, int i)>> _tmpTargets = [];
    private readonly Queue<List<float>> _tmpTargetDistances = [];

    /// <summary>
    /// Sorts a collection of targets based on a custom sorter and predicate, optionally using a source position for distance calculations.
    /// </summary>
    /// <typeparam name="TOrigin">The original type of the targets.</typeparam>
    /// <typeparam name="TThing">The type to which each target is converted for sorting and filtering.</typeparam>
    /// <typeparam name="TSorter">The type used for sorting, which must implement <see cref="IComparable{TSorter}"/>.</typeparam>
    /// <param name="unsortedTargets">The collection of unsorted targets.</param>
    /// <param name="sortedTargets">The list to store the sorted targets.</param>
    /// <param name="predicate">A predicate to filter the targets.</param>
    /// <param name="sorter">A function to determine the sort order based on the target and its distance.</param>
    /// <param name="toThing">A function to convert from <typeparamref name="TOrigin"/> to <typeparamref name="TThing"/>.</param>
    /// <param name="sourcePosition">An optional source position for distance calculations; if null, the base center is used.</param>
    /// <returns>A coroutine that sorts the targets as specified.</returns>
    protected virtual Coroutine GetThingsSorted<TOrigin, TThing, TSorter>(
        IEnumerable<TOrigin> unsortedTargets,
        List<TOrigin> sortedTargets,
        Func<TThing, bool> predicate,
        Func<TThing, float, TSorter> sorter,
        Func<TOrigin, TThing> toThing,
        IntVec3? sourcePosition = null
    )
        where TSorter : IComparable<TSorter>
    {
        if (unsortedTargets == null)
        {
            throw new ArgumentNullException(nameof(unsortedTargets));
        }
        if (sortedTargets == null)
        {
            throw new ArgumentNullException(nameof(sortedTargets));
        }
        if (toThing == null)
        {
            throw new ArgumentNullException(nameof(toThing));
        }

        var targets = _tmpTargets.Count > 0 ? _tmpTargets.Dequeue() : [];
        var targetDistances = _tmpTargetDistances.Count > 0 ? _tmpTargetDistances.Dequeue() : [];
        targets.AddRange(
            unsortedTargets.Where(o => predicate(toThing(o))).Select((t, i) => ((object)t!, i))
        );

        using var _ = new DoOnDispose(() =>
        {
            targets.Clear();
            targetDistances.Clear();

            _tmpTargets.Enqueue(targets);
            _tmpTargetDistances.Enqueue(targetDistances);
        });

        var position = sourcePosition ?? Manager.map.GetBaseCenter();

        yield return DistancesCoroutine(
                targets.Select(t => (toThing((TOrigin)t.thing) as Thing)!),
                position,
                targetDistances
            )
            .ResumeWhenOtherCoroutineIsCompleted();

        var origins = new List<TOrigin>(targets.Count);
        var things = new List<TThing>(targets.Count);
        foreach (var (thing, i) in targets)
        {
            var origin = (TOrigin)thing;
            origins.Add(origin);
            things.Add(toThing(origin));
        }

        sortedTargets.Clear();
        sortedTargets.AddRange(SortByScoreDescending(origins, things, targetDistances, sorter));
    }

    /// <summary>
    /// Sorts <paramref name="origins"/> descending by the score <paramref name="sorter"/> assigns
    /// each corresponding <paramref name="things"/>/<paramref name="distances"/> pair.
    /// </summary>
    internal static List<TOrigin> SortByScoreDescending<TOrigin, TThing, TSorter>(
        IReadOnlyList<TOrigin> origins,
        IReadOnlyList<TThing> things,
        IReadOnlyList<float> distances,
        Func<TThing, float, TSorter> sorter
    )
        where TSorter : IComparable<TSorter>
    {
        var indices = new List<int>(origins.Count);
        for (var i = 0; i < origins.Count; i++)
        {
            indices.Add(i);
        }
        indices.SortByDescending(i => sorter(things[i], distances[i]));

        var result = new List<TOrigin>(origins.Count);
        foreach (var i in indices)
        {
            result.Add(origins[i]);
        }
        return result;
    }

    /// <summary>
    /// Sorts a collection of targets based on a custom sorter and predicate, optionally using a source position for distance calculations.
    /// </summary>
    /// <typeparam name="TThing">The type of the targets, must be a Thing.</typeparam>
    /// <typeparam name="TSorter">The type used for sorting, which must implement <see cref="IComparable{TSorter}"/>.</typeparam>
    /// <param name="unsortedTargets">The collection of unsorted targets.</param>
    /// <param name="sortedTargets">The list to store the sorted targets.</param>
    /// <param name="predicate">A predicate to filter the targets.</param>
    /// <param name="sorter">A function to determine the sort order based on the target and its distance.</param>
    /// <param name="sourcePosition">An optional source position for distance calculations; if null, the base center is used.</param>
    /// <returns>A coroutine that sorts the targets as specified.</returns>
    [Obsolete("Use GetThingsSorted instead; " + "this method will be removed in a future version")]
    public virtual Coroutine GetTargetsSorted<TThing, TSorter>(
        IEnumerable<TThing> unsortedTargets,
        List<TThing> sortedTargets,
        Func<TThing, bool> predicate,
        Func<TThing, float, TSorter> sorter,
        IntVec3? sourcePosition = null
    )
        where TThing : Thing
        where TSorter : IComparable<TSorter> =>
        GetThingsSorted(unsortedTargets, sortedTargets, predicate, sorter, t => t, sourcePosition);

    /// <summary>
    /// Sorts a collection of targets of type <typeparamref name="TThing"/> based on a custom sorter and predicate,
    /// optionally using a source position for distance calculations.
    /// </summary>
    /// <typeparam name="TThing">The type of the targets, must be a Thing.</typeparam>
    /// <typeparam name="TSorter">The type used for sorting, which must implement <see cref="IComparable{TSorter}"/>.</typeparam>
    /// <param name="sortedTargets">The list to store the sorted targets.</param>
    /// <param name="predicate">A predicate to filter the targets.</param>
    /// <param name="sorter">A function to determine the sort order based on the target and its distance.</param>
    /// <param name="sourcePosition">An optional source position for distance calculations; if null, the base center is used.</param>
    /// <returns>A coroutine that sorts the targets as specified.</returns>
    public virtual Coroutine GetTargetsSorted<TThing, TSorter>(
        List<TThing> sortedTargets,
        Func<TThing, bool> predicate,
        Func<TThing, float, TSorter> sorter,
        IntVec3? sourcePosition = null
    )
        where TThing : Thing
        where TSorter : IComparable<TSorter> =>
        GetThingsSorted(
            typeof(Pawn).IsAssignableFrom(typeof(TThing))
                ? Manager.map.mapPawns.AllPawns.OfType<TThing>()
                : Manager.map.listerThings.AllThings.OfType<TThing>(),
            sortedTargets,
            predicate,
            sorter,
            t => t,
            sourcePosition: sourcePosition
        );

    /// <summary>
    /// <b>Obsolete.</b> This method is obsolete and will be removed in a future version.
    /// Use <see cref="IsReachable(Thing, PathEndMode, Danger)"/> instead.
    /// <para>
    /// Determines whether the specified target <see cref="Thing"/> is reachable by any free colonist on the map,
    /// considering fog of war and the <see cref="ShouldCheckReachable"/> setting.
    /// </para>
    /// </summary>
    /// <param name="target">The target <see cref="Thing"/> to check for reachability.</param>
    /// <returns>True if the target is reachable; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="target"/> is null.</exception>
    [Obsolete(
        "Use IsReachable(Thing, PathEndMode, Danger) instead. "
            + "This method will be removed in a future version."
    )]
    public virtual bool IsReachable(Thing target) =>
        IsReachable(target, PathEndMode.Touch, Danger.Some);

    /// <summary>
    /// Determines whether the specified target <see cref="Thing"/> is reachable by any free colonist on the map,
    /// considering fog of war and the <see cref="ShouldCheckReachable"/> setting, with specified path end mode and danger level.
    /// </summary>
    /// <param name="target">The target <see cref="Thing"/> to check for reachability.</param>
    /// <param name="pathEndMode">The path end mode to use when checking reachability (default is <see cref="PathEndMode.Touch"/>).</param>
    /// <param name="danger">The danger level to allow when checking reachability (default is <see cref="Danger.Some"/>).</param>
    /// <returns>True if the target is reachable; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="target"/> is null.</exception>
    public virtual bool IsReachable(
        Thing target,
        PathEndMode pathEndMode = PathEndMode.Touch,
        Danger danger = Danger.Some
    )
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (target.Position.Fogged(Manager.map))
        {
            return false;
        }

        if (!ShouldCheckReachable)
        {
            return true;
        }

        var key = (target.Position, pathEndMode, danger);
        if (
            _reachabilityCache.TryGetValue(key, out var cachedValue)
            && cachedValue.TryGetValue(out var cached)
        )
        {
            return cached;
        }

        var result = Manager.map.mapPawns.FreeColonistsSpawned.Any(p =>
            p.CanReach(target, pathEndMode, danger)
        );

        if (cachedValue != null)
        {
            _ = cachedValue.Update(result);
        }
        else
        {
            _reachabilityCache.Add(key, new CachedValue<bool>(result, ReachabilityCacheTicks));
        }

        return result;
    }

    // Colony reachability rarely changes tick-to-tick (only when doors/pathing
    // change nearby), but IsReachable is called once per candidate thing on every
    // job planning pass across every job type. A short-lived, per-(position,
    // pathEndMode, danger) TTL cache avoids re-running an O(free colonists)
    // CanReach scan per candidate per pass.
    private const int ReachabilityCacheTicks = 2000;
    private readonly Dictionary<
        (IntVec3 Position, PathEndMode PathEndMode, Danger Danger),
        CachedValue<bool>
    > _reachabilityCache = [];

    internal void IntTick()
    {
        Tick();
        foreach (var c in _comps)
        {
            c.CompTick();
        }
    }

    /// <summary>
    /// Called every tick to update the state of the manager job.
    /// </summary>
    public virtual void Tick() { }

    /// <inheritdoc/>
    public override string ToString() =>
        new StringBuilder()
            .AppendLine(Label)
            .AppendLine("Load ID:" + GetUniqueLoadID())
            .AppendLine("Priority: " + Priority)
            .AppendLine("Active: " + IsSuspended)
            .AppendLine("JobCreatedTick: " + _jobCreatedTick)
            .AppendLine("LastActionTick: " + _lastActionTick)
            .AppendLine("Interval: " + UpdateInterval.Label)
            .AppendLine("TicksSinceLastUpdate: " + TicksSinceLastUpdate)
            .AppendLine("TicksSinceShouldUpdate: " + TicksSinceShouldUpdate)
            .AppendLine("HasBeenUpdated: " + HasBeenUpdated)
            .AppendLine("IsSuspended: " + _isSuspended)
            .AppendLine("JobState: " + JobState)
            .AppendLine("IsManaged: " + IsManaged)
            .AppendLine("ShouldUpdate: " + ShouldUpdate)
            .AppendLine("ShouldDoNow: " + ShouldDoNow)
            .ToString();

    /// <summary>
    /// Updates the last action tick to the current game tick, marking the job as recently updated.
    /// </summary>
    public void Touch() => _lastActionTick = Find.TickManager.TicksGame;

    /// <summary>
    /// Resets the last action tick, marking the job as not having been updated.
    /// </summary>
    public void Untouch() => _lastActionTick = -1;

    /// <inheritdoc/>
    public string GetUniqueLoadID() => $"ColonyManagerRedux_ManagerJob_{Manager.id}_{_loadID}";

    /// <summary>
    /// Returns the first component of type <typeparamref name="T"/> attached to this manager job, or null if none exists.
    /// </summary>
    /// <typeparam name="T">The type of the component to retrieve.</typeparam>
    /// <returns>The first component of type <typeparamref name="T"/>, or null if not found.</returns>
    public T? CompOfType<T>()
        where T : ManagerJobComp => _comps?.FirstOrDefault(c => c is T) as T;

    /// <summary>
    /// Returns all components of type <typeparamref name="T"/> attached to this manager job.
    /// </summary>
    /// <typeparam name="T">The type of the components to retrieve.</typeparam>
    /// <returns>An enumerable collection of components of type <typeparamref name="T"/>.</returns>
    public IEnumerable<T> CompsOfType<T>()
        where T : ManagerJobComp => _comps?.Where(c => c is T).Cast<T>() ?? [];

    /// <summary>
    /// Executes the specified action for all components of type <typeparamref name="T"/> attached to this manager job.
    /// </summary>
    /// <typeparam name="T">The type of the components to operate on.</typeparam>
    /// <param name="action">The action to execute for each component of type <typeparamref name="T"/>.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="action"/> is null.</exception>
    public void ForAllCompsOfType<T>(Action<T> action)
        where T : ManagerJobComp
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        foreach (var comp in CompsOfType<T>())
        {
            action(comp);
        }
    }

    /// <summary>
    /// Called when an area is removed from the map, allowing the manager job to respond as needed.
    /// </summary>
    /// <param name="area">The area that was removed.</param>
    protected internal virtual void Notify_AreaRemoved(Area area) { }
}
