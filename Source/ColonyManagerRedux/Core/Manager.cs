// Manager.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.Extensions;

namespace ColonyManagerRedux;

[HotSwappable]
public class Manager : MapComponent, ILoadReferenceable
{
    private readonly List<ManagerTab> _tabs;
    public List<ManagerTab> Tabs => _tabs;

    private List<ManagerTab>? _managerTabsLeft;
    private List<ManagerTab>? _managerTabsMiddle;
    private List<ManagerTab>? _managerTabsRight;
    internal int id = -1;

    private bool _wasLoaded;
    private int _nextManagerJobID;

    private bool _hasCheckedAncientDangerRect;
    private CellRect? _ancientDangerRect;

    [Obsolete(
        "The logic behind this property was entirely wrong; switch to the AncientDangerRects " +
        "property instead", true)]
    public CellRect? AncientDangerRect => _ancientDangerRect;

    private List<CellRect> _ancientDangerRects = [];
    public List<CellRect> AncientDangerRects => _ancientDangerRects;

    private readonly List<ManagerComp> _comps;

    public bool ScribeGameSpecificData { get; set; } = true;

    public Manager(Map map) : base(map)
    {
        if (map == null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        _jobTracker = new(this);

        _tabs = DefDatabase<ManagerDef>.AllDefs
            .OrderBy(m => m.order)
            .Select(m => ManagerDefMaker.MakeManagerTab(m, this))
            .ToList();

        _comps = [];
        var managerComps = DefDatabase<ManagerDef>.AllDefs
            .SelectMany(m => m.managerComps);
        foreach (var compProperties in managerComps)
        {
            ManagerComp? managerComp = null;
            try
            {
                managerComp = (ManagerComp)Activator.CreateInstance(compProperties.compClass);
                managerComp.Manager = this;
                _comps.Add(managerComp);
                managerComp.InitializeInt(compProperties);
            }
            catch (Exception ex)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    "Could not instantiate or initialize a ManagerComp: " + ex);
                if (managerComp != null)
                {
                    _comps.Remove(managerComp);
                }
            }
        }

        // if not created in SavingLoading, give yourself the ID of the map you were constructed on.
        id = map.uniqueID;
    }

    private JobTracker _jobTracker;
    public JobTracker JobTracker => _jobTracker ??= new JobTracker(this);

    internal List<ManagerTab> ManagerTabsLeft
    {
        get
        {
            _managerTabsLeft ??= _tabs.Where(tab => tab.Def.iconArea == IconArea.Left).ToList();
            return _managerTabsLeft;
        }
    }

    internal List<ManagerTab> ManagerTabsMiddle
    {
        get
        {
            _managerTabsMiddle ??=
                _tabs.Where(tab => tab.Def.iconArea == IconArea.Middle).ToList();
            return _managerTabsMiddle;
        }
    }

    internal List<ManagerTab> ManagerTabsRight
    {
        get
        {
            _managerTabsRight ??=
                _tabs.Where(tab => tab.Def.iconArea == IconArea.Right).ToList();
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

        Scribe_Values.Look(ref _hasCheckedAncientDangerRect, "hasCheckedAncientDangerRect", false);
        Scribe_Collections.Look(ref _ancientDangerRects, "ancientDangerRects", LookMode.Value);

        var exposableTabs = _tabs.OfType<IExposable>().ToList();
        Scribe_Collections.Look(ref exposableTabs, "tabs", LookMode.Deep, this);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            Scribe_Values.Look(ref _ancientDangerRect, "ancientDangerRect", null);

            foreach (var exposableTab in exposableTabs.Where(t => t != null))
            {
                var oldTab = _tabs.Select((t, i) => (t, i))
                    .SingleOrDefault(v => v.t.GetType() == exposableTab.GetType());
                if (oldTab.t != null)
                {
                    var newTab = (ManagerTab)exposableTab;
                    newTab.Def = oldTab.t.Def;
                    _tabs[oldTab.i] = newTab;
                }
            }

            _wasLoaded = true;
        }

        _jobTracker ??= new JobTracker(this);

        foreach (ManagerComp comp in _comps)
        {
            comp.PostExposeData();
        }
    }

    public override void FinalizeInit()
    {
        if (_ancientDangerRects == null)
        {
            _ancientDangerRects = [];
            CheckAncientDangerRects();

            // This might add a duplicated entry, but that's not a big deal; the logic will work
            // just fine nonetheless.
            if (_ancientDangerRect.HasValue)
            {
                ColonyManagerReduxMod.Instance.LogDebug("Transferred _ancientDangerRect value");
                _ancientDangerRects.Add(_ancientDangerRect.Value);
                _ancientDangerRect = null;
            }
            else
            {
                ColonyManagerReduxMod.Instance.LogDebug("Had no _ancientDangerRect value");
            }
        }
    }

    public override void MapComponentTick()
    {
        base.MapComponentTick();
        if (!_hasCheckedAncientDangerRect)
        {
            CheckAncientDangerRects();
        }

        // tick jobs
        foreach (var job in JobTracker.JobsOfType<ManagerJob>())
        {
            if (!job.IsSuspended)
            {
                try
                {
                    job.Tick();
                }
                catch (Exception err)
                {
                    ColonyManagerReduxMod.Instance
                        .LogException($"Suspending manager job because it errored on tick", err);
                    job.IsSuspended = true;
                    job.CausedException = err;
                }
            }
        }

        // tick tabs
        foreach (var tab in _tabs)
        {
            try
            {
                tab.Tick();
            }
            catch (Exception err)
            {
                ColonyManagerReduxMod.Instance
                    .LogException(
                        $"Tab caused exception during {nameof(ManagerTab.Tick)}", err);
            }
        }

        foreach (ManagerComp c in _comps)
        {
            try
            {
                c.CompTick();
            }
            catch (Exception err)
            {
                ColonyManagerReduxMod.Instance
                    .LogException(
                        $"ManagerComp caused exception during {nameof(ManagerComp.CompTick)}", err);
            }
        }
    }

    private void CheckAncientDangerRects()
    {
        _ancientDangerRects.AddRange(map.listerThings.GetThingsOfType<RectTrigger>()
            .Where(t => t.signalTag.StartsWith("ancientTempleApproached"))
            .Select(t => t.Rect));

        ColonyManagerReduxMod.Instance.LogDebug(
            $"_ancientDangerRects.Count = {_ancientDangerRects.Count} after " +
            "CheckAncientDangerRects");

        _hasCheckedAncientDangerRect = true;
    }

    public override void MapComponentUpdate()
    {
        base.MapComponentUpdate();

        foreach (ManagerComp c in _comps)
        {
            try
            {
                c.CompUpdate();
            }
            catch (Exception err)
            {
                ColonyManagerReduxMod.Instance
                    .LogException(
                        $"ManagerComp caused exception during {nameof(ManagerComp.CompUpdate)}",
                        err);
            }
        }
    }

    public IEnumerable<IShouldRunCondition>? TryDoWork()
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

    public T? CompOfType<T>() where T : class
    {
        return _comps?.FirstOrDefault(c => c is T) as T;
    }

    public IEnumerable<T> CompsOfType<T>() where T : class
    {
        return _comps?.Where(c => c is T).Cast<T>() ?? [];
    }
}
