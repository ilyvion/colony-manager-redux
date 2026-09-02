// ManagerJob_Livestock.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024–2026 Alexander Krivács Schrøder

using System.Text;
using Verse.AI;
using Verse.Sound;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
[CoroutineSettingsType]
internal sealed partial class ManagerJob_Livestock
    : ManagerJob<
        ManagerSettings,
        ManagerDefaultSettings_Livestock,
        ManagerJob_Livestock.LivestockWorkData
    >
{
    // What GatherJobDataCoroutine decides needs to happen; ExecuteJobDataCoroutine applies it.
    // Unlike some other migrated jobs, Livestock doesn't branch on a single "Kind" - culling,
    // training, area restriction, follow settings, and taming are independent concerns that are
    // all considered on every run, so each gets its own decision list here. No field here should
    // ever be read as a signal that a change has already happened.
    internal sealed class LivestockWorkData
    {
        // Kind == culling
        public List<(
            Pawn Animal,
            AgeAndSex AgeSex,
            int AnimalCountAfter,
            int Target
        )> AnimalsToCull = [];
        public List<(
            Pawn Animal,
            AgeAndSex AgeSex,
            int AnimalCountAfter,
            int Target
        )> CullingsToStop = [];

        // training
        public List<(Pawn Animal, TrainableDef Def, bool Wanted)> TrainingsToSet = [];

        // area restriction
        public List<(Pawn Animal, Area? Area, AreaAssignReason Reason)> AreasToSet = [];

        // follow settings
        public List<(Pawn Animal, Pawn? Master)> MastersToSet = [];
        public List<(Pawn Animal, bool Drafted, bool Fieldwork)> FollowSettingsToSet = [];

        // taming
        public List<(
            Pawn Animal,
            AgeAndSex AgeSex,
            int AnimalCountAfter,
            int Target
        )> AnimalsToTame = [];
        public List<(
            Pawn Animal,
            AgeAndSex AgeSex,
            int AnimalCountAfter,
            int Target
        )> TamingsToStop = [];
    }

    /// <summary>
    /// Which UI-facing reason an area assignment in <see cref="LivestockWorkData.AreasToSet"/>
    /// was made for; used to build the matching log message once the mutation is actually
    /// applied in the execute phase, since the message text can't be resolved before then.
    /// </summary>
    internal enum AreaAssignReason
    {
        Culling,
        Milking,
        Shearing,
        Training,
        Trained,
        Restrict,
    }

    public sealed class History : HistoryWorker<ManagerJob_Livestock>
    {
        public override Coroutine GetCountForHistoryChapterCoroutine(
            ManagerJob_Livestock managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> count
        )
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryAdultFemale)
            {
                count.Value = managerJob.TriggerPawnKind.GetCountFor(
                    AgeAndSex.AdultFemale,
                    cached: false
                );
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryAdultMale)
            {
                count.Value = managerJob.TriggerPawnKind.GetCountFor(
                    AgeAndSex.AdultMale,
                    cached: false
                );
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryJuvenileFemale)
            {
                count.Value = managerJob.TriggerPawnKind.GetCountFor(
                    AgeAndSex.JuvenileFemale,
                    cached: false
                );
            }
#pragma warning disable IDE0045
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryJuvenileMale)
            {
                count.Value = managerJob.TriggerPawnKind.GetCountFor(
                    AgeAndSex.JuvenileMale,
                    cached: false
                );
            }
            else
            {
                throw new ArgumentException($"Unexpected chapterDef value {chapterDef.defName}");
            }
#pragma warning restore IDE0045
            yield break;
        }

        public override Coroutine GetTargetForHistoryChapterCoroutine(
            ManagerJob_Livestock managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> target
        )
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
#pragma warning disable IDE0045
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryJuvenileMale)
            {
                target.Value = managerJob.TriggerPawnKind.GetTargetFor(AgeAndSex.JuvenileMale);
            }
            else
            {
                target.Value = 0;
            }
#pragma warning restore IDE0045
            yield break;
        }
    }

    public enum LivestockCullingStrategy
    {
        None,
        Butcher,
        Release,
        Sterilize,
    }

    public bool CullTrained;
    public bool CullPregnant;
    public bool CullBonded;
    public bool AvoidCullingMilkable;
    public float AvoidCullingMilkableThreshold = 0.7f;
    public bool AvoidCullingShearable;
    public float AvoidCullingShearableThreshold = 0.7f;
    public bool AvoidCullingNamed;
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
    public bool SendToTrainedArea;
    public bool SetFollow;
    public Area? ShearArea;
    public Area? CullingArea;
    public Area? TameArea;
    public bool InvertTameArea;
    public Pawn? Trainer;
    public MasterMode Trainers;
    public TrainingTracker Training;
    public Area? TrainingArea;
    public Area? TrainedArea;
    public bool TryTameMore;
    public bool TamePastTargets;

    private LivestockCullingStrategy _cullingStrategy = LivestockCullingStrategy.Butcher;
    public LivestockCullingStrategy CullingStrategy
    {
        get => _cullingStrategy;
        set
        {
            _cullingStrategy = value;
            _cachedLabel.Invalidate();
        }
    }
    public bool CullExcess => CullingStrategy != LivestockCullingStrategy.None;

    public CullingAction CullingStrategyAction =>
        _cullingStrategy switch
        {
            LivestockCullingStrategy.None => new NoneCullingAction(this),
            LivestockCullingStrategy.Butcher => new DesignationCullingAction(
                this,
                DesignationDefOf.Slaughter
            ),
            LivestockCullingStrategy.Release => new DesignationCullingAction(
                this,
                DesignationDefOf.ReleaseAnimalToWild
            ),
            LivestockCullingStrategy.Sterilize => new SterilizeCullingAction(this),
            _ => throw new NotImplementedException($"{_cullingStrategy} not handled"),
        };

    private readonly CachedValue<string> _cachedLabel;
    private List<Designation> _designations;

    public Trigger_PawnKind TriggerPawnKind => (Trigger_PawnKind)Trigger!;

    public ManagerJob_Livestock(Manager manager)
        : base(manager)
    {
        _cachedLabel = new CachedValue<string>(LabelGenerator);

        // init designations
        _designations = [];

        // set up the trigger, set all target counts to 5
        Trigger = new Trigger_PawnKind(this);

        // set all training to false
        Training = new TrainingTracker();

        // set areas for restriction and taming to unrestricted
        TameArea = null;
        InvertTameArea = false;
        RestrictToArea = false;
        RestrictArea = [.. Utilities_Livestock.AgeSexArray.Select(k => (Area?)null)];

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

        // set up trained area
        SendToTrainedArea = false;
        TrainedArea = null;

        // taming
        TryTameMore = false;
        TamePastTargets = false;
        TameArea = null;
        InvertTameArea = false;

        // set defaults for culling
        CullTrained = false;
        CullPregnant = false;
        CullBonded = false;
        AvoidCullingNamed = false;

        // following
        SetFollow = true;
        FollowDrafted = true;
        FollowFieldwork = true;
        FollowTraining = false;
        Masters = MasterMode.Manual;
        Master = null;
        Trainers = MasterMode.Manual;
        Trainer = null;

        TamingPawnSortScore = DefaultTamingPawnSortScore;
        CullingPawnSorter = DefaultCullingPawnSorter;
    }

    public override void PostMake()
    {
        var livestockSettings = ManagerDefaultSettings;
        if (livestockSettings == null)
        {
            return;
        }

        var pawnKind = TriggerPawnKind.pawnKind;
        if (pawnKind == null)
        {
            return;
        }

        var pawnKindSettings = livestockSettings.GetSettingsFor(pawnKind);
        for (var i = 0; i < pawnKindSettings.DefaultCountTargets.Length; i++)
        {
            TriggerPawnKind.CountTargets[i] = pawnKindSettings.DefaultCountTargets[i];
        }
        TryTameMore = pawnKindSettings.DefaultTryTameMore;
        TamePastTargets = pawnKindSettings.DefaultTamePastTargets;
        CullTrained = pawnKindSettings.DefaultCullTrained;
        CullPregnant = pawnKindSettings.DefaultCullPregnant;
        CullBonded = pawnKindSettings.DefaultCullBonded;
        AvoidCullingNamed = pawnKindSettings.DefaultAvoidCullingNamed;
        _cullingStrategy = pawnKindSettings.DefaultCullingStrategy;

        foreach (var def in TrainingTracker.TrainableDefs)
        {
            var report = CanBeTrained(pawnKind, def, out var visible);
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

    public override void PostImport()
    {
        base.PostImport();
        TriggerPawnKind.Job = this;
    }

    public ManagerJob_Livestock(Manager manager, PawnKindDef pawnKindDef)
        : this(manager) // set defaults
    {
        // set pawnkind and get list of current colonist pawns of that def.
        TriggerPawnKind.pawnKind = pawnKindDef;
    }

    public List<Designation> Designations => [.. _designations];

    public string FullLabel =>
        _cachedLabel.TryGetValue(out var label)
            ? label
            : (
                TriggerPawnKind.pawnKind == null
                    ? TriggerPawnKind.ExpectedPawnKindName
                    : _cachedLabel.Value
            );

    private string LabelGenerator()
    {
        var text = Label + "\n";
        foreach (var ageSex in Utilities_Livestock.AgeSexArray)
        {
            var tame = TriggerPawnKind
                .pawnKind!.GetTame(Manager, ageSex, includeGuests: false)
                .Count();
            var culled = CullingStrategyAction.GetAlreadyCulledForAgeSex(ageSex);
            var target = TriggerPawnKind.CountTargets[(int)ageSex];
            text += FormatAgeSexBucket(
                tame,
                culled,
                target,
                CullingStrategyAction.CullingRemovesAnimals
            );
        }

        text += $"{TriggerPawnKind.pawnKind!.GetWild(Manager).Count()}";
        return text;
    }

    /// <summary>
    /// Formats a single age/sex bucket's portion of <see cref="LabelGenerator"/>'s label, e.g.
    /// "3/5(+2), " — showing the already-culled-adjusted count against the target, plus a
    /// "(+N)" suffix (the still-pending cull count) when culling doesn't actually remove
    /// animals from the tame count.
    /// </summary>
    internal static string FormatAgeSexBucket(
        int tame,
        int culled,
        int target,
        bool cullingRemovesAnimals
    )
    {
        var text = $"{tame - culled}/{target}";
        if (!cullingRemovesAnimals)
        {
            text += $"(+{culled})";
        }
        return text + ", ";
    }

    public override bool IsValid => base.IsValid && Training != null && Trigger != null;

    public override string Label =>
        (TriggerPawnKind.pawnKind?.GetLabelPlural().CapitalizeFirst())
        ?? TriggerPawnKind.ExpectedPawnKindName;

    public override IEnumerable<string> Targets =>
        Utilities_Livestock.AgeSexArray.Select(ageSex =>
            $"ColonyManagerRedux.Thresholds.{ageSex}Count"
                .Translate(
                    TriggerPawnKind.pawnKind?.GetTame(Manager, ageSex, includeGuests: false).Count()
                        ?? 0,
                    TriggerPawnKind.CountTargets[(int)ageSex]
                )
                .Resolve()
        );

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

    public static AcceptanceReport CanBeTrained(
        PawnKindDef pawnKind,
        TrainableDef td,
        out bool visible
    ) => CanBeTrained(
#if !v1_5
            ModsConfig.OdysseyActive,
#else
            false,
#endif
            pawnKind, td, out visible);

    /// <summary>
    /// Same as <see cref="CanBeTrained(PawnKindDef, TrainableDef, out bool)"/>, but with
    /// <see cref="ModsConfig.OdysseyActive"/> passed in explicitly so this logic is
    /// unit-testable without depending on which DLCs happen to be active.
    /// </summary>
    internal static AcceptanceReport CanBeTrained(
        bool odysseyActive,
        PawnKindDef pawnKind,
        TrainableDef td,
        out bool visible
    )
    {
        var raceProps = pawnKind.RaceProps;
        var untrainableTags = raceProps.untrainableTags;

        if (untrainableTags != null)
        {
            foreach (var tag in untrainableTags)
            {
                if (td.MatchesTag(tag))
                {
                    visible = false;
                    return false;
                }
            }
        }

#if !v1_5
        if (odysseyActive && td.specialTrainable)
        {
            var specialTrainables = raceProps.specialTrainables;
            if (specialTrainables == null || !specialTrainables.Contains(td))
            {
                visible = false;
                return false;
            }
        }
#endif

        var trainableTags = raceProps.trainableTags;
        var baseBodySize = raceProps.baseBodySize;
        var minBodySize = td.minBodySize;
        if (trainableTags != null)
        {
            foreach (var tag in trainableTags)
            {
                if (td.MatchesTag(tag))
                {
                    if (baseBodySize < minBodySize)
                    {
                        visible = true;
                        return new AcceptanceReport(
                            "CannotTrainTooSmall".Translate(pawnKind.LabelCap)
                        );
                    }

                    visible = true;
                    return true;
                }
            }
        }

        if (!td.defaultTrainable
#if !v1_5
            && !td.specialTrainable
#endif
        )
        {
            visible = false;
            return false;
        }

        if (baseBodySize < (double)td.minBodySize)
        {
            visible = true;
            return new AcceptanceReport(
                "ColonyManagerRedux.Livestock.CannotTrainTooSmall".Translate(
                    pawnKind.GetLabelPlural().CapitalizeFirst()
                )
            );
        }

        if (
#if !v1_5
            td.requiredTrainability != null
            &&
#endif
            raceProps.trainability.intelligenceOrder < td.requiredTrainability.intelligenceOrder)
        {
            visible = true;
            return new AcceptanceReport(
                "CannotTrainNotSmartEnough".Translate(td.requiredTrainability)
            );
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
        workList.AddRange(
            _designations.Where(des =>
                des.def == def
                && des.target.HasThing
                && des.target.Thing is Pawn pawn
                && pawn.PawnIsOfAgeSex(ageSex)
            )
        );
    }

    /// <summary>
    /// Decides which animals need their master and/or follow settings changed, without
    /// touching the game. The original single-phase code ran this as a single unthrottled pass
    /// over every tame animal (no coroutine, no per-item yields); since it now runs as part of
    /// a coroutine and iterates the same potentially-large "all tame animals of this kind" set
    /// as <see cref="PlanAreaRestrictions"/>, it's given the same per-item throttling here
    /// rather than repeating the missing-throttle mistake called out for the Mining migration.
    /// </summary>
    [CoroutineSettingsMethod]
    private Coroutine PlanFollowSettings(LivestockWorkData data)
    {
        if (TriggerPawnKind.pawnKind == null)
        {
            yield break;
        }

        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            PlanFollowSettings
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                PlanFollowSettings
            );

        var animalCounter = -1;
        foreach (var animal in TriggerPawnKind.pawnKind.GetTame(Manager))
        {
            if (animal.training.HasLearned(TrainableDefOf.Obedience))
            {
                if (FollowTraining && animal.training.NextTrainableToTrain() != null)
                {
                    if (Trainers != MasterMode.Manual)
                    {
                        PlanMaster(animal, Trainers, Trainer, data);
                        PlanFollowing(animal, false, true, data);
                    }
                }
                // default
                else
                {
                    if (Masters != MasterMode.Manual)
                    {
                        PlanMaster(animal, Masters, Master, data);
                    }

                    if (SetFollow)
                    {
                        PlanFollowing(animal, FollowDrafted, FollowFieldwork, data);
                    }
                }
            }

            if (++animalCounter > 0 && animalCounter % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    private void PlanMaster(
        Pawn animal,
        MasterMode mode,
        Pawn? specificMaster,
        LivestockWorkData data
    )
    {
        if (mode == MasterMode.Manual)
        {
            return;
        }

        var master = mode == MasterMode.Specific ? specificMaster : GetMaster(animal, mode);
        if (animal.playerSettings.Master != master)
        {
            data.MastersToSet.Add((animal, master));
        }
    }

    private static void PlanFollowing(
        Pawn animal,
        bool drafted,
        bool fieldwork,
        LivestockWorkData data
    )
    {
        if (animal?.playerSettings == null)
        {
            ColonyManagerReduxMod.Instance.LogWarning(
                $"Attempted to use PlanFollowing on {animal}, which is either null or has "
                    + "a null playerSettings field"
            );
            return;
        }

        if (
            animal.playerSettings.followDrafted != drafted
            || animal.playerSettings.followFieldwork != fieldwork
        )
        {
            data.FollowSettingsToSet.Add((animal, drafted, fieldwork));
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine ExecuteFollowSettings(
        ManagerLog jobLog,
        LivestockWorkData data,
        Boxed<bool> workDone
    )
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            ExecuteFollowSettings
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                ExecuteFollowSettings
            );

        var i = 0;
        foreach (var (animal, master) in data.MastersToSet)
        {
            // The execute phase is gated behind ~95% of the job's work timer, so the animal
            // planned for a master change during gather may have died or been sold by now, or
            // its master may already have changed again since.
            if (!animal.DestroyedOrNull() && animal.playerSettings.Master != master)
            {
                animal.playerSettings.Master = master;
                jobLog.AddDetail(
                    "ColonyManagerRedux.Livestock.Logs.AssigningMasterOf".Translate(
                        master?.Label ?? "ColonyManagerRedux.Livestock.Logs.Nobody".Translate(),
                        animal.Label
                    ),
                    animal,
                    master
                );
                workDone.Value = true;
            }

            i++;
            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }

        foreach (var (animal, drafted, fieldwork) in data.FollowSettingsToSet)
        {
            if (!animal.DestroyedOrNull() && animal.playerSettings != null)
            {
                if (animal.playerSettings.followDrafted != drafted)
                {
                    animal.playerSettings.followDrafted = drafted;
                    jobLog.AddDetail(
                        "ColonyManagerRedux.Livestock.Logs.SetFollow".Translate(
                            animal.Label,
                            drafted ? "" : "ColonyManagerRedux.Livestock.Logs.Not".Translate(),
                            "ColonyManagerRedux.Livestock.Logs.Drafted".Translate()
                        ),
                        animal
                    );
                    workDone.Value = true;
                }

                if (animal.playerSettings.followFieldwork != fieldwork)
                {
                    animal.playerSettings.followFieldwork = fieldwork;
                    jobLog.AddDetail(
                        "ColonyManagerRedux.Livestock.Logs.SetFollow".Translate(
                            animal.Label,
                            fieldwork ? "" : "ColonyManagerRedux.Livestock.Logs.Not".Translate(),
                            "ColonyManagerRedux.Livestock.Logs.FieldWork".Translate()
                        ),
                        animal
                    );
                    workDone.Value = true;
                }
            }

            i++;
            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    private static readonly string?[] _tmpRestrictAreaLabel =
    [
        .. Utilities_Livestock.AgeSexArray.Select(k => (string?)null),
    ];
    private string? _tmpTameAreaLabel;
    private string? _tmpCullingAreaLabel;
    private string? _tmpMilkAreaLabel;
    private string? _tmpShearAreaLabel;
    private string? _tmpTrainingAreaLabel;
    private string? _tmpTrainedAreaLabel;

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(
            ref _cullingStrategy,
            "cullingStrategy",
            LivestockCullingStrategy.Butcher
        );
        ForwardCompatibleButcherExcess();
        Scribe_Values.Look(ref CullTrained, "butcherTrained");
        Scribe_Values.Look(ref CullPregnant, "butcherPregnant");
        Scribe_Values.Look(ref CullBonded, "butcherBonded");
        Scribe_Values.Look(ref AvoidCullingMilkable, "avoidCullingMilkable");
        Scribe_Values.Look(
            ref AvoidCullingMilkableThreshold,
            "avoidCullingMilkableThreshold",
            0.7f
        );
        Scribe_Values.Look(ref AvoidCullingShearable, "avoidCullingShearable");
        Scribe_Values.Look(
            ref AvoidCullingShearableThreshold,
            "avoidCullingShearableThreshold",
            0.7f
        );
        Scribe_Values.Look(ref AvoidCullingNamed, "avoidCullingNamed");

        Scribe_Values.Look(ref RestrictToArea, "restrictToArea");
        Scribe_Values.Look(ref SendToCullingArea, "sendToSlaughterArea");
        Scribe_Values.Look(ref SendToMilkingArea, "sendToMilkingArea");
        Scribe_Values.Look(ref SendToShearingArea, "sendToShearingArea");
        Scribe_Values.Look(ref SendToTrainingArea, "sendToTrainingArea");
        Scribe_Values.Look(ref SendToTrainedArea, "sendToTrainedArea");
        Scribe_Values.Look(ref TryTameMore, "tryTameMore");
        Scribe_Values.Look(ref TamePastTargets, "tamePastTargets");
        Scribe_Values.Look(ref InvertTameArea, "invertTameArea");
        Scribe_Values.Look(ref SetFollow, "setFollow", true);
        Scribe_Values.Look(ref FollowDrafted, "followDrafted", true);
        Scribe_Values.Look(ref FollowFieldwork, "followFieldwork", true);
        Scribe_Values.Look(ref FollowTraining, "followTraining");
        Scribe_Values.Look(ref Masters, "masters");
        Scribe_Values.Look(ref Trainers, "trainers");
        Scribe_Values.Look(ref RespectBonds, "respectBonds", true);

        if (Manager.ScribeSameGameData)
        {
            Scribe_References.Look(ref Master, "master");
            Scribe_References.Look(ref Trainer, "trainer");

            Scribe_Deep.Look(ref Training, "training");
        }
        if (Manager.ScribeSameMapData)
        {
            foreach (var ageAndSex in Utilities_Livestock.AgeSexArray)
            {
                Scribe_References.Look(
                    ref RestrictArea[(int)ageAndSex],
                    $"{ageAndSex.ToString().UncapitalizeFirst()}AreaRestriction"
                );
            }

            Scribe_References.Look(ref TameArea, "tameArea");
            Scribe_References.Look(ref CullingArea, "slaughterArea");
            Scribe_References.Look(ref MilkArea, "milkArea");
            Scribe_References.Look(ref ShearArea, "shearArea");
            Scribe_References.Look(ref TrainingArea, "trainingArea");
            Scribe_References.Look(ref TrainedArea, "trainedArea");

            Utilities.Scribe_Designations(ref _designations, Manager);

            // our current designations
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (TriggerPawnKind.pawnKind == null)
                {
                    var errorText = new StringBuilder(
                        "Loaded Livestock job without a valid pawn kind. This most likely means "
                            + "that the mod that added this pawn kind was removed from the game. "
                    );
                    _ =
                        TriggerPawnKind.ExpectedPawnKindNameRaw != null
                            ? errorText
                                .Append("The pawn kind was saved as '")
                                .Append(TriggerPawnKind.ExpectedPawnKindNameRaw)
                                .Append("'. ")
                            : errorText
                                .Append("The pawn kind was not saved. That usually means ")
                                .Append("that the game was saved after this error had already ")
                                .Append("happened, and the information is therefore lost. ");
                    ColonyManagerReduxMod.Instance.LogError(
                        errorText
                            .Append("Remember to remove jobs that reference other mods' ")
                            .Append("content before removing them from your game mid-save.")
                            .ToString()
                    );

                    CausedException = new InvalidOperationException(
                        $"Loaded Livestock job without a valid pawn kind: {TriggerPawnKind.ExpectedPawnKindName}"
                    );
                }

                // populate with all designations.
                if (CullingStrategyAction is DesignationCullingAction designationCullingAction)
                {
                    _designations.AddRange(
                        Manager
                            .map.designationManager.SpawnedDesignationsOfDef(
                                designationCullingAction.DesignationDef
                            )
                            .Where(des =>
                                ((Pawn)des.target.Thing).kindDef == TriggerPawnKind.pawnKind
                            )
                    );
                }
                _designations.AddRange(
                    Manager
                        .map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Tame)
                        .Where(des => ((Pawn)des.target.Thing).kindDef == TriggerPawnKind.pawnKind)
                );

                // We no longer mark livestock jobs as complete, so set them all back to active
                JobState = ManagerJobState.Active;
            }
        }
        else
        {
            foreach (var ageAndSex in Utilities_Livestock.AgeSexArray)
            {
                Utilities.Scribe_AreaByLabel(
                    ref RestrictArea[(int)ageAndSex],
                    ref _tmpRestrictAreaLabel[(int)ageAndSex],
                    $"{ageAndSex.ToString().UncapitalizeFirst()}AreaRestriction",
                    Manager.map.areaManager
                );
            }
            Utilities.Scribe_AreaByLabel(
                ref TameArea,
                ref _tmpTameAreaLabel,
                "tameArea",
                Manager.map.areaManager
            );
            Utilities.Scribe_AreaByLabel(
                ref CullingArea,
                ref _tmpCullingAreaLabel,
                "slaughterArea",
                Manager.map.areaManager
            );
            Utilities.Scribe_AreaByLabel(
                ref MilkArea,
                ref _tmpMilkAreaLabel,
                "milkArea",
                Manager.map.areaManager
            );
            Utilities.Scribe_AreaByLabel(
                ref ShearArea,
                ref _tmpShearAreaLabel,
                "shearArea",
                Manager.map.areaManager
            );
            Utilities.Scribe_AreaByLabel(
                ref TrainingArea,
                ref _tmpTrainingAreaLabel,
                "trainingArea",
                Manager.map.areaManager
            );
            Utilities.Scribe_AreaByLabel(
                ref TrainedArea,
                ref _tmpTrainedAreaLabel,
                "trainedArea",
                Manager.map.areaManager
            );
        }
    }

    private bool? _oldCullExcessValue;

    private void ForwardCompatibleButcherExcess()
    {
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            if (Scribe.EnterNode("butcherExcess"))
            {
                Scribe.ExitNode();
                var oldCullExcess = false;
                Scribe_Values.Look(ref oldCullExcess, "butcherExcess");
                _oldCullExcessValue = oldCullExcess;
            }
        }
        if (Scribe.mode == LoadSaveMode.PostLoadInit && _oldCullExcessValue.HasValue)
        {
            ColonyManagerReduxMod.Instance.LogMessage(
                "Detected old 'butcherExcess' value while loading livestock job. "
                    + "This setting has been replaced with a more flexible 'culling strategy' setting. "
                    + $"The old value was '{_oldCullExcessValue.Value}'. "
                    + "If it was 'false', culling strategy has been set to 'None'. "
                    + "If it was 'true', culling strategy didn't change."
            );
            if (!_oldCullExcessValue.Value)
            {
                _cullingStrategy = LivestockCullingStrategy.None;
            }
            _oldCullExcessValue = null;
        }
    }

    public Pawn? GetMaster(Pawn animal, MasterMode mode)
    {
        var master = animal.playerSettings.Master;
        var options = animal.kindDef.GetMasterOptions(Manager, mode);

        // if the animal is bonded, and we care about bonds, there's no discussion
        if (RespectBonds)
        {
            var bondee = animal.relations.GetFirstDirectRelationPawn(
                PawnRelationDefOf.Bond,
                p => !p.Dead
            );
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

        // forceRefresh is intentional: a stale cache here caused masters to be picked based on
        // outdated follower counts, unevenly piling animals onto the same master (fixes #24).
        return ChooseMaster(
            master,
            options,
            RoughlyEquallyDistributed(options),
            p => p.GetFollowers(forceRefresh: true).Count
        );
    }

    /// <summary>
    /// Decides whether to keep <paramref name="currentMaster"/> or switch to the option with the
    /// fewest followers. Kept separate from <see cref="GetMaster"/> so the "keep vs. switch"
    /// decision is unit-testable without live <see cref="Pawn"/>s or follower-count caches.
    /// </summary>
    internal static T? ChooseMaster<T>(
        T? currentMaster,
        IReadOnlyList<T> options,
        bool currentOptionsRoughlyEquallyDistributed,
        Func<T, int> followerCount
    )
        where T : class
    {
        // if we currently have a master, our current master is a valid option,
        // and all the options have roughly equal amounts of pets following them, we don't need to take action
        if (
            currentMaster != null
            && options.Contains(currentMaster)
            && currentOptionsRoughlyEquallyDistributed
        )
        {
            return currentMaster;
        }

        // otherwise, assign a master that has the least amount of current followers.
        return options.MinBy(followerCount);
    }

    [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
    protected override Coroutine GatherJobDataCoroutine(
        ManagerLog jobLog,
        AnyBoxed<LivestockWorkData?> data
    )
    {
        if (TriggerPawnKind.pawnKind == null)
        {
            throw new InvalidOperationException(
                $"Loaded Livestock job without a valid pawn kind: {TriggerPawnKind.ExpectedPawnKindName}"
            );
        }

        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                (Func<ManagerLog, AnyBoxed<LivestockWorkData?>, Coroutine>)GatherJobDataCoroutine
            );

#if v1_5
        jobLog.LogLabel = Tab.GetMainLabel(this).Replace("\n", " (") + ")";
#else
        jobLog.LogLabel =
            Tab.GetMainLabel(this).Replace("\n", " (", StringComparison.Ordinal) + ")";
#endif

        // Resync our own bookkeeping against designations that have disappeared or that
        // already exist in the game unbeknownst to us; this doesn't change anything in the
        // game itself, so it's safe to do while gathering.
        CleanDeadDesignations(_designations, null, jobLog);
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        yield return AddRelevantGameDesignations(jobLog).ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        var workData = new LivestockWorkData();

        // handle culling
        if (CullExcess)
        {
            yield return PlanCullingJobs(jobLog, CullingStrategyAction, workData)
                .ResumeWhenOtherCoroutineIsCompleted();
            yield return new ResumeAfterTicks(ticksBetweenOperations);
        }

        // handle training
        yield return PlanTrainingJobs(workData).ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        // area restrictions
        // skip for roamers
        if (!(TriggerPawnKind.pawnKind?.RaceProps.Roamer ?? true))
        {
            yield return PlanAreaRestrictions(workData).ResumeWhenOtherCoroutineIsCompleted();
            yield return new ResumeAfterTicks(ticksBetweenOperations);
        }

        // follow settings
        yield return PlanFollowSettings(workData).ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        // handle taming
        if (TryTameMore)
        {
            yield return PlanTamingJobs(jobLog, workData).ResumeWhenOtherCoroutineIsCompleted();
            yield return new ResumeAfterTicks(ticksBetweenOperations);
        }

        data.Value = workData;
    }

    [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
    protected override Coroutine ExecuteJobDataCoroutine(
        ManagerLog jobLog,
        LivestockWorkData data,
        Boxed<bool> workDone
    )
    {
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                (Func<ManagerLog, LivestockWorkData, Boxed<bool>, Coroutine>)ExecuteJobDataCoroutine
            );

        if (CullExcess)
        {
            yield return ExecuteCullingJobs(jobLog, CullingStrategyAction, data, workDone)
                .ResumeWhenOtherCoroutineIsCompleted();
            yield return new ResumeAfterTicks(ticksBetweenOperations);
        }

        yield return ExecuteTrainingJobs(jobLog, data, workDone)
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        yield return ExecuteAreaRestrictions(jobLog, data, workDone)
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        yield return ExecuteFollowSettings(jobLog, data, workDone)
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        if (TryTameMore)
        {
            yield return ExecuteTamingJobs(jobLog, data, workDone)
                .ResumeWhenOtherCoroutineIsCompleted();
        }
    }

    [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
    public Coroutine AddRelevantGameDesignations(ManagerLog jobLog)
    {
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                AddRelevantGameDesignations
            );

        // get list of game designations not managed by this job that could have been assigned by this job.
        var addedCount = 0;
        List<LocalTargetInfo> newTargets = [];
        if (CullingStrategyAction is DesignationCullingAction designationCullingAction)
        {
            foreach (
                var des in Manager
                    .map.designationManager.SpawnedDesignationsOfDef(
                        designationCullingAction.DesignationDef
                    )
                    .Except(_designations)
                    .Where(des => des.target.Pawn.kindDef == TriggerPawnKind.pawnKind)
            )
            {
                addedCount++;
                AddDesignation(des, false);
                newTargets.Add(des.target);
            }
        }
        yield return new ResumeAfterTicks(ticksBetweenOperations);
        foreach (
            var des in Manager
                .map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Tame)
                .Except(_designations)
                .Where(des => des.target.Pawn.kindDef == TriggerPawnKind.pawnKind)
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

    /// <summary>
    /// Decides which animal/trainable-def combinations need their "wanted" flag changed,
    /// without touching the game. Also used directly by <see cref="Trigger_PawnKind"/> for a
    /// dry run (checking whether any training assignment is pending) since it never mutates
    /// anything itself.
    /// </summary>
    [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
    internal Coroutine PlanTrainingJobs(LivestockWorkData data)
    {
        if (TriggerPawnKind.pawnKind == null)
        {
            yield break;
        }

        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(PlanTrainingJobs);

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
                    var trainingDef = Training[def];

                    if ( // only train if allowed.
                        animal.training.CanAssignToTrain(def, out _).Accepted
                        &&
                        // only assign training, unless unassign is ticked.
                        animal.training.GetWanted(def) != trainingDef
                        && (trainingDef || Training.UnassignTraining)
                    )
                    {
                        data.TrainingsToSet.Add((animal, def, trainingDef));
                    }
                }
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine ExecuteTrainingJobs(
        ManagerLog jobLog,
        LivestockWorkData data,
        Boxed<bool> workDone
    )
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            ExecuteTrainingJobs
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                ExecuteTrainingJobs
            );

        var i = 0;
        foreach (var (animal, def, wanted) in data.TrainingsToSet)
        {
            // The execute phase is gated behind ~95% of the job's work timer, so the animal
            // planned for a training change during gather may have died or been sold by now.
            if (!animal.DestroyedOrNull() && animal.training.GetWanted(def) != wanted)
            {
                animal.training.SetWanted(def, wanted);
                jobLog.AddDetail(
                    (
                        wanted
                            ? "ColonyManagerRedux.Livestock.Logs.AddTraining"
                            : "ColonyManagerRedux.Livestock.Logs.RemoveTraining"
                    ).Translate(def.label, animal.Label),
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
    }

    [CoroutineSettingsMethod]
    private Coroutine PlanAreaRestrictions(LivestockWorkData data)
    {
        if (TriggerPawnKind.pawnKind == null)
        {
            yield break;
        }

        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            PlanAreaRestrictions
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                PlanAreaRestrictions
            );

        var animalCounter = -1;
        for (var i = 0; i < Utilities_Livestock.AgeSexArray.Length; i++)
        {
            foreach (
                var animal in TriggerPawnKind.pawnKind.GetTame(
                    Manager,
                    Utilities_Livestock.AgeSexArray[i]
                )
            )
            {
                var currentArea = animal.playerSettings.AreaRestrictionInPawnCurrentMap;

                // culling
                if (SendToCullingArea && CullingStrategyAction.IsAlreadyCulling(animal))
                {
                    // Note: unlike the other branches below, this one is planned unconditionally
                    // whenever it applies, even if currentArea already equals CullingArea - this
                    // matches a pre-existing quirk of the original single-phase code, which
                    // always re-applied and logged here (workDone was the only thing actually
                    // gated on a real change). Preserved as-is rather than "fixed" during this
                    // mechanical migration.
                    data.AreasToSet.Add((animal, CullingArea, AreaAssignReason.Culling));
                }
                // milking
                else if (
                    SendToMilkingArea
                    && animal.GetComp<CompMilkable>() != null
                    && animal.GetComp<CompMilkable>().TicksTillHarvestable() < UpdateInterval.Ticks
                    && currentArea != MilkArea
                )
                {
                    data.AreasToSet.Add((animal, MilkArea, AreaAssignReason.Milking));
                }
                // shearing
                else if (
                    SendToShearingArea
                    && animal.GetComp<CompShearable>() != null
                    && animal.GetComp<CompShearable>().TicksTillHarvestable() < UpdateInterval.Ticks
                    && currentArea != ShearArea
                )
                {
                    data.AreasToSet.Add((animal, ShearArea, AreaAssignReason.Shearing));
                }
                // training
                else if (
                    SendToTrainingArea
                    && animal.training.NextTrainableToTrain() != null
                    && currentArea != TrainingArea
                )
                {
                    data.AreasToSet.Add((animal, TrainingArea, AreaAssignReason.Training));
                }
                // trained
                else if (
                    SendToTrainedArea
                    && animal.training.NextTrainableToTrain() == null
                    && currentArea != TrainedArea
                )
                {
                    data.AreasToSet.Add((animal, TrainedArea, AreaAssignReason.Trained));
                }
                // all
                else if (RestrictToArea && currentArea != RestrictArea[i])
                {
                    data.AreasToSet.Add((animal, RestrictArea[i], AreaAssignReason.Restrict));
                }

                if (++animalCounter > 0 && animalCounter % operationsPerTick == 0)
                {
                    yield return new ResumeAfterTicks(ticksBetweenOperations);
                }
            }
        }
    }

    private static string BuildAreaLogMessage(AreaAssignReason reason, Pawn animal, Area? area) =>
        reason switch
        {
            AreaAssignReason.Culling => "ColonyManagerRedux.Livestock.Logs.AssigningToAreaFor"
                .Translate(
                    animal,
                    AreaUtility.AreaAllowedLabel_Area(area),
                    "ColonyManagerRedux.Livestock.Culling".Translate()
                )
                .Resolve(),
            AreaAssignReason.Milking => "ColonyManagerRedux.Livestock.Logs.AssigningToAreaFor"
                .Translate(
                    animal,
                    AreaUtility.AreaAllowedLabel_Area(area),
                    ManagerWorkGiverDefOf.Milk.gerund
                )
                .Resolve(),
            AreaAssignReason.Shearing => "ColonyManagerRedux.Livestock.Logs.AssigningToAreaFor"
                .Translate(
                    animal,
                    AreaUtility.AreaAllowedLabel_Area(area),
                    ManagerWorkGiverDefOf.Shear.gerund
                )
                .Resolve(),
            AreaAssignReason.Training => "ColonyManagerRedux.Livestock.Logs.AssigningToAreaFor"
                .Translate(
                    animal,
                    AreaUtility.AreaAllowedLabel_Area(area),
                    ManagerWorkGiverDefOf.Train.gerund
                )
                .Resolve(),
            AreaAssignReason.Trained => "ColonyManagerRedux.Livestock.Logs.AssigningToAreaFor"
                .Translate(
                    animal,
                    AreaUtility.AreaAllowedLabel_Area(area),
                    "ColonyManagerRedux.Livestock.Trained".Translate()
                )
                .Resolve(),
            AreaAssignReason.Restrict => "ColonyManagerRedux.Livestock.Logs.AssigningToArea"
                .Translate(animal, AreaUtility.AreaAllowedLabel_Area(area))
                .Resolve(),
            _ => throw new ArgumentException($"Unexpected {nameof(AreaAssignReason)} {reason}"),
        };

    [CoroutineSettingsMethod]
    private Coroutine ExecuteAreaRestrictions(
        ManagerLog jobLog,
        LivestockWorkData data,
        Boxed<bool> workDone
    )
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            ExecuteAreaRestrictions
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                ExecuteAreaRestrictions
            );

        var i = 0;
        foreach (var (animal, area, reason) in data.AreasToSet)
        {
            if (!animal.DestroyedOrNull())
            {
                var currentArea = animal.playerSettings.AreaRestrictionInPawnCurrentMap;
                var changed = currentArea != area;
                animal.playerSettings.AreaRestrictionInPawnCurrentMap = area;

                // matches the pre-existing quirk noted in PlanAreaRestrictions: the culling
                // reason always re-applies + logs, but only counts as work done if the area
                // actually changed.
                workDone.Value |= reason != AreaAssignReason.Culling || changed;

                jobLog.AddDetail(BuildAreaLogMessage(reason, animal, area), animal);
            }

            i++;
            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    private readonly List<Designation> _tmpDesignations = [];

    [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
    private Coroutine PlanCullingJobs(
        ManagerLog jobLog,
        CullingAction cullingAction,
        LivestockWorkData data
    )
    {
        if (TriggerPawnKind.pawnKind == null)
        {
            yield break;
        }

        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(PlanCullingJobs);

        foreach (var ageSex in Utilities_Livestock.AgeSexArray)
        {
            // too many animals?
            var animalCount =
                TriggerPawnKind.pawnKind.GetTame(Manager, ageSex, includeGuests: false).Count()
                - cullingAction.GetAlreadyCulledForAgeSex(ageSex);
            var alreadyCulling = cullingAction.GetAlreadyCullingCountForAgeSex(ageSex);
            var target = TriggerPawnKind.CountTargets[(int)ageSex];
            var targetDifference = CalculateCullingTargetDifference(
                animalCount,
                alreadyCulling,
                target
            );

            jobLog.AddDetail(
                "ColonyManagerRedux.Livestock.Logs.CurrentCountCulling".Translate(
                    $"ColonyManagerRedux.Livestock.Logs.{cullingAction.TranslationKey}".Translate(),
                    TriggerPawnKind.pawnKind.label,
                    ageSex.GetLabel(true),
                    animalCount,
                    target,
                    alreadyCulling,
                    $"ColonyManagerRedux.Livestock.Logs.{cullingAction.TranslationKey}.Action".Translate(),
                    targetDifference < 0
                        ? "ColonyManagerRedux.Livestock.Logs.TooFew".Translate(-targetDifference)
                        : "ColonyManagerRedux.Livestock.Logs.TooMany".Translate(targetDifference)
                )
            );

            if (targetDifference > 0)
            {
                // get list of animals in correct sort order.
                var animalsUnsorted = TriggerPawnKind
                    .pawnKind.GetTame(Manager, ageSex, includeGuests: false)
                    .Where(p =>
                        IsEligibleForCulling(
                            cullingAction.IsAlreadyCulling(p),
                            cullingAction.IsAlreadyCulled(p),
                            CullTrained,
                            p.training.HasLearned(TrainableDefOf.Obedience),
                            CullPregnant,
                            p.VisiblyPregnant(),
                            CullBonded,
                            p.BondedWithColonist(),
                            AvoidCullingMilkable,
                            p.GetMilkFullness(),
                            AvoidCullingMilkableThreshold,
                            AvoidCullingShearable,
                            p.GetWoolFullness(),
                            AvoidCullingShearableThreshold,
                            AvoidCullingNamed,
                            p.Name?.Numerical == false
                        )
                    );
                var animals = CullingPawnSorter(ageSex, animalsUnsorted);

                var animalsEnumerator = animals.GetEnumerator();

                for (var i = 0; i < targetDifference && animalsEnumerator.MoveNext(); i++)
                {
                    var animal = animalsEnumerator.Current;
                    animalCount--;
                    data.AnimalsToCull.Add((animal, ageSex, animalCount, target));
                }
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }

            // plan removing extra designations
            if (targetDifference < 0)
            {
                foreach (var animal in cullingAction.PeekCullingToStop(ageSex, -targetDifference))
                {
                    animalCount++;
                    data.CullingsToStop.Add((animal, ageSex, animalCount, target));
                }
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine ExecuteCullingJobs(
        ManagerLog jobLog,
        CullingAction cullingAction,
        LivestockWorkData data,
        Boxed<bool> workDone
    )
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            ExecuteCullingJobs
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                ExecuteCullingJobs
            );

        var i = 0;
        foreach (var (animal, ageSex, animalCountAfter, target) in data.AnimalsToCull)
        {
            // The execute phase is gated behind ~95% of the job's work timer, so the animal
            // planned for culling during gather may have died or been sold by now.
            if (!animal.DestroyedOrNull())
            {
                cullingAction.Cull(animal);
                jobLog.AddDetail(
                    "ColonyManagerRedux.Livestock.Logs.AddDesignation".Translate(
                        cullingAction.ActionText,
                        animal.Label,
                        ageSex.GetLabel(),
                        animalCountAfter,
                        target
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

        foreach (var (animal, ageSex, animalCountAfter, target) in data.CullingsToStop)
        {
            if (!animal.DestroyedOrNull())
            {
                cullingAction.StopCulling(animal);
                jobLog.AddDetail(
                    "ColonyManagerRedux.Livestock.Logs.RemoveDesignation".Translate(
                        cullingAction.ActionText,
                        animal.Label,
                        ageSex.GetLabel(),
                        animalCountAfter,
                        target
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
    }

    [CoroutineSettingsMethod]
    private Coroutine PlanTamingJobs(ManagerLog jobLog, LivestockWorkData data)
    {
        if (TriggerPawnKind.pawnKind == null)
        {
            yield break;
        }

        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            PlanTamingJobs
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(PlanTamingJobs);

        using var _ = new DoOnDispose(_tmpDesignations.Clear);

        var animalCounter = -1;
        foreach (var ageSex in Utilities_Livestock.AgeSexArray)
        {
            // not enough animals?
            var target = TriggerPawnKind.CountTargets[(int)ageSex];
            var animalCount = TriggerPawnKind
                .pawnKind.GetTame(Manager, ageSex, includeGuests: false)
                .Count();
            DesignationsOfOn(DesignationDefOf.Tame, ageSex, _tmpDesignations);
            var alreadyTaming = _tmpDesignations.Count;
            var targetDifference = CalculateTamingTargetDifference(
                target,
                animalCount,
                alreadyTaming
            );

            if (!TamePastTargets)
            {
                jobLog.AddDetail(
                    "ColonyManagerRedux.Livestock.Logs.CurrentCountTame".Translate(
                        TriggerPawnKind.pawnKind.label,
                        ageSex.GetLabel(true),
                        animalCount,
                        target,
                        alreadyTaming,
                        targetDifference > 0
                            ? "ColonyManagerRedux.Livestock.Logs.TooFew".Translate(targetDifference)
                            : "ColonyManagerRedux.Livestock.Logs.TooMany".Translate(
                                -targetDifference
                            )
                    )
                );
            }
            else
            {
                jobLog.AddDetail(
                    "ColonyManagerRedux.Livestock.Logs.TamePastTargets".Translate(
                        "ColonyManagerRedux.Livestock.TamePastTargets".Translate(),
                        ageSex.GetLabel(true)
                    )
                );
            }

            if (targetDifference > 0 || TamePastTargets)
            {
                // get list of animals in sorted by youngest weighted by distance.
                List<Pawn> sortedAnimals = [];
                yield return GetThingsSorted(
                        TriggerPawnKind.pawnKind.GetWild(Manager, ageSex),
                        sortedAnimals,
                        p =>
                            p != null
                            && p.Spawned
                            && Manager.map.designationManager.DesignationOn(p) == null
                            && Utilities.IsInAllowedArea(TameArea, p.Position, InvertTameArea)
                            && IsReachable(p, PathEndMode.Touch),
                        TamingPawnSortScore,
                        t => t
                    )
                    .ResumeWhenOtherCoroutineIsCompleted();
                yield return new ResumeAfterTicks(ticksBetweenOperations);

                foreach (var (animal, i) in sortedAnimals.Select((a, i) => (a, i)))
                {
                    if (!TamePastTargets && i >= targetDifference)
                    {
                        break;
                    }

                    animalCount++;
                    data.AnimalsToTame.Add((animal, ageSex, animalCount, target));

                    if (++animalCounter > 0 && animalCounter % operationsPerTick == 0)
                    {
                        yield return new ResumeAfterTicks(ticksBetweenOperations);
                    }
                }
            }

            // plan removing extra designations
            if (targetDifference < 0 && !TamePastTargets)
            {
                foreach (
                    var animal in PeekDesignationsToRemove(
                        DesignationDefOf.Tame,
                        ageSex,
                        -targetDifference
                    )
                )
                {
                    animalCount--;
                    data.TamingsToStop.Add((animal, ageSex, animalCount, target));

                    if (++animalCounter > 0 && animalCounter % operationsPerTick == 0)
                    {
                        yield return new ResumeAfterTicks(ticksBetweenOperations);
                    }
                }
            }
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine ExecuteTamingJobs(
        ManagerLog jobLog,
        LivestockWorkData data,
        Boxed<bool> workDone
    )
    {
        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            ExecuteTamingJobs
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(ExecuteTamingJobs);

        var i = 0;
        foreach (var (animal, ageSex, animalCountAfter, target) in data.AnimalsToTame)
        {
            // The execute phase is gated behind ~95% of the job's work timer, so the animal
            // planned for taming during gather may have died, fled, or already been designated
            // by the time we get here.
            if (
                !animal.DestroyedOrNull()
                && animal.Spawned
                && Manager.map.designationManager.DesignationOn(animal) == null
            )
            {
                AddDesignation(new(animal, DesignationDefOf.Tame));
                jobLog.AddDetail(
                    "ColonyManagerRedux.Livestock.Logs.AddDesignation".Translate(
                        DesignationDefOf.Tame.ActionText(),
                        ageSex.GetLabel(),
                        animal.Label,
                        animalCountAfter,
                        target
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

        foreach (var (animal, ageSex, animalCountAfter, target) in data.TamingsToStop)
        {
            if (!animal.DestroyedOrNull())
            {
                RemoveDesignationOn(animal, DesignationDefOf.Tame);
                jobLog.AddDetail(
                    "ColonyManagerRedux.Livestock.Logs.RemoveDesignation".Translate(
                        DesignationDefOf.Tame.ActionText(),
                        ageSex.GetLabel(),
                        animal.Label,
                        animalCountAfter,
                        target
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
    }

    public enum SortingKind
    {
        Taming,
        Culling,
    }

    public Func<Pawn, float, float> TamingPawnSortScore;

    private float DefaultTamingPawnSortScore(Pawn pawn, float distance) =>
        pawn.ageTracker.AgeBiologicalTicks / distance;

    public Func<AgeAndSex, IEnumerable<Pawn>, IEnumerable<Pawn>> CullingPawnSorter;

    private IEnumerable<Pawn> DefaultCullingPawnSorter(
        AgeAndSex ageAndSex,
        IEnumerable<Pawn> pawns
    ) =>
        // should cull oldest adults, youngest juveniles.
        OrderForCulling(
            ageAndSex.IsAdult(),
            pawns,
            p => p.training.learned.Count(l => l.Value),
            p => p.ageTracker.AgeBiologicalTicks
        );

    /// <summary>
    /// Orders <paramref name="items"/> least-trained-first, then by age (oldest first if
    /// <paramref name="oldestFirst"/>, youngest first otherwise). Generic over the item type and
    /// key selectors, kept separate from <see cref="DefaultCullingPawnSorter"/> so this ordering
    /// is unit-testable without constructing live <see cref="Pawn"/>s.
    /// </summary>
    internal static IEnumerable<T> OrderForCulling<T>(
        bool oldestFirst,
        IEnumerable<T> items,
        Func<T, int> learnedTraitCount,
        Func<T, long> ageBiologicalTicks
    ) =>
        items
            .OrderBy(learnedTraitCount)
            .ThenBy(p => (oldestFirst ? -1 : 1) * ageBiologicalTicks(p));

    private bool RoughlyEquallyDistributed(List<Pawn> masters)
    {
        if (TriggerPawnKind.pawnKind == null)
        {
            return true;
        }

        var followerCounts = masters.Select(p =>
            p.GetFollowers(TriggerPawnKind.pawnKind).EnumerableCount()
        );
        return IsRoughlyEquallyDistributed(followerCounts);
    }

    internal static bool IsRoughlyEquallyDistributed(IEnumerable<int> followerCounts)
    {
        var counts = followerCounts.ToArray();
        return counts.Max() - counts.Min() <= 1;
    }

    // Positive means excess (cull more), negative means deficit (stop culling some
    // already-marked animals).
    internal static int CalculateCullingTargetDifference(
        int animalCount,
        int alreadyCulling,
        int target
    ) => animalCount - alreadyCulling - target;

    // Positive means we need to tame more animals to reach the target.
    internal static int CalculateTamingTargetDifference(
        int target,
        int animalCount,
        int alreadyTaming
    ) => target - animalCount - alreadyTaming;

    internal static bool IsEligibleForCulling(
        bool alreadyCulling,
        bool alreadyCulled,
        bool cullTrained,
        bool isTrained,
        bool cullPregnant,
        bool isPregnant,
        bool cullBonded,
        bool isBonded,
        bool avoidMilkable,
        float milkFullness,
        float milkThreshold,
        bool avoidShearable,
        float woolFullness,
        float woolThreshold,
        bool avoidNamed,
        bool isNamed
    ) =>
        !alreadyCulling
        && !alreadyCulled
        && (cullTrained || !isTrained)
        && (cullPregnant || !isPregnant)
        && (cullBonded || !isBonded)
        && (!avoidMilkable || milkFullness < milkThreshold)
        && (!avoidShearable || woolFullness < woolThreshold)
        && (!avoidNamed || !isNamed);

    /// <summary>
    /// Decides which up to <paramref name="count"/> animals currently designated with
    /// <paramref name="def"/> for <paramref name="ageSex"/> would be picked by
    /// <see cref="RemoveDesignationOn"/>, without touching the game. Picks from the end of the
    /// tracked list first (i.e. the same "youngest/oldest" designation last-in-first-out order
    /// the single-phase code used to remove in).
    /// </summary>
    private List<Pawn> PeekDesignationsToRemove(DesignationDef def, AgeAndSex ageSex, int count)
    {
        using var _disposeDesignations = new DoOnDispose(_tmpDesignations.Clear);
        DesignationsOfOn(def, ageSex, _tmpDesignations);

        return [.. TakeLastReversed(_tmpDesignations, count, d => (Pawn)d.target.Thing)];
    }

    /// <summary>
    /// Returns up to <paramref name="count"/> items from the end of <paramref name="items"/>,
    /// most-recently-added first (i.e. <c>items[^1]</c>, then <c>items[^2]</c>, ...) - the same
    /// order the pre-migration single-phase code removed designations in (it always called
    /// <c>.Last()</c> repeatedly). Kept generic and side-effect-free so the selection order is
    /// unit-testable without live <see cref="Designation"/>/<see cref="Pawn"/> objects.
    /// </summary>
    internal static List<TResult> TakeLastReversed<TItem, TResult>(
        IReadOnlyList<TItem> items,
        int count,
        Func<TItem, TResult> selector
    )
    {
        List<TResult> result = new(Math.Min(count, items.Count));
        for (var i = items.Count - 1; i >= 0 && result.Count < count; i--)
        {
            result.Add(selector(items[i]));
        }
        return result;
    }

    /// <summary>
    /// Removes the <paramref name="def"/> designation on <paramref name="animal"/>, both from
    /// the game and from this job's own tracking list. A no-op if the designation is already
    /// gone by the time this runs (e.g. the animal died between gather and execute).
    /// </summary>
    private void RemoveDesignationOn(Pawn animal, DesignationDef def)
    {
        var designation = Manager.map.designationManager.DesignationOn(animal, def);
        if (designation == null)
        {
            return;
        }
        _ = _designations.Remove(designation);
        designation.Delete();
    }

    internal sealed class TrainingTracker : IExposable
    {
        public DefMap<TrainableDef, bool> TrainingTargets = new();
        public bool TrainYoung;
        public bool UnassignTraining;

        public bool AnyEnabled
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

        public static List<TrainableDef> TrainableDefs =>
            DefDatabase<TrainableDef>.AllDefsListForReading;

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
                var enumerable =
                    from t in DefDatabase<TrainableDef>.AllDefsListForReading
                    where t.prerequisites != null && t.prerequisites.Contains(td)
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
        if (TrainedArea == area)
        {
            TrainedArea = null;
        }
        for (var i = 0; i < RestrictArea.Length; i++)
        {
            if (RestrictArea[i] == area)
            {
                RestrictArea[i] = null;
            }
        }
    }
}
