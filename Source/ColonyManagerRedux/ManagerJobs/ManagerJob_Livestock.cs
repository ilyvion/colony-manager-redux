// ManagerJob_Livestock.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Reflection;
using Verse.Sound;

namespace ColonyManagerRedux;

[HotSwappable]
internal sealed class ManagerJob_Livestock : ManagerJob
{
    public sealed class History : HistoryWorker<ManagerJob_Livestock>
    {
        public override int GetCountForHistoryChapter(ManagerJob_Livestock managerJob, ManagerJobHistoryChapterDef chapterDef)
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryAdultFemale)
            {
                return managerJob.TriggerPawnKind.GetCountFor(AgeAndSex.AdultFemale);
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryAdultMale)
            {
                return managerJob.TriggerPawnKind.GetCountFor(AgeAndSex.AdultMale);
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryJuvenileFemale)
            {
                return managerJob.TriggerPawnKind.GetCountFor(AgeAndSex.JuvenileFemale);
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryJuvenileMale)
            {
                return managerJob.TriggerPawnKind.GetCountFor(AgeAndSex.JuvenileMale);
            }
            else
            {
                throw new ArgumentException($"Unexpected chapterDef value {chapterDef.defName}");
            }
        }

        public override int GetTargetForHistoryChapter(ManagerJob_Livestock managerJob, ManagerJobHistoryChapterDef chapterDef)
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryAdultFemale)
            {
                return managerJob.TriggerPawnKind.GetTargetFor(AgeAndSex.AdultFemale);
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryAdultMale)
            {
                return managerJob.TriggerPawnKind.GetTargetFor(AgeAndSex.AdultMale);
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryJuvenileFemale)
            {
                return managerJob.TriggerPawnKind.GetTargetFor(AgeAndSex.JuvenileFemale);
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryJuvenileMale)
            {
                return managerJob.TriggerPawnKind.GetTargetFor(AgeAndSex.JuvenileMale);
            }
            return 0;
        }
    }

    private static readonly MethodInfo SetWanted_MI
        = AccessTools.Method(typeof(Pawn_TrainingTracker), "SetWanted");
    public bool ButcherBonded;
    public bool ButcherExcess;
    public bool ButcherPregnant;
    public bool ButcherTrained;
    public bool FollowDrafted;
    public bool FollowFieldwork;
    public bool FollowTraining;
    public Pawn? Master;
    public MasterMode Masters;
    public Area? MilkArea;
    public bool RespectBonds = true;
    public Area?[] RestrictArea;
    public bool RestrictToArea;
    public bool SendToMilkingArea;
    public bool SendToShearingArea;
    public bool SendToSlaughterArea;
    public bool SendToTrainingArea;
    public bool SetFollow;
    public Area? ShearArea;
    public Area? SlaughterArea;
    public Area? TameArea;
    public Pawn? Trainer;
    public MasterMode Trainers;
    public TrainingTracker Training;
    public Area? TrainingArea;
    public bool TryTameMore;

    private CachedValue<string>? _cachedLabel;
    private List<Designation> _designations;

    public Trigger_PawnKind TriggerPawnKind => (Trigger_PawnKind)Trigger!;

    public ManagerJob_Livestock(Manager manager) : base(manager)
    {
        // init designations
        _designations = [];

        // set up the trigger, set all target counts to 5
        Trigger = new Trigger_PawnKind(this);

        // set all training to false
        Training = new TrainingTracker();

        // set areas for restriction and taming to unrestricted
        TameArea = null;
        RestrictToArea = false;
        RestrictArea = Utilities_Livestock.AgeSexArray.Select(k => (Area?)null).ToArray();

        // set up sending animals designated for slaughter to an area (freezer)
        SendToSlaughterArea = false;
        SlaughterArea = null;

        // set up milking area
        SendToMilkingArea = false;
        MilkArea = null;

        // set up shearing area
        SendToShearingArea = false;
        ShearArea = null;

        // set up training area
        SendToTrainingArea = false;
        TrainingArea = null;

        // taming
        TryTameMore = false;
        TameArea = null;

        // set defaults for butchering
        ButcherExcess = true;
        ButcherTrained = false;
        ButcherPregnant = false;
        ButcherBonded = false;

        // following
        SetFollow = true;
        FollowDrafted = true;
        FollowFieldwork = true;
        FollowTraining = false;
        Masters = MasterMode.Manual;
        Master = null;
        Trainers = MasterMode.Manual;
        Trainer = null;
    }

    public override void PostMake()
    {
        var livestockSettings = ColonyManagerReduxMod.Settings.ManagerJobSettingsFor<ManagerJobSettings_Livestock>(def);
        if (livestockSettings != null)
        {
            for (int i = 0; i < livestockSettings.DefaultCountTargets.Length; i++)
            {
                TriggerPawnKind.CountTargets[i] = livestockSettings.DefaultCountTargets[i];
            }
            TryTameMore = livestockSettings.DefaultTryTameMore;
            ButcherExcess = livestockSettings.DefaultButcherExcess;
            ButcherTrained = livestockSettings.DefaultButcherTrained;
            ButcherPregnant = livestockSettings.DefaultButcherPregnant;
            ButcherBonded = livestockSettings.DefaultButcherBonded;

            foreach (var def in TrainingTracker.TrainableDefs)
            {
                var report = CanBeTrained(TriggerPawnKind.pawnKind, def, out bool visible);
                if (report.Accepted && visible && livestockSettings.EnabledTrainingTargets.Contains(def))
                {
                    Training[def] = true;
                }
            }
            Training.UnassignTraining = livestockSettings.DefaultUnassignTraining;
            Training.TrainYoung = livestockSettings.DefaultTrainYoung;

            Masters = livestockSettings.DefaultMasterMode;
            RespectBonds = livestockSettings.DefaultRespectBonds;
            SetFollow = livestockSettings.DefaultSetFollow;
            FollowDrafted = livestockSettings.DefaultFollowDrafted;
            FollowFieldwork = livestockSettings.DefaultFollowFieldwork;
            FollowTraining = livestockSettings.DefaultFollowTraining;
            Trainers = livestockSettings.DefaultTrainerMode;
        }
    }

    public override void PostImport()
    {
        base.PostImport();
        TriggerPawnKind.job = this;
    }

    public ManagerJob_Livestock(Manager manager, PawnKindDef pawnKindDef) : this(manager) // set defaults
    {
        // set pawnkind and get list of current colonist pawns of that def.
        TriggerPawnKind.pawnKind = pawnKindDef;
    }

    public override bool IsCompleted => TriggerPawnKind.State;

    public List<Designation> Designations => new(_designations);

    public string FullLabel
    {
        get
        {
            if (_cachedLabel != null && _cachedLabel.TryGetValue(out var label))
            {
                return label;
            }

            string labelGetter()
            {
                var text = Label + "\n<i>";
                foreach (var ageSex in Utilities_Livestock.AgeSexArray)
                {
                    text += TriggerPawnKind.pawnKind.GetTame(Manager, ageSex).Count() + "/" +
                        TriggerPawnKind.CountTargets[(int)ageSex] +
                        ", ";
                }

                text += TriggerPawnKind.pawnKind.GetWild(Manager).Count() + "</i>";
                return text;
            }
            _cachedLabel = new CachedValue<string>(labelGetter(), 250, labelGetter);
            return labelGetter();
        }
    }

    public override bool IsValid => base.IsValid && Training != null && Trigger != null;

    public override string Label => TriggerPawnKind.pawnKind.GetLabelPlural().CapitalizeFirst();

    public override IEnumerable<string> Targets
    {
        get
        {
            return Utilities_Livestock.AgeSexArray
                .Select(ageSex => $"ColonyManagerRedux.Thresholds.{ageSex}Count".Translate(
                    TriggerPawnKind.pawnKind.GetTame(Manager, ageSex).Count(),
                    TriggerPawnKind.CountTargets[(int)ageSex])
                .Resolve());
        }
    }

    public override WorkTypeDef WorkTypeDef => WorkTypeDefOf.Handling;

    public void AddDesignation(Pawn p, DesignationDef def)
    {
        // create and add designation to the game and our managed list.
        var des = new Designation(p, def);
        _designations.Add(des);
        Manager.map.designationManager.AddDesignation(des);
    }

    public static AcceptanceReport CanBeTrained(PawnKindDef pawnKind, TrainableDef td, out bool visible)
    {
        if (pawnKind.RaceProps.untrainableTags != null)
        {
            for (var index = 0; index < pawnKind.RaceProps.untrainableTags.Count; ++index)
            {
                if (td.MatchesTag(pawnKind.RaceProps.untrainableTags[index]))
                {
                    visible = false;
                    return false;
                }
            }
        }

        if (pawnKind.RaceProps.trainableTags != null)
        {
            for (var index = 0; index < pawnKind.RaceProps.trainableTags.Count; ++index)
            {
                if (td.MatchesTag(pawnKind.RaceProps.trainableTags[index]))
                {
                    if (pawnKind.RaceProps.baseBodySize < td.minBodySize)
                    {
                        visible = true;
                        return new AcceptanceReport(
                            "CannotTrainTooSmall".Translate(pawnKind.LabelCap));
                    }

                    visible = true;
                    return true;
                }
            }
        }

        if (!td.defaultTrainable)
        {
            visible = false;
            return false;
        }

        if (pawnKind.RaceProps.baseBodySize < (double)td.minBodySize)
        {
            visible = true;
            return new AcceptanceReport(
                "ColonyManagerRedux.ManagerLivestock.CannotTrainTooSmall".Translate(
                    pawnKind.GetLabelPlural().CapitalizeFirst()));
        }

        if (pawnKind.RaceProps.trainability.intelligenceOrder < td.requiredTrainability.intelligenceOrder)
        {
            visible = true;
            return
                new AcceptanceReport("CannotTrainNotSmartEnough".Translate(td.requiredTrainability));
        }

        visible = true;
        return true;
    }

    public override void CleanUp()
    {
        foreach (var des in _designations)
        {
            des.Delete();
        }

        _designations.Clear();
    }

    public List<Designation> DesignationsOfOn(DesignationDef def, AgeAndSex ageSex)
    {
        return _designations
            .Where(des => des.def == def
                && des.target.HasThing
                && des.target.Thing is Pawn pawn
                && pawn.PawnIsOfAgeSex(ageSex))
            .ToList();
    }

    public void DoFollowSettings(ref bool actionTaken)
    {
        foreach (var animal in TriggerPawnKind.pawnKind.GetTame(Manager).ToList())
        {
            // training
            Logger.Follow(animal.LabelShort);
            if (animal.training.HasLearned(TrainableDefOf.Obedience))
            {
                if (FollowTraining && animal.training.NextTrainableToTrain() != null)
                {
                    Logger.Follow("\ttraining");
                    if (Trainers != MasterMode.Manual)
                    {
                        SetMaster(animal, Trainers, Trainer, ref actionTaken);
                        SetFollowing(animal, false, true, ref actionTaken);
                    }
                }
                // default 
                else
                {
                    if (Masters != MasterMode.Manual)
                    {
                        SetMaster(animal, Masters, Master, ref actionTaken);
                    }

                    if (SetFollow)
                    {
                        SetFollowing(animal, FollowDrafted, FollowFieldwork, ref actionTaken);
                    }
                }
            }
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref ButcherExcess, "butcherExcess", true);
        Scribe_Values.Look(ref ButcherTrained, "butcherTrained");
        Scribe_Values.Look(ref ButcherPregnant, "butcherPregnant");
        Scribe_Values.Look(ref ButcherBonded, "butcherBonded");
        Scribe_Values.Look(ref RestrictToArea, "restrictToArea");
        Scribe_Values.Look(ref SendToSlaughterArea, "sendToSlaughterArea");
        Scribe_Values.Look(ref SendToMilkingArea, "sendToMilkingArea");
        Scribe_Values.Look(ref SendToShearingArea, "sendToShearingArea");
        Scribe_Values.Look(ref SendToTrainingArea, "sendToTrainingArea");
        Scribe_Values.Look(ref TryTameMore, "tryTameMore");
        Scribe_Values.Look(ref SetFollow, "setFollow", true);
        Scribe_Values.Look(ref FollowDrafted, "followDrafted", true);
        Scribe_Values.Look(ref FollowFieldwork, "followFieldwork", true);
        Scribe_Values.Look(ref FollowTraining, "followTraining");
        Scribe_Values.Look(ref Masters, "masters");
        Scribe_Values.Look(ref Trainers, "trainers");
        Scribe_Values.Look(ref RespectBonds, "respectBonds", true);

        if (Manager.Mode == Manager.ScribingMode.Normal)
        {
            foreach (var ageAndSex in Utilities_Livestock.AgeSexArray)
            {
                Scribe_References.Look(
                    ref RestrictArea[(int)ageAndSex],
                    $"{ageAndSex.ToString().UncapitalizeFirst()}AreaRestriction");
            }

            Scribe_References.Look(ref TameArea, "tameArea");
            Scribe_References.Look(ref SlaughterArea, "slaughterArea");
            Scribe_References.Look(ref MilkArea, "milkArea");
            Scribe_References.Look(ref ShearArea, "shearArea");
            Scribe_References.Look(ref TrainingArea, "trainingArea");
            Scribe_References.Look(ref Master, "master");
            Scribe_References.Look(ref Trainer, "trainer");

            Scribe_Deep.Look(ref Training, "training");

            Utilities.Scribe_Designations(ref _designations, Manager);

            // our current designations
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // populate with all designations.
                _designations.AddRange(
                    Manager.map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Slaughter)
                        .Where(des => ((Pawn)des.target.Thing).kindDef == TriggerPawnKind.pawnKind));
                _designations.AddRange(
                    Manager.map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Tame)
                        .Where(des => ((Pawn)des.target.Thing).kindDef == TriggerPawnKind.pawnKind));
            }
        }
    }

    public Pawn? GetMaster(Pawn animal, MasterMode mode)
    {
        var master = animal.playerSettings.Master;
        var options = animal.kindDef.GetMasterOptions(Manager, mode);

        // if the animal is bonded, and we care about bonds, there's no discussion
        if (RespectBonds)
        {
            var bondee = animal.relations.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond, p => !p.Dead);
            if (bondee != null && TrainableUtility.CanBeMaster(bondee, animal))
            {
                return bondee;
            }
        }

        Logger.Follow(
            $"Getting master for {animal.LabelShort}:\n\tcurrent: {master?.LabelShort ?? "None"}\n\toptions:\n");
#if DEBUG_FOLLOW
        foreach ( var option in options )
        {
            Logger.Follow( $"\t\t{option.LabelShort}\n" );
        }
#endif

        // cop out if no options
        if (options.NullOrEmpty())
        {
            return null;
        }

        // if we currently have a master, our current master is a valid option, 
        // and all the options have roughly equal amounts of pets following them, we don't need to take action
        if (master != null && options.Contains(master) && RoughlyEquallyDistributed(options))
        {
            return master;
        }

        // otherwise, assign a master that has the least amount of current followers.
        return options.MinBy(p => p.GetFollowers().Count);
    }

    public static void SetFollowing(Pawn animal, bool drafted, bool fieldwork, ref bool actionTaken)
    {
        if (animal?.playerSettings == null)
        {
            Log.Warning("NULL!");
            return;
        }

        Logger.Follow(
            $"Current: {animal.playerSettings.followDrafted} | {animal.playerSettings.followFieldwork}, {drafted} | {fieldwork}");
        if (animal.playerSettings.followDrafted != drafted)
        {
            animal.playerSettings.followDrafted = drafted;
            actionTaken = true;
        }

        if (animal.playerSettings.followFieldwork != fieldwork)
        {
            animal.playerSettings.followFieldwork = fieldwork;
            actionTaken = true;
        }
    }

    public void SetMaster(Pawn animal, MasterMode mode, Pawn? specificMaster, ref bool actionTaken)
    {
        switch (mode)
        {
            case MasterMode.Manual:
                break;
            case MasterMode.Specific:
                SetMaster(animal, specificMaster, ref actionTaken);
                break;
            default:
                var master = GetMaster(animal, mode);
                SetMaster(animal, master, ref actionTaken);
                break;
        }
    }

    public static void SetMaster(Pawn animal, Pawn? master, ref bool actionTaken)
    {
        Logger.Follow($"Current: {master?.LabelShort ?? "None"}, New: {master?.LabelShort ?? "None"}");
        if (animal.playerSettings.Master != master)
        {
            animal.playerSettings.Master = master;
            actionTaken = true;
        }
    }

    public override bool TryDoJob()
    {
        // work done?
        var actionTaken = false;

#if DEBUG_LIFESTOCK
        Log.Message( "Doing livestock (" + Trigger.pawnKind.LabelCap + ") job" );
#endif

        // update changes in game designations in our managed list
        // intersect filters our list down to designations that exist both in our list and in the game state.
        // This should handle manual cancellations and natural completions.
        // it deliberately won't add new designations made manually.
        // Note that this also has the unfortunate side-effect of not re-adding designations after loading a game.
        _designations = _designations.Intersect(Manager.map.designationManager.AllDesignations).ToList();

        // handle butchery
        DoButcherJobs(ref actionTaken);

        // handle training
        DoTrainingJobs(ref actionTaken);

        // area restrictions
        DoAreaRestrictions(ref actionTaken);

        // handle taming
        DoTamingJobs(ref actionTaken);

        // follow settings
        DoFollowSettings(ref actionTaken);

        return actionTaken;
    }

    internal void DoTrainingJobs(ref bool actionTaken, bool assign = true)
    {
        actionTaken = false;

        foreach (var ageSex in Utilities_Livestock.AgeSexArray)
        {
            // skip juveniles if TrainYoung is not enabled.
            if (ageSex.Juvenile() && !Training.TrainYoung)
            {
                continue;
            }

            foreach (var animal in TriggerPawnKind.pawnKind.GetTame(Manager, ageSex))
            {
                foreach (var def in TrainingTracker.TrainableDefs)
                {
                    if ( // only train if allowed.
                        animal.training.CanAssignToTrain(def, out _).Accepted &&

                        // only assign training, unless unassign is ticked.
                        animal.training.GetWanted(def) != Training[def] &&
                        (Training[def] || Training.UnassignTraining))
                    {
                        if (assign)
                        {
                            SetWanted_MI.Invoke(animal.training, [def, Training[def]]);
                        }

                        actionTaken = true;
                    }
                }
            }
        }
    }

    private void DoAreaRestrictions(ref bool actionTaken)
    {
        // skip for roamers
        if (TriggerPawnKind.pawnKind.RaceProps.Roamer)
        {
            return;
        }

        for (var i = 0; i < Utilities_Livestock.AgeSexArray.Length; i++)
        {
            foreach (var p in TriggerPawnKind.pawnKind.GetTame(Manager, Utilities_Livestock.AgeSexArray[i]))
            {
                // slaughter
                if (SendToSlaughterArea &&
                     Manager.map.designationManager.DesignationOn(p, DesignationDefOf.Slaughter) != null)
                {
                    actionTaken = p.playerSettings.AreaRestrictionInPawnCurrentMap != SlaughterArea;
                    p.playerSettings.AreaRestrictionInPawnCurrentMap = SlaughterArea;
                }

                // milking
                else if (SendToMilkingArea &&
                    p.GetComp<CompMilkable>() != null &&
                    p.GetComp<CompMilkable>().TicksTillHarvestable() < UpdateInterval.ticks)
                {
                    if (p.playerSettings.AreaRestrictionInPawnCurrentMap != MilkArea)
                    {
                        actionTaken = true;
                        p.playerSettings.AreaRestrictionInPawnCurrentMap = MilkArea;
                    }
                }

                // shearing
                else if (SendToShearingArea &&
                    p.GetComp<CompShearable>() != null &&
                    p.GetComp<CompShearable>().TicksTillHarvestable() < UpdateInterval.ticks)
                {
                    if (p.playerSettings.AreaRestrictionInPawnCurrentMap != ShearArea)
                    {
                        actionTaken = true;
                        p.playerSettings.AreaRestrictionInPawnCurrentMap = ShearArea;
                    }
                }

                // training
                else if (SendToTrainingArea && p.training.NextTrainableToTrain() != null)
                {
                    if (p.playerSettings.AreaRestrictionInPawnCurrentMap != TrainingArea)
                    {
                        actionTaken = true;
                        p.playerSettings.AreaRestrictionInPawnCurrentMap = TrainingArea;
                    }
                }

                // all
                else if (RestrictToArea && p.playerSettings.AreaRestrictionInPawnCurrentMap != RestrictArea[i])
                {
                    actionTaken = true;
                    p.playerSettings.AreaRestrictionInPawnCurrentMap = RestrictArea[i];
                }
            }
        }
    }

    private void DoButcherJobs(ref bool actionTaken)
    {
        if (!ButcherExcess)
        {
            return;
        }

#if DEBUG_LIFESTOCK
        Log.Message( "Doing butchery: " + Trigger.pawnKind.LabelCap );
#endif

        foreach (var ageSex in Utilities_Livestock.AgeSexArray)
        {
            // too many animals?
            var surplus = TriggerPawnKind.pawnKind.GetTame(Manager, ageSex).Count()
                - DesignationsOfOn(DesignationDefOf.Slaughter, ageSex).Count
                - TriggerPawnKind.CountTargets[(int)ageSex];

#if DEBUG_LIFESTOCK
            Log.Message( "Butchering " + ageSex + ", surplus" + surplus );
#endif

            if (surplus > 0)
            {
                // should slaughter oldest adults, youngest juveniles.
                var oldestFirst = ageSex == AgeAndSex.AdultFemale ||
                    ageSex == AgeAndSex.AdultMale;

                // get list of animals in correct sort order.
                var animals = TriggerPawnKind.pawnKind.GetTame(Manager, ageSex)
                    .Where(
                        p => Manager.map.designationManager.DesignationOn(
                            p, DesignationDefOf.Slaughter) == null
                        && (ButcherTrained ||
                            !p.training.HasLearned(TrainableDefOf.Obedience))
                        && (ButcherPregnant || !p.VisiblyPregnant())
                        && (ButcherBonded || !p.BondedWithColonist()))
                    .OrderBy(
                        p => (oldestFirst ? -1 : 1) * p.ageTracker.AgeBiologicalTicks)
                    .ToList();

#if DEBUG_LIFESTOCK
                Log.Message( "Tame animals: " + animals.Count );
#endif

                for (var i = 0; i < surplus && i < animals.Count; i++)
                {
#if DEBUG_LIFESTOCK
                    Log.Message( "Butchering " + animals[i].GetUniqueLoadID() );
#endif
                    AddDesignation(animals[i], DesignationDefOf.Slaughter);
                }
            }

            // remove extra designations
            while (surplus < 0)
            {
                if (TryRemoveDesignation(ageSex, DesignationDefOf.Slaughter))
                {
#if DEBUG_LIFESTOCK
                    Log.Message( "Removed extra butchery designation" );
#endif
                    actionTaken = true;
                    surplus++;
                }
                else
                {
                    break;
                }
            }
        }
    }

    private void DoTamingJobs(ref bool actionTaken)
    {
        if (!TryTameMore)
        {
            return;
        }

        foreach (var ageSex in Utilities_Livestock.AgeSexArray)
        {
            // not enough animals?
            var deficit = TriggerPawnKind.CountTargets[(int)ageSex]
                - TriggerPawnKind.pawnKind.GetTame(Manager, ageSex).Count()
                - DesignationsOfOn(DesignationDefOf.Tame, ageSex).Count;

#if DEBUG_LIFESTOCK
            Log.Message( "Taming " + ageSex + ", deficit: " + deficit );
#endif

            if (deficit > 0)
            {
                // get the 'home' position
                var position = Manager.map.GetBaseCenter();

                // get list of animals in sorted by youngest weighted to distance.
                var animals = TriggerPawnKind.pawnKind.GetWild(Manager, ageSex)
                    .Where(p => p != null &&
                        p.Spawned &&
                        Manager.map.designationManager.DesignationOn(p) == null &&
                        (TameArea == null || TameArea.ActiveCells.Contains(p.Position)) &&
                        IsReachable(p))
                    .ToList();

                // skip if no animals available.
                if (animals.Count == 0)
                {
                    continue;
                }

                animals =
                    animals.OrderBy(p => p.ageTracker.AgeBiologicalTicks / Distance(p, position)).ToList();

#if DEBUG_LIFESTOCK
                Log.Message( "Wild: " + animals.Count );
#endif

                for (var i = 0; i < deficit && i < animals.Count; i++)
                {
#if DEBUG_LIFESTOCK
                    Log.Message( "Adding taming designation: " + animals[i].GetUniqueLoadID() );
#endif
                    AddDesignation(animals[i], DesignationDefOf.Tame);
                }
            }

            // remove extra designations
            while (deficit < 0)
            {
                if (TryRemoveDesignation(ageSex, DesignationDefOf.Tame))
                {
#if DEBUG_LIFESTOCK
                    Log.Message( "Removed extra taming designation" );
#endif
                    actionTaken = true;
                    deficit++;
                }
                else
                {
                    break;
                }
            }
        }
    }

    private bool RoughlyEquallyDistributed(List<Pawn> masters)
    {
        var followerCounts = masters.Select(p => p.GetFollowers(TriggerPawnKind.pawnKind).Count).ToArray();
        return followerCounts.Max() - followerCounts.Min() <= 1;
    }

    private bool TryRemoveDesignation(AgeAndSex ageSex, DesignationDef def)
    {
        // get current designations
        var currentDesignations = DesignationsOfOn(def, ageSex);

        // if none, return false
        if (currentDesignations.Count == 0)
        {
            return false;
        }

        // else, remove one from the game as well as our managed list. (delete last - this should be the youngest/oldest).
        var designation = currentDesignations.Last();
        _designations.Remove(designation);
        designation.Delete();
        return true;
    }


    internal sealed class TrainingTracker : IExposable
    {
        public DefMap<TrainableDef, bool> TrainingTargets = new();
        public bool TrainYoung;
        public bool UnassignTraining;

        public bool Any
        {
            get
            {
                foreach (var def in TrainableDefs)
                {
                    if (TrainingTargets[def])
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public int Count => TrainingTargets.Count;

        public static List<TrainableDef> TrainableDefs => DefDatabase<TrainableDef>.AllDefsListForReading;

        public bool this[TrainableDef index]
        {
            get => TrainingTargets[index];
            set => SetWantedRecursive(index, value);
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref TrainYoung, "trainYoung");
            Scribe_Values.Look(ref UnassignTraining, "unassignTraining");
            Scribe_Deep.Look(ref TrainingTargets, "trainingTargets");
        }

        private void SetWantedRecursive(TrainableDef td, bool wanted)
        {
            // cop out if nothing changed
            if (TrainingTargets[td] == wanted)
            {
                return;
            }

            // make changes
            TrainingTargets[td] = wanted;
            if (wanted)
            {
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
                if (td.prerequisites != null)
                {
                    foreach (var trainable in td.prerequisites)
                    {
                        SetWantedRecursive(trainable, true);
                    }
                }
            }
            else
            {
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
                var enumerable = from t in DefDatabase<TrainableDef>.AllDefsListForReading
                                 where
                                     t.prerequisites != null && t.prerequisites.Contains(td)
                                 select t;
                foreach (var current in enumerable)
                {
                    SetWantedRecursive(current, false);
                }
            }
        }
    }
}
