﻿// ManagerJob_Hunting.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public class ManagerJob_Hunting : ManagerJob
{
    public class History : HistoryWorker<ManagerJob_Hunting>
    {
        public override int GetCountForHistoryChapter(ManagerJob_Hunting managerJob, ManagerJobHistoryChapterDef chapterDef)
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryStock)
            {
                return managerJob.trigger.CurrentCount;
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryDesignated)
            {
                return managerJob.GetMeatInDesignations();
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryCorpses)
            {
                return managerJob.GetMeatInCorpses();
            }
            else
            {
                throw new ArgumentException($"Unexpected chapterDef value {chapterDef.defName}");
            }
        }

        public override int GetTargetForHistoryChapter(ManagerJob_Hunting managerJob, ManagerJobHistoryChapterDef chapterDef)
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryStock)
            {
                return managerJob.trigger.TargetCount;
            }
            return 0;
        }
    }

    private readonly Utilities.CachedValue<int> _corpseCachedValue = new(0);
    private readonly Utilities.CachedValue<int> _designatedCachedValue = new(0);

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

    private Trigger_Threshold trigger;
    public Trigger_Threshold Trigger { get => trigger; }

    public ManagerJob_Hunting(Manager manager) : base(manager)
    {
        // populate the trigger field, set the root category to meats and allow all but human & insect meat.
        trigger = new Trigger_Threshold(this);
        trigger.ThresholdFilter.SetAllow(Utilities_Hunting.MeatRaw, true);

        ConfigureThresholdTriggerParentFilter();
    }

    public override void PostMake()
    {
        var huntingSettings = ColonyManagerReduxMod.Instance.Settings.ManagerJobSettingsFor<ManagerJobSettings_Hunting>(def);
        if (huntingSettings != null)
        {
            UnforbidCorpses = huntingSettings.DefaultUnforbidCorpses;
            _allowHumanLikeMeat = huntingSettings.DefaultAllowHumanLikeMeat;
            _allowInsectMeat = huntingSettings.DefaultAllowInsectMeat;

            if (!_allowHumanLikeMeat)
            {
                foreach (var def in HumanLikeMeatDefs)
                {
                    trigger.ThresholdFilter.SetAllow(def, false);
                }
            }

            if (!_allowInsectMeat)
            {
                trigger.ThresholdFilter.SetAllow(Utilities_Hunting.InsectMeat, false);
            }
        }
    }

    public override void PostImport()
    {
        base.PostImport();
        trigger.job = this;

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
                trigger.ThresholdFilter.SetAllow(def, value);
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
            trigger.ThresholdFilter.SetAllow(Utilities_Hunting.InsectMeat, value);
        }
    }

    public override bool IsCompleted => !trigger.State;

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
            _humanLikeMeatDefs ??=
                    DefDatabase<ThingDef>.AllDefsListForReading
                                         .Where(def => def.category == ThingCategory.Pawn &&
                                                        (def.race?.Humanlike ?? false) &&
                                                        (def.race?.IsFlesh ?? false))
                                         .Select(pk => pk.race.meatDef)
                                         .Distinct()
                                         .ToList();

            return _humanLikeMeatDefs;
        }
    }

    public override bool IsValid => base.IsValid && trigger != null;

    public override string Label => "ColonyManagerRedux.Hunting.Hunting".Translate();

    public override string[] Targets => AllowedAnimals
        .Select(pk => pk.LabelCap.Resolve())
        .ToArray();

    public override WorkTypeDef WorkTypeDef => WorkTypeDefOf.Hunting;

    /// <summary>
    ///     Remove obsolete designations from the list.
    /// </summary>
    public void CleanDesignations()
    {
        // get the intersection of bills in the game and bills in our list.
        var GameDesignations =
            Manager.map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Hunt).ToList();
        _designations = _designations.Intersect(GameDesignations).ToList();
    }

    public override void CleanUp()
    {
        // clear the list of obsolete designations
        CleanDesignations();

        // cancel outstanding designation
        foreach (var des in _designations)
        {
            des.Delete();
        }

        // clear the list completely
        _designations.Clear();
    }

    public string DesignationLabel(Designation designation)
    {
        // label, dist, yield.
        var thing = designation.target.Thing;
        return "ColonyManagerRedux.Manager.DesignationLabel".Translate(
            thing.LabelCap,
            Distance(thing, Manager.map.GetBaseCenter()).ToString("F0"),
            thing.GetStatValue(StatDefOf.MeatAmount).ToString("F0"),
            thing.def.race.meatDef.LabelCap);
    }

    public override void ExposeData()
    {
        // scribe base things
        base.ExposeData();

        // must be after references, because reasons.
        Scribe_Deep.Look(ref trigger, "trigger", this);

        // settings
        Scribe_Collections.Look(ref AllowedAnimals, "allowedAnimals", LookMode.Def);
        Scribe_Values.Look(ref UnforbidCorpses, "unforbidCorpses", true);
        Scribe_Values.Look(ref _allowHumanLikeMeat, "allowHumanLikeMeat");
        Scribe_Values.Look(ref _allowInsectMeat, "allowInsectMeat");

        // don't store history in import/export mode.
        if (Manager.Mode == Manager.ScribingMode.Normal)
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

    public int GetMeatInCorpses()
    {
        // get current count + corpses in storage that is not a grave + designated count
        // current count in storage

        // try get cached value
        if (_corpseCachedValue.TryGetValue(out int count))
        {
            return count;
        }

        // corpses not buried / forbidden
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

    public int GetMeatInDesignations()
    {

        // try get cache
        if (_designatedCachedValue.TryGetValue(out int count))
        {
            return count;
        }

        // designated animals
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

    public void RefreshAllAnimals()
    {
        Logger.Debug("Refreshing all animals");

        _allAnimals = null;
    }
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

    public override bool TryDoJob()
    {
        // did we do any work?
        var workDone = false;

        // clean designations not in area
        CleanAreaDesignations();

        // clean dead designations
        CleanDesignations();

        // add designations that could have been handed out by us
        AddRelevantGameDesignations();

        // get the total count of meat in storage, expected meat in corpses and expected meat in designations.
        var totalCount = trigger.CurrentCount + GetMeatInCorpses() + GetMeatInDesignations();

        // get a list of huntable animals sorted by distance (ignoring obstacles) and expected meat count.
        // note; attempt to balance cost and benefit, current formula: value = meat / ( distance ^ 2)
        var huntableAnimals = GetHuntableAnimalsSorted();

        // while totalCount < count AND we have animals that can be designated, designate animal.
        for (var i = 0; i < huntableAnimals.Count && totalCount < trigger.TargetCount; i++)
        {
            AddDesignation(huntableAnimals[i]);
            totalCount += huntableAnimals[i].EstimatedMeatCount();
            workDone = true;
        }

        // unforbid if required
        if (UnforbidCorpses)
        {
            DoUnforbidCorpses(ref workDone);
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

    private void AddDesignation(Pawn p)
    {
        // create designation
        var des = new Designation(p, DesignationDefOf.Hunt);

        // pass to adder
        AddDesignation(des);
    }

    private void AddRelevantGameDesignations()
    {
        foreach (
            var des in
            Manager.map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Hunt)
                   .Except(_designations)
                   .Where(des => IsValidHuntingTarget(des.target, true)))
        {
            AddDesignation(des, false);
        }
    }

    private void CleanAreaDesignations()
    {
        // huntinggrounds of null denotes unrestricted
        if (HuntingGrounds != null)
        {
            foreach (var des in _designations)
            {
                if (des.target.HasThing &&
                     !HuntingGrounds.ActiveCells.Contains(des.target.Thing.Position))
                {
                    des.Delete();
                }
            }
        }
    }

    // copypasta from autohuntbeacon by Carry
    // https://ludeon.com/forums/index.php?topic=8930.0
    private void DoUnforbidCorpses(ref bool workDone)
    {
        foreach (var corpse in Corpses)
        {
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
                }
            }
        }
    }

    // TODO: refactor into a yielding iterator for performance?
    private List<Pawn> GetHuntableAnimalsSorted()
    {
        // get the 'home' position
        var position = Manager.map.GetBaseCenter();

        return Manager.map.mapPawns.AllPawns
                      .Where(p => IsValidHuntingTarget(p, false))
                      .OrderByDescending(p => p.EstimatedMeatCount() / Distance(p, position))
                      .ToList();
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
        trigger.ParentFilter.SetAllow(Utilities_Hunting.FoodRaw, true);
    }
}
