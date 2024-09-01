// ManagerJob.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Buffers;
using System.Text;
using ilyvion.Laboratory.Extensions;
using Verse.AI;

namespace ColonyManagerRedux;

[HotSwappable]
public abstract class ManagerJob<TSettings>(Manager manager) : ManagerJob(manager)
    where TSettings : ManagerSettings
{
    public TSettings ManagerSettings => ColonyManagerReduxMod.Settings
        .ManagerSettingsFor<TSettings>(Def)
            ?? throw new InvalidOperationException($"Type {GetType().Name} claims to have a "
            + $"manager settings type of {typeof(TSettings).Name}, but no such type has been "
            + "registered. Did you remember to add your settings type to your ManagerDef with a "
            + "managerSettingsClass value?");
}

[HotSwappable]
public abstract class ManagerJob : ILoadReferenceable, IExposable
{
    internal ManagerDef _def;
    public ManagerDef Def => _def;

    private List<ManagerJobComp> _comps;

    private bool _shouldCheckReachable;
    public ref bool ShouldCheckReachable { get => ref _shouldCheckReachable; }

    private int _lastActionTick = -1;
    public int TicksSinceLastUpdate => Find.TickManager.TicksGame - _lastActionTick;
    public bool HasBeenUpdated => _lastActionTick != -1;

    internal Manager _manager;
    public Manager Manager => _manager;

    private bool _usePathBasedDistance;
    public ref bool UsePathBasedDistance { get => ref _usePathBasedDistance; }

    internal int Priority;

    private bool _isSuspended;

    private UpdateInterval? _updateInterval;
    private int _updateIntervalScribe;

    private int _loadID = -1;
    private bool isManaged;

    private Trigger? _trigger;
    public Trigger? Trigger
    {
        get => _trigger;
        protected set
        {
            _trigger = value;
        }
    }

    public virtual bool IsTransferable => true;

#pragma warning disable CS8618 // Set by ManagerDefMaker.MakeManagerJob
    protected ManagerJob(Manager manager)
#pragma warning restore CS8618
    {
        Settings settings = ColonyManagerReduxMod.Settings;
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
    public ManagerJobState JobState { get => _jobState; protected set => _jobState = value; }
    public bool IsCompleted => JobState == ManagerJobState.Completed;
    public virtual string IsCompletedTooltip => "ColonyManagerRedux.Job.JobHasbeenCompletedTooltip".Translate();

    public virtual bool IsValid => Manager != null;
    public virtual string Label => _def.label.CapitalizeFirst();
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


    public bool ShouldDoNow => IsManaged && ShouldUpdate;

    private bool ShouldUpdate => _lastActionTick < 0 || ((_lastActionTick + UpdateInterval.Ticks) < Find.TickManager.TicksGame);

    public bool IsSuspended
    {
        get => _isSuspended;
        set
        {
            _isSuspended = value;
            CausedException = null;
        }

    }
    public virtual string IsSuspendedTooltip => "ColonyManagerRedux.Job.JobHasBeenSuspendedTooltip".Translate();
    public virtual string IsSuspendedDueToExceptionTooltip => "ColonyManagerRedux.Job.JobHasBeenSuspendedDueToExceptionTooltip".Translate();

    public ManagerTab Tab => Manager.Tabs.First(tab => tab.GetType() == _def.managerTabClass);
    public abstract IEnumerable<string> Targets { get; }

    public virtual UpdateInterval UpdateInterval
    {
        get => _updateInterval ?? ColonyManagerReduxMod.Settings.DefaultUpdateInterval;
        set => _updateInterval = value;
    }

    public abstract WorkTypeDef? WorkTypeDef { get; }

    public virtual int MaxUpperThreshold { get; } = Constants.DefaultMaxUpperThreshold;

    private Exception? _causedException;
    public Exception? CausedException
    {
        get => _causedException;
        internal set
        {
            _causedException = value;
            _causedExceptionToStringCache = null;
        }

    }
    private string? _causedExceptionToStringCache;
    public string? CausedExceptionText
    {
        get
        {
            if (_causedExceptionToStringCache == null && _causedException != null)
            {
                ref bool noStacktraceCaching = ref AccessTools.StaticFieldRefAccess<bool>(
                    "HarmonyMod.HarmonyMain:noStacktraceCaching");

                var originalValue = noStacktraceCaching;
                noStacktraceCaching = true;
                _causedExceptionToStringCache = _causedException.ToString();
                noStacktraceCaching = originalValue;

            }
            return _causedExceptionToStringCache;
        }
    }

    internal void Initialize()
    {
        if (_def.jobComps.Any())
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
                        "Could not instantiate or initialize a ManagerJobComp: " + ex);
                    if (managerJobComp != null)
                    {
                        _comps.Remove(managerJobComp);
                    }
                }
            }
        }
    }

    public virtual void PostMake()
    {
    }

    public virtual void PreExport()
    {
    }

    public virtual void PostExport()
    {
    }

    public virtual void PreImport()
    {
    }

    public virtual void PostImport()
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
    }

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

        if (Manager.ScribeGameSpecificData)
        {
            Scribe_Values.Look(ref _loadID, "loadID", 0);
            Scribe_References.Look(ref _manager, "manager");
            Scribe_Values.Look(ref _lastActionTick, "lastActionTick");
            Scribe_Values.Look(ref Priority, "priority");
            Scribe_Values.Look(ref _isSuspended, "isSuspended");
            Scribe_Values.Look(ref _jobState, "jobState");

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
            _updateInterval = Utilities.UpdateIntervalOptions.FirstOrDefault(ui => ui.Ticks == _updateIntervalScribe) ??
                ColonyManagerReduxMod.Settings.DefaultUpdateInterval;
        }

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            Initialize();
        }

        foreach (ManagerJobComp comp in _comps)
        {
            comp.PostExposeData();
        }
    }

    protected internal virtual void FinalizeInit()
    {
    }

    [Obsolete("Implement TryDoJobCoroutine; this is only here for backwards compatibility")]
    public virtual bool TryDoJob(ManagerLog jobLog)
    {
        // This should never be called as long as the Coroutine has been
        // properly implemented in the subclass.
        return false;
    }

    public virtual Coroutine TryDoJobCoroutine(ManagerLog jobLog, Boxed<bool> workDone)
    {
        // We're allowing returning null here despite the signature because of backwards
        // compatibility, but anyone overriding this method should not!
        return null!;
    }

    public abstract void CleanUp(ManagerLog? jobLog = null);

    protected static void CleanUpDesignations(
        List<Designation> designations, ManagerLog? jobLog = null)
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
            jobLog?.AddDetail("ColonyManagerRedux.Logs.CleanJobCompletedDesignations"
                .Translate(originalCount - newCount, originalCount, newCount));
        }
    }

    protected virtual IEnumerable<Designation> GetIntersectionDesignations(
        DesignationDef? designationDef)
    {
        return designationDef != null
            ? Manager.map.designationManager.SpawnedDesignationsOfDef(designationDef)
            : Manager.map.designationManager.AllDesignations;
    }

    protected void CleanDeadDesignations(
        List<Designation> designations, DesignationDef? designationDef, ManagerLog? jobLog = null)
    {
        if (designations == null)
        {
            throw new ArgumentNullException(nameof(designations));
        }

        var originalCount = designations.Count;
        var gameDesignations = GetIntersectionDesignations(designationDef);
        using var designationsIntersection = ArrayPool<Designation>.Shared
            .RentWithSelfReturn(designations.Count);
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
            jobLog?.AddDetail("ColonyManagerRedux.Logs.CleanDeadDesignations"
                .Translate(originalCount - newCount, originalCount, newCount));
        }
    }

    public virtual void Delete(bool cleanup = true)
    {
        if (cleanup)
        {
            CleanUp();
        }

        Manager.JobTracker.Delete(this, false);
    }

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
                ColonyManagerReduxMod.Instance.LogWarning($"{target} does not have a valid Map; " +
                    "cannot use path based distance.");
            }
            else
            {
                var path = target.Map.pathFinder.FindPath(source, target,
                    TraverseParms.For(TraverseMode.PassDoors, Danger.Some),
                    PathEndMode.Touch);
                var cost = path.Found ? path.TotalCost : int.MaxValue;
                path.ReleaseToPool();
                return cost * 2;
            }
        }

        return Mathf.Sqrt(source.DistanceToSquared(target.Position)) * 2;
    }

    public virtual Coroutine DistancesCoroutine(
        IEnumerable<Thing> targets, IntVec3 source, List<float> distances)
    {
        if (targets == null)
        {
            throw new ArgumentNullException(nameof(targets));
        }
        if (distances == null)
        {
            throw new ArgumentNullException(nameof(distances));
        }

        foreach (var (target, i) in targets.Select((t, i) => (t, i)))
        {
            distances.Add(Distance(target, source));

            if (i > 0 && i % Constants.CoroutineBreakAfter == 0)
            {
                yield return ResumeImmediately.Singleton;
            }
        }
    }

    private Queue<List<(Thing thing, int i)>> _tmpTargets = [];
    private Queue<List<float>> _tmpTargetDistances = [];
    public virtual Coroutine GetTargetsSorted<TThing, TSorter>(
        IEnumerable<TThing> unsortedTargets,
        List<TThing> sortedTargets,
        Func<TThing, bool> predicate,
        Func<TThing, float, TSorter> sorter,
        IntVec3? sourcePosition = null)
        where TThing : Thing
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

        List<(Thing thing, int i)> targets;
        if (_tmpTargets.Count > 0)
        {
            targets = _tmpTargets.Dequeue();
        }
        else
        {
            targets = [];
        }

        List<float> targetDistances;
        if (_tmpTargetDistances.Count > 0)
        {
            targetDistances = _tmpTargetDistances.Dequeue();
        }
        else
        {
            targetDistances = [];
        }

        targets.AddRange(unsortedTargets.Where(predicate).Cast<Thing>().Select((t, i) => (t, i)));

        using var _ = new DoOnDispose(() =>
        {
            targets.Clear();
            targetDistances.Clear();

            _tmpTargets.Enqueue(targets);
            _tmpTargetDistances.Enqueue(targetDistances);
        });

        var position = sourcePosition ?? Manager.map.GetBaseCenter();

        yield return DistancesCoroutine(targets.Select(t => t.thing), position, targetDistances)
            .ResumeWhenOtherCoroutineIsCompleted();

        targets.SortByDescending(t => sorter((TThing)t.thing, targetDistances[t.i]));

        sortedTargets.Clear();
        sortedTargets.AddRange(targets.Select(t => t.thing).Cast<TThing>());
    }

    public virtual Coroutine GetTargetsSorted<TThing, TSorter>(
        List<TThing> sortedTargets,
        Func<TThing, bool> predicate,
        Func<TThing, float, TSorter> sorter,
        IntVec3? sourcePosition = null)
        where TThing : Thing
        where TSorter : IComparable<TSorter>
    {
        return GetTargetsSorted(
            typeof(Pawn).IsAssignableFrom(typeof(TThing))
                ? Manager.map.mapPawns.AllPawns.OfType<TThing>()
                : Manager.map.listerThings.AllThings.OfType<TThing>(),
            sortedTargets,
            predicate,
            sorter,
            sourcePosition);
    }

    public virtual bool IsReachable(Thing target)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        return !target.Position.Fogged(Manager.map)
            && (!ShouldCheckReachable ||
                Manager.map.mapPawns.FreeColonistsSpawned.Any(
                    p => p.CanReach(target, PathEndMode.Touch, Danger.Some)));
    }

    public virtual void Tick()
    {
        foreach (ManagerJobComp c in _comps)
        {
            c.CompTick();
        }
    }

    public override string ToString()
    {
        var s = new StringBuilder();
        s.AppendLine(Label);
        s.AppendLine("Load ID:" + GetUniqueLoadID());
        s.AppendLine("Priority: " + Priority);
        s.AppendLine("Active: " + IsSuspended);
        s.AppendLine("LastActionTick: " + _lastActionTick);
        s.AppendLine("Interval: " + UpdateInterval.Label);
        s.AppendLine("TicksSinceLastUpdate: " + TicksSinceLastUpdate);
        s.AppendLine("HasBeenUpdated: " + HasBeenUpdated);
        s.AppendLine("IsSuspended: " + _isSuspended);
        s.AppendLine("JobState: " + JobState);
        s.AppendLine("IsManaged: " + IsManaged);
        s.AppendLine("ShouldUpdate: " + ShouldUpdate);
        s.AppendLine("ShouldDoNow: " + ShouldDoNow);
        return s.ToString();
    }

    public void Touch()
    {
        _lastActionTick = Find.TickManager.TicksGame;
    }

    public void Untouch()
    {
        _lastActionTick = -1;
    }

    public string GetUniqueLoadID()
    {
        return $"ColonyManagerRedux_ManagerJob_{Manager.id}_{_loadID}";
    }

    public T? CompOfType<T>() where T : ManagerJobComp
    {
        return _comps?.FirstOrDefault(c => c is T) as T;
    }

    public IEnumerable<T> CompsOfType<T>() where T : ManagerJobComp
    {
        return _comps?.Where(c => c is T).Cast<T>() ?? [];
    }

    protected internal virtual void Notify_AreaRemoved(Area area)
    {
    }
}
