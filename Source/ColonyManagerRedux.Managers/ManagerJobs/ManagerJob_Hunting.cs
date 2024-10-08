﻿// ManagerJob_Hunting.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

[HotSwappable]
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
                var cachedValue = managerJob.GetYieldInDesignationsCache();
                yield return cachedValue.DoUpdateIfNeeded(force: true)
                    .ResumeWhenOtherCoroutineIsCompleted();
                count.Value = cachedValue.Value;
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryCorpses)
            {
                var cachedValue = managerJob.GetYieldInCorpsesCache();
                yield return cachedValue.DoUpdateIfNeeded(force: true)
                    .ResumeWhenOtherCoroutineIsCompleted();
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

    private readonly MultiTickCachedValue<int> _corpseMeatCachedValue;
    private readonly MultiTickCachedValue<int> _corpseLeatherCachedValue;
    private readonly MultiTickCachedValue<int> _designatedMeatCachedValue;
    private readonly MultiTickCachedValue<int> _designatedLeatherCachedValue;

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

    private List<Designation> _designations = [];

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
        _corpseMeatCachedValue = new(0, GetMeatInCorpsesCoroutine);
        _corpseLeatherCachedValue = new(0, GetLeatherInCorpsesCoroutine);
        _designatedMeatCachedValue = new(0, GetMeatInDesignationsCoroutine);
        _designatedLeatherCachedValue = new(0, GetLeatherInDesignationsCoroutine);

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

            SyncFilterAndAllowed = huntingSettings.DefaultSyncFilterAndAllowed;

            foreach (var def in HumanLikeMeatDefs)
            {
                TriggerThreshold.ThresholdFilter.SetAllow(
                    def, huntingSettings.DefaultAllowHumanLikeMeat);
            }

            TriggerThreshold.ThresholdFilter.SetAllow(
                ManagerThingDefOf.Meat_Megaspider, huntingSettings.DefaultAllowInsectMeat);

            if (ModsConfig.AnomalyActive)
            {
                TriggerThreshold.ThresholdFilter.SetAllow(
                    ManagerThingDefOf.Meat_Twisted, huntingSettings.DefaultAllowTwistedMeat);
            }
        }
    }

    public override void PostImport()
    {
        base.PostImport();

        _allowedAnimalsMeat.RemoveWhere(a => !AllAnimals.Contains(a));
        _allowedAnimalsLeather.RemoveWhere(a => !AllAnimals.Contains(a));
    }

    public bool AllowAllHumanLikeMeat
        => HumanLikeMeatDefs.All(TriggerThreshold.ThresholdFilter.Allows);
    public bool AllowNoneHumanLikeMeat
        => !HumanLikeMeatDefs.Any(TriggerThreshold.ThresholdFilter.Allows);
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

    private static List<ThingDef>? _humanLikeMeatDefs;
    public static List<ThingDef> HumanLikeMeatDefs
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

    public MultiTickCachedValue<int> GetYieldInCorpsesCache()
    {
        return TargetResource == HuntingTargetResource.Meat
            ? _corpseMeatCachedValue
            : _corpseLeatherCachedValue;
    }

    private Coroutine GetMeatInCorpsesCoroutine(AnyBoxed<int> count)
    {
        return GetResourceInCorpses(count, c => c.EstimatedMeatCount());
    }

    private Coroutine GetLeatherInCorpsesCoroutine(AnyBoxed<int> count)
    {
        return GetResourceInCorpses(count, c => c.EstimatedLeatherCount());
    }

    private Coroutine GetResourceInCorpses(AnyBoxed<int> count, Func<Corpse, int> resourceCounter)
    {
        // corpses not buried / forbidden
        foreach (var (corpse, i) in Corpses.Select((c, i) => (c, i)))
        {
            if (i > 0 && i % Constants.CoroutineBreakAfter == 0)
            {
                yield return ResumeImmediately.Singleton;
            }

            // make sure it's not forbidden and can be reached.
            if (IsCountedResource(corpse) &&
                !corpse.IsForbidden(Faction.OfPlayer) &&
                 Manager.map.reachability.CanReachColony(corpse.Position))
            {
                // check to see if it's buried.
                // Sarcophagus inherits grave, so we don't have to check for it separately.
                var slotGroup = Manager.map.haulDestinationManager.SlotGroupAt(corpse.Position);
                if (slotGroup?.parent is Building_Storage building_Storage &&
                     building_Storage.def == ThingDefOf.Grave)
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

    public MultiTickCachedValue<int> GetYieldInDesignationsCache()
    {
        return TargetResource == HuntingTargetResource.Meat
            ? _designatedMeatCachedValue
            : _designatedLeatherCachedValue;
    }

    private Coroutine GetMeatInDesignationsCoroutine(AnyBoxed<int> count)
    {
        // designated animals
        for (int i = 0; i < _designations.Count; i++)
        {
            if (i > 0 && i % Constants.CoroutineBreakAfter == 0)
            {
                yield return ResumeImmediately.Singleton;
            }

            Designation? des = _designations[i];
            if (des.target.Thing is Pawn target)
            {
                count.Value += target.EstimatedMeatCount();
            }
        }
    }

    private Coroutine GetLeatherInDesignationsCoroutine(AnyBoxed<int> count)
    {
        // designated animals
        for (int i = 0; i < _designations.Count; i++)
        {
            if (i > 0 && i % Constants.CoroutineBreakAfter == 0)
            {
                yield return ResumeImmediately.Singleton;
            }

            Designation? des = _designations[i];
            if (des.target.Thing is Pawn target)
            {
                count.Value += target.EstimatedLeatherCount();
            }
        }
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
        var corpsesCachedValue = GetYieldInCorpsesCache();
        yield return corpsesCachedValue.DoUpdateIfNeeded(force: true)
            .ResumeWhenOtherCoroutineIsCompleted();

        var designationsCachedValue = GetYieldInDesignationsCache();
        yield return designationsCachedValue.DoUpdateIfNeeded(force: true)
            .ResumeWhenOtherCoroutineIsCompleted();

        Boxed<int> totalCount = new(
            TriggerThreshold.GetCurrentCount()
            + corpsesCachedValue.Value
            + designationsCachedValue.Value);

        if (totalCount >= TriggerThreshold.TargetCount
            || ColonyManagerReduxMod.Settings.ShouldRemoveMoreDesignations(_designations.Count))
        {
            List<Designation> sortedDesignations = [];
            yield return GetThingsSorted(
                _designations.Where(d => d.target.HasThing),
                sortedDesignations,
                _ => true,
                (p, d) => -p.EstimatedYield(TargetResource) / d,
                d => (Pawn)d.target.Thing)
                .ResumeWhenOtherCoroutineIsCompleted();

            // reduce designations until we're just above target
            for (int i = 0; i < sortedDesignations.Count; i++)
            {
                var designation = sortedDesignations[i];

                var plant = (Pawn)designation.target.Thing;
                int yield = plant.EstimatedYield(TargetResource);
                totalCount.Value -= yield;
                if (totalCount >= TriggerThreshold.TargetCount
                    || ColonyManagerReduxMod.Settings
                        .ShouldRemoveMoreDesignations(_designations.Count))
                {
                    designation.Delete();
                    _designations.Remove(designation);
                    jobLog.AddDetail("ColonyManagerRedux.Logs.RemoveDesignation"
                        .Translate(
                            DesignationDefOf.Hunt.ActionText(),
                            "ColonyManagerRedux.Hunting.Logs.Animal".Translate(),
                            plant.Label,
                            yield,
                            totalCount.Value,
                            TriggerThreshold.TargetCount),
                        plant);
                    workDone.Value = true;
                }
                else
                {
                    break;
                }

                if (i > 0 && i % Constants.CoroutineBreakAfter == 0)
                {
                    yield return ResumeImmediately.Singleton;
                }
            }

            if (!workDone)
            {
                jobLog.AddDetail("ColonyManagerRedux.Logs.TargetsAlreadySatisfied".Translate(
                    "ColonyManagerRedux.Hunting.Logs.Animals".Translate(),
                    Def.label
                ));
            }

            yield break;
        }

        jobLog.AddDetail("ColonyManagerRedux.Logs.CurrentCount".Translate(
            totalCount.Value, TriggerThreshold.TargetCount));

        // unforbid if allowed
        if (_unforbidCorpses)
        {
            yield return DoUnforbidCorpses(jobLog, workDone, totalCount)
                .ResumeWhenOtherCoroutineIsCompleted();

            if (workDone && totalCount >= TriggerThreshold.TargetCount)
            {
                yield break;
            }
        }

        if (!ColonyManagerReduxMod.Settings.CanAddMoreDesignations(_designations.Count))
        {
            jobLog.AddDetail("ColonyManagerRedux.Logs.CantAddMoreDesignations".Translate(
                "ColonyManagerRedux.Hunting.Logs.Animals".Translate(),
                Def.label
            ));
            yield break;
        }

        // get a list of huntable animals sorted by distance (ignoring obstacles) and
        // expected meat count. NOTE: attempted to balance cost and benefit, current formula:
        // value = meat / ( distance ^ 2)
        List<Pawn> huntableAnimals = [];
        yield return GetTargetsSorted(
            huntableAnimals,
            IsValidUndesignatedHuntingTarget,
            (p, d) => p.EstimatedYield(TargetResource) / d)
            .ResumeWhenOtherCoroutineIsCompleted();

        if (huntableAnimals.Count == 0)
        {
            jobLog.AddDetail("ColonyManagerRedux.Logs.NoValidTargets".Translate(
                "ColonyManagerRedux.Hunting.Logs.Animals".Translate(),
                Def.label
            ));
            yield break;
        }

        // while totalCount < count AND we have animals that can be designated, designate animal.
        foreach (var (huntableAnimal, i) in huntableAnimals.Select((h, i) => (h, i)))
        {
            if (totalCount >= TriggerThreshold.TargetCount
                || !ColonyManagerReduxMod.Settings.CanAddMoreDesignations(_designations.Count))
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
                .Where(des => IsValidDesignatedHuntingTarget(des.target)))
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

    private bool IsValidUndesignatedHuntingTarget(LocalTargetInfo t)
    {
        return t.HasThing
            && t.Thing is Pawn pawn
            && IsValidUndesignatedHuntingTarget(pawn);
    }

    private bool IsValidUndesignatedHuntingTarget(Pawn target)
    {
        return target.RaceProps.Animal
            && target.Map == Manager.map
            && !target.health.Dead

            && AllowedAnimals.Contains(target.kindDef)
            && target.Spawned
            && Manager.map.designationManager.DesignationOn(target) == null

            // wild animals only
            && target.Faction == null

            // non-biome animals won't be on the list
            && (HuntingGrounds == null || HuntingGrounds.ActiveCells.Contains(target.Position))

            && IsReachable(target);
    }

    private bool IsValidDesignatedHuntingTarget(LocalTargetInfo t)
    {
        return t.HasThing
            && t.Thing is Pawn pawn
            && IsValidDesignatedHuntingTarget(pawn);
    }

    private bool IsValidDesignatedHuntingTarget(Pawn target)
    {
        return target.RaceProps.Animal
            && target.Map == Manager.map
            && !target.health.Dead

            && AllowedAnimals.Contains(target.kindDef)
            && target.Spawned

            // wild animals only
            && target.Faction == null

            // non-biome animals won't be on the list
            && (HuntingGrounds == null || HuntingGrounds.ActiveCells.Contains(target.Position));
    }

    private bool IsCountedResource(PawnKindDef pawnKindDef)
    {
        ThingDef resourceDef = TargetResource == HuntingTargetResource.Meat
            ? pawnKindDef.RaceProps.meatDef
            : pawnKindDef.RaceProps.leatherDef;
        return resourceDef != null && TriggerThreshold.ThresholdFilter.Allows(resourceDef);
    }

    private bool IsValidResource(PawnKindDef pawnKindDef)
    {
        ThingDef resourceDef = TargetResource == HuntingTargetResource.Meat
            ? pawnKindDef.RaceProps.meatDef
            : pawnKindDef.RaceProps.leatherDef;
        return resourceDef != null;
    }

    private bool IsCountedResource(Pawn pawn) => IsCountedResource(pawn.kindDef);

    private bool IsCountedResource(Corpse corpse) => IsCountedResource(corpse.InnerPawn);

    private void ConfigureThresholdTriggerParentFilter()
    {
        TriggerThreshold.ParentFilter.SetDisallowAll();
        if (TargetResource == HuntingTargetResource.Meat)
        {
            foreach (var item in Utilities_Hunting.GetMapPawnKindDefs(Manager, false)
                .Where(IsValidResource))
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
            foreach (var item in Utilities_Hunting.GetMapPawnKindDefs(Manager, false)
                .Where(IsValidResource))
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
