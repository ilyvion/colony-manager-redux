// Manager.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024–2025 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// The main manager component for Colony Manager Redux, responsible for managing jobs,
/// tabs, and map-specific state.
/// </summary>
[HotSwappable]
public class Manager : MapComponent, ILoadReferenceable
{
    private readonly List<ManagerTab> _tabs;

    /// <summary>
    /// Gets the list of manager tabs associated with this manager instance.
    /// </summary>
    public IReadOnlyList<ManagerTab> Tabs => _tabs.AsReadOnly();
    internal int id = -1;

    private bool _wasLoaded;
    private int _nextManagerJobID;

    private bool _hasCheckedAncientDangerRect;
    private CellRect? _ancientDangerRect;

    /// <summary>
    /// (Obsolete) Gets the ancient danger rectangle for the map.
    /// This property is obsolete; use <see cref="AncientDangerRects"/> instead.
    /// </summary>
    [Obsolete(
        "The logic behind this property was entirely wrong; switch to the AncientDangerRects "
            + "property instead; this property will be removed in a future version",
        true
    )]
    public CellRect? AncientDangerRect => _ancientDangerRect;

    private List<CellRect> _ancientDangerRects = [];

    /// <summary>
    /// Gets the list of ancient danger rectangles for the map.
    /// </summary>
    public List<CellRect> AncientDangerRects => _ancientDangerRects;

    private readonly List<ManagerComp> _comps;

    /// <summary>
    /// Controls whether data specific to the current map (such as references to Things, areas, and per-map state)
    /// should be serialized (scribed). This is <c>true</c> only during normal gameplay, saving, and loading.
    /// It is <c>false</c> during import/export or inter-map transfers. This flag implies <see cref="ScribeSameGameData"/> is also <c>true</c>.
    /// </summary>
    public bool ScribeSameMapData { get; set; } = true;

    /// <summary>
    /// (Obsolete) Controls whether data specific to the current map should be serialized (scribed).
    /// Use <see cref="ScribeSameGameData"/> instead. This property will be removed in a future version.
    /// </summary>
    [Obsolete("Use ScribeSameGameData instead. This will be removed in a future version.")]
    public bool ScribeGameSpecificData
    {
        get => ScribeSameMapData;
        set => ScribeSameMapData = value;
    }

    /// <summary>
    /// Controls whether data that is valid within the same game session but across different maps
    /// (e.g., timestamps, game-unique identifiers) should be serialized (scribed). This is <c>true</c>
    /// during gameplay, saving, loading, and inter-map operations, but <c>false</c> during import/export
    /// between different game instances.
    /// </summary>
    public bool ScribeSameGameData { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="Manager"/> class for the specified map.
    /// </summary>
    /// <param name="map">The map to associate with this manager instance.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="map"/> is null.</exception>
    public Manager(Map map)
        : base(map)
    {
        if (map == null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        _jobTracker = new(this);

        _tabs =
        [
            .. DefDatabase<ManagerDef>
                .AllDefs.OrderBy(m => m.order)
                .Select(m => ManagerDefMaker.MakeManagerTab(m, this)),
        ];

        _comps = [];
        var managerComps = DefDatabase<ManagerDef>.AllDefs.SelectMany(m => m.managerComps);
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
                    "Could not instantiate or initialize a ManagerComp: " + ex
                );
                if (managerComp != null)
                {
                    _ = _comps.Remove(managerComp);
                }
            }
        }

        // if not created in SavingLoading, give yourself the ID of the map you were constructed on.
        id = map.uniqueID;
    }

    private JobTracker _jobTracker;

    /// <summary>
    /// Gets the job tracker associated with this manager instance.
    /// </summary>
    public JobTracker JobTracker => _jobTracker ??= new JobTracker(this);

    /// <inheritdoc/>
    public string GetUniqueLoadID() => $"ColonyManagerRedux_{id}";

    /// <summary>
    /// Gets the <see cref="Manager"/> instance associated with the specified map.
    /// </summary>
    /// <param name="map">The map for which to retrieve the manager.</param>
    /// <returns>The <see cref="Manager"/> instance for the given map.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="map"/> is null.</exception>
    public static Manager For(Map map) =>
        map == null ? throw new ArgumentNullException(nameof(map)) : map.GetComponent<Manager>();

    /// <summary>
    /// Implicitly converts a <see cref="Manager"/> instance to its associated <see cref="Map"/>.
    /// </summary>
    /// <param name="manager">The <see cref="Manager"/> instance to convert.</param>
    /// <returns>The <see cref="Map"/> associated with the manager, or <c>null</c> if the manager is <c>null</c>.</returns>
    public static implicit operator Map(Manager manager)
    {
        return manager?.map!;
    }

    /// <summary>
    /// Returns the <see cref="Map"/> associated with this <see cref="Manager"/> instance.
    /// </summary>
    /// <returns>The associated <see cref="Map"/>.</returns>
    public Map ToMap() => this;

    private List<IExposable> _tmpExposableTabs = [];

    /// <inheritdoc/>
    public override void ExposeData()
    {
        Scribe_Values.Look(ref id, "id", -1, true);
        Scribe_Deep.Look(ref _jobTracker, "jobStack", this);

        Scribe_Values.Look(ref _nextManagerJobID, "nextManagerJobID", 0);

        Scribe_Values.Look(ref _hasCheckedAncientDangerRect, "hasCheckedAncientDangerRect", false);
        Scribe_Collections.Look(ref _ancientDangerRects, "ancientDangerRects", LookMode.Value);

        _tmpExposableTabs.AddRange(Tabs.OfType<IExposable>());
        using var _ = new DoOnDispose(_tmpExposableTabs.Clear);
        Scribe_Collections.Look(ref _tmpExposableTabs, "tabs", LookMode.Deep, this);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            Scribe_Values.Look(ref _ancientDangerRect, "ancientDangerRect", null);

            foreach (var exposableTab in _tmpExposableTabs.Where(t => t != null))
            {
                var oldTab = Tabs.Select((t, i) => (t, i))
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

        foreach (var comp in _comps)
        {
            comp.PostExposeData();
        }
    }

    /// <inheritdoc/>
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

        foreach (var job in _jobTracker.Jobs)
        {
            job.FinalizeInit();
        }

        foreach (var comp in _comps)
        {
            comp.FinalizeInit();
        }

        // Let's retroactively finish research that should be finished for the given starting faction.
        var startingResearchTags = Faction.OfPlayer.def.startingResearchTags;
        if (startingResearchTags != null)
        {
            foreach (var startingResearchTag in startingResearchTags)
            {
                foreach (
                    var allDef in DefDatabase<ResearchProjectDef>.AllDefs.Where(r =>
                        r.tab == ManagerResearchTabDefOf.CMR_ResearchTab
                    )
                )
                {
                    if (allDef.HasTag(startingResearchTag))
                    {
                        Find.ResearchManager.FinishProject(
                            allDef,
                            doCompletionDialog: false,
                            null,
                            doCompletionLetter: false
                        );
                    }
                }
            }
        }

        _wasLoaded = true;
    }

    /// <inheritdoc/>
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
                    job.IntTick();
                }
                catch (Exception err)
                {
                    ColonyManagerReduxMod.Instance.LogException(
                        $"Suspending manager job because it errored on tick",
                        err
                    );
                    job.IsSuspended = true;
                    job.CausedException = err;
                }
            }
        }

        // tick tabs
        foreach (var tab in Tabs)
        {
            try
            {
                tab.Tick();
            }
            catch (Exception err)
            {
                ColonyManagerReduxMod.Instance.LogException(
                    $"Tab caused exception during {nameof(ManagerTab.Tick)}",
                    err
                );
            }
        }

        // tick comps
        foreach (var c in _comps)
        {
            try
            {
                c.CompTick();
            }
            catch (Exception err)
            {
                ColonyManagerReduxMod.Instance.LogException(
                    $"ManagerComp caused exception during {nameof(ManagerComp.CompTick)}",
                    err
                );
            }
        }
    }

    private void CheckAncientDangerRects()
    {
        _ancientDangerRects.AddRange(
            map.listerThings.GetThingsOfType<RectTrigger>()
                .Where(t =>
                    t.signalTag.StartsWith("ancientTempleApproached", StringComparison.Ordinal)
                )
                .Select(t => t.Rect)
        );

        ColonyManagerReduxMod.Instance.LogDebug(
            $"_ancientDangerRects.Count = {_ancientDangerRects.Count} after "
                + "CheckAncientDangerRects"
        );

        _hasCheckedAncientDangerRect = true;
    }

    /// <inheritdoc/>
    public override void MapComponentUpdate()
    {
        base.MapComponentUpdate();

        foreach (var c in _comps)
        {
            try
            {
                c.CompUpdate();
            }
            catch (Exception err)
            {
                ColonyManagerReduxMod.Instance.LogException(
                    $"ManagerComp caused exception during {nameof(ManagerComp.CompUpdate)}",
                    err
                );
            }
        }
    }

    /// <summary>
    /// Attempts to execute the next available manager job, if any.
    /// </summary>
    /// <returns>
    /// A <see cref="Coroutine"/> representing the job execution, or <c>null</c> if no job was executed.
    /// </returns>
    public Coroutine? TryDoWork() => JobTracker.TryDoNextJob();

    internal int GetNextManagerJobID()
    {
        if (Scribe.mode == LoadSaveMode.LoadingVars && !_wasLoaded)
        {
            ColonyManagerReduxMod.Instance.LogWarning(
                "Getting next unique manager job ID during LoadingVars before Manager was loaded. Assigning a random value."
            );
            return Rand.Int;
        }
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            ColonyManagerReduxMod.Instance.LogWarning(
                "Getting next unique manager job ID during saving. This may cause bugs."
            );
        }
        var result = _nextManagerJobID;
        _nextManagerJobID++;
        if (_nextManagerJobID == int.MaxValue)
        {
            ColonyManagerReduxMod.Instance.LogWarning(
                "Next manager job ID is at max value. Resetting to 0. This may cause bugs."
            );
            _nextManagerJobID = 0;
        }
        return result;
    }

    /// <summary>
    /// Returns the first manager component of the specified type, or <c>null</c> if none exists.
    /// </summary>
    /// <typeparam name="T">The type of the manager component to retrieve.</typeparam>
    /// <returns>The first component of type <typeparamref name="T"/>, or <c>null</c> if not found.</returns>
    public T? CompOfType<T>()
        where T : class => _comps?.FirstOrDefault(c => c is T) as T;

    /// <summary>
    /// Returns all manager components of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of the manager components to retrieve.</typeparam>
    /// <returns>An enumerable of components of type <typeparamref name="T"/>.</returns>
    public IEnumerable<T> CompsOfType<T>()
        where T : class => _comps?.Where(c => c is T).Cast<T>() ?? [];

    /// <summary>
    /// Purges all historical data from all manager jobs with history components.
    /// This operation cannot be undone and will clear all historical tracking data.
    /// </summary>
    public void PurgeAllHistory()
    {
        foreach (var job in _jobTracker.Jobs)
        {
            var historyComp = job.CompOfType<CompManagerJobHistory>();
            if (historyComp != null)
            {
                historyComp.History.PurgeHistory();
            }
        }
    }
}
