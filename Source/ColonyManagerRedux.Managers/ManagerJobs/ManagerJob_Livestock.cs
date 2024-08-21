// ManagerJob_Livestock.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Diagnostics.CodeAnalysis;
using Verse.Sound;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed partial class ManagerJob_Livestock : ManagerJob<ManagerSettings_Livestock>
{
    public sealed class History : HistoryWorker<ManagerJob_Livestock>
    {
        public override Coroutine GetCountForHistoryChapterCoroutine(
            ManagerJob_Livestock managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> count)
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryAdultFemale)
            {
                count.Value = managerJob.TriggerPawnKind.GetCountFor(AgeAndSex.AdultFemale, cached: false);
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryAdultMale)
            {
                count.Value = managerJob.TriggerPawnKind.GetCountFor(AgeAndSex.AdultMale, cached: false);
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryJuvenileFemale)
            {
                count.Value = managerJob.TriggerPawnKind.GetCountFor(AgeAndSex.JuvenileFemale, cached: false);
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryJuvenileMale)
            {
                count.Value = managerJob.TriggerPawnKind.GetCountFor(AgeAndSex.JuvenileMale, cached: false);
            }
            else
            {
                throw new ArgumentException($"Unexpected chapterDef value {chapterDef.defName}");
            }
            yield break;
        }

        public override Coroutine GetTargetForHistoryChapterCoroutine(
            ManagerJob_Livestock managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> target)
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryAdultFemale)
            {
                target.Value = managerJob.TriggerPawnKind.GetTargetFor(AgeAndSex.AdultFemale);
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryAdultMale)
            {
                target.Value = managerJob.TriggerPawnKind.GetTargetFor(AgeAndSex.AdultMale);
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryJuvenileFemale)
            {
                target.Value = managerJob.TriggerPawnKind.GetTargetFor(AgeAndSex.JuvenileFemale);
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryJuvenileMale)
            {
                target.Value = managerJob.TriggerPawnKind.GetTargetFor(AgeAndSex.JuvenileMale);
            }
            else
            {
                target.Value = 0;
            }
            yield break;
        }
    }

    public enum LivestockCullingStrategy
    {
        Butcher,
        Release
    }

    public bool CullBonded;
    public bool CullExcess;
    public bool CullPregnant;
    public bool CullTrained;
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
    public bool SendToCullingArea;
    public bool SendToTrainingArea;
    public bool SetFollow;
    public Area? ShearArea;
    public Area? CullingArea;
    public Area? TameArea;
    public Pawn? Trainer;
    public MasterMode Trainers;
    public TrainingTracker Training;
    public Area? TrainingArea;
    public bool TryTameMore;
    public bool TamePastTargets;

    private LivestockCullingStrategy _cullingStrategy = LivestockCullingStrategy.Butcher;
    public LivestockCullingStrategy CullingStrategy
    {
        get => _cullingStrategy; set => _cullingStrategy = value;
    }
    public DesignationDef CullingDesignationDef => _cullingStrategy switch
    {
        LivestockCullingStrategy.Butcher => DesignationDefOf.Slaughter,
        LivestockCullingStrategy.Release => DesignationDefOf.ReleaseAnimalToWild,
        _ => throw new NotImplementedException($"{_cullingStrategy} not handled"),
    };

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
        SendToCullingArea = false;
        CullingArea = null;

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
        TamePastTargets = false;
        TameArea = null;

        // set defaults for culling
        CullExcess = true;
        CullTrained = false;
        CullPregnant = false;
        CullBonded = false;

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
        var livestockSettings = ManagerSettings;
        if (livestockSettings != null)
        {
            PawnKindDef pawnKind = TriggerPawnKind.pawnKind;
            PawnKindSettings pawnKindSettings = livestockSettings.GetSettingsFor(pawnKind);
            for (int i = 0; i < pawnKindSettings.DefaultCountTargets.Length; i++)
            {
                TriggerPawnKind.CountTargets[i] = pawnKindSettings.DefaultCountTargets[i];
            }
            TryTameMore = pawnKindSettings.DefaultTryTameMore;
            TamePastTargets = pawnKindSettings.DefaultTamePastTargets;
            CullExcess = pawnKindSettings.DefaultCullExcess;
            CullTrained = pawnKindSettings.DefaultCullTrained;
            CullPregnant = pawnKindSettings.DefaultCullPregnant;
            CullBonded = pawnKindSettings.DefaultCullBonded;
            _cullingStrategy = pawnKindSettings.DefaultCullingStrategy;

            foreach (var def in TrainingTracker.TrainableDefs)
            {
                var report = CanBeTrained(pawnKind, def, out bool visible);
                if (report.Accepted && visible && pawnKindSettings.EnabledTrainingTargets.Contains(def))
                {
                    Training[def] = true;
                }
            }
            Training.UnassignTraining = pawnKindSettings.DefaultUnassignTraining;
            Training.TrainYoung = pawnKindSettings.DefaultTrainYoung;

            Masters = pawnKindSettings.DefaultMasterMode;
            RespectBonds = pawnKindSettings.DefaultRespectBonds;
            SetFollow = pawnKindSettings.DefaultSetFollow;
            FollowDrafted = pawnKindSettings.DefaultFollowDrafted;
            FollowFieldwork = pawnKindSettings.DefaultFollowFieldwork;
            FollowTraining = pawnKindSettings.DefaultFollowTraining;
            Trainers = pawnKindSettings.DefaultTrainerMode;
        }
    }

    public override void PostImport()
    {
        base.PostImport();
        TriggerPawnKind.Job = this;
    }

    public ManagerJob_Livestock(Manager manager, PawnKindDef pawnKindDef) : this(manager) // set defaults
    {
        // set pawnkind and get list of current colonist pawns of that def.
        TriggerPawnKind.pawnKind = pawnKindDef;
    }

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
                    text += TriggerPawnKind.pawnKind.GetTame(Manager, ageSex, includeGuests: false).Count() + "/" +
                        TriggerPawnKind.CountTargets[(int)ageSex] +
                        ", ";
                }

                text += TriggerPawnKind.pawnKind.GetWild(Manager).Count() + "</i>";
                return text;
            }
            _cachedLabel = new CachedValue<string>(labelGetter);
            return _cachedLabel.Value;
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
                    TriggerPawnKind.pawnKind.GetTame(Manager, ageSex, includeGuests: false).Count(),
                    TriggerPawnKind.CountTargets[(int)ageSex])
                .Resolve());
        }
    }

    public override WorkTypeDef WorkTypeDef => WorkTypeDefOf.Handling;

    public void AddDesignation(Designation des, bool addToGame = true)
    {
        // add to game
        if (addToGame)
        {
            Manager.map.designationManager.AddDesignation(des);
        }

        // add to internal list
        _designations.Add(des);
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
                "ColonyManagerRedux.Livestock.CannotTrainTooSmall".Translate(
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

    public override void CleanUp(ManagerLog? jobLog)
    {
        CleanDeadDesignations(_designations, null, jobLog);
        CleanUpDesignations(_designations, jobLog);
    }

    public void DesignationsOfOn(DesignationDef def, AgeAndSex ageSex, List<Designation> workList)
    {
        workList.Clear();
        workList.AddRange(_designations
            .Where(des => des.def == def
                && des.target.HasThing
                && des.target.Thing is Pawn pawn
                && pawn.PawnIsOfAgeSex(ageSex)));
    }

    public void DoFollowSettings(ManagerLog jobLog, Boxed<bool> workDone)
    {
        foreach (var animal in TriggerPawnKind.pawnKind.GetTame(Manager))
        {
            if (animal.training.HasLearned(TrainableDefOf.Obedience))
            {
                if (FollowTraining && animal.training.NextTrainableToTrain() != null)
                {
                    if (Trainers != MasterMode.Manual)
                    {
                        SetMaster(jobLog, animal, Trainers, Trainer, workDone);
                        SetFollowing(jobLog, animal, false, true, workDone);
                    }
                }
                // default 
                else
                {
                    if (Masters != MasterMode.Manual)
                    {
                        SetMaster(jobLog, animal, Masters, Master, workDone);
                    }

                    if (SetFollow)
                    {
                        SetFollowing(jobLog, animal, FollowDrafted, FollowFieldwork, workDone);
                    }
                }
            }
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref CullExcess, "butcherExcess", true);
        Scribe_Values.Look(ref CullTrained, "butcherTrained");
        Scribe_Values.Look(ref CullPregnant, "butcherPregnant");
        Scribe_Values.Look(ref CullBonded, "butcherBonded");
        Scribe_Values.Look(
            ref _cullingStrategy, "cullingStrategy", LivestockCullingStrategy.Butcher);

        Scribe_Values.Look(ref RestrictToArea, "restrictToArea");
        Scribe_Values.Look(ref SendToCullingArea, "sendToSlaughterArea");
        Scribe_Values.Look(ref SendToMilkingArea, "sendToMilkingArea");
        Scribe_Values.Look(ref SendToShearingArea, "sendToShearingArea");
        Scribe_Values.Look(ref SendToTrainingArea, "sendToTrainingArea");
        Scribe_Values.Look(ref TryTameMore, "tryTameMore");
        Scribe_Values.Look(ref TamePastTargets, "tamePastTargets");
        Scribe_Values.Look(ref SetFollow, "setFollow", true);
        Scribe_Values.Look(ref FollowDrafted, "followDrafted", true);
        Scribe_Values.Look(ref FollowFieldwork, "followFieldwork", true);
        Scribe_Values.Look(ref FollowTraining, "followTraining");
        Scribe_Values.Look(ref Masters, "masters");
        Scribe_Values.Look(ref Trainers, "trainers");
        Scribe_Values.Look(ref RespectBonds, "respectBonds", true);

        if (Manager.ScribeGameSpecificData)
        {
            foreach (var ageAndSex in Utilities_Livestock.AgeSexArray)
            {
                Scribe_References.Look(
                    ref RestrictArea[(int)ageAndSex],
                    $"{ageAndSex.ToString().UncapitalizeFirst()}AreaRestriction");
            }

            Scribe_References.Look(ref TameArea, "tameArea");
            Scribe_References.Look(ref CullingArea, "slaughterArea");
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

    public static void SetFollowing(ManagerLog jobLog, Pawn animal, bool drafted, bool fieldwork, Boxed<bool> workDone)
    {
        if (animal?.playerSettings == null)
        {
            ColonyManagerReduxMod.Instance.LogWarning(
                $"Attempted to use SetFollowing on {animal}, which is either null or has " +
                "a null playerSettings field");
            return;
        }

        if (animal.playerSettings.followDrafted != drafted)
        {
            animal.playerSettings.followDrafted = drafted;
            jobLog.AddDetail(
                "ColonyManagerRedux.Livestock.Logs.SetFollow".Translate(
                    animal.Label,
                    drafted ? "" : "ColonyManagerRedux.Livestock.Logs.Not".Translate(),
                    "ColonyManagerRedux.Livestock.Logs.Drafted".Translate()),
                animal);
            workDone.Value = true;
        }

        if (animal.playerSettings.followFieldwork != fieldwork)
        {
            animal.playerSettings.followFieldwork = fieldwork;
            jobLog.AddDetail(
                "ColonyManagerRedux.Livestock.Logs.SetFollow".Translate(
                    animal.Label,
                    fieldwork ? "" : "ColonyManagerRedux.Livestock.Logs.Not".Translate(),
                    "ColonyManagerRedux.Livestock.Logs.FieldWork".Translate()),
                animal);
            workDone.Value = true;
        }
    }

    public void SetMaster(ManagerLog jobLog, Pawn animal, MasterMode mode, Pawn? specificMaster, Boxed<bool> workDone)
    {
        switch (mode)
        {
            case MasterMode.Manual:
                break;
            case MasterMode.Specific:
                SetMaster(jobLog, animal, specificMaster, workDone);
                break;
            default:
                var master = GetMaster(animal, mode);
                SetMaster(jobLog, animal, master, workDone);
                break;
        }
    }

    public static void SetMaster(ManagerLog jobLog, Pawn animal, Pawn? master, Boxed<bool> workDone)
    {
        if (animal.playerSettings.Master != master)
        {
            animal.playerSettings.Master = master;
            jobLog.AddDetail(
                "ColonyManagerRedux.Livestock.Logs.AssigningMasterOf".Translate(
                    master?.Label ?? "ColonyManagerRedux.Livestock.Logs.Nobody".Translate(),
                    animal.Label),
                animal, master);
            workDone.Value = true;
        }
    }

    public override Coroutine TryDoJobCoroutine(
        ManagerLog jobLog,
        Boxed<bool> workDone)
    {
        jobLog.LogLabel = Tab.GetMainLabel(this).Replace("\n", " (") + ")";

        if (!(TamePastTargets && TriggerPawnKind.pawnKind.GetWild(Manager).Any()) && TriggerPawnKind.State)
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

        // clean up designations that were completed.
        CleanDeadDesignations(_designations, null, jobLog);
        yield return ResumeImmediately.Singleton;

        // add designations in the game that could have been handled by this job
        yield return AddRelevantGameDesignations(jobLog).ResumeWhenOtherCoroutineIsCompleted();

        // handle culling
        if (CullExcess)
        {
            var cullingDesignationDef = CullingDesignationDef;
            yield return DoCullingJobs(jobLog, cullingDesignationDef, workDone)
                .ResumeWhenOtherCoroutineIsCompleted();
        }

        // handle training
        yield return DoTrainingJobs(jobLog: jobLog, workDone: workDone)
            .ResumeWhenOtherCoroutineIsCompleted();

        // area restrictions
        // skip for roamers
        if (!TriggerPawnKind.pawnKind.RaceProps.Roamer)
        {
            yield return DoAreaRestrictions(jobLog, workDone).ResumeWhenOtherCoroutineIsCompleted();
        }

        // follow settings
        DoFollowSettings(jobLog, workDone);
        yield return ResumeImmediately.Singleton;

        // handle taming
        if (TryTameMore)
        {
            yield return DoTamingJobs(jobLog, workDone).ResumeWhenOtherCoroutineIsCompleted();
        }
    }

    public Coroutine AddRelevantGameDesignations(ManagerLog jobLog)
    {
        // get list of game designations not managed by this job that could have been assigned by this job.
        int addedCount = 0;
        List<LocalTargetInfo> newTargets = [];
        foreach (
            var des in Manager.map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Slaughter)
                .Except(_designations)
                .Where(des => des.target.Pawn.kindDef == TriggerPawnKind.pawnKind))
        {
            addedCount++;
            AddDesignation(des, false);
            newTargets.Add(des.target);
        }
        yield return ResumeImmediately.Singleton;
        foreach (
            var des in Manager.map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Tame)
                .Except(_designations)
                .Where(des => des.target.Pawn.kindDef == TriggerPawnKind.pawnKind))
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

    internal Coroutine DoTrainingJobs(Boxed<bool> workDone, ManagerLog? jobLog = null, bool assign = true)
    {
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
                    bool trainingDef = Training[def];

                    if ( // only train if allowed.
                        animal.training.CanAssignToTrain(def, out _).Accepted &&

                        // only assign training, unless unassign is ticked.
                        animal.training.GetWanted(def) != trainingDef &&
                        (trainingDef || Training.UnassignTraining))
                    {
                        if (assign)
                        {
                            animal.training.SetWanted(def, trainingDef);
                            jobLog?.AddDetail((trainingDef
                                ? "ColonyManagerRedux.Livestock.Logs.AddTraining"
                                : "ColonyManagerRedux.Livestock.Logs.RemoveTraining")
                                .Translate(
                                    def.label,
                                    animal.Label),
                                animal);
                        }

                        workDone.Value = true;
                    }
                }
                yield return ResumeImmediately.Singleton;
            }
        }
    }

    private Coroutine DoAreaRestrictions(ManagerLog jobLog, Boxed<bool> workDone)
    {
        int animalCounter = -1;
        for (var i = 0; i < Utilities_Livestock.AgeSexArray.Length; i++)
        {
            foreach (var animal in TriggerPawnKind.pawnKind.GetTame(Manager,
                Utilities_Livestock.AgeSexArray[i]))
            {
                var currentArea = animal.playerSettings.AreaRestrictionInPawnCurrentMap;
                void SetArea(Area? area) =>
                    animal.playerSettings.AreaRestrictionInPawnCurrentMap = area;

                // slaughter
                if (SendToCullingArea &&
                     Manager.map.designationManager.DesignationOn(animal,
                        DesignationDefOf.Slaughter) != null)
                {
                    workDone.Value |= currentArea != CullingArea;
                    SetArea(CullingArea);
                    jobLog.AddDetail(
                        "ColonyManagerRedux.Livestock.Logs.AssigningToAreaFor".Translate(
                        animal,
                        AreaUtility.AreaAllowedLabel_Area(CullingArea),
                        ManagerWorkGiverDefOf.Slaughter.gerund
                    ), animal);
                }

                // milking
                else if (SendToMilkingArea &&
                    animal.GetComp<CompMilkable>() != null &&
                    animal.GetComp<CompMilkable>().TicksTillHarvestable() < UpdateInterval.Ticks)
                {
                    if (currentArea != MilkArea)
                    {
                        workDone.Value = true;
                        SetArea(MilkArea);
                        jobLog.AddDetail(
                            "ColonyManagerRedux.Livestock.Logs.AssigningToAreaFor".Translate(
                                animal,
                                AreaUtility.AreaAllowedLabel_Area(MilkArea),
                                ManagerWorkGiverDefOf.Milk.gerund
                        ), animal);
                    }
                }

                // shearing
                else if (SendToShearingArea &&
                    animal.GetComp<CompShearable>() != null &&
                    animal.GetComp<CompShearable>().TicksTillHarvestable() < UpdateInterval.Ticks)
                {
                    if (currentArea != ShearArea)
                    {
                        workDone.Value = true;
                        SetArea(ShearArea);
                        jobLog.AddDetail("ColonyManagerRedux.Livestock.Logs.AssigningToAreaFor".Translate(
                            animal, AreaUtility.AreaAllowedLabel_Area(ShearArea), ManagerWorkGiverDefOf.Shear.gerund
                        ), animal);
                    }
                }

                // training
                else if (SendToTrainingArea && animal.training.NextTrainableToTrain() != null)
                {
                    if (currentArea != TrainingArea)
                    {
                        workDone.Value = true;
                        SetArea(TrainingArea);
                        jobLog.AddDetail("ColonyManagerRedux.Livestock.Logs.AssigningToAreaFor".Translate(
                            animal, AreaUtility.AreaAllowedLabel_Area(TrainingArea), ManagerWorkGiverDefOf.Train.gerund
                        ), animal);
                    }
                }

                // all
                else if (RestrictToArea && currentArea != RestrictArea[i])
                {
                    workDone.Value = true;
                    SetArea(RestrictArea[i]);
                    jobLog.AddDetail("ColonyManagerRedux.Livestock.Logs.AssigningToArea".Translate(
                        animal, AreaUtility.AreaAllowedLabel_Area(RestrictArea[i])
                    ), animal);
                }

                if (++animalCounter > 0 && animalCounter % Constants.CoroutineBreakAfter == 0)
                {
                    yield return ResumeImmediately.Singleton;
                }
            }
        }
    }

    private List<Designation> _tmpDesignations = [];
    private Coroutine DoCullingJobs(
        ManagerLog jobLog, DesignationDef cullingDesignationDef, Boxed<bool> workDone)
    {
        using var _ = new DoOnDispose(_tmpDesignations.Clear);
        foreach (var ageSex in Utilities_Livestock.AgeSexArray)
        {
            // too many animals?
            int animalCount = TriggerPawnKind.pawnKind
                .GetTame(Manager, ageSex, includeGuests: false).Count();
            DesignationsOfOn(cullingDesignationDef, ageSex, _tmpDesignations);
            int alreadyCulling = _tmpDesignations.Count;
            int target = TriggerPawnKind.CountTargets[(int)ageSex];
            var targetDifference = animalCount - alreadyCulling - target;

            jobLog.AddDetail("ColonyManagerRedux.Livestock.Logs.CurrentCountCulling".Translate(
                $"ColonyManagerRedux.Livestock.Logs.{cullingDesignationDef.defName}".Translate(),
                TriggerPawnKind.pawnKind.label,
                ageSex.GetLabel(true),
                animalCount,
                target,
                alreadyCulling,
                $"ColonyManagerRedux.Livestock.Logs.{cullingDesignationDef.defName}.Action"
                    .Translate(),
                targetDifference < 0
                    ? "ColonyManagerRedux.Livestock.Logs.TooFew".Translate(-targetDifference)
                    : "ColonyManagerRedux.Livestock.Logs.TooMany".Translate(targetDifference)));

            if (targetDifference > 0)
            {
                // should cull oldest adults, youngest juveniles.
                var oldestFirst = ageSex.IsAdult();

                // get list of animals in correct sort order.
                var animals = TriggerPawnKind.pawnKind
                    .GetTame(Manager, ageSex, includeGuests: false)
                    .Where(
                        p => Manager.map.designationManager.DesignationOn(
                            p, cullingDesignationDef) == null
                        && (CullTrained ||
                            !p.training.HasLearned(TrainableDefOf.Obedience))
                        && (CullPregnant || !p.VisiblyPregnant())
                        && (CullBonded || !p.BondedWithColonist()))
                    // cull least trained animals first
                    .OrderBy(p => p.training.learned.Count(l => l.Value))
                    .ThenBy(
                        p => (oldestFirst ? -1 : 1) * p.ageTracker.AgeBiologicalTicks);

                var animalsEnumerator = animals.GetEnumerator();

                for (var i = 0; i < targetDifference && animalsEnumerator.MoveNext(); i++)
                {
                    Pawn animal = animalsEnumerator.Current;
                    AddDesignation(new(animal, cullingDesignationDef));
                    animalCount--;
                    jobLog.AddDetail("ColonyManagerRedux.Livestock.Logs.AddDesignation"
                        .Translate(
                            cullingDesignationDef.ActionText(),
                            animal.Label,
                            ageSex.GetLabel(),
                            animalCount,
                            target),
                        animal);
                    workDone.Value = true;
                }
                yield return ResumeImmediately.Singleton;
            }

            // remove extra designations
            bool didRemove = false;
            while (targetDifference < 0)
            {
                if (TryRemoveDesignation(ageSex, cullingDesignationDef, out var animal))
                {
                    workDone.Value = true;
                    targetDifference++;
                    animalCount++;
                    didRemove = true;

                    jobLog.AddDetail("ColonyManagerRedux.Livestock.Logs.RemoveDesignation"
                        .Translate(
                            DesignationDefOf.Slaughter.ActionText(),
                            animal.Label,
                            ageSex.GetLabel(),
                            animalCount,
                            target),
                        animal);
                }
                else
                {
                    break;
                }
            }
            if (didRemove)
            {
                yield return ResumeImmediately.Singleton;
            }
        }
    }

    private Coroutine DoTamingJobs(ManagerLog jobLog, Boxed<bool> workDone)
    {
        using var _ = new DoOnDispose(_tmpDesignations.Clear);

        int animalCounter = -1;
        foreach (var ageSex in Utilities_Livestock.AgeSexArray)
        {
            // not enough animals?
            int target = TriggerPawnKind.CountTargets[(int)ageSex];
            int animalCount = TriggerPawnKind.pawnKind.GetTame(Manager, ageSex, includeGuests: false).Count();
            DesignationsOfOn(DesignationDefOf.Tame, ageSex, _tmpDesignations);
            int alreadyTaming = _tmpDesignations.Count;
            var targetDifference = target
                - animalCount
                - alreadyTaming;

            if (!TamePastTargets)
            {
                jobLog.AddDetail("ColonyManagerRedux.Livestock.Logs.CurrentCountTame".Translate(
                    TriggerPawnKind.pawnKind.label,
                    ageSex.GetLabel(true),
                    animalCount,
                    target,
                    alreadyTaming,
                    targetDifference > 0
                        ? "ColonyManagerRedux.Livestock.Logs.TooFew".Translate(targetDifference)
                        : "ColonyManagerRedux.Livestock.Logs.TooMany".Translate(-targetDifference)));
            }
            else
            {
                jobLog.AddDetail("ColonyManagerRedux.Livestock.Logs.TamePastTargets".Translate(
                    "ColonyManagerRedux.Livestock.TamePastTargets".Translate(),
                    ageSex.GetLabel(true)));
            }

            if (targetDifference > 0 || TamePastTargets)
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
                    .OrderBy(p => p.ageTracker.AgeBiologicalTicks / Distance(p, position));

                var animalEnumerator = animals.GetEnumerator();
                for (var i = 0; (TamePastTargets || i < targetDifference) && animalEnumerator.MoveNext(); i++)
                {
                    Pawn animal = animalEnumerator.Current;
                    AddDesignation(new(animal, DesignationDefOf.Tame));
                    animalCount++;
                    jobLog.AddDetail("ColonyManagerRedux.Livestock.Logs.AddDesignation"
                        .Translate(
                            DesignationDefOf.Tame.ActionText(),
                            ageSex.GetLabel(),
                            animal.Label,
                            animalCount,
                            target),
                        animal);
                    workDone.Value = true;

                    if (++animalCounter > 0 && animalCounter % Constants.CoroutineBreakAfter == 0)
                    {
                        yield return ResumeImmediately.Singleton;
                    }
                }
            }

            // remove extra designations
            while (targetDifference < 0 && !TamePastTargets)
            {
                if (TryRemoveDesignation(ageSex, DesignationDefOf.Tame, out var animal))
                {
                    workDone.Value = true;
                    targetDifference++;
                    animalCount--;

                    jobLog.AddDetail("ColonyManagerRedux.Livestock.Logs.RemoveDesignation"
                        .Translate(
                            DesignationDefOf.Tame.ActionText(),
                            ageSex.GetLabel(),
                            animal.Label,
                            animalCount,
                            target),
                        animal);
                }
                else
                {
                    break;
                }

                if (++animalCounter > 0 && animalCounter % Constants.CoroutineBreakAfter == 0)
                {
                    yield return ResumeImmediately.Singleton;
                }
            }
        }
    }

    private bool RoughlyEquallyDistributed(List<Pawn> masters)
    {
        var followerCounts = masters.Select(p => p.GetFollowers(TriggerPawnKind.pawnKind).EnumerableCount()).ToArray();
        return followerCounts.Max() - followerCounts.Min() <= 1;
    }

    private bool TryRemoveDesignation(AgeAndSex ageSex, DesignationDef def, [NotNullWhen(true)] out Pawn? animal)
    {
        animal = null;

        // get current designations
        using var _ = new DoOnDispose(_tmpDesignations.Clear);
        DesignationsOfOn(def, ageSex, _tmpDesignations);

        // if none, return false
        if (_tmpDesignations.Count == 0)
        {
            return false;
        }

        // else, remove one from the game as well as our managed list. (delete last - this should be the youngest/oldest).
        var designation = _tmpDesignations.Last();
        animal = (Pawn)designation.target.Thing;
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

    protected override void Notify_AreaRemoved(Area area)
    {
        if (MilkArea == area)
        {
            MilkArea = null;
        }
        if (ShearArea == area)
        {
            ShearArea = null;
        }
        if (CullingArea == area)
        {
            CullingArea = null;
        }
        if (TameArea == area)
        {
            TameArea = null;
        }
        if (TrainingArea == area)
        {
            TrainingArea = null;
        }
        for (int i = 0; i < RestrictArea.Length; i++)
        {
            if (RestrictArea[i] == area)
            {
                RestrictArea[i] = null;
            }
        }
    }
}
