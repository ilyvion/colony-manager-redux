// ManagerJob.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Text;
using Verse.AI;

namespace ColonyManagerRedux;

[HotSwappable]
public abstract class ManagerJob : ILoadReferenceable, IExposable
{
    internal ManagerDef _def;
    public ManagerDef Def => _def;

    private List<ManagerJobComp>? _comps;

    public bool ShouldCheckReachable;

    private int _lastActionTick = -1;
    public int TimeSinceLastUpdate => Find.TickManager.TicksGame - _lastActionTick;
    public bool HasBeenUpdated => _lastActionTick != -1;

    internal Manager _manager;
    public Manager Manager => _manager;

    public bool UsePathBasedDistance;

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

    public abstract bool IsCompleted { get; }
    public virtual string IsCompletedTooltip => "ColonyManagerRedux.Overview.JobHasbeenCompletedTooltip".Translate();

    public virtual bool IsValid => Manager != null;
    public abstract string Label { get; }
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


    public virtual bool ShouldDoNow => IsManaged && !IsSuspended && !IsCompleted && ShouldUpdate;

    private bool ShouldUpdate => _lastActionTick < 0 || ((_lastActionTick + UpdateInterval.ticks) < Find.TickManager.TicksGame);

    public virtual bool IsSuspended
    {
        get => _isSuspended;
        set => _isSuspended = value;
    }
    public virtual string IsSuspendedTooltip => "ColonyManagerRedux.Overview.JobHasBeenSuspendedTooltip".Translate();

    public ManagerTab Tab => Manager.Tabs.First(tab => tab.GetType() == _def.managerTabClass);
    public abstract IEnumerable<string> Targets { get; }

    public virtual UpdateInterval UpdateInterval
    {
        get => _updateInterval ?? ColonyManagerReduxMod.Settings.DefaultUpdateInterval;
        set => _updateInterval = value;
    }

    public abstract WorkTypeDef? WorkTypeDef { get; }

    public virtual int MaxUpperThreshold { get; } = Constants.DefaultMaxUpperThreshold;

    internal void Initialize()
    {
        if (_def.comps.Any())
        {
            _comps = [];
            foreach (var compProperties in _def.comps)
            {
                ManagerJobComp? managerJobComp = null;
#pragma warning disable CA1031 // Do not catch general exception types
                try
                {
                    managerJobComp = (ManagerJobComp)Activator.CreateInstance(compProperties.compClass);
                    managerJobComp.parent = this;
                    _comps.Add(managerJobComp);
                    managerJobComp.Initialize(compProperties);
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
#pragma warning restore CA1031 // Do not catch general exception types
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
            _updateIntervalScribe = UpdateInterval.ticks;
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

        if (Manager.Mode == Manager.ScribingMode.Normal)
        {
            Scribe_Values.Look(ref _loadID, "loadID", 0);
            Scribe_References.Look(ref _manager, "manager");
            Scribe_Values.Look(ref _lastActionTick, "lastActionTick");
            Scribe_Values.Look(ref Priority, "priority");
            Scribe_Values.Look(ref _isSuspended, "isSuspended");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // must be true if it was saved.
                IsManaged = true;
            }
        }
        else if (Manager.Mode == Manager.ScribingMode.Transfer)
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                _loadID = Manager.GetNextManagerJobID();
            }
        }

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            _updateInterval = Utilities.UpdateIntervalOptions.FirstOrDefault(ui => ui.ticks == _updateIntervalScribe) ??
                ColonyManagerReduxMod.Settings.DefaultUpdateInterval;
        }

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            Initialize();
        }

        if (_comps != null)
        {
            foreach (ManagerJobComp comp in _comps)
            {
                comp.PostExposeData();
            }
        }
    }

    public abstract bool TryDoJob();

    public abstract void CleanUp();

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
            var path = target.Map.pathFinder.FindPath(source, target,
                TraverseParms.For(TraverseMode.PassDoors, Danger.Some),
                PathEndMode.Touch);
            var cost = path.Found ? path.TotalCost : int.MaxValue;
            path.ReleaseToPool();
            return cost * 2;
        }

        return Mathf.Sqrt(source.DistanceToSquared(target.Position)) * 2;
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
        if (!_comps.NullOrEmpty())
        {
            foreach (ManagerJobComp c in _comps!)
            {
                c.CompTick();
            }
        }
    }

    public override string ToString()
    {
        var s = new StringBuilder();
        s.AppendLine("Priority: " + Priority);
        s.AppendLine("Active: " + IsSuspended);
        s.AppendLine("LastAction: " + _lastActionTick);
        s.AppendLine("Interval: " + UpdateInterval);
        s.AppendLine("GameTick: " + Find.TickManager.TicksGame);
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
}
