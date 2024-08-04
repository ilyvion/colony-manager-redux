﻿// Manager.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HotSwappable]
public class Manager : MapComponent, ILoadReferenceable
{
    public enum ScribingMode
    {
        Transfer,
        Normal
    }

    public static ScribingMode Mode { get; internal set; } = ScribingMode.Normal;

    private readonly List<ManagerTab> _tabs;
    public List<ManagerTab> Tabs => _tabs;

    private List<ManagerTab>? _managerTabsLeft;
    private List<ManagerTab>? _managerTabsMiddle;
    private List<ManagerTab>? _managerTabsRight;
    internal int id = -1;

    private bool _wasLoaded;
    private int _nextManagerJobID;

    internal DebugComponent debugComponent;

    public Manager(Map map) : base(map)
    {
        if (map == null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        debugComponent = new(this);
        _jobTracker = new(this);

        _tabs = DefDatabase<ManagerDef>.AllDefs
            .OrderBy(m => m.order)
            .Select(m => ManagerDefMaker.MakeManagerTab(m, this))
            .ToList();

        // if not created in SavingLoading, give yourself the ID of the map you were constructed on.
        id = map.uniqueID;
    }

    private JobTracker _jobTracker;
    public JobTracker JobTracker => _jobTracker ??= new JobTracker(this);

    internal List<ManagerTab> ManagerTabsLeft
    {
        get
        {
            _managerTabsLeft ??= _tabs.Where(tab => tab.def.iconArea == IconArea.Left).ToList();
            return _managerTabsLeft;
        }
    }

    internal List<ManagerTab> ManagerTabsMiddle
    {
        get
        {
            _managerTabsMiddle ??=
                _tabs.Where(tab => tab.def.iconArea == IconArea.Middle).ToList();
            return _managerTabsMiddle;
        }
    }

    internal List<ManagerTab> ManagerTabsRight
    {
        get
        {
            _managerTabsRight ??=
                _tabs.Where(tab => tab.def.iconArea == IconArea.Right).ToList();
            return _managerTabsRight;
        }
    }

    public string GetUniqueLoadID()
    {
        return $"ColonyManagerRedux_{id}";
    }

    public static Manager For(Map map)
    {
        if (map == null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        return map.GetComponent<Manager>();
    }

    public static implicit operator Map(Manager manager)
    {
        return manager?.map!;
    }

    public Map ToMap()
    {
        return this;
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref id, "id", -1, true);
        Scribe_Deep.Look(ref _jobTracker, "jobStack", this);

        Scribe_Values.Look(ref _nextManagerJobID, "nextManagerJobID", 0);

        var exposableTabs = _tabs.OfType<IExposable>().ToList();
        Scribe_Collections.Look(ref exposableTabs, "tabs", LookMode.Deep, this);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            foreach (var exposableTab in exposableTabs)
            {
                var oldTab = _tabs.Select((t, i) => (t, i))
                    .SingleOrDefault(v => v.t.GetType() == exposableTab.GetType());
                if (oldTab.t != null)
                {
                    var newTab = (ManagerTab)exposableTab;
                    newTab.def = oldTab.t.def;
                    _tabs[oldTab.i] = newTab;
                }
            }

            _wasLoaded = true;
        }

        _jobTracker ??= new JobTracker(this);
    }

    public override void MapComponentTick()
    {
        base.MapComponentTick();

        // tick jobs
        foreach (var job in JobTracker.JobsOfType<ManagerJob>())
        {
            if (!job.IsSuspended)
            {
#pragma warning disable CA1031 // Do not catch general exception types
                try
                {
                    job.Tick();
                }
                catch (Exception err)
                {
                    ColonyManagerReduxMod.Instance
                        .LogError($"Suspending manager job because it errored on tick: \n{err}");
                    job.IsSuspended = true;
                }
#pragma warning restore CA1031 // Do not catch general exception types
            }
        }

        // tick tabs
        foreach (var tab in _tabs)
        {
            tab.Tick();
        }
    }

    public override void MapComponentUpdate()
    {
        base.MapComponentUpdate();
        debugComponent.Update();
    }

    public bool TryDoWork()
    {
        return JobTracker.TryDoNextJob();
    }

    internal int GetNextManagerJobID()
    {
        if (Scribe.mode == LoadSaveMode.LoadingVars && !_wasLoaded)
        {
            ColonyManagerReduxMod.Instance
                .LogWarning("Getting next unique manager job ID during LoadingVars before Manager was loaded. Assigning a random value.");
            return Rand.Int;
        }
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            ColonyManagerReduxMod.Instance
                .LogWarning("Getting next unique manager job ID during saving. This may cause bugs.");
        }
        int result = _nextManagerJobID;
        _nextManagerJobID++;
        if (_nextManagerJobID == int.MaxValue)
        {
            ColonyManagerReduxMod.Instance
                .LogWarning("Next manager job ID is at max value. Resetting to 0. This may cause bugs.");
            _nextManagerJobID = 0;
        }
        return result;
    }

    // internal void NewJobStack(JobStack jobstack)
    // {
    //     // clean up old jobs
    //     foreach (var job in _stack.FullStack())
    //     {
    //         job.CleanUp();
    //     }

    //     // replace stack
    //     _stack = jobstack;

    //     // touch new jobs in inappropriate places (reset timing so they are properly performed)
    //     foreach (var job in _stack.FullStack())
    //     {
    //         job.manager = this;
    //         job.Touch();
    //     }
    // }
}
