// Manager.cs
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

    public static bool helpShown;

    public static ScribingMode Mode = ScribingMode.Normal;

    public List<ManagerTab> tabs;

    private List<ManagerTab>? _managerTabsLeft;
    private List<ManagerTab>? _managerTabsMiddle;
    private List<ManagerTab>? _managerTabsRight;
    private JobStack _stack;
    internal int id = -1;

    private bool _wasLoaded;
    private int _nextManagerJobID;

    internal DebugComponent debugComponent;

    public Manager(Map map) : base(map)
    {
        debugComponent = new(this);
        _stack = new(this);

        tabs = DefDatabase<ManagerDef>.AllDefs
            .OrderBy(m => m.order)
            .Select(m => ManagerDefMaker.MakeManagerTab(m, this))
            .ToList();

        // if not created in SavingLoading, give yourself the ID of the map you were constructed on.
        if (Scribe.mode == LoadSaveMode.Inactive)
        {
            id = map.uniqueID;
        }
    }

    public JobStack JobStack => _stack ??= new JobStack(this);

    public List<ManagerTab> ManagerTabsLeft
    {
        get
        {
            _managerTabsLeft ??= tabs.Where(tab => tab.def.iconArea == IconArea.Left).ToList();
            return _managerTabsLeft;
        }
    }

    public List<ManagerTab> ManagerTabsMiddle
    {
        get
        {
            _managerTabsMiddle ??=
                tabs.Where(tab => tab.def.iconArea == IconArea.Middle).ToList();
            return _managerTabsMiddle;
        }
    }

    public List<ManagerTab> ManagerTabsRight
    {
        get
        {
            _managerTabsRight ??=
                tabs.Where(tab => tab.def.iconArea == IconArea.Right).ToList();
            return _managerTabsRight;
        }
    }

    public string GetUniqueLoadID()
    {
        return $"ColonyManagerRedux_{id}";
    }

    public static Manager For(Map map)
    {
        return map.GetComponent<Manager>();
    }

    public static implicit operator Map(Manager manager)
    {
        return manager.map;
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref id, "id", -1, true);
        Scribe_Values.Look(ref helpShown, "helpShown");
        Scribe_Deep.Look(ref _stack, "jobStack", this);

        Scribe_Values.Look(ref _nextManagerJobID, "nextManagerJobID", 0);

        var exposableTabs = tabs.OfType<IExposable>().ToList();
        Scribe_Collections.Look(ref exposableTabs, "tabs", LookMode.Deep, this);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            foreach (var exposableTab in exposableTabs)
            {
                var oldTab = tabs.Select((t, i) => (t, i))
                    .SingleOrDefault(v => v.t.GetType() == exposableTab.GetType());
                if (oldTab.t != null)
                {
                    var newTab = (ManagerTab)exposableTab;
                    newTab.def = oldTab.t.def;
                    tabs[oldTab.i] = newTab;
                }
            }

            _wasLoaded = true;
        }

        _stack ??= new JobStack(this);
    }

    public override void MapComponentTick()
    {
        base.MapComponentTick();

        // tick jobs
        foreach (var job in JobStack.FullStack())
        {
            if (!job.IsSuspended)
            {
                try
                {
                    job.Tick();
                }
                catch (Exception err)
                {
                    Log.Error($"Suspending manager job because it errored on tick: \n{err}");
                    job.IsSuspended = true;
                }
            }
        }

        // tick tabs
        foreach (var tab in tabs)
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
        return JobStack.TryDoNextJob();
    }

    internal int GetNextManagerJobID()
    {
        if (Scribe.mode == LoadSaveMode.LoadingVars && !_wasLoaded)
        {
            Log.Warning("Getting next unique manager job ID during LoadingVars before Manager was loaded. Assigning a random value.");
            return Rand.Int;
        }
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            Log.Warning("Getting next unique manager job ID during saving. This may cause bugs.");
        }
        int result = _nextManagerJobID;
        _nextManagerJobID++;
        if (_nextManagerJobID == int.MaxValue)
        {
            Log.Warning("Next manager job ID is at max value. Resetting to 0. This may cause bugs.");
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
