// ManagerJob_Mining.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Verse.AI;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
[CoroutineSettingsType]
internal sealed class ManagerJob_Mining
    : ManagerJob<ManagerSettings_Mining, ManagerJob_Mining.MiningWorkData>,
        INotifyStoneChunkMined
{
    // What GatherJobDataCoroutine decided needs to happen; ExecuteJobDataCoroutine applies it.
    // Gathering only ever decides on one of these paths per run (mirroring the branches that
    // used to early-return/yield break in the old single-phase TryDoJobCoroutine).
    internal enum MiningWorkKind
    {
        // Trigger is disabled: clean up all existing designations and stop drilling.
        CleanUpAndDisableDrills,

        // Trigger is already satisfied (or too many designations exist): remove some.
        ReduceDesignations,

        // Trigger is not yet satisfied: add new designations for the configured tasks.
        AddDesignations,

        // Job already completed; only newly discovered drills need rechecking, without a
        // full CleanUp() pass (designations were already cleaned up when the job first
        // completed, and re-running CleanUp() here would needlessly redelete them and
        // double-flick the drills gathered below).
        RecheckDrillsOnly,
    }

    /// <summary>
    /// Carries the decisions made by <see cref="GatherJobDataCoroutine"/> (which doesn't touch
    /// the game) to <see cref="ExecuteJobDataCoroutine"/> (which applies them). No field here
    /// should ever be read as a signal that a change has already happened.
    /// </summary>
    internal sealed class MiningWorkData
    {
        public MiningWorkKind Kind;

        // Kind == CleanUpAndDisableDrills or AddDesignations
        public bool ShouldEnableDrills;
        public List<(CompFlickable Flickable, ThingDef ResDef)> DrillCandidatesToFlick = [];

        // Kind == ReduceDesignations
        public List<(
            Designation Designation,
            int Yield,
            int CountAfter
        )> MineDesignationsToRemove = [];
        public List<(
            Designation Designation,
            int Yield,
            int CountAfter
        )> DeconstructDesignationsToRemove = [];
        public List<(
            Designation Designation,
            int Yield,
            int CountAfter
        )> HaulDesignationsToRemove = [];

        // Kind == AddDesignations
        public List<(
            Thing Target,
            DesignationDef Def,
            int Amount,
            int CountAfter
        )> DesignationsToAdd = [];
    }

    [CoroutineSettingsType]
    public sealed class History : HistoryWorker<ManagerJob_Mining>
    {
        [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
        public override Coroutine GetCountForHistoryChapterCoroutine(
            ManagerJob_Mining managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> count
        )
        {
            var ticksBetweenOperations =
                ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                    (Func<
                        ManagerJob_Mining,
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
                    .DesignatedCachedValue.DoUpdateIfNeeded(force: true)
                    .ResumeWhenOtherCoroutineIsCompleted();
                yield return new ResumeAfterTicks(ticksBetweenOperations);
                count.Value = managerJob.DesignatedCachedValue.Value;
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryChunks)
            {
                yield return managerJob
                    .ChunksCachedValue.DoUpdateIfNeeded(force: true)
                    .ResumeWhenOtherCoroutineIsCompleted();
                yield return new ResumeAfterTicks(ticksBetweenOperations);
                count.Value = managerJob.ChunksCachedValue.Value;
            }
            else
            {
                throw new ArgumentException($"Unexpected chapterDef value {chapterDef.defName}");
            }
        }

        public override Coroutine GetTargetForHistoryChapterCoroutine(
            ManagerJob_Mining managerJob,
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

    public enum Task
    {
        HaulChunks,
        DeconstructBuildings,
        Mine,
    }

    private const int RoofSupportGridSpacing = 5;

    internal MultiTickCachedValue<int> ChunksCachedValue { get; }
    private readonly CachedValue<ChunkProcessingKind> _chunkProductKindCachedValue = new(
        ChunkProcessingKind.Neither
    );

    internal MultiTickCachedValue<int> DesignatedCachedValue { get; }

    /// <inheritdoc/>
    public override int ExpectedAdditionalCount
    {
        get
        {
            _ = ChunksCachedValue.DoUpdateIfNeeded();
            _ = DesignatedCachedValue.DoUpdateIfNeeded();
            return ChunksCachedValue.Value + DesignatedCachedValue.Value;
        }
    }

    public HashSet<ThingDef> AllowedBuildings = [];

    public HashSet<ThingDef> AllowedMinerals = [];

    private readonly CachedValue<List<(Building building, CompDeepDrill drill)>> _cachedDeepDrills;
    private List<(Building building, CompDeepDrill drill)> DeepDrills =>
        [
            .. Manager
                .map.listerBuildings.allBuildingsColonist.Select(b =>
                {
                    var drill = b.TryGetComp<CompDeepDrill>();
                    return (b, drill);
                })
                .Where(d => d.drill != null)
                .Select(d => (d.b, d.drill)),
        ];

    public bool MineThickRoofs = true;
    public bool AllowMining = true;
    public bool TakeOwnershipOfMiningJobs;
    public bool ControlDeepDrills;
    public bool CheckRoofSupport = true;
    public bool CheckRoofSupportAdvanced;
    public bool CheckRoomDivision = true;
    public bool HaulMapChunks = true;
    public bool HaulMinedChunks = true;
    private bool _deconstructBuildings;
    public bool DeconstructAncientDangerWhenFogged;
    public Area? MiningArea;
    public bool InvertMiningArea;
    public Utilities.SyncDirection Sync = Utilities.SyncDirection.AllowedToFilter;

    private bool _mineralsLockedToMap = ColonyManagerReduxMod
        .Settings
        .NewJobsShouldBeResourceLocked;
    public bool MineralsLockedToMap
    {
        get => _mineralsLockedToMap;
        set
        {
            if (_mineralsLockedToMap != value)
            {
                _mineralsLockedToMap = value;
                AllMinerals = null; // reset cached minerals
            }
        }
    }

    private bool _buildingsLockedToMap = ColonyManagerReduxMod
        .Settings
        .NewJobsShouldBeResourceLocked;
    public bool BuildingsLockedToMap
    {
        get => _buildingsLockedToMap;
        set
        {
            if (_buildingsLockedToMap != value)
            {
                _buildingsLockedToMap = value;
                AllDeconstructibleBuildings = null; // reset cached buildings
            }
        }
    }

    [AllowNull]
    public List<ThingDef> AllMinerals
    {
        get
        {
            field ??= [.. Utilities_Mining.GetMinerals(_mineralsLockedToMap ? Manager.map : null)];
            return field;
        }
        private set;
    }

    public bool DeconstructBuildings
    {
        get => _deconstructBuildings;
        set
        {
            _deconstructBuildings = value;

            if (!SyncFilterAndAllowed || Sync != Utilities.SyncDirection.FilterToAllowed)
            {
                return;
            }

            foreach (var building in AllDeconstructibleBuildings)
            {
                if (GetMaterialsInBuilding(building).Any(Counted))
                {
                    _ = AllowedBuildings.Add(building);
                }
            }
        }
    }

    public bool SyncFilterAndAllowed = true;
    private List<Designation> _designations = [];

    [AllowNull]
    public List<ThingDef> AllDeconstructibleBuildings
    {
        get
        {
            field ??=
            [
                .. Utilities_Mining.GetDeconstructibleBuildings(
                    _buildingsLockedToMap ? Manager.map : null
                ),
            ];
            return field;
        }
        private set;
    }

    public Trigger_Threshold TriggerThreshold => (Trigger_Threshold)Trigger!;

    public List<Task> TaskPriorityOrder = [Task.HaulChunks, Task.DeconstructBuildings, Task.Mine];

    public ManagerJob_Mining(Manager manager)
        : base(manager)
    {
        _cachedDeepDrills = new(() => DeepDrills);
        ChunksCachedValue = new(0, GetCountInChunksCoroutine);
        DesignatedCachedValue = new(0, GetCountInDesignationsCoroutine);
        // populate the trigger field
        Trigger = new Trigger_Threshold(this)
        {
            AllowAnyThresholdChanged = ConfigureThresholdTriggerParentFilter,
        };
        ConfigureThresholdTriggerParentFilter();
        TriggerThreshold.SettingsChanged = Notify_ThresholdFilterChanged;
    }

    public override void PostMake()
    {
        var miningSettings = ManagerSettings;
        if (miningSettings != null)
        {
            SyncFilterAndAllowed = miningSettings.DefaultSyncFilterAndAllowed;
            AllowMining = miningSettings.DefaultAllowMining;
            TakeOwnershipOfMiningJobs = miningSettings.DefaultTakeOwnershipOfMiningJobs;
            ControlDeepDrills = miningSettings.DefaultControlDeepDrills;
            HaulMapChunks = miningSettings.DefaultHaulMapChunks;
            HaulMinedChunks = miningSettings.DefaultHaulMinedChunks;
            DeconstructBuildings = miningSettings.DefaultDeconstructBuildings;
            DeconstructAncientDangerWhenFogged =
                miningSettings.DefaultDeconstructAncientDangerWhenFogged;
            CheckRoofSupport = miningSettings.DefaultCheckRoofSupport;
            CheckRoofSupportAdvanced = miningSettings.DefaultCheckRoofSupportAdvanced;
            CheckRoomDivision = miningSettings.DefaultCheckRoomDivision;
            MineThickRoofs = miningSettings.DefaultMineThickRoofs;
            TaskPriorityOrder = miningSettings.DefaultTaskPriorityOrder;
        }
    }

    public override void PostImport()
    {
        base.PostImport();

        _ = AllowedMinerals.RemoveWhere(m => !AllMinerals.Contains(m));
        _ = AllowedBuildings.RemoveWhere(b => !AllDeconstructibleBuildings.Contains(b));
    }

    public List<Designation> Designations => [.. _designations];

    public override bool IsValid => base.IsValid && TriggerThreshold != null;

    public override IEnumerable<string> Targets =>
        AllowedMinerals.Select(pk => pk.LabelCap.Resolve());

    public override WorkTypeDef WorkTypeDef => WorkTypeDefOf.Mining;

    public static bool IsDesignatedForRemoval(Building building, Map map) =>
        IsDesignatedForRemoval(
            map.designationManager.DesignationOn(building)?.def,
            DesignationDefOf.Mine,
            DesignationDefOf.Deconstruct
        );

    /// <summary>
    /// Same as <see cref="IsDesignatedForRemoval(Building, Map)"/>, but with the actual
    /// designation def (if any) and the "removal" designation defs passed in directly so this
    /// is unit-testable without a live <see cref="Map"/>/<see cref="DesignationManager"/>.
    /// </summary>
    internal static bool IsDesignatedForRemoval(
        DesignationDef? designationOn,
        DesignationDef mine,
        DesignationDef deconstruct
    ) => designationOn != null && (designationOn == mine || designationOn == deconstruct);

    // largely copypasta from RoofCollapseUtility.WithinRangeOfRoofHolder
    // TODO: PERFORMANCE; maintain a cellgrid of 'safe' supported areas.
    private static bool WouldCollapseIfSupportDestroyed(IntVec3 position, IntVec3 support, Map map)
    {
        if (!position.InBounds(map) || !position.Roofed(map))
        {
            return false;
        }

        // cell indexes and buildings on map indexed by cellIndex
        var cellIndices = map.cellIndices;
        var innerArray = map.edificeGrid.InnerArray;

        for (var i = 0; i < RoofCollapseUtility.RoofSupportRadialCellsCount; i++)
        {
            var candidate = position + GenRadial.RadialPattern[i];
            if (candidate != support && candidate.InBounds(map))
            {
                var building = innerArray[cellIndices.CellToIndex(candidate)];
#if DEBUG
                map.debugDrawer.FlashCell(
                    candidate,
                    DebugSolidColorMats.MaterialOf(new Color(0f, 0f, 1f, .1f)),
                    ".",
                    500
                );
#endif
                if (
                    building != null
                    && building.def.holdsRoof
                    && !IsDesignatedForRemoval(building, map)
                )
                {
#if DEBUG
                    map.debugDrawer.FlashCell(
                        candidate,
                        DebugSolidColorMats.MaterialOf(new Color(0f, 1f, 0f, .1f)),
                        "!",
                        500
                    );
                    map.debugDrawer.FlashCell(
                        position,
                        DebugSolidColorMats.MaterialOf(new Color(0f, 1f, 0f, .1f)),
                        "V",
                        500
                    );
#endif
                    return false;
                }
            }
        }
#if DEBUG
        map.debugDrawer.FlashCell(position, DebugSolidColorMats.MaterialOf(Color.red), "X");
#endif
        return true;
    }

    private void AddDesignation(Thing target, DesignationDef designationDef) =>
        AddDesignation(new Designation(target, designationDef));

    private void AddDesignation(Designation designation)
    {
        var designationManager = Manager.map.designationManager;
        if (
            designation.def.targetType == TargetType.Thing
            && !designationManager.HasMapDesignationOn(designation.target.Thing)
        )
        {
            designationManager.AddDesignation(designation);
        }
        else if (
            designation.def.targetType == TargetType.Cell
            && !designationManager.HasMapDesignationAt(designation.target.Cell)
        )
        {
            designationManager.AddDesignation(designation);
        }
        _designations.Add(designation);
    }

    [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
    public Coroutine AddRelevantGameDesignations(ManagerLog? jobLog = null)
    {
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                AddRelevantGameDesignations
            );

        var addedMineCount = 0;
        var addedDeconstructCount = 0;
        var addedHaulCount = 0;

        if (TakeOwnershipOfMiningJobs)
        {
            foreach (
                var des in Manager
                    .map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Mine)
                    .Except(_designations)
                    .Where(des => IsValidMiningTarget(des.target, true))
            )
            {
                addedMineCount++;
                AddDesignation(des);
            }
            yield return new ResumeAfterTicks(ticksBetweenOperations);
        }

        foreach (
            var des in Manager
                .map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Deconstruct)
                .Except(_designations)
                .Where(des => IsValidDeconstructionTarget(des.target, true))
        )
        {
            addedDeconstructCount++;
            AddDesignation(des);
        }
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        foreach (
            var des in Manager
                .map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Haul)
                .Except(_designations)
                .Where(des =>
                    des.target.HasThing && des.target.Thing.def.GetChunkProducts().Any(Counted)
                )
        )
        {
            addedHaulCount++;
            AddDesignation(des);
        }

        if (addedMineCount > 0 || addedDeconstructCount > 0 || addedHaulCount > 0)
        {
            jobLog?.AddDetail(
                "ColonyManagerRedux.Mining.Logs.AddRelevantGameDesignations".Translate(
                    addedMineCount,
                    addedDeconstructCount,
                    addedHaulCount
                )
            );
        }
    }

    public bool Allowed(ThingDef? thingDef) =>
        thingDef != null && (AllowedMineral(thingDef) || AllowedBuilding(thingDef));

    public bool AllowedBuilding(ThingDef? thingDef) =>
        thingDef != null && AllowedBuildings.Contains(thingDef);

    public bool AllowedMineral(ThingDef? thingDef) =>
        thingDef != null && AllowedMinerals.Contains(thingDef);

    public override void CleanUp(ManagerLog? jobLog)
    {
        CleanDeadDesignations(_designations, null, jobLog);

        var originalCount = _designations.Count;

        // cancel outstanding designation
        foreach (var designation in _designations)
        {
            designation.Delete();
        }

        // clear the list completely
        _designations.Clear();

        var newCount = _designations.Count;
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

        if (ControlDeepDrills)
        {
            var drillData = new MiningWorkData();
            PlanDeepDrillUpdates(jobLog, null, drillData);
            // Immediately, synchronously apply the plan: CleanUp() is used outside the
            // gather/execute coroutine flow (e.g. when a job is deleted), so there's no
            // later execute phase to defer to.
            foreach (var _ in ExecuteDrillFlicks(jobLog, drillData, null)) { }
        }
    }

    public bool Counted(ThingDefCountClass thingDefCount) => Counted(thingDefCount.thingDef);

    public bool Counted(ThingDef thingDef) => TriggerThreshold.ThresholdFilter.Allows(thingDef);

    public string DesignationLabel(Designation designation)
    {
        if (designation.def == DesignationDefOf.Deconstruct)
        {
            var building = (Building)designation.target.Thing;
            var buildingCounts = GetCountsInBuilding(building);
            if (buildingCounts.Count > 1)
            {
                return "ColonyManagerRedux.Job.DesignationLabelMulti".Translate(
                    building.LabelCap,
                    Distance(building, Manager.map.GetBaseCenter())
                        .ToString("F0", CultureInfo.InvariantCulture),
                    buildingCounts.Join(tc => $"{tc.count}x {tc.thingDef.LabelCap}", "\n- ")
                );
            }
            else
            {
                var buildingCount = buildingCounts[0];
                return "ColonyManagerRedux.Job.DesignationLabel".Translate(
                    building.LabelCap,
                    Distance(building, Manager.map.GetBaseCenter())
                        .ToString("F0", CultureInfo.InvariantCulture),
                    buildingCount.count,
                    buildingCount.thingDef.LabelCap
                );
            }
        }

        if (designation.def == DesignationDefOf.Mine)
        {
            var mineable = designation.target.Cell.GetFirstMineable(Manager.map);
            return "ColonyManagerRedux.Job.DesignationLabel".Translate(
                mineable.LabelCap,
                Distance(mineable, Manager.map.GetBaseCenter())
                    .ToString("F0", CultureInfo.InvariantCulture),
                GetCountInMineral(mineable),
                GetMaterialsInMineral(mineable.def)?.First().LabelCap ?? "?"
            );
        }

        if (designation.def == DesignationDefOf.Haul && designation.target.HasThing)
        {
            var thing = designation.target.Thing;
            return "ColonyManagerRedux.Job.DesignationLabel".Translate(
                thing.LabelCap,
                Distance(thing, Manager.map.GetBaseCenter())
                    .ToString("F0", CultureInfo.InvariantCulture),
                GetCountInChunk(thing),
                thing.def.GetChunkProducts().First().thingDef.LabelCap
            );
        }

        return string.Empty;
    }

    private string? _tmpMiningAreaLabel;

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Collections.Look(ref AllowedMinerals, "allowedMinerals", LookMode.Def);
        Scribe_Collections.Look(ref AllowedBuildings, "allowedBuildings", LookMode.Def);
        Scribe_Values.Look(
            ref _mineralsLockedToMap,
            "mineralsLockedToMap",
            ColonyManagerReduxMod.Settings.NewJobsShouldBeResourceLocked
        );
        Scribe_Values.Look(
            ref _buildingsLockedToMap,
            "buildingsLockedToMap",
            ColonyManagerReduxMod.Settings.NewJobsShouldBeResourceLocked
        );
        Scribe_Values.Look(ref SyncFilterAndAllowed, "syncFilterAndAllowed", true);
        Scribe_Values.Look(ref HaulMapChunks, "haulMapChunks", true);
        Scribe_Values.Look(ref HaulMinedChunks, "haulMinedChunks", true);
        Scribe_Values.Look(ref _deconstructBuildings, "deconstructBuildings", false);
        Scribe_Values.Look(
            ref DeconstructAncientDangerWhenFogged,
            "deconstructAncientDangerWhenFogged",
            false
        );
        Scribe_Values.Look(ref CheckRoofSupport, "checkRoofSupport", true);
        Scribe_Values.Look(ref CheckRoofSupportAdvanced, "checkRoofSupportAdvanced");
        Scribe_Values.Look(ref CheckRoomDivision, "checkRoomDivision", true);
        Scribe_Values.Look(ref MineThickRoofs, "mineThickRoofs", true);
        Scribe_Values.Look(ref AllowMining, "allowMining", true);
        Scribe_Values.Look(ref TakeOwnershipOfMiningJobs, "takeOwnershipOfMiningJobs", false);
        Scribe_Values.Look(ref ControlDeepDrills, "controlDeepDrills", false);
        Scribe_Values.Look(ref InvertMiningArea, "invertMiningArea");
        Scribe_Collections.Look(ref TaskPriorityOrder, "taskPriorityOrder", LookMode.Value);

        if (Manager.ScribeSameMapData)
        {
            Scribe_References.Look(ref MiningArea, "miningArea");

            Utilities.Scribe_Designations(ref _designations, Manager);
        }
        else
        {
            Utilities.Scribe_AreaByLabel(
                ref MiningArea,
                ref _tmpMiningAreaLabel,
                "miningArea",
                Manager.map.areaManager
            );
        }

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            ConfigureThresholdTriggerParentFilter();
            TriggerThreshold.SettingsChanged = Notify_ThresholdFilterChanged;
            TriggerThreshold.AllowAnyThresholdChanged = ConfigureThresholdTriggerParentFilter;

            TaskPriorityOrder = Utilities_Mining.EnsureAllEnumValuesPresent(
                TaskPriorityOrder ?? ManagerSettings.DefaultTaskPriorityOrder,
                [.. Enum.GetValues(typeof(Task)).Cast<Task>()]
            );
        }
    }

    private static readonly List<ThingDefCountClass> _tmpBuildingCounts = [];

    public static List<ThingDefCountClass> GetCountsInBuilding(Building? building)
    {
        _tmpBuildingCounts.Clear();

        var def = building?.def;
        if (def == null || building == null)
        {
            return _tmpBuildingCounts;
        }

        foreach (var item in def.CostListAdjusted(building.Stuff, false))
        {
            var item2 = new ThingDefCountClass(item.thingDef, item.count);
            item2.count = ClampScaledCount(
                GenMath.RoundRandom(item2.count * def.resourcesFractionWhenDeconstructed),
                item2.count
            );

            if (item2.count != 0)
            {
                _tmpBuildingCounts.Add(item2);
            }
        }

        return _tmpBuildingCounts;
    }

    internal static int ClampScaledCount(int roundedScaledCount, int originalCount) =>
        Mathf.Min(roundedScaledCount, originalCount);

    public int GetCountInBuilding(Building? building)
    {
        var def = building?.def;
        if (def == null || building == null)
        {
            return 0;
        }

        var items = def.CostListAdjusted(building.Stuff)
            .Select(tc => (tc.count, counted: Counted(tc)));
        return SumScaledCounts(items, def.resourcesFractionWhenDeconstructed);
    }

    internal static int SumScaledCounts(
        IEnumerable<(int count, bool counted)> items,
        float fraction
    ) => Mathf.RoundToInt(items.Where(i => i.counted).Sum(i => i.count * fraction));

    public int GetCountInChunk(Thing chunk) => GetCountInChunk(chunk.def);

    public int GetCountInChunk(ThingDef chunk) =>
        chunk.butcherProducts.NullOrEmpty() && chunk.smeltProducts.NullOrEmpty()
            ? 0
            : chunk.GetChunkProducts().Where(Counted).Sum(tc => tc.count);

    public ChunkProcessingKind GetChunkProductKind()
    {
        if (_chunkProductKindCachedValue.TryGetValue(out var chunkProductKind))
        {
            return chunkProductKind;
        }

        chunkProductKind = AccumulateChunkProcessingKind(
            DefDatabase<ThingDef>
                .AllDefs.Where(t =>
                    t.IsChunk()
                    && (
                        (t.butcherProducts?.Any(Counted) ?? false)
                        || (t.smeltProducts?.Any(Counted) ?? false)
                    )
                )
                .Select(t => (t.butcherProducts != null, t.smeltProducts != null))
        );

        _ = _chunkProductKindCachedValue.Update(chunkProductKind);
        return chunkProductKind;
    }

    internal static ChunkProcessingKind AccumulateChunkProcessingKind(
        IEnumerable<(bool hasButcherProducts, bool hasSmeltProducts)> chunks
    )
    {
        var chunkProductKind = ChunkProcessingKind.Neither;
        foreach (var (hasButcherProducts, hasSmeltProducts) in chunks)
        {
            if (hasButcherProducts)
            {
                chunkProductKind |= ChunkProcessingKind.Stonecutting;
            }
            if (hasSmeltProducts)
            {
                chunkProductKind |= ChunkProcessingKind.Smelting;
            }

            if (chunkProductKind == ChunkProcessingKind.Both)
            {
                break;
            }
        }

        return chunkProductKind;
    }

    private readonly List<Thing> _tmpAllThings = [];

    [CoroutineSettingsMethod]
    private Coroutine GetCountInChunksCoroutine(AnyBoxed<int> count)
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            GetCountInChunksCoroutine
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                GetCountInChunksCoroutine
            );

        _tmpAllThings.AddRange(Manager.map.listerThings.AllThings);
        using var _ = new DoOnDispose(_tmpAllThings.Clear);

        foreach (
            var (chunk, i) in _tmpAllThings
                .Where(t => t.def.IsChunk() && t.IsInAnyStorage())
                .Select((c, i) => (c, i))
        )
        {
            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }

            if (chunk.IsForbidden(Faction.OfPlayer))
            {
                continue;
            }

            count.Value += GetCountInChunk(chunk);
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine GetCountInDesignationsCoroutine(AnyBoxed<int> count)
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            GetCountInDesignationsCoroutine
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                GetCountInDesignationsCoroutine
            );

        Dictionary<ThingDef, int> mineralCounts = [];
        for (var i = 0; i < _designations.Count; i++)
        {
            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }

            var des = _designations[i];

            if (des.def == DesignationDefOf.Deconstruct)
            {
                count.Value += GetCountInBuilding(des.target.Thing as Building);
            }
            else if (des.def == DesignationDefOf.Mine && des.target.Cell.IsValid)
            {
                var mineralDef = Manager
                    .map.thingGrid.ThingsListAtFast(des.target.Cell)
                    .FirstOrDefault()
                    ?.def;
                if (mineralDef == null || !Allowed(mineralDef))
                {
                    continue;
                }
                if (!mineralCounts.TryAdd(mineralDef, 1))
                {
                    mineralCounts[mineralDef]++;
                }
            }
            else if (des.def == DesignationDefOf.Haul && des.target.HasThing)
            {
                count.Value += GetCountInChunk(des.target.Thing);
            }
        }

        count.Value += mineralCounts.Sum(kv => GetCountInMineral(kv.Key) * kv.Value);
    }

    public int GetCountInMineral(Mineable rock) => GetCountInMineral(rock.def);

    public int GetCountInMineral(ThingDef? rock)
    {
        var resource = rock?.building?.mineableThing;
        if (resource == null || rock == null)
        {
            return 0;
        }

        // stone chunks
        if (resource.IsChunk())
        {
            return CalculateMineralYield(
                isChunk: true,
                GetCountInChunk(resource),
                rock.building.mineableDropChance,
                mineableYield: 0,
                mineYieldFactor: 0,
                counted: false
            );
        }

        // metals
        return CalculateMineralYield(
            isChunk: false,
            chunkCount: 0,
            rock.building.mineableDropChance,
            rock.building.mineableYield,
            Find.Storyteller.difficulty.mineYieldFactor,
            Counted(resource)
        );
    }

    internal static int CalculateMineralYield(
        bool isChunk,
        int chunkCount,
        float dropChance,
        float mineableYield,
        float mineYieldFactor,
        bool counted
    ) =>
        isChunk ? (int)(chunkCount * dropChance)
        : counted ? (int)(mineableYield * mineYieldFactor * dropChance)
        : 0;

    public static IEnumerable<ThingDef> GetMaterialsInBuilding(ThingDef building)
    {
        if (building == null)
        {
            return [];
        }

        var baseCosts = building.costList.NullOrEmpty()
            ? []
            : building.costList.Select(tc => tc.thingDef);

        return baseCosts.Concat(GenStuff.AllowedStuffsFor(building));
    }

    public static IEnumerable<ThingDef> GetMaterialsInChunk(ThingDef chunk) =>
        chunk.GetChunkProducts().Select(tc => tc.thingDef);

    private readonly CachedValues<ThingDef, List<ThingDef>> _materialsInMineralCache = new();

    public List<ThingDef> GetMaterialsInMineral(ThingDef mineral)
    {
        if (!_materialsInMineralCache.TryGetValue(mineral, out var materials))
        {
            _materialsInMineralCache.Add(mineral, () => UpdateMaterialsInMineral(mineral));
            materials = _materialsInMineralCache[mineral];
        }
        return materials!;

        static List<ThingDef> UpdateMaterialsInMineral(ThingDef mineral)
        {
            var resource = mineral.building?.mineableThing;
            if (resource == null)
            {
                return [];
            }

            // stone chunks
            if (resource.IsChunk())
            {
                return [.. GetMaterialsInChunk(resource)];
            }

            // metals
            List<ThingDef> list = [resource];
            return list;
        }
    }

    public bool IsARoofSupport_Advanced(Building building)
    {
        if (!CheckRoofSupport || !CheckRoofSupportAdvanced)
        {
            return false;
        }

        // check if any cell in roofing range would collapse if this cell were to be removed
        for (var i = RoofCollapseUtility.RoofSupportRadialCellsCount - 1; i >= 0; i--)
        {
            if (
                WouldCollapseIfSupportDestroyed(
                    GenRadial.RadialPattern[i] + building.Position,
                    building.Position,
                    Manager.map
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    public bool IsARoofSupport_Basic(Building building)
    {
        if (!CheckRoofSupport || CheckRoofSupportAdvanced)
        {
            return false;
        }

        // simply check location, leaving a grid of pillars
        return IsARoofSupport_Basic(building.Position);
    }

    public static bool IsARoofSupport_Basic(IntVec3 cell) =>
        cell.x % RoofSupportGridSpacing == 0 && cell.z % RoofSupportGridSpacing == 0;

    private const float MaxPathCost = 500f;

    // Room-divider status rarely changes tick-to-tick (it only changes when
    // walls/doors are built or removed nearby), but this method is called
    // repeatedly for the same cells across scan passes. A short-lived,
    // per-cell TTL cache avoids re-running up to 28 pathfinds per candidate
    // on every pass, mirroring the TTL caching used elsewhere (e.g.
    // Utilities_Livestock's per-pawn caches) rather than a fully
    // invalidation-tracked grid.
    private const int RoomDividerCacheTicks = 2000;
    private readonly Dictionary<IntVec3, CachedValue<bool>> _roomDividerCache = [];

    public bool IsARoomDivider(Thing target)
    {
        if (!CheckRoomDivision)
        {
            return false;
        }

        var position = target.Position;
        if (
            _roomDividerCache.TryGetValue(position, out var cachedValue)
            && cachedValue.TryGetValue(out var cached)
        )
        {
            return cached;
        }

        var result = ComputeIsARoomDivider(position);

        if (cachedValue != null)
        {
            _ = cachedValue.Update(result);
        }
        else
        {
            _roomDividerCache.Add(position, new CachedValue<bool>(result, RoomDividerCacheTicks));
        }

        return result;
    }

    private bool ComputeIsARoomDivider(IntVec3 position)
    {
        var adjacent = GenAdjFast
            .AdjacentCells8Way(position)
            .Where(c =>
                c.InBounds(Manager.map) && !c.Fogged(Manager.map) && !c.Impassable(Manager.map)
            )
            .ToArray();

        // check if there are more than two rooms in the surrounding cells.
        var rooms = adjacent.Select(c => c.GetRoom(Manager.map)).Where(r => r != null).Distinct();

        if (rooms.Count() >= 2)
        {
            return true;
        }

        // check if any adjacent region is more than x regions from any other region
        for (var i = 0; i < adjacent.Length; i++)
        {
            for (var j = i + 1; j < adjacent.Length; j++)
            {
                var path = Manager.map.pathFinder.FindPathCmr(
                    adjacent[i],
                    adjacent[j],
                    TraverseParms.For(TraverseMode.NoPassClosedDoors, Danger.Some)
                );
                var cost = path.TotalCost;
                path.ReleaseToPool();

                if (cost > MaxPathCost)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool IsAllowedToMineRoofAt(Thing target) =>
        MineThickRoofs || (!target.Map.roofGrid.RoofAt(target.Position)?.isThickRoof ?? true);

    public bool IsInAllowedArea(Thing target) =>
        Utilities.IsInAllowedArea(MiningArea, target.Position, InvertMiningArea);

    public bool IsRelevantDeconstructionTarget(Building target) =>
        target.def.building.IsDeconstructible
        && target.def.resourcesFractionWhenDeconstructed > 0
        && target
            .def.CostListAdjusted(target.Stuff)
            .Any(tc => TriggerThreshold.ThresholdFilter.Allows(tc.thingDef));

    public bool IsRelevantMiningTarget(Mineable target) => GetCountInMineral(target) > 0;

    public bool IsValidDeconstructionTarget(Building target, bool includeDesignated = false)
    {
        if (target == null)
        {
            return false;
        }

        var designation = Manager.map.designationManager.DesignationOn(target);

        return target.Spawned
            // not ours
            && target.Faction != Faction.OfPlayer
            && (
                includeDesignated
                    ? (designation == null || designation.def == DesignationDefOf.Deconstruct)
                    : designation == null
            )
            // allowed
            && !target.IsForbidden(Faction.OfPlayer)
            && AllowedBuilding(target.def)
            // drops things we want
            && IsRelevantDeconstructionTarget(target)
            // in allowed area & reachable
            && IsInAllowedArea(target)
            && IsReachable(target, PathEndMode.InteractionCell)
            // doesn't create safety hazards
            && !IsARoofSupport_Basic(target)
            && !IsARoomDivider(target);
    }

    public bool IsValidDeconstructionTarget(
        LocalTargetInfo target,
        bool includeDesignated = false
    ) =>
        target.HasThing
        && target.IsValid
        && target.Thing is Building building
        && IsValidDeconstructionTarget(building, includeDesignated);

    public bool IsValidMiningTarget(LocalTargetInfo target, bool includeDesignated = false) =>
        target.IsValid
        && target.Cell.GetFirstThing<Mineable>(Manager.map) is Mineable mineable
        && IsValidMiningTarget(mineable, includeDesignated);

    public bool IsValidMiningTarget(Mineable? target, bool includeDesignated = false)
    {
        if (target == null)
        {
            return false;
        }

        var designation =
            Manager.map.designationManager.DesignationOn(target)
            ?? Manager.map.designationManager.DesignationAt(target.Position, DesignationDefOf.Mine);
        return target.def.mineable
            // allowed
            && AllowedMineral(target.def)
            // discovered
            // NOTE: also in IsReachable, but we expect a lot of fogged tiles, so move this check up a bit.
            && !target.Position.Fogged(Manager.map)
            && (
                includeDesignated
                    ? (designation == null || designation.def == DesignationDefOf.Mine)
                    : designation == null
            )
            // matches settings
            && IsInAllowedArea(target)
            && IsRelevantMiningTarget(target)
            && !IsARoomDivider(target)
            // note, is true if advanced checking is enabled - checks will then be done before designating
            && !IsARoofSupport_Basic(target)
            && IsAllowedToMineRoofAt(target)
            // can be reached
            && IsReachable(target, PathEndMode.InteractionCell);
    }

    public void Notify_ThresholdFilterChanged()
    {
        ColonyManagerReduxMod.Instance.LogDebug("Threshold changed.");

        _chunkProductKindCachedValue.Invalidate();

        if (!SyncFilterAndAllowed || Sync == Utilities.SyncDirection.AllowedToFilter)
        {
            return;
        }

        foreach (var building in AllDeconstructibleBuildings)
        {
            _ = GetMaterialsInBuilding(building).Any(TriggerThreshold.ThresholdFilter.Allows)
                ? AllowedBuildings.Add(building)
                : AllowedBuildings.Remove(building);
        }

        foreach (var mineral in AllMinerals)
        {
            _ = GetMaterialsInMineral(mineral).Any(TriggerThreshold.ThresholdFilter.Allows)
                ? AllowedMinerals.Add(mineral)
                : AllowedMinerals.Remove(mineral);
        }
        Notify_TargetsChanged();
    }

    public void RefreshAllBuildingsAndMinerals()
    {
        ColonyManagerReduxMod.Instance.LogDebug("Refreshing all deconstructible buildings");

        AllDeconstructibleBuildings = null;
        AllMinerals = null;

        ConfigureThresholdTriggerParentFilter();
    }

    public void SetBuildingAllowed(ThingDef building, bool allow, bool sync = true)
    {
        _ = allow ? AllowedBuildings.Add(building) : AllowedBuildings.Remove(building);
        Notify_TargetsChanged();

        if (SyncFilterAndAllowed && sync)
        {
            Sync = Utilities.SyncDirection.AllowedToFilter;

            foreach (var material in GetMaterialsInBuilding(building))
            {
                var setAllow =
                    AllowedBuildings.Any(b => GetMaterialsInBuilding(b).Contains(material))
                    || AllowedMinerals.Any(m => GetMaterialsInMineral(m).Contains(material));
                TriggerThreshold.ThresholdFilter.SetAllow(material, setAllow);
            }
        }
    }

    public void SetAllowMineral(ThingDef mineral, bool allow, bool sync = true)
    {
        _ = allow ? AllowedMinerals.Add(mineral) : AllowedMinerals.Remove(mineral);
        Notify_TargetsChanged();

        if (SyncFilterAndAllowed && sync)
        {
            Sync = Utilities.SyncDirection.AllowedToFilter;

            foreach (var material in GetMaterialsInMineral(mineral))
            {
                var setAllow =
                    AllowedBuildings.Any(b => GetMaterialsInBuilding(b).Contains(material))
                    || AllowedMinerals.Any(m => GetMaterialsInMineral(m).Contains(material));
                TriggerThreshold.ThresholdFilter.SetAllow(material, setAllow);
            }
        }
    }

    [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
    protected override Coroutine GatherJobDataCoroutine(
        ManagerLog jobLog,
        AnyBoxed<MiningWorkData?> data
    )
    {
        if (!TriggerThreshold.State)
        {
            if (JobState != ManagerJobState.Completed)
            {
                JobState = ManagerJobState.Completed;
                jobLog.AddDetail("ColonyManagerRedux.Logs.JobCompleted".Translate());

                data.Value = new MiningWorkData { Kind = MiningWorkKind.CleanUpAndDisableDrills };
            }
            else if (ControlDeepDrills)
            {
                // The quota is still met, but a deep drill placed on a new
                // deposit after the job completed wouldn't have been caught
                // by the cleanup above, so keep planning to flick off any
                // newly discovered drills here as well.
                var drillData = new MiningWorkData { Kind = MiningWorkKind.RecheckDrillsOnly };
                PlanDeepDrillUpdates(jobLog, null, drillData);
                data.Value = drillData;
            }
            yield break;
        }
        else
        {
            JobState = ManagerJobState.Active;
        }

        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                (Func<ManagerLog, AnyBoxed<MiningWorkData?>, Coroutine>)GatherJobDataCoroutine
            );

        // Resync our own bookkeeping against designations that have disappeared or that
        // already exist in the game unbeknownst to us; this doesn't change anything in the
        // game itself, so it's safe to do while gathering.
        CleanDeadDesignations(_designations, null, jobLog);
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        yield return AddRelevantGameDesignations(jobLog).ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        // update counts
        yield return ChunksCachedValue
            .DoUpdateIfNeeded(force: true)
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);
        yield return DesignatedCachedValue
            .DoUpdateIfNeeded(force: true)
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        // designate work until trigger is met.
        var count = new Boxed<int>(
            TriggerThreshold.GetCurrentCount()
                + ChunksCachedValue.Value
                + DesignatedCachedValue.Value
        );

        // Plan drill updates once, against the not-yet-decremented count, exactly like the
        // pre-two-phase-split code did before branching on whether to reduce or add
        // designations below — the drill on/off decision only cares whether the trigger's
        // target is currently met, not which branch that leads to.
        var workData = new MiningWorkData();
        if (ControlDeepDrills)
        {
            ColonyManagerReduxMod.Instance.LogVerboseMessage("Planning deep drill updates");
            PlanDeepDrillUpdates(jobLog, count, workData);
        }
        else
        {
            ColonyManagerReduxMod.Instance.LogVerboseMessage("Not updating deep drills");
        }

        if (
            TriggerThreshold.DoesCountMeetTarget(count)
            || ColonyManagerReduxMod.Settings.ShouldRemoveMoreDesignations(_designations.Count)
        )
        {
            workData.Kind = MiningWorkKind.ReduceDesignations;

            yield return PlanReduceDesignations(jobLog, count, workData)
                .ResumeWhenOtherCoroutineIsCompleted();

            data.Value = workData;
            yield break;
        }

        workData.Kind = MiningWorkKind.AddDesignations;

        if (!HaulMapChunks && !DeconstructBuildings && !AllowMining)
        {
            data.Value = workData;
            yield break;
        }

        if (!ColonyManagerReduxMod.Settings.CanAddMoreDesignations(_designations.Count))
        {
            jobLog.AddDetail(
                "ColonyManagerRedux.Logs.CantAddMoreDesignations".Translate(
                    "ColonyManagerRedux.Mining.Logs.Rocks".Translate(),
                    Def.label
                )
            );
            data.Value = workData;
            yield break;
        }

        jobLog.AddDetail(
            "ColonyManagerRedux.Logs.CurrentCount".Translate(
                count.Value,
                TriggerThreshold.TargetCount
            )
        );

        yield return new ResumeAfterTicks(ticksBetweenOperations);

        for (var i = 0; i < TaskPriorityOrder.Count; i++)
        {
            var task = TaskPriorityOrder[i];
            var isLastTask = i == TaskPriorityOrder.Count - 1;
            switch (task)
            {
                case Task.HaulChunks:
                    if (HaulMapChunks)
                    {
                        yield return PlanHaulChunks(count, workData)
                            .ResumeWhenOtherCoroutineIsCompleted();
                    }
                    else
                    {
                        continue;
                    }
                    break;
                case Task.DeconstructBuildings:
                    if (DeconstructBuildings)
                    {
                        yield return PlanDeconstructBuildings(jobLog, count, workData)
                            .ResumeWhenOtherCoroutineIsCompleted();
                    }
                    else
                    {
                        continue;
                    }
                    break;
                case Task.Mine:
                    if (AllowMining)
                    {
                        yield return PlanMineResources(count, workData)
                            .ResumeWhenOtherCoroutineIsCompleted();
                    }
                    else
                    {
                        continue;
                    }
                    break;
                default:
                    ColonyManagerReduxMod.Instance.LogError($"Unknown task {task}");
                    break;
            }

            if (
                !isLastTask
                && !ColonyManagerReduxMod.Settings.CanAddMoreDesignations(
                    _designations.Count + workData.DesignationsToAdd.Count
                )
            )
            {
                jobLog.AddDetail(
                    "ColonyManagerRedux.Logs.CantAddMoreDesignations".Translate(
                        "ColonyManagerRedux.Mining.Logs.Rocks".Translate(),
                        Def.label
                    )
                );
                break;
            }
        }

        data.Value = workData;
    }

    /// <summary>
    /// Decides which deep drills need to be flicked on/off to reach the desired state, without
    /// actually touching them. <paramref name="count"/> being <see langword="null"/> means the
    /// trigger is disabled and drills should always be planned to flick off.
    /// </summary>
    private void PlanDeepDrillUpdates(ManagerLog? jobLog, Boxed<int>? count, MiningWorkData data)
    {
        var drills = _cachedDeepDrills.Value;
        ColonyManagerReduxMod.Instance.LogVerboseMessage(
            $"Found {drills.Count} deep drills on map {Manager.map.Tile}"
        );
        data.ShouldEnableDrills =
            count != null && !TriggerThreshold.DoesCountMeetTarget(count.Value);

        foreach (var (building, drill) in drills.Where(d => !d.building.DestroyedOrNull()))
        {
            _ = drill.GetNextResource(out var resDef, out _, out _);
            if (!Counted(resDef) && !resDef.GetChunkProducts().Any(Counted))
            {
                ColonyManagerReduxMod.Instance.LogVerboseMessage(
                    $"Skipping deep drill {building.Label} - not counted because {resDef.label} is not counted"
                );
                continue;
            }

            if (building.TryGetComp<CompForbiddable>(out var forbiddable) && forbiddable.Forbidden)
            {
                ColonyManagerReduxMod.Instance.LogVerboseMessage(
                    $"Skipping deep drill {building.Label} - forbidden"
                );
                jobLog?.AddDetail(
                    "ColonyManagerRedux.Mining.Logs.SkipForbiddenDeepDrill".Translate(resDef.label),
                    building
                );

                continue;
            }

            if (building.TryGetComp<CompFlickable>(out var flickable))
            {
                data.DrillCandidatesToFlick.Add((flickable, resDef));
            }
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine PlanReduceDesignations(
        ManagerLog jobLog,
        Boxed<int> count,
        MiningWorkData data
    )
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            PlanReduceDesignations
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                PlanReduceDesignations
            );

        List<Designation> sortedMineDesignations = [];
        yield return GetThingsSorted(
                _designations.Where(d =>
                    d.def == DesignationDefOf.Mine
                    && d.target.IsValid
                    && d.target.Cell.GetFirstThing<Mineable>(Manager.map) is not null
                ),
                sortedMineDesignations,
                _ => true,
                (m, d) => -GetCountInMineral(m) / d,
                d => d.target.Cell.GetFirstThing<Mineable>(Manager.map)
            )
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        // Removal is only planned here, not applied, so _designations.Count itself never
        // shrinks as we go (unlike the pre-two-phase-split code, which deleted designations
        // as it iterated). Track how many we've planned to remove so far and subtract that
        // from _designations.Count to get the same shrinking "effective count" the old
        // ShouldRemoveMoreDesignations checks relied on to stop once back under the cap.
        var plannedRemovalCount = 0;

        // plan reducing designations until we're just above target
        for (var i = 0; i < sortedMineDesignations.Count; i++)
        {
            var designation = sortedMineDesignations[i];
            var mineable = designation.target.Cell.GetFirstThing<Mineable>(Manager.map);
            var mineableYield = GetCountInMineral(mineable);
            count.Value -= mineableYield;
            if (
                TriggerThreshold.DoesCountMeetTarget(count)
                || ColonyManagerReduxMod.Settings.ShouldRemoveMoreDesignations(
                    _designations.Count - plannedRemovalCount
                )
            )
            {
                data.MineDesignationsToRemove.Add((designation, mineableYield, count.Value));
                plannedRemovalCount++;
            }
            else
            {
                break;
            }

            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        if (
            TriggerThreshold.DoesCountMeetTarget(count)
            || ColonyManagerReduxMod.Settings.ShouldRemoveMoreDesignations(
                _designations.Count - plannedRemovalCount
            )
        )
        {
            List<Designation> sortedDeconstructDesignations = [];
            yield return GetThingsSorted(
                    _designations.Where(d =>
                        d.target.HasThing && d.def == DesignationDefOf.Deconstruct
                    ),
                    sortedDeconstructDesignations,
                    _ => true,
                    (b, d) => -GetCountInBuilding(b) / d,
                    d => (Building)d.target.Thing
                )
                .ResumeWhenOtherCoroutineIsCompleted();
            yield return new ResumeAfterTicks(ticksBetweenOperations);

            for (var i = 0; i < sortedDeconstructDesignations.Count; i++)
            {
                var designation = sortedDeconstructDesignations[i];
                var building = (Building)designation.target.Thing;
                var buildingYield = GetCountInBuilding(building);
                count.Value -= buildingYield;
                if (
                    TriggerThreshold.DoesCountMeetTarget(count)
                    || ColonyManagerReduxMod.Settings.ShouldRemoveMoreDesignations(
                        _designations.Count - plannedRemovalCount
                    )
                )
                {
                    data.DeconstructDesignationsToRemove.Add(
                        (designation, buildingYield, count.Value)
                    );
                    plannedRemovalCount++;
                }
                else
                {
                    break;
                }

                if (i > 0 && i % operationsPerTick == 0)
                {
                    yield return new ResumeAfterTicks(ticksBetweenOperations);
                }
            }
            yield return new ResumeAfterTicks(ticksBetweenOperations);
        }

        if (
            TriggerThreshold.DoesCountMeetTarget(count)
            || ColonyManagerReduxMod.Settings.ShouldRemoveMoreDesignations(
                _designations.Count - plannedRemovalCount
            )
        )
        {
            List<Designation> sortedHaulDesignations = [];
            yield return GetThingsSorted(
                    _designations.Where(d => d.target.HasThing && d.def == DesignationDefOf.Haul),
                    sortedHaulDesignations,
                    _ => true,
                    (c, d) => -GetCountInChunk(c) / d,
                    d => d.target.Thing
                )
                .ResumeWhenOtherCoroutineIsCompleted();
            yield return new ResumeAfterTicks(ticksBetweenOperations);

            for (var i = 0; i < sortedHaulDesignations.Count; i++)
            {
                var designation = sortedHaulDesignations[i];
                var chunk = designation.target.Thing;
                var chunkCount = GetCountInChunk(chunk);
                count.Value -= chunkCount;
                if (
                    TriggerThreshold.DoesCountMeetTarget(count)
                    || ColonyManagerReduxMod.Settings.ShouldRemoveMoreDesignations(
                        _designations.Count - plannedRemovalCount
                    )
                )
                {
                    data.HaulDesignationsToRemove.Add((designation, chunkCount, count.Value));
                    plannedRemovalCount++;
                }
                else
                {
                    break;
                }

                if (i > 0 && i % operationsPerTick == 0)
                {
                    yield return new ResumeAfterTicks(ticksBetweenOperations);
                }
            }
        }

        if (
            data.MineDesignationsToRemove.Count == 0
            && data.DeconstructDesignationsToRemove.Count == 0
            && data.HaulDesignationsToRemove.Count == 0
        )
        {
            jobLog.AddDetail(
                "ColonyManagerRedux.Logs.TargetsAlreadySatisfied".Translate(
                    "ColonyManagerRedux.Mining.Logs.Rocks".Translate(),
                    Def.label
                )
            );
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine PlanHaulChunks(Boxed<int> count, MiningWorkData data)
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            PlanHaulChunks
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(PlanHaulChunks);

        var map = Manager.map;
        List<Thing> sortedChunks = [];
        yield return GetTargetsSorted(
                sortedChunks,
                t =>
                    t.def.IsChunk()
                    && !t.IsInAnyStorage()
                    && !t.IsForbidden(Faction.OfPlayer)
                    && !map.reservationManager.IsReserved(t)
                    && Manager.map.designationManager.DesignationOn(t) == null
                    && GetCountInChunk(t) > 0,
                (c, d) => GetCountInChunk(c) / d
            )
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        for (var i = 0; i < sortedChunks.Count; i++)
        {
            var chunk = sortedChunks[i];
            if (
                TriggerThreshold.DoesCountMeetTarget(count)
                || !ColonyManagerReduxMod.Settings.CanAddMoreDesignations(
                    _designations.Count + data.DesignationsToAdd.Count
                )
            )
            {
                break;
            }

            var chunkCount = GetCountInChunk(chunk);
            count.Value += chunkCount;
            data.DesignationsToAdd.Add((chunk, DesignationDefOf.Haul, chunkCount, count.Value));

            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine PlanDeconstructBuildings(
        ManagerLog jobLog,
        Boxed<int> count,
        MiningWorkData data
    )
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            PlanDeconstructBuildings
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                PlanDeconstructBuildings
            );

        List<Building> sortedBuildings = [];
        yield return GetTargetsSorted(
                sortedBuildings,
                b => IsValidDeconstructionTarget(b),
                (b, d) => GetCountInBuilding(b) / d
            )
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        var ancientDangerRects = Manager.AncientDangerRects;
        List<LocalTargetInfo> skippedAncientDangerTargets = [];

        for (var i = 0; i < sortedBuildings.Count; i++)
        {
            var building = sortedBuildings[i];
            if (
                TriggerThreshold.DoesCountMeetTarget(count)
                || !ColonyManagerReduxMod.Settings.CanAddMoreDesignations(
                    _designations.Count + data.DesignationsToAdd.Count
                )
            )
            {
                break;
            }

            var buildingCount = GetCountInBuilding(building);

            var skipBuilding = false;
            if (!DeconstructAncientDangerWhenFogged)
            {
                for (var j = ancientDangerRects.Count - 1; j >= 0; j--)
                {
                    var ancientDangerRect = ancientDangerRects[j];
                    if (!ancientDangerRect.CenterCell.Fogged(Manager))
                    {
                        ancientDangerRects.RemoveAt(j);
                        continue;
                    }

                    if (ancientDangerRect.Contains(building.Position))
                    {
                        skipBuilding = true;
                        skippedAncientDangerTargets.Add(building);
                        break;
                    }
                }
            }

            if (!skipBuilding)
            {
                count.Value += buildingCount;
                data.DesignationsToAdd.Add(
                    (building, DesignationDefOf.Deconstruct, buildingCount, count.Value)
                );
            }

            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
        if (skippedAncientDangerTargets.Count > 0)
        {
            jobLog.AddDetail(
                "ColonyManagerRedux.Mining.Logs.SkippedAncientDangerBuildings".Translate(
                    skippedAncientDangerTargets.Count
                ),
                skippedAncientDangerTargets
            );
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine PlanMineResources(Boxed<int> count, MiningWorkData data)
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            PlanMineResources
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(PlanMineResources);

        List<Mineable> sortedMineable = [];
        yield return GetTargetsSorted(
                sortedMineable,
                m => IsValidMiningTarget(m),
                (m, d) => GetCountInMineral(m) / d
            )
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        for (var i = 0; i < sortedMineable.Count; i++)
        {
            var mineable = sortedMineable[i];
            if (
                TriggerThreshold.DoesCountMeetTarget(count)
                || !ColonyManagerReduxMod.Settings.CanAddMoreDesignations(
                    _designations.Count + data.DesignationsToAdd.Count
                )
            )
            {
                break;
            }

            var mineableCount = GetCountInMineral(mineable);

            if (!IsARoofSupport_Advanced(mineable))
            {
                count.Value += mineableCount;
                data.DesignationsToAdd.Add(
                    (mineable, DesignationDefOf.Mine, mineableCount, count.Value)
                );
            }

            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    protected override Coroutine ExecuteJobDataCoroutine(
        ManagerLog jobLog,
        MiningWorkData data,
        Boxed<bool> workDone
    )
    {
        if (data.Kind == MiningWorkKind.CleanUpAndDisableDrills)
        {
            // Matches the old behavior of not counting a completion cleanup, or flicking off
            // newly discovered drills after completion, as "work done" for this cycle.
            // CleanUp() plans and applies its own drill flicks synchronously (data's
            // DrillCandidatesToFlick is never populated for this Kind), so there's nothing
            // left for ExecuteDrillFlicks to do here.
            CleanUp(jobLog);
            yield break;
        }

        if (data.Kind == MiningWorkKind.RecheckDrillsOnly)
        {
            // The job already completed and its designations were cleaned up previously;
            // only the newly discovered drills gathered above need flicking, so don't
            // re-run CleanUp() here.
            yield return ExecuteDrillFlicks(jobLog, data, null)
                .ResumeWhenOtherCoroutineIsCompleted();
            yield break;
        }

        yield return ExecuteDrillFlicks(jobLog, data, workDone)
            .ResumeWhenOtherCoroutineIsCompleted();

        yield return data.Kind == MiningWorkKind.ReduceDesignations
            ? ExecuteReduceDesignations(jobLog, data, workDone)
                .ResumeWhenOtherCoroutineIsCompleted()
            : ExecuteAddDesignations(jobLog, data, workDone).ResumeWhenOtherCoroutineIsCompleted();
    }

    [CoroutineSettingsMethod]
    private static Coroutine ExecuteDrillFlicks(
        ManagerLog? jobLog,
        MiningWorkData data,
        Boxed<bool>? workDone
    )
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            ExecuteDrillFlicks
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                ExecuteDrillFlicks
            );

        var i = 0;
        foreach (var (flickable, resDef) in data.DrillCandidatesToFlick)
        {
            if (flickable.parent.DestroyedOrNull())
            {
                continue;
            }

            ColonyManagerReduxMod.Instance.LogVerboseMessage(
                $"Flicking deep drill {flickable.parent.Label} "
                    + $"{(data.ShouldEnableDrills ? "on" : "off")} - currently "
                    + $"{(flickable.SwitchIsOn ? "on" : "off")}"
            );
            flickable.wantSwitchOn = data.ShouldEnableDrills;
            FlickUtility.UpdateFlickDesignation(flickable.parent);
            if (flickable.WantsFlick())
            {
                jobLog?.AddDetail(
                    "ColonyManagerRedux.Mining.Logs.FlickedDeepDrill".Translate(
                        resDef.label,
                        (
                            (string)(data.ShouldEnableDrills ? "On" : "Off").Translate()
                        ).UncapitalizeFirst()
                    ),
                    flickable.parent
                );
                if (data.ShouldEnableDrills)
                {
                    if (workDone != null)
                    {
                        workDone.Value = true;
                    }
                    else
                    {
                        ColonyManagerReduxMod.Instance.LogWarning(
                            "workDone is null when trying to enable deep drill. This is a bug."
                        );
                    }
                }
            }

            i++;
            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine ExecuteReduceDesignations(
        ManagerLog jobLog,
        MiningWorkData data,
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

        var designationCounter = 0;

        foreach (var (designation, yieldAmount, countAfter) in data.MineDesignationsToRemove)
        {
            var mineable = designation.target.Cell.GetFirstThing<Mineable>(Manager.map);
            designation.Delete();
            _ = _designations.Remove(designation);

            // The execute phase is gated behind ~95% of the job's work timer, so the mineable
            // planned for removal during gather may already be gone by the time we get here;
            // the designation is still cleaned up above, but there's nothing to report.
            if (!mineable.DestroyedOrNull())
            {
                jobLog.AddDetail(
                    "ColonyManagerRedux.Logs.RemoveDesignation".Translate(
                        DesignationDefOf.Mine.ActionText(),
                        "ColonyManagerRedux.Mining.Logs.Rock".Translate(),
                        mineable.Label,
                        yieldAmount,
                        countAfter,
                        TriggerThreshold.TargetLabel
                    ),
                    mineable
                );
                workDone.Value = true;
            }
            designationCounter++;

            if (designationCounter > 0 && designationCounter % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }

        foreach (var (designation, yieldAmount, countAfter) in data.DeconstructDesignationsToRemove)
        {
            var building = (Building)designation.target.Thing;
            designation.Delete();
            _ = _designations.Remove(designation);

            if (!building.DestroyedOrNull())
            {
                jobLog.AddDetail(
                    "ColonyManagerRedux.Logs.RemoveDesignation".Translate(
                        DesignationDefOf.Deconstruct.ActionText(),
                        "ColonyManagerRedux.Mining.Logs.Building".Translate(),
                        building.Label,
                        yieldAmount,
                        countAfter,
                        TriggerThreshold.TargetLabel
                    ),
                    building
                );
                workDone.Value = true;
            }
            designationCounter++;

            if (designationCounter > 0 && designationCounter % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }

        foreach (var (designation, yieldAmount, countAfter) in data.HaulDesignationsToRemove)
        {
            var chunk = designation.target.Thing;
            designation.Delete();
            _ = _designations.Remove(designation);

            if (!chunk.DestroyedOrNull())
            {
                jobLog.AddDetail(
                    "ColonyManagerRedux.Logs.RemoveDesignation".Translate(
                        DesignationDefOf.Haul.ActionText(),
                        "ColonyManagerRedux.Mining.Logs.Chunk".Translate(),
                        chunk.Label,
                        yieldAmount,
                        countAfter,
                        TriggerThreshold.TargetLabel
                    ),
                    chunk
                );
                workDone.Value = true;
            }
            designationCounter++;

            if (designationCounter > 0 && designationCounter % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine ExecuteAddDesignations(
        ManagerLog jobLog,
        MiningWorkData data,
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
        foreach (var (target, def, amount, countAfter) in data.DesignationsToAdd)
        {
            // The execute phase is gated behind ~95% of the job's work timer, so the target
            // planned during gather may have been destroyed, despawned, or hauled away by the
            // time we get here; re-validate before designating it.
            if (target.DestroyedOrNull())
            {
                continue;
            }

            AddDesignation(target, def);
            workDone.Value = true;

            var (actionText, itemLabel) =
                def == DesignationDefOf.Haul
                    ? (
                        DesignationDefOf.Haul.ActionText(),
                        "ColonyManagerRedux.Mining.Logs.Chunk".Translate()
                    )
                : def == DesignationDefOf.Deconstruct
                    ? (
                        DesignationDefOf.Deconstruct.ActionText(),
                        "ColonyManagerRedux.Mining.Logs.Building".Translate()
                    )
                : (
                    DesignationDefOf.Mine.ActionText(),
                    "ColonyManagerRedux.Mining.Logs.Rock".Translate()
                );

            jobLog.AddDetail(
                "ColonyManagerRedux.Logs.AddDesignation".Translate(
                    actionText,
                    itemLabel,
                    target.Label,
                    amount,
                    countAfter,
                    TriggerThreshold.TargetLabel
                ),
                target
            );

            i++;
            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    protected override IEnumerable<Designation> GetIntersectionDesignations(
        DesignationDef? designationDef
    ) =>
        Manager.map.designationManager.AllDesignations.Where(d =>
            (
                d.def == DesignationDefOf.Mine
                || d.def == DesignationDefOf.Deconstruct
                || d.def == DesignationDefOf.Haul
            ) && (!d.target.HasThing || d.target.Thing.Map == Manager.map)
        );

    private void ConfigureThresholdTriggerParentFilter()
    {
        if (!TriggerThreshold.AllowAnyThreshold)
        {
            TriggerThreshold.ParentFilter.SetDisallowAll();
            foreach (var mineral in AllMinerals)
            {
                TriggerThreshold.ParentFilter.SetAllow(mineral.building.mineableThing, true);
            }
            foreach (
                var material in AllDeconstructibleBuildings
                    .SelectMany(GetMaterialsInBuilding)
                    .Distinct()
            )
            {
                TriggerThreshold.ParentFilter.SetAllow(material, true);
            }
            TriggerThreshold.ParentFilter.SetAllow(ThingCategoryDefOf.Chunks, false);
        }
    }

    public void Notify_StoneChunkMined(Pawn _, Thing thing)
    {
        if (!HaulMinedChunks)
        {
            return;
        }

        if (
            thing.def.designateHaulable
            && thing.def.GetChunkProducts().Any(Counted)
            && _designations.Any(d => d.target.Cell == thing.Position)
        )
        {
            AddDesignation(thing, DesignationDefOf.Haul);
        }
    }

    protected override void Notify_AreaRemoved(Area area)
    {
        if (MiningArea == area)
        {
            MiningArea = null;
        }
    }
}

[Flags]
internal enum ChunkProcessingKind
{
    Neither = 0x0,
    Stonecutting = 0x1,
    Smelting = 0x2,
    Both = Stonecutting | Smelting,
}
