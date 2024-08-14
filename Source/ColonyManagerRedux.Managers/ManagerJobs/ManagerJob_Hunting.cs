// ManagerJob_Hunting.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

internal sealed class ManagerJob_Hunting : ManagerJob<ManagerSettings_Hunting>
{
    public sealed class History : HistoryWorker<ManagerJob_Hunting>
    {
        public override int GetCountForHistoryChapter(ManagerJob_Hunting managerJob, int tick, ManagerJobHistoryChapterDef chapterDef)
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryStock)
            {
                return managerJob.TriggerThreshold.GetCurrentCount(cached: false);
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryDesignated)
            {
                return managerJob.GetMeatInDesignations(cached: false);
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryCorpses)
            {
                return managerJob.GetMeatInCorpses(cached: false);
            }
            else
            {
                throw new ArgumentException($"Unexpected chapterDef value {chapterDef.defName}");
            }
        }

        public override int GetTargetForHistoryChapter(ManagerJob_Hunting managerJob, int tick, ManagerJobHistoryChapterDef chapterDef)
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryStock)
            {
                return managerJob.TriggerThreshold.TargetCount;
            }
            return 0;
        }
    }

    private readonly CachedValue<int> _corpseCachedValue = new(0);
    private readonly CachedValue<int> _designatedCachedValue = new(0);

    public HashSet<PawnKindDef> AllowedAnimals = [];
    public Area? HuntingGrounds;
    public bool UnforbidCorpses = true;
    private bool _allowHumanLikeMeat;

    private bool _allowInsectMeat;
    private List<Designation> _designations = [];
    private List<ThingDef>? _humanLikeMeatDefs;

    private List<PawnKindDef>? _allAnimals;
    public List<PawnKindDef> AllAnimals
    {
        get
        {
            _allAnimals ??= Utilities_Hunting.GetAnimals(Manager).ToList();
            return _allAnimals;
        }
    }

    public Trigger_Threshold TriggerThreshold => (Trigger_Threshold)Trigger!;

    public ManagerJob_Hunting(Manager manager) : base(manager)
    {
        // populate the trigger field, set the root category to meats and allow all but human & insect meat.
        Trigger = new Trigger_Threshold(this);
        TriggerThreshold.ThresholdFilter.SetAllow(ThingCategoryDefOf.MeatRaw, true);

        ConfigureThresholdTriggerParentFilter();
    }

    public override void PostMake()
    {
        var huntingSettings = ManagerSettings;
        if (huntingSettings != null)
        {
            UnforbidCorpses = huntingSettings.DefaultUnforbidCorpses;
            _allowHumanLikeMeat = huntingSettings.DefaultAllowHumanLikeMeat;
            _allowInsectMeat = huntingSettings.DefaultAllowInsectMeat;

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

        AllowedAnimals.RemoveWhere(a => !AllAnimals.Contains(a));
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

    public List<Corpse> Corpses
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
                    AllowedAnimals.Contains(thing.InnerPawn.kindDef)).ToList();
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

    public override WorkTypeDef WorkTypeDef => WorkTypeDefOf.Hunting;

    /// <summary>
    ///     Remove obsolete designations from the list.
    /// </summary>
    public void CleanDesignations(ManagerLog? jobLog = null)
    {
        var originalCount = _designations.Count;
        var gameDesignations =
            Manager.map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Hunt);
        _designations = _designations.Intersect(gameDesignations).ToList();
        var newCount = _designations.Count;

        if (originalCount != newCount)
        {
            jobLog?.AddDetail("ColonyManagerRedux.Logs.CleanDeadDesignations"
                .Translate(originalCount - newCount, originalCount, newCount));
        }
    }

    public override void CleanUp(ManagerLog? jobLog)
    {
        // clear the list of obsolete designations
        CleanDesignations(jobLog);

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
            jobLog?.AddDetail("ColonyManagerRedux.Logs.CleanJobCompletedDesignations"
                .Translate(originalCount - newCount, originalCount, newCount));
        }
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
        Scribe_Collections.Look(ref AllowedAnimals, "allowedAnimals", LookMode.Def);
        Scribe_Values.Look(ref UnforbidCorpses, "unforbidCorpses", true);
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
        }
    }

    public int GetMeatInCorpses(bool cached = true)
    {
        // get current count + corpses in storage that is not a grave + designated count
        // current count in storage

        // try get cached value
        if (cached && _corpseCachedValue.TryGetValue(out int cachedCount))
        {
            return cachedCount;
        }

        // corpses not buried / forbidden
        var count = 0;
        foreach (Thing current in Corpses)
        {
            // make sure it's a real corpse. (I dunno, poke it?)
            // and that it's not forbidden (anymore) and can be reached.
            if (current is Corpse corpse &&
                 !corpse.IsForbidden(Faction.OfPlayer) &&
                 Manager.map.reachability.CanReachColony(corpse.Position))
            {
                // check to see if it's buried.
                var buried = false;
                var slotGroup = Manager.map.haulDestinationManager.SlotGroupAt(corpse.Position);

                // Sarcophagus inherits grave
                if (slotGroup?.parent is Building_Storage building_Storage &&
                     building_Storage.def == ThingDefOf.Grave)
                {
                    buried = true;
                }

                // get the rottable comp and check how far gone it is.
                var rottable = corpse.TryGetComp<CompRottable>();

                if (!buried && rottable?.Stage == RotStage.Fresh)
                {
                    count += corpse.EstimatedMeatCount();
                }
            }
        }

        // set cache
        _corpseCachedValue.Update(count);

        return count;
    }

    public int GetMeatInDesignations(bool cached = false)
    {
        if (cached && _designatedCachedValue.TryGetValue(out int cachedCount))
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
        _designatedCachedValue.Update(count);

        return count;
    }

    public void RefreshAllAnimals() => _allAnimals = null;

    public void SetAnimalAllowed(PawnKindDef animal, bool allow)
    {
        if (allow)
        {
            AllowedAnimals.Add(animal);
        }
        else
        {
            AllowedAnimals.Remove(animal);
        }
    }

    public override bool TryDoJob(ManagerLog jobLog)
    {
        // did we do any work?
        var workDone = false;

        if (!TriggerThreshold.State)
        {
            if (JobState != ManagerJobState.Completed)
            {
                JobState = ManagerJobState.Completed;
                jobLog.AddDetail("ColonyManagerRedux.Logs.JobCompleted".Translate());

                CleanUp(jobLog);
            }
            return workDone;
        }
        else
        {
            JobState = ManagerJobState.Active;
        }

        // clean dead designations
        CleanDesignations(jobLog);

        // clean designations not in area
        CleanAreaDesignations(jobLog);

        // add designations that could have been handed out by us
        AddRelevantGameDesignations(jobLog);

        // get the total count of meat in storage, expected meat in corpses and expected meat in designations.
        var totalCount = TriggerThreshold.GetCurrentCount() + GetMeatInCorpses() + GetMeatInDesignations();
        if (totalCount >= TriggerThreshold.TargetCount)
        {
            jobLog.AddDetail("ColonyManagerRedux.Logs.TargetsAlreadySatisfied".Translate(
                "ColonyManagerRedux.Hunting.Logs.Animals".Translate(),
                Def.label
            ));
            return false;
        }

        jobLog.AddDetail("ColonyManagerRedux.Logs.CurrentCount".Translate(totalCount, TriggerThreshold.TargetCount));

        // unforbid if allowed
        if (UnforbidCorpses)
        {
            DoUnforbidCorpses(jobLog, ref workDone, ref totalCount);

            if (workDone && totalCount >= TriggerThreshold.TargetCount)
            {
                return workDone;
            }
        }

        // get a list of huntable animals sorted by distance (ignoring obstacles) and expected meat count.
        // note; attempt to balance cost and benefit, current formula: value = meat / ( distance ^ 2)
        var huntableAnimals = GetHuntableAnimalsSorted();

        if (huntableAnimals.Count == 0)
        {
            jobLog.AddDetail("ColonyManagerRedux.Logs.NoValidTargets".Translate(
                "ColonyManagerRedux.Hunting.Logs.Animals".Translate(),
                Def.label
            ));
        }

        // while totalCount < count AND we have animals that can be designated, designate animal.
        foreach (var huntableAnimal in huntableAnimals)
        {
            if (totalCount >= TriggerThreshold.TargetCount)
            {
                break;
            }

            AddDesignation(new(huntableAnimal, DesignationDefOf.Hunt));
            int yield = huntableAnimal.EstimatedMeatCount();
            totalCount += yield;
            jobLog.AddDetail("ColonyManagerRedux.Logs.AddDesignation"
                .Translate(
                    DesignationDefOf.Hunt.ActionText(),
                    "ColonyManagerRedux.Hunting.Logs.Animal".Translate(),
                    huntableAnimal.Label,
                    yield,
                    totalCount,
                    TriggerThreshold.TargetCount),
                huntableAnimal);
            workDone = true;
        }

        return workDone;
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
        // get list of game designations not managed by this job that could have been assigned by this job.
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

    // copypasta from autohuntbeacon by Carry
    // https://ludeon.com/forums/index.php?topic=8930.0
    private void DoUnforbidCorpses(ManagerLog jobLog, ref bool workDone, ref int totalCount)
    {
        foreach (var corpse in Corpses)
        {
            if (totalCount >= TriggerThreshold.TargetCount)
            {
                break;
            }

            // don't unforbid corpses in storage - we're going to assume they were manually set.
            if (corpse != null &&
                 !corpse.IsInAnyStorage() &&
                 corpse.IsForbidden(Faction.OfPlayer))
            {
                // only fresh corpses
                var comp = corpse.GetComp<CompRottable>();
                if (comp != null &&
                     comp.Stage == RotStage.Fresh)
                {
                    // unforbid
                    workDone = true;
                    corpse.SetForbidden(false, false);

                    int yield = corpse.EstimatedMeatCount();
                    totalCount += yield;
                    jobLog.AddDetail("ColonyManagerRedux.Hunting.Logs.UnforbidCorpse"
                        .Translate(
                            corpse.Label,
                            yield,
                            totalCount,
                            TriggerThreshold.TargetCount),
                        corpse);
                }
            }
        }
    }

    private List<Pawn> GetHuntableAnimalsSorted()
    {
        // get the 'home' position
        var position = Manager.map.GetBaseCenter();

        return Manager.map.mapPawns.AllPawns
            .Where(p => IsValidHuntingTarget(p, false))
            .OrderByDescending(p => p.EstimatedMeatCount() / Distance(p, position)).ToList();
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

    private void ConfigureThresholdTriggerParentFilter()
    {
        TriggerThreshold.ParentFilter.SetAllow(ManagerThingCategoryDefOf.FoodRaw, true);
    }
}
