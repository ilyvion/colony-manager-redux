// ManagerJob_Hunting.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Diagnostics.CodeAnalysis;
using Verse.AI;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
[CoroutineSettingsType]
internal sealed class ManagerJob_Hunting
    : ManagerJob<ManagerSettings_Hunting, ManagerJob_Hunting.HuntingWorkData>
{
    // What GatherJobDataCoroutine decided needs to happen; ExecuteJobDataCoroutine applies it.
    // Gathering only ever decides on one of these paths per run (mirroring the branches that
    // used to early-return/yield break in the old single-phase TryDoJobCoroutine).
    internal enum HuntingWorkKind
    {
        // Trigger is disabled: clean up all existing designations and stop hunting.
        CleanUp,

        // Trigger is already satisfied (or too many designations exist): remove some.
        ReduceDesignations,

        // Trigger is not yet satisfied: unforbid corpses and/or add new designations.
        AddDesignations,
    }

    /// <summary>
    /// Carries the decisions made by <see cref="GatherJobDataCoroutine"/> (which doesn't touch
    /// the game) to <see cref="ExecuteJobDataCoroutine"/> (which applies them). No field here
    /// should ever be read as a signal that a change has already happened.
    /// </summary>
    internal sealed class HuntingWorkData
    {
        public HuntingWorkKind Kind;

        // Always considered, regardless of Kind (except CleanUp, where CleanUp() deletes
        // every outstanding designation anyway).
        public List<Designation> AreaDesignationsToRemove = [];

        // Kind == ReduceDesignations
        public List<(Designation Designation, int Yield, int CountAfter)> DesignationsToRemove = [];

        // Kind == AddDesignations
        public List<(Corpse Corpse, int Yield, int CountAfter)> CorpsesToUnforbid = [];
        public List<(Pawn Target, int Yield, int CountAfter)> DesignationsToAdd = [];
    }

    [HotSwappable]
    [CoroutineSettingsType]
    public sealed class History : HistoryWorker<ManagerJob_Hunting>
    {
        [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
        public override Coroutine GetCountForHistoryChapterCoroutine(
            ManagerJob_Hunting managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> count
        )
        {
            var ticksBetweenOperations =
                ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                    (Func<
                        ManagerJob_Hunting,
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
                var cachedValue = managerJob.GetYieldInDesignationsCache();
                yield return cachedValue
                    .DoUpdateIfNeeded(force: true)
                    .ResumeWhenOtherCoroutineIsCompleted();
                yield return new ResumeAfterTicks(ticksBetweenOperations);
                count.Value = cachedValue.Value;
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryCorpses)
            {
                var cachedValue = managerJob.GetYieldInCorpsesCache();
                yield return cachedValue
                    .DoUpdateIfNeeded(force: true)
                    .ResumeWhenOtherCoroutineIsCompleted();
                yield return new ResumeAfterTicks(ticksBetweenOperations);
                count.Value = cachedValue.Value;
            }
            else
            {
                throw new ArgumentException($"Unexpected chapterDef value {chapterDef.defName}");
            }
        }

        public override Coroutine GetTargetForHistoryChapterCoroutine(
            ManagerJob_Hunting managerJob,
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

    public enum HuntingTargetResource
    {
        Leather,
        Meat,
    }

    private readonly MultiTickCachedValue<int> _corpseMeatCachedValue;
    private readonly MultiTickCachedValue<int> _corpseLeatherCachedValue;
    private readonly MultiTickCachedValue<int> _designatedMeatCachedValue;
    private readonly MultiTickCachedValue<int> _designatedLeatherCachedValue;

    private HashSet<PawnKindDef> _allowedAnimalsMeat = [];
    public HashSet<PawnKindDef> _allowedAnimalsLeather = [];

    public HashSet<PawnKindDef> AllowedAnimals =>
        TargetResource == HuntingTargetResource.Meat ? _allowedAnimalsMeat : _allowedAnimalsLeather;

    public Area? HuntingGrounds;
    public bool InvertHuntingGrounds;

    public Utilities.SyncDirection Sync = Utilities.SyncDirection.AllowedToFilter;
    public bool SyncFilterAndAllowed = true;

    private bool _unforbidCorpses = true;
    public ref bool UnforbidCorpses => ref _unforbidCorpses;
    private bool _unforbidAllCorpses = true;
    public ref bool UnforbidAllCorpses => ref _unforbidAllCorpses;

    private List<Designation> _designations = [];

    private bool _animalsLockedToMap = ColonyManagerReduxMod.Settings.NewJobsShouldBeResourceLocked;
    public bool AnimalsLockedToMap
    {
        get => _animalsLockedToMap;
        set
        {
            if (_animalsLockedToMap != value)
            {
                _animalsLockedToMap = value;
                AllAnimals = null; // reset cached animals
            }
        }
    }

    [AllowNull]
    public List<PawnKindDef> AllAnimals
    {
        get
        {
            field ??=
            [
                .. Utilities_Hunting.GetMapPawnKindDefs(_animalsLockedToMap ? Manager.map : null),
            ];
            return field;
        }
        private set;
    }

    private HuntingTargetResource _targetResource = HuntingTargetResource.Meat;

    public Trigger_Threshold TriggerThreshold => (Trigger_Threshold)Trigger!;

    public ManagerJob_Hunting(Manager manager)
        : base(manager)
    {
        _corpseMeatCachedValue = new(0, GetMeatInCorpsesCoroutine);
        _corpseLeatherCachedValue = new(0, GetLeatherInCorpsesCoroutine);
        _designatedMeatCachedValue = new(0, GetMeatInDesignationsCoroutine);
        _designatedLeatherCachedValue = new(0, GetLeatherInDesignationsCoroutine);

        // populate the trigger field
        Trigger = new Trigger_Threshold(this)
        {
            AllowAnyThresholdChanged = ConfigureThresholdTriggerParentFilter,
        };

        TriggerThreshold.SettingsChanged = Notify_ThresholdFilterChanged;
    }

    public override void PostMake()
    {
        var huntingSettings = ManagerSettings;
        if (huntingSettings != null)
        {
            _targetResource = huntingSettings.DefaultTargetResource;
            ConfigureThresholdTriggerParentFilter();

            _unforbidCorpses = huntingSettings.DefaultUnforbidCorpses;
            _unforbidAllCorpses = huntingSettings.DefaultUnforbidAllCorpses;

            SyncFilterAndAllowed = huntingSettings.DefaultSyncFilterAndAllowed;

            foreach (var def in HumanLikeMeatDefs)
            {
                TriggerThreshold.ThresholdFilter.SetAllow(
                    def,
                    huntingSettings.DefaultAllowHumanLikeMeat
                );
            }

            TriggerThreshold.ThresholdFilter.SetAllow(
                ManagerThingDefOf.Meat_Megaspider,
                huntingSettings.DefaultAllowInsectMeat
            );

            if (ModsConfig.AnomalyActive)
            {
                TriggerThreshold.ThresholdFilter.SetAllow(
                    ManagerThingDefOf.Meat_Twisted,
                    huntingSettings.DefaultAllowTwistedMeat
                );
            }
        }
    }

    public override void PostImport()
    {
        base.PostImport();

        _ = _allowedAnimalsMeat.RemoveWhere(a => !AllAnimals.Contains(a));
        _ = _allowedAnimalsLeather.RemoveWhere(a => !AllAnimals.Contains(a));
    }

    public bool AllowAllHumanLikeMeat =>
        HumanLikeMeatDefs.All(TriggerThreshold.ThresholdFilter.Allows);
    public bool AllowNoneHumanLikeMeat =>
        !HumanLikeMeatDefs.Any(TriggerThreshold.ThresholdFilter.Allows);
    public bool AllowHumanLikeMeat
    {
        set
        {
            // update filter
            Sync = Utilities.SyncDirection.FilterToAllowed;
            foreach (var def in HumanLikeMeatDefs)
            {
                TriggerThreshold.ThresholdFilter.SetAllow(def, value);
            }
        }
    }

    public bool AllowInsectMeat
    {
        set
        {
            // update filter
            Sync = Utilities.SyncDirection.FilterToAllowed;
            TriggerThreshold.ThresholdFilter.SetAllow(ManagerThingDefOf.Meat_Megaspider, value);
        }
    }

    public bool AllowTwistedMeat
    {
        set
        {
            // update filter
            Sync = Utilities.SyncDirection.FilterToAllowed;
            TriggerThreshold.ThresholdFilter.SetAllow(ManagerThingDefOf.Meat_Twisted, value);
        }
    }

    public IEnumerable<Corpse> Corpses
    {
        get
        {
            var corpses = Manager
                .map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse)
                .ConvertAll(thing => (Corpse)thing);
            return corpses.Where(thing =>
                thing?.InnerPawn != null
                && Utilities.IsInAllowedArea(HuntingGrounds, thing.Position, InvertHuntingGrounds)
                && (_unforbidAllCorpses || AllowedAnimals.Contains(thing.InnerPawn.kindDef))
            );
        }
    }

    public List<Designation> Designations => [.. _designations];

    public static List<ThingDef> HumanLikeMeatDefs
    {
        get
        {
            field ??=
            [
                .. DefDatabase<ThingDef>
                    .AllDefsListForReading.Where(def =>
                        def.category == ThingCategory.Pawn
                        && def.race != null
                        && def.race.hasMeat
                        && def.race.Humanlike
                        && def.race.IsFlesh
                        && CheckAndReportIfInvalidMeatDef(def)
                    )
                    .Select(pk => pk.race.meatDef)
                    .Distinct(),
            ];

            return field;

            static bool CheckAndReportIfInvalidMeatDef(ThingDef def)
            {
                if (def.race.meatDef != null)
                {
                    return true;
                }

                ColonyManagerReduxMod.Instance.LogWarning(
                    $"The race of {def} (from {def.modContentPack.Name}) claims to have "
                        + "humanlike meat, but its meatDef is null. This race is probably missing "
                        + "having the property `hasMeat` set to `false`."
                );
                return false;
            }
        }
    }

    public override bool IsValid => base.IsValid && TriggerThreshold != null;

    public override IEnumerable<string> Targets =>
        AllowedAnimals.Select(pk => pk.LabelCap.Resolve());

    public HuntingTargetResource TargetResource
    {
        get => _targetResource;
        set
        {
            _targetResource = value;
            RefreshAllAnimals();
        }
    }

    public override WorkTypeDef WorkTypeDef => WorkTypeDefOf.Hunting;

    public override void CleanUp(ManagerLog? jobLog)
    {
        // clear the list of obsolete designations
        CleanDeadDesignations(_designations, DesignationDefOf.Hunt, jobLog);
        CleanUpDesignations(_designations, jobLog);
    }

    public string? DesignationLabel(Designation designation)
    {
        if (!designation.target.HasThing)
        {
            return null;
        }

        // label, dist, yield.
        var thing = designation.target.Thing;
        return "ColonyManagerRedux.Job.DesignationLabel".Translate(
            thing.LabelCap,
            Distance(thing, Manager.map.GetBaseCenter())
                .ToString("F0", CultureInfo.InvariantCulture),
            thing.GetStatValue(StatDefOf.MeatAmount).ToString("F0", CultureInfo.InvariantCulture),
            thing.def.race.meatDef.LabelCap
        );
    }

    private string? _tmpHuntingGroundsLabel;

    public override void ExposeData()
    {
        // scribe base things
        base.ExposeData();

        // settings
        Scribe_Values.Look(ref _targetResource, "targetResource", HuntingTargetResource.Meat);
        Scribe_Collections.Look(ref _allowedAnimalsMeat, "allowedAnimals", LookMode.Def);
        Scribe_Collections.Look(ref _allowedAnimalsLeather, "allowedAnimalsLeather", LookMode.Def);
        Scribe_Values.Look(ref SyncFilterAndAllowed, "syncFilterAndAllowed", true);
        Scribe_Values.Look(ref _unforbidCorpses, "unforbidCorpses", true);
        Scribe_Values.Look(ref _unforbidAllCorpses, "unforbidAllCorpses", true);
        Scribe_Values.Look(
            ref _animalsLockedToMap,
            "animalsLockedToMap",
            ColonyManagerReduxMod.Settings.NewJobsShouldBeResourceLocked
        );
        Scribe_Values.Look(ref InvertHuntingGrounds, "invertHuntingGrounds");

        if (Manager.ScribeSameMapData)
        {
            Scribe_References.Look(ref HuntingGrounds, "huntingGrounds");

            Utilities.Scribe_Designations(ref _designations, Manager);
        }
        else
        {
            Utilities.Scribe_AreaByLabel(
                ref HuntingGrounds,
                ref _tmpHuntingGroundsLabel,
                "huntingGrounds",
                Manager.map.areaManager
            );
        }

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            ConfigureThresholdTriggerParentFilter();
            TriggerThreshold.SettingsChanged = Notify_ThresholdFilterChanged;
            TriggerThreshold.AllowAnyThresholdChanged = ConfigureThresholdTriggerParentFilter;
        }
    }

    /// <inheritdoc/>
    public override int ExpectedAdditionalCount
    {
        get
        {
            var corpsesCache = GetYieldInCorpsesCache();
            _ = corpsesCache.DoUpdateIfNeeded();
            var designationsCache = GetYieldInDesignationsCache();
            _ = designationsCache.DoUpdateIfNeeded();
            return corpsesCache.Value + designationsCache.Value;
        }
    }

    public MultiTickCachedValue<int> GetYieldInCorpsesCache() =>
        TargetResource == HuntingTargetResource.Meat
            ? _corpseMeatCachedValue
            : _corpseLeatherCachedValue;

    private Coroutine GetMeatInCorpsesCoroutine(AnyBoxed<int> count) =>
        GetResourceInCorpses(count, c => c.EstimatedMeatCount());

    private Coroutine GetLeatherInCorpsesCoroutine(AnyBoxed<int> count) =>
        GetResourceInCorpses(count, c => c.EstimatedLeatherCount());

    [CoroutineSettingsMethod]
    private Coroutine GetResourceInCorpses(AnyBoxed<int> count, Func<Corpse, int> resourceCounter)
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            GetResourceInCorpses
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                GetResourceInCorpses
            );

        // corpses not buried / forbidden
        foreach (var (corpse, i) in Corpses.Select((c, i) => (c, i)))
        {
            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }

            // make sure it's not forbidden and can be reached.
            if (
                IsCountedResource(corpse)
                && !corpse.IsForbidden(Faction.OfPlayer)
                && Manager.map.reachability.CanReachColony(corpse.Position)
            )
            {
                // check to see if it's buried.
                // Sarcophagus inherits grave, so we don't have to check for it separately.
                var slotGroup = Manager.map.haulDestinationManager.SlotGroupAt(corpse.Position);
                if (
                    slotGroup?.parent is Building_Storage building_Storage
                    && building_Storage.def == ThingDefOf.Grave
                )
                {
                    continue;
                }

                // get the rottable comp and check how far gone it is.
                if (!corpse.IsNotFresh())
                {
                    count.Value += resourceCounter(corpse);
                }
            }
        }
    }

    public MultiTickCachedValue<int> GetYieldInDesignationsCache() =>
        TargetResource == HuntingTargetResource.Meat
            ? _designatedMeatCachedValue
            : _designatedLeatherCachedValue;

    [CoroutineSettingsMethod]
    private Coroutine GetMeatInDesignationsCoroutine(AnyBoxed<int> count)
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            GetMeatInDesignationsCoroutine
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                GetMeatInDesignationsCoroutine
            );

        // designated animals
        for (var i = 0; i < _designations.Count; i++)
        {
            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }

            var des = _designations[i];
            if (des.target.Thing is Pawn target)
            {
                count.Value += target.EstimatedMeatCount();
            }
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine GetLeatherInDesignationsCoroutine(AnyBoxed<int> count)
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            GetLeatherInDesignationsCoroutine
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                GetLeatherInDesignationsCoroutine
            );

        // designated animals
        for (var i = 0; i < _designations.Count; i++)
        {
            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }

            var des = _designations[i];
            if (des.target.Thing is Pawn target)
            {
                count.Value += target.EstimatedLeatherCount();
            }
        }
    }

    public void RefreshAllAnimals()
    {
        AllAnimals = null;
        ConfigureThresholdTriggerParentFilter();
    }

    public void SetAnimalAllowed(PawnKindDef animal, bool allow, bool sync = true)
    {
        _ = allow ? AllowedAnimals.Add(animal) : AllowedAnimals.Remove(animal);
        Notify_TargetsChanged();

        if (SyncFilterAndAllowed && sync)
        {
            Sync = Utilities.SyncDirection.AllowedToFilter;

            ThingDef AnimalResource(PawnKindDef animal)
            {
                return TargetResource == HuntingTargetResource.Meat
                    ? animal.RaceProps.meatDef
                    : animal.RaceProps.leatherDef;
            }

            var resource = AnimalResource(animal);

            var setAllow = Utilities_ResourceSync.ShouldResourceStayAllowed(
                AllowedAnimals,
                AnimalResource,
                resource
            );
            TriggerThreshold.ThresholdFilter.SetAllow(resource, setAllow);
        }
    }

    [CoroutineSettingsMethod]
    protected override Coroutine GatherJobDataCoroutine(
        ManagerLog jobLog,
        AnyBoxed<HuntingWorkData?> data
    )
    {
        if (!TriggerThreshold.State)
        {
            if (JobState != ManagerJobState.Completed)
            {
                JobState = ManagerJobState.Completed;
                jobLog.AddDetail("ColonyManagerRedux.Logs.JobCompleted".Translate());

                data.Value = new HuntingWorkData { Kind = HuntingWorkKind.CleanUp };
            }
            yield break;
        }
        else
        {
            JobState = ManagerJobState.Active;
        }

        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            (Func<ManagerLog, AnyBoxed<HuntingWorkData?>, Coroutine>)GatherJobDataCoroutine
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                (Func<ManagerLog, AnyBoxed<HuntingWorkData?>, Coroutine>)GatherJobDataCoroutine
            );

        // Resync our own bookkeeping against designations that have disappeared or that
        // already exist in the game unbeknownst to us; this doesn't change anything in the
        // game itself, so it's safe to do while gathering.
        CleanDeadDesignations(_designations, DesignationDefOf.Hunt, jobLog);
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        var workData = new HuntingWorkData();
        workData.AreaDesignationsToRemove.AddRange(PlanAreaCleanupDesignations());
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        AddRelevantGameDesignations(jobLog);
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        // get the total count of meat in storage, expected meat in corpses and
        // expected meat in designations.
        var corpsesCachedValue = GetYieldInCorpsesCache();
        yield return corpsesCachedValue
            .DoUpdateIfNeeded(force: true)
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        var designationsCachedValue = GetYieldInDesignationsCache();
        yield return designationsCachedValue
            .DoUpdateIfNeeded(force: true)
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        var count = new Boxed<int>(
            TriggerThreshold.GetCurrentCount()
                + corpsesCachedValue.Value
                + designationsCachedValue.Value
        );

        if (
            TriggerThreshold.DoesCountMeetTarget(count)
            || ColonyManagerReduxMod.Settings.ShouldRemoveMoreDesignations(_designations.Count)
        )
        {
            workData.Kind = HuntingWorkKind.ReduceDesignations;

            yield return PlanReduceDesignations(count, workData)
                .ResumeWhenOtherCoroutineIsCompleted();

            data.Value = workData;
            yield break;
        }

        workData.Kind = HuntingWorkKind.AddDesignations;

        jobLog.AddDetail(
            "ColonyManagerRedux.Logs.CurrentCount".Translate(
                count.Value,
                TriggerThreshold.TargetCount
            )
        );

        // plan unforbidding corpses if allowed
        if (_unforbidCorpses)
        {
            yield return PlanUnforbidCorpses(count, workData).ResumeWhenOtherCoroutineIsCompleted();
            yield return new ResumeAfterTicks(ticksBetweenOperations);

            if (workData.CorpsesToUnforbid.Count > 0 && TriggerThreshold.DoesCountMeetTarget(count))
            {
                data.Value = workData;
                yield break;
            }
        }

        if (!ColonyManagerReduxMod.Settings.CanAddMoreDesignations(_designations.Count))
        {
            jobLog.AddDetail(
                "ColonyManagerRedux.Logs.CantAddMoreDesignations".Translate(
                    "ColonyManagerRedux.Hunting.Logs.Animals".Translate(),
                    Def.label
                )
            );
            data.Value = workData;
            yield break;
        }

        // get a list of huntable animals sorted by distance (ignoring obstacles) and
        // expected meat count. NOTE: attempted to balance cost and benefit, current formula:
        // value = meat / ( distance ^ 2)
        List<Pawn> huntableAnimals = [];
        yield return GetTargetsSorted(
                huntableAnimals,
                IsValidUndesignatedHuntingTarget,
                (p, d) => p.EstimatedYield(TargetResource) / d
            )
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        if (huntableAnimals.Count == 0)
        {
            jobLog.AddDetail(
                "ColonyManagerRedux.Logs.NoValidTargets".Translate(
                    "ColonyManagerRedux.Hunting.Logs.Animals".Translate(),
                    Def.label
                )
            );
            data.Value = workData;
            yield break;
        }

        var sortedYields = huntableAnimals.Select(a => a.EstimatedYield(TargetResource)).ToList();
        var designateCount = Utilities_Plants.ComputeNumberToDesignate(
            count.Value,
            sortedYields,
            _designations.Count,
            TriggerThreshold.DoesCountMeetTarget,
            ColonyManagerReduxMod.Settings.CanAddMoreDesignations
        );
        for (var i = 0; i < designateCount; i++)
        {
            var animal = huntableAnimals[i];
            var yield = sortedYields[i];
            count.Value += yield;
            workData.DesignationsToAdd.Add((animal, yield, count.Value));

            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }

        data.Value = workData;
    }

    /// <summary>
    /// Decides whether a designation whose target either has no thing, or whose thing is no
    /// longer inside the configured hunting grounds, should be removed. Pure function, kept
    /// separate so it's unit-testable without a live <see cref="Map"/>.
    /// </summary>
    internal static bool ShouldRemoveForAreaCleanup(bool hasThing, bool inAllowedArea) =>
        !hasThing || !inAllowedArea;

    /// <summary>
    /// Decides which designations need to be removed because their target has vanished or has
    /// left the configured hunting grounds, without deleting anything yet.
    /// </summary>
    private List<Designation> PlanAreaCleanupDesignations()
    {
        List<Designation> toRemove = [];
        foreach (var des in _designations)
        {
            var inAllowedArea =
                des.target.HasThing
                && Utilities.IsInAllowedArea(
                    HuntingGrounds,
                    des.target.Thing.Position,
                    InvertHuntingGrounds
                );
            if (ShouldRemoveForAreaCleanup(des.target.HasThing, inAllowedArea))
            {
                toRemove.Add(des);
            }
        }
        return toRemove;
    }

    [CoroutineSettingsMethod]
    private Coroutine PlanReduceDesignations(Boxed<int> count, HuntingWorkData data)
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
                (p, d) => -p.EstimatedYield(TargetResource) / d,
                d => (Pawn)d.target.Thing
            )
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        var sortedYields = sortedDesignations
            .Select(d => ((Pawn)d.target.Thing).EstimatedYield(TargetResource))
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

    // originally copypasta from autohuntbeacon by Carry
    // https://ludeon.com/forums/index.php?topic=8930.0
    [CoroutineSettingsMethod]
    private Coroutine PlanUnforbidCorpses(Boxed<int> count, HuntingWorkData data)
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            PlanUnforbidCorpses
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                PlanUnforbidCorpses
            );

        foreach (var (corpse, i) in Corpses.Select((c, i) => (c, i)))
        {
            if (TriggerThreshold.DoesCountMeetTarget(count))
            {
                yield break;
            }

            // don't unforbid corpses in storage - we're going to assume they were
            // intentionally forbidden.
            if (corpse != null && !corpse.IsInAnyStorage() && corpse.IsForbidden(Faction.OfPlayer))
            {
                if (!corpse.IsNotFresh())
                {
                    var yield = corpse.EstimatedYield(TargetResource);
                    count.Value += yield;
                    data.CorpsesToUnforbid.Add((corpse, yield, count.Value));
                }
            }

            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
    protected override Coroutine ExecuteJobDataCoroutine(
        ManagerLog jobLog,
        HuntingWorkData data,
        Boxed<bool> workDone
    )
    {
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                (Func<ManagerLog, HuntingWorkData, Boxed<bool>, Coroutine>)ExecuteJobDataCoroutine
            );

        if (data.Kind == HuntingWorkKind.CleanUp)
        {
            // Matches the old behavior of not counting a completion cleanup as "work done"
            // for this cycle.
            CleanUp(jobLog);
            yield break;
        }

        ExecuteAreaCleanupDesignations(jobLog, data);
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        if (data.Kind == HuntingWorkKind.ReduceDesignations)
        {
            yield return ExecuteReduceDesignations(jobLog, data, workDone)
                .ResumeWhenOtherCoroutineIsCompleted();
            yield break;
        }

        yield return ExecuteUnforbidCorpses(jobLog, data, workDone)
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        yield return ExecuteAddDesignations(jobLog, data, workDone)
            .ResumeWhenOtherCoroutineIsCompleted();
    }

    private void ExecuteAreaCleanupDesignations(ManagerLog jobLog, HuntingWorkData data)
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
                    HuntingGrounds,
                    des.target.Thing.Position,
                    InvertHuntingGrounds
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

    [CoroutineSettingsMethod]
    private Coroutine ExecuteReduceDesignations(
        ManagerLog jobLog,
        HuntingWorkData data,
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
            var animal = (Pawn)designation.target.Thing;
            designation.Delete();
            _ = _designations.Remove(designation);

            // The target planned for removal during gather may already be gone by the time
            // the (now much later) execute phase actually runs; the designation is still
            // cleaned up above, but there's nothing to report in that case.
            if (!animal.DestroyedOrNull())
            {
                jobLog.AddDetail(
                    "ColonyManagerRedux.Logs.RemoveDesignation".Translate(
                        DesignationDefOf.Hunt.ActionText(),
                        "ColonyManagerRedux.Hunting.Logs.Animal".Translate(),
                        animal.Label,
                        yieldAmount,
                        countAfter,
                        TriggerThreshold.TargetLabel
                    ),
                    animal
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
                    "ColonyManagerRedux.Hunting.Logs.Animals".Translate(),
                    Def.label
                )
            );
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine ExecuteUnforbidCorpses(
        ManagerLog jobLog,
        HuntingWorkData data,
        Boxed<bool> workDone
    )
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            ExecuteUnforbidCorpses
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                ExecuteUnforbidCorpses
            );

        var i = 0;
        foreach (var (corpse, yieldAmount, countAfter) in data.CorpsesToUnforbid)
        {
            // The target planned for unforbidding during gather may have been destroyed,
            // hauled into storage, or re-forbidden by the time the (now much later) execute
            // phase actually runs; re-validate before touching it.
            if (
                corpse.DestroyedOrNull()
                || corpse.IsInAnyStorage()
                || !corpse.IsForbidden(Faction.OfPlayer)
            )
            {
                continue;
            }

            corpse.SetForbidden(false, false);
            workDone.Value = true;

            jobLog.AddDetail(
                "ColonyManagerRedux.Hunting.Logs.UnforbidCorpse".Translate(
                    corpse.Label,
                    yieldAmount,
                    countAfter,
                    TriggerThreshold.TargetCount
                ),
                corpse
            );

            i++;
            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine ExecuteAddDesignations(
        ManagerLog jobLog,
        HuntingWorkData data,
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
        foreach (var (animal, yieldAmount, countAfter) in data.DesignationsToAdd)
        {
            // The target planned for designation during gather may have been destroyed,
            // despawned, or killed by the time the (now much later) execute phase actually
            // runs; re-validate before designating it.
            if (animal.DestroyedOrNull())
            {
                continue;
            }

            AddDesignation(new(animal, DesignationDefOf.Hunt));
            workDone.Value = true;

            jobLog.AddDetail(
                "ColonyManagerRedux.Logs.AddDesignation".Translate(
                    DesignationDefOf.Hunt.ActionText(),
                    "ColonyManagerRedux.Hunting.Logs.Animal".Translate(),
                    animal.Label,
                    yieldAmount,
                    countAfter,
                    TriggerThreshold.TargetLabel
                ),
                animal
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

    private void AddRelevantGameDesignations(ManagerLog jobLog)
    {
        // get list of game designations not managed by this job that could have been assigned
        // by this job.
        var addedCount = 0;
        List<LocalTargetInfo> newTargets = [];
        foreach (
            var des in Manager
                .map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Hunt)
                .Except(_designations)
                .Where(des => IsValidDesignatedHuntingTarget(des.target))
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

    private bool IsValidUndesignatedHuntingTarget(Pawn target) =>
        target.RaceProps.Animal
        && target.Map == Manager.map
        && !target.health.Dead
        && AllowedAnimals.Contains(target.kindDef)
        && target.Spawned
        && Manager.map.designationManager.DesignationOn(target) == null
        // wild animals only
        && target.Faction == null
        // non-biome animals won't be on the list
        && Utilities.IsInAllowedArea(HuntingGrounds, target.Position, InvertHuntingGrounds)
        && IsReachable(target, PathEndMode.Touch);

    private bool IsValidDesignatedHuntingTarget(LocalTargetInfo t) =>
        t.HasThing && t.Thing is Pawn pawn && IsValidDesignatedHuntingTarget(pawn);

    private bool IsValidDesignatedHuntingTarget(Pawn target) =>
        target.RaceProps.Animal
        && target.Map == Manager.map
        && !target.health.Dead
        && AllowedAnimals.Contains(target.kindDef)
        && target.Spawned
        // wild animals only
        && target.Faction == null
        // non-biome animals won't be on the list
        && Utilities.IsInAllowedArea(HuntingGrounds, target.Position, InvertHuntingGrounds);

    /// <summary>
    /// Picks the meat or leather def a pawn kind's resource threshold should track, depending on
    /// <paramref name="targetResource"/>. Kept separate from <see cref="IsCountedResource(PawnKindDef)"/>
    /// / <see cref="IsValidResource(PawnKindDef)"/> so the null-selection logic (the subject of
    /// CHANGELOG 0.5.3/0.5.4's NullReferenceException fixes) is unit-testable without a live
    /// <see cref="PawnKindDef"/>.
    /// </summary>
    internal static T? SelectResourceDef<T>(
        HuntingTargetResource targetResource,
        T? meatDef,
        T? leatherDef
    )
        where T : class => targetResource == HuntingTargetResource.Meat ? meatDef : leatherDef;

    private bool IsCountedResource(PawnKindDef pawnKindDef)
    {
        var resourceDef = SelectResourceDef(
            TargetResource,
            pawnKindDef.RaceProps.meatDef,
            pawnKindDef.RaceProps.leatherDef
        );
        return resourceDef != null && TriggerThreshold.ThresholdFilter.Allows(resourceDef);
    }

    private bool IsValidResource(PawnKindDef pawnKindDef)
    {
        var resourceDef = SelectResourceDef(
            TargetResource,
            pawnKindDef.RaceProps.meatDef,
            pawnKindDef.RaceProps.leatherDef
        );
        return resourceDef != null;
    }

    private bool IsCountedResource(Pawn pawn) => IsCountedResource(pawn.kindDef);

    private bool IsCountedResource(Corpse corpse) => IsCountedResource(corpse.InnerPawn);

    private void ConfigureThresholdTriggerParentFilter()
    {
        if (!TriggerThreshold.AllowAnyThreshold)
        {
            TriggerThreshold.ParentFilter.SetDisallowAll();
            if (TargetResource == HuntingTargetResource.Meat)
            {
                foreach (
                    var item in Utilities_Hunting
                        .GetMapPawnKindDefs(Manager, false)
                        .Where(IsValidResource)
                )
                {
                    TriggerThreshold.ParentFilter.SetAllow(item.RaceProps.meatDef, true);
                }

                // Hard code human meats, insect meat and twisted meat
                foreach (var meatDef in HumanLikeMeatDefs)
                {
                    TriggerThreshold.ParentFilter.SetAllow(meatDef, true);
                }

                TriggerThreshold.ParentFilter.SetAllow(ManagerThingDefOf.Meat_Megaspider, true);

                if (ModsConfig.AnomalyActive)
                {
                    TriggerThreshold.ParentFilter.SetAllow(ManagerThingDefOf.Meat_Twisted, true);
                }
            }
            else
            {
                foreach (
                    var item in Utilities_Hunting
                        .GetMapPawnKindDefs(Manager, false)
                        .Where(IsValidResource)
                )
                {
                    TriggerThreshold.ParentFilter.SetAllow(item.RaceProps.leatherDef, true);
                }
            }
        }
    }

    public void Notify_ThresholdFilterChanged()
    {
        ColonyManagerReduxMod.Instance.LogDebug("Threshold changed.");

        if (!SyncFilterAndAllowed || Sync == Utilities.SyncDirection.AllowedToFilter)
        {
            return;
        }

        foreach (var pawnKindDef in AllAnimals)
        {
            _ = IsCountedResource(pawnKindDef)
                ? AllowedAnimals.Add(pawnKindDef)
                : AllowedAnimals.Remove(pawnKindDef);
        }
        Notify_TargetsChanged();
    }

    protected override void Notify_AreaRemoved(Area area)
    {
        if (HuntingGrounds == area)
        {
            HuntingGrounds = null;
        }
    }
}
