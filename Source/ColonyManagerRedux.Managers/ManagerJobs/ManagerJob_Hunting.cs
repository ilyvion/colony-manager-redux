// ManagerJob_Hunting.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

internal sealed class ManagerJob_Hunting : ManagerJob<ManagerSettings_Hunting>
{
    [HotSwappable]
    public sealed class History : HistoryWorker<ManagerJob_Hunting>
    {
        public override Coroutine GetCountForHistoryChapterCoroutine(
            ManagerJob_Hunting managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> count)
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryStock)
            {
                yield return managerJob.TriggerThreshold.GetCurrentCountCoroutine(count)
                    .ResumeWhenOtherCoroutineIsCompleted();
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryDesignated)
            {
                count.Value = managerJob.GetYieldInDesignations(cached: false);
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryCorpses)
            {
                count.Value = managerJob.GetYieldInCorpses(cached: false);
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
            Boxed<int> target)
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryStock)
            {
                target.Value = managerJob.TriggerThreshold.TargetCount;
            }
            else
            {
                target.Value = 0;
            }
            yield break;
        }
    }

    public enum HuntingTargetResource
    {
        Leather,
        Meat
    }

    private readonly CachedValue<int> _corpseMeatCachedValue = new(0);
    private readonly CachedValue<int> _corpseLeatherCachedValue = new(0);
    private readonly CachedValue<int> _designatedMeatCachedValue = new(0);
    private readonly CachedValue<int> _designatedLeatherCachedValue = new(0);

    private HashSet<PawnKindDef> _allowedAnimalsMeat = [];
    public HashSet<PawnKindDef> _allowedAnimalsLeather = [];

    public HashSet<PawnKindDef> AllowedAnimals =>
        TargetResource == HuntingTargetResource.Meat
            ? _allowedAnimalsMeat
            : _allowedAnimalsLeather;

    public Area? HuntingGrounds;

    public Utilities.SyncDirection Sync = Utilities.SyncDirection.AllowedToFilter;
    public bool SyncFilterAndAllowed = true;

    private bool _unforbidCorpses = true;
    public ref bool UnforbidCorpses => ref _unforbidCorpses;
    private bool _unforbidAllCorpses = true;
    public ref bool UnforbidAllCorpses => ref _unforbidAllCorpses;

    private bool _allowHumanLikeMeat;
    private bool _allowInsectMeat;

    private List<Designation> _designations = [];
    private List<ThingDef>? _humanLikeMeatDefs;

    private List<PawnKindDef>? _allAnimals;
    public List<PawnKindDef> AllAnimals
    {
        get
        {
            _allAnimals ??= Utilities_Hunting.GetMapPawnKindDefs(Manager).ToList();
            return _allAnimals;
        }
    }

    private HuntingTargetResource _targetResource = HuntingTargetResource.Meat;

    public Trigger_Threshold TriggerThreshold => (Trigger_Threshold)Trigger!;

    public ManagerJob_Hunting(Manager manager) : base(manager)
    {
        // populate the trigger field
        Trigger = new Trigger_Threshold(this);

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
            _allowHumanLikeMeat = huntingSettings.DefaultAllowHumanLikeMeat;
            _allowInsectMeat = huntingSettings.DefaultAllowInsectMeat;

            SyncFilterAndAllowed = huntingSettings.DefaultSyncFilterAndAllowed;

            // XXX: What is the point of this? Aren't they all unset at the point of make anyway?
            if (!_allowHumanLikeMeat)
            {
                foreach (var def in HumanLikeMeatDefs)
                {
                    TriggerThreshold.ThresholdFilter.SetAllow(def, false);
                }
            }

            if (!_allowInsectMeat)
            {
                TriggerThreshold.ThresholdFilter.SetAllow(ManagerThingDefOf.Meat_Megaspider, false);
            }
        }
    }

    public override void PostImport()
    {
        base.PostImport();

        _allowedAnimalsMeat.RemoveWhere(a => !AllAnimals.Contains(a));
        _allowedAnimalsLeather.RemoveWhere(a => !AllAnimals.Contains(a));
    }

    public bool AllowHumanLikeMeat
    {
        get => _allowHumanLikeMeat;
        set
        {
            // no change
            if (value == _allowHumanLikeMeat)
            {
                return;
            }

            // update value and filter
            _allowHumanLikeMeat = value;
            foreach (var def in HumanLikeMeatDefs)
            {
                TriggerThreshold.ThresholdFilter.SetAllow(def, value);
            }
        }
    }

    public bool AllowInsectMeat
    {
        get => _allowInsectMeat;
        set
        {
            // no change
            if (value == _allowInsectMeat)
            {
                return;
            }

            // update value and filter
            _allowInsectMeat = value;
            TriggerThreshold.ThresholdFilter.SetAllow(ManagerThingDefOf.Meat_Megaspider, value);
        }
    }

    public IEnumerable<Corpse> Corpses
    {
        get
        {
            var corpses =
                Manager.map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse)
                    .ConvertAll(thing => (Corpse)thing);
            return corpses.Where(
                thing => thing?.InnerPawn != null &&
                    (HuntingGrounds == null ||
                    HuntingGrounds.ActiveCells.Contains(thing.Position)) &&
                    (_unforbidAllCorpses || AllowedAnimals.Contains(thing.InnerPawn.kindDef)));
        }
    }

    public List<Designation> Designations => new(_designations);

    public List<ThingDef> HumanLikeMeatDefs
    {
        get
        {
            _humanLikeMeatDefs ??= DefDatabase<ThingDef>.AllDefsListForReading
                .Where(def => def.category == ThingCategory.Pawn &&
                    (def.race?.Humanlike ?? false) &&
                    (def.race?.IsFlesh ?? false))
                .Select(pk => pk.race.meatDef)
                .Distinct()
                .ToList();

            return _humanLikeMeatDefs;
        }
    }

    public override bool IsValid => base.IsValid && TriggerThreshold != null;

    public override IEnumerable<string> Targets => AllowedAnimals
        .Select(pk => pk.LabelCap.Resolve());

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
            Distance(thing, Manager.map.GetBaseCenter()).ToString("F0"),
            thing.GetStatValue(StatDefOf.MeatAmount).ToString("F0"),
            thing.def.race.meatDef.LabelCap);
    }

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
        Scribe_Values.Look(ref _allowHumanLikeMeat, "allowHumanLikeMeat");
        Scribe_Values.Look(ref _allowInsectMeat, "allowInsectMeat");

        if (Manager.ScribeGameSpecificData)
        {
            // references first, reasons
            Scribe_References.Look(ref HuntingGrounds, "huntingGrounds");

            Utilities.Scribe_Designations(ref _designations, Manager);
        }

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            ConfigureThresholdTriggerParentFilter();
            TriggerThreshold.SettingsChanged = Notify_ThresholdFilterChanged;
        }
    }

    public int GetMeatInCorpses(bool cached = true)
    {
        return GetResourceInCorpses(_corpseMeatCachedValue, c => c.EstimatedMeatCount(), cached);
    }

    public int GetLeatherInCorpses(bool cached = true)
    {
        return GetResourceInCorpses(
            _corpseLeatherCachedValue, c => c.EstimatedLeatherCount(), cached);
    }

    private int GetResourceInCorpses(
        CachedValue<int> cache, Func<Corpse, int> resourceCounter, bool cached = true)
    {
        if (cached && cache.TryGetValue(out int cachedCount))
        {
            return cachedCount;
        }

        // corpses not buried / forbidden
        var count = 0;
        foreach (Corpse corpse in Corpses)
        {
            // make sure it's not forbidden and can be reached.
            if (!corpse.IsForbidden(Faction.OfPlayer) &&
                 Manager.map.reachability.CanReachColony(corpse.Position))
            {
                // check to see if it's buried.
                // Sarcophagus inherits grave, so we don't have to check it separately.
                var slotGroup = Manager.map.haulDestinationManager.SlotGroupAt(corpse.Position);
                if (slotGroup?.parent is Building_Storage building_Storage &&
                     building_Storage.def == ThingDefOf.Grave)
                {
                    continue;
                }

                // get the rottable comp and check how far gone it is.
                if (!corpse.IsNotFresh())
                {
                    count += resourceCounter(corpse);
                }
            }
        }

        // set cache
        cache.Update(count);

        return count;
    }

    public int GetYieldInCorpses(bool cached = true)
    {
        return TargetResource == HuntingTargetResource.Meat
            ? GetMeatInCorpses(cached)
            : GetLeatherInCorpses(cached);
    }

    public int GetMeatInDesignations(bool cached = false)
    {
        if (cached && _designatedMeatCachedValue.TryGetValue(out int cachedCount))
        {
            return cachedCount;
        }

        // designated animals
        var count = 0;
        foreach (var des in _designations)
        {
            if (des.target.Thing is Pawn target)
            {
                count += target.EstimatedMeatCount();
            }
        }

        // update cache
        _designatedMeatCachedValue.Update(count);

        return count;
    }

    public int GetLeatherInDesignations(bool cached = false)
    {
        if (cached && _designatedLeatherCachedValue.TryGetValue(out int cachedCount))
        {
            return cachedCount;
        }

        // designated animals
        var count = 0;
        foreach (var des in _designations)
        {
            if (des.target.Thing is Pawn target)
            {
                count += target.EstimatedLeatherCount();
            }
        }

        // update cache
        _designatedLeatherCachedValue.Update(count);

        return count;
    }

    public int GetYieldInDesignations(bool cached = false)
    {
        return TargetResource == HuntingTargetResource.Meat
            ? GetMeatInDesignations(cached)
            : GetLeatherInDesignations(cached);
    }

    public void RefreshAllAnimals()
    {
        _allAnimals = null;
        ConfigureThresholdTriggerParentFilter();
    }

    public void SetAnimalAllowed(PawnKindDef animal, bool allow, bool sync = true)
    {
        if (allow)
        {
            AllowedAnimals.Add(animal);
        }
        else
        {
            AllowedAnimals.Remove(animal);
        }

        if (SyncFilterAndAllowed && sync)
        {
            Sync = Utilities.SyncDirection.AllowedToFilter;

            ThingDef AnimalResource(PawnKindDef animal) =>
                TargetResource == HuntingTargetResource.Meat
                    ? animal.RaceProps.meatDef
                    : animal.RaceProps.leatherDef;

            var resource = AnimalResource(animal);

            var setAllow = AllowedAnimals.Any(a => AnimalResource(a) == resource);
            TriggerThreshold.ThresholdFilter.SetAllow(resource, setAllow);
        }
    }

    public override Coroutine TryDoJobCoroutine(
        ManagerLog jobLog,
        Boxed<bool> workDone)
    {
        if (!TriggerThreshold.State)
        {
            if (JobState != ManagerJobState.Completed)
            {
                JobState = ManagerJobState.Completed;
                jobLog.AddDetail("ColonyManagerRedux.Logs.JobCompleted".Translate());

                CleanUp(jobLog);
            }
            yield break;
        }
        else
        {
            JobState = ManagerJobState.Active;
        }

        // clean dead designations
        CleanDeadDesignations(_designations, DesignationDefOf.Hunt, jobLog);
        yield return ResumeImmediately.Singleton;

        // clean designations not in area
        CleanAreaDesignations(jobLog);
        yield return ResumeImmediately.Singleton;

        // add designations that could have been handed out by us
        AddRelevantGameDesignations(jobLog);
        yield return ResumeImmediately.Singleton;

        // get the total count of meat in storage, expected meat in corpses and
        // expected meat in designations.
        Boxed<int> totalCount = new(
            TriggerThreshold.GetCurrentCount() + GetYieldInCorpses() + GetYieldInDesignations());
        if (totalCount >= TriggerThreshold.TargetCount)
        {
            jobLog.AddDetail("ColonyManagerRedux.Logs.TargetsAlreadySatisfied".Translate(
                "ColonyManagerRedux.Hunting.Logs.Animals".Translate(),
                Def.label
            ));
            yield break;
        }

        jobLog.AddDetail("ColonyManagerRedux.Logs.CurrentCount".Translate(
            totalCount.Value, TriggerThreshold.TargetCount));

        // unforbid if allowed
        if (_unforbidCorpses)
        {
            var handle = MultiTickCoroutineManager.StartCoroutine(
                DoUnforbidCorpses(jobLog, workDone, totalCount));
            yield return handle.ResumeWhenOtherCoroutineIsCompleted();

            if (workDone && totalCount >= TriggerThreshold.TargetCount)
            {
                yield break;
            }
        }

        // get a list of huntable animals sorted by distance (ignoring obstacles) and
        // expected meat count. NOTE: attempted to balance cost and benefit, current formula:
        // value = meat / ( distance ^ 2)
        List<Pawn> huntableAnimals = [];
        yield return GetTargetsSorted(
            huntableAnimals,
            p => IsValidHuntingTarget(p, false),
            (p, d) => p.EstimatedYield(TargetResource) / d)
            .ResumeWhenOtherCoroutineIsCompleted();

        if (huntableAnimals.Count == 0)
        {
            jobLog.AddDetail("ColonyManagerRedux.Logs.NoValidTargets".Translate(
                "ColonyManagerRedux.Hunting.Logs.Animals".Translate(),
                Def.label
            ));
        }

        // while totalCount < count AND we have animals that can be designated, designate animal.
        foreach (var (huntableAnimal, i) in huntableAnimals.Select((h, i) => (h, i)))
        {
            if (totalCount >= TriggerThreshold.TargetCount)
            {
                break;
            }

            AddDesignation(new(huntableAnimal, DesignationDefOf.Hunt));
            int yield = huntableAnimal.EstimatedYield(TargetResource);
            totalCount.Value += yield;
            jobLog.AddDetail("ColonyManagerRedux.Logs.AddDesignation"
                .Translate(
                    DesignationDefOf.Hunt.ActionText(),
                    "ColonyManagerRedux.Hunting.Logs.Animal".Translate(),
                    huntableAnimal.Label,
                    yield,
                    totalCount.Value,
                    TriggerThreshold.TargetCount),
                huntableAnimal);
            workDone.Value = true;

            if (i > 0 && i % Constants.CoroutineBreakAfter == 0)
            {
                yield return ResumeImmediately.Singleton;
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
        int addedCount = 0;
        List<LocalTargetInfo> newTargets = [];
        foreach (var des in
            Manager.map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Hunt)
                .Except(_designations)
                .Where(des => IsValidHuntingTarget(des.target, true)))
        {
            addedCount++;
            AddDesignation(des, false);
            newTargets.Add(des.target);
        }
        if (addedCount > 0)
        {
            jobLog.AddDetail("ColonyManagerRedux.Logs.AddRelevantGameDesignations"
                .Translate(addedCount, Def.label), newTargets);
        }
    }

    private void CleanAreaDesignations(ManagerLog jobLog)
    {
        int missingThingCount = 0;
        int incorrectAreaCount = 0;

        foreach (var des in _designations)
        {
            if (!des.target.HasThing)
            {
                missingThingCount++;
                des.Delete();
            }
            else if (!HuntingGrounds?.ActiveCells.Contains(des.target.Thing.Position) ?? false)
            {
                incorrectAreaCount++;
                des.Delete();
            }
        }
        if (missingThingCount != 0 || incorrectAreaCount != 0)
        {
            jobLog.AddDetail("ColonyManagerRedux.Logs.CleanAreaDesignations"
                .Translate(
                    missingThingCount + incorrectAreaCount,
                    missingThingCount,
                    incorrectAreaCount,
                    Def.label));
        }
    }

    // originally copypasta from autohuntbeacon by Carry
    // https://ludeon.com/forums/index.php?topic=8930.0
    private Coroutine DoUnforbidCorpses(
        ManagerLog jobLog,
        Boxed<bool> workDone,
        Boxed<int> totalCount)
    {
        foreach (var (corpse, i) in Corpses.Select((c, i) => (c, i)))
        {
            if (totalCount >= TriggerThreshold.TargetCount)
            {
                yield break;
            }

            // don't unforbid corpses in storage - we're going to assume they were intentionally
            // forbidden.
            if (corpse != null && !corpse.IsInAnyStorage() && corpse.IsForbidden(Faction.OfPlayer))
            {
                if (!corpse.IsNotFresh())
                {
                    corpse.SetForbidden(false, false);
                    workDone.Value = true;

                    int yield = corpse.EstimatedYield(TargetResource);
                    totalCount.Value += yield;
                    jobLog.AddDetail("ColonyManagerRedux.Hunting.Logs.UnforbidCorpse"
                        .Translate(
                            corpse.Label,
                            yield,
                            totalCount.Value,
                            TriggerThreshold.TargetCount),
                        corpse);
                }
            }

            if (i > 0 && i % Constants.CoroutineBreakAfter == 0)
            {
                yield return ResumeImmediately.Singleton;
            }
        }
    }

    private bool IsValidHuntingTarget(LocalTargetInfo t, bool allowHunted)
    {
        return t.HasThing
            && t.Thing is Pawn pawn
            && IsValidHuntingTarget(pawn, allowHunted);
    }

    private bool IsValidHuntingTarget(Pawn target, bool allowHunted)
    {
        return target.RaceProps.Animal
            && !target.health.Dead
            && target.Spawned

            // wild animals only
            && target.Faction == null

            // non-biome animals won't be on the list
            && AllowedAnimals.Contains(target.kindDef)
            && (allowHunted || Manager.map.designationManager.DesignationOn(target) == null)
            && (HuntingGrounds == null ||
                 HuntingGrounds.ActiveCells.Contains(target.Position))
            && IsReachable(target);
    }

    private bool IsCountedResource(PawnKindDef pawnKindDef)
    {
        return TriggerThreshold.ThresholdFilter.Allows(TargetResource == HuntingTargetResource.Meat
            ? pawnKindDef.RaceProps.meatDef
            : pawnKindDef.RaceProps.leatherDef);
    }

    private void ConfigureThresholdTriggerParentFilter()
    {
        TriggerThreshold.ParentFilter.SetDisallowAll();
        if (TargetResource == HuntingTargetResource.Meat)
        {
            foreach (var item in AllAnimals)
            {
                TriggerThreshold.ParentFilter.SetAllow(item.RaceProps.meatDef, true);
            }
        }
        else
        {
            foreach (var item in AllAnimals)
            {
                TriggerThreshold.ParentFilter.SetAllow(item.RaceProps.leatherDef, true);
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
            if (IsCountedResource(pawnKindDef))
            {
                AllowedAnimals.Add(pawnKindDef);
            }
            else
            {
                AllowedAnimals.Remove(pawnKindDef);
            }
        }
    }

    protected override void Notify_AreaRemoved(Area area)
    {
        if (HuntingGrounds == area)
        {
            HuntingGrounds = null;
        }
    }
}
