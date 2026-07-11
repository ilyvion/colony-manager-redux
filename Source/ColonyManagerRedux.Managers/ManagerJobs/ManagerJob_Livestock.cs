// ManagerJob_Livestock.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024–2025 Alexander Krivács Schrøder

using System.Diagnostics.CodeAnalysis;
using System.Text;
using Verse.AI;
using Verse.Sound;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
[CoroutineSettingsType]
internal sealed partial class ManagerJob_Livestock : ManagerJob<ManagerSettings_Livestock>
{
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

        // set defaults for culling
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

        TamingPawnSortScore = DefaultTamingPawnSortScore;
        CullingPawnSorter = DefaultCullingPawnSorter;
    }

    public override void PostMake()
    {
        var livestockSettings = ManagerSettings;
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
            text +=
                TriggerPawnKind.pawnKind!.GetTame(Manager, ageSex, includeGuests: false).Count()
                - CullingStrategyAction.GetAlreadyCulledForAgeSex(ageSex)
                + "/"
                + TriggerPawnKind.CountTargets[(int)ageSex];
            if (!CullingStrategyAction.CullingRemovesAnimals)
            {
                text += $"(+{CullingStrategyAction.GetAlreadyCulledForAgeSex(ageSex)})";
            }
            text += ", ";
        }

        text += $"{TriggerPawnKind.pawnKind!.GetWild(Manager).Count()}";
        return text;
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
        if (ModsConfig.OdysseyActive && td.specialTrainable)
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

    public void DoFollowSettings(ManagerLog jobLog, Boxed<bool> workDone)
    {
        if (TriggerPawnKind.pawnKind == null)
        {
            return;
        }

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

        Scribe_Values.Look(ref RestrictToArea, "restrictToArea");
        Scribe_Values.Look(ref SendToCullingArea, "sendToSlaughterArea");
        Scribe_Values.Look(ref SendToMilkingArea, "sendToMilkingArea");
        Scribe_Values.Look(ref SendToShearingArea, "sendToShearingArea");
        Scribe_Values.Look(ref SendToTrainingArea, "sendToTrainingArea");
        Scribe_Values.Look(ref SendToTrainedArea, "sendToTrainedArea");
        Scribe_Values.Look(ref TryTameMore, "tryTameMore");
        Scribe_Values.Look(ref TamePastTargets, "tamePastTargets");
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

        // if we currently have a master, our current master is a valid option,
        // and all the options have roughly equal amounts of pets following them, we don't need to take action
        if (master != null && options.Contains(master) && RoughlyEquallyDistributed(options))
        {
            return master;
        }

        // otherwise, assign a master that has the least amount of current followers.
        // forceRefresh is intentional: a stale cache here caused masters to be picked based on
        // outdated follower counts, unevenly piling animals onto the same master (fixes #24).
        return options.MinBy(p => p.GetFollowers(forceRefresh: true).Count);
    }

    public static void SetFollowing(
        ManagerLog jobLog,
        Pawn animal,
        bool drafted,
        bool fieldwork,
        Boxed<bool> workDone
    )
    {
        if (animal?.playerSettings == null)
        {
            ColonyManagerReduxMod.Instance.LogWarning(
                $"Attempted to use SetFollowing on {animal}, which is either null or has "
                    + "a null playerSettings field"
            );
            return;
        }

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

    public void SetMaster(
        ManagerLog jobLog,
        Pawn animal,
        MasterMode mode,
        Pawn? specificMaster,
        Boxed<bool> workDone
    )
    {
#pragma warning disable IDE0010
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
#pragma warning restore IDE0010
    }

    public static void SetMaster(ManagerLog jobLog, Pawn animal, Pawn? master, Boxed<bool> workDone)
    {
        if (animal.playerSettings.Master != master)
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
    }

    [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
    public override Coroutine TryDoJobCoroutine(ManagerLog jobLog, Boxed<bool> workDone)
    {
        if (TriggerPawnKind.pawnKind == null)
        {
            throw new InvalidOperationException(
                $"Loaded Livestock job without a valid pawn kind: {TriggerPawnKind.ExpectedPawnKindName}"
            );
        }

        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(TryDoJobCoroutine);

#if v1_5
        jobLog.LogLabel = Tab.GetMainLabel(this).Replace("\n", " (") + ")";
#else
        jobLog.LogLabel =
            Tab.GetMainLabel(this).Replace("\n", " (", StringComparison.Ordinal) + ")";
#endif

        // clean up designations that were completed.
        CleanDeadDesignations(_designations, null, jobLog);
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        // add designations in the game that could have been handled by this job
        yield return AddRelevantGameDesignations(jobLog).ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        // handle culling
        if (CullExcess)
        {
            yield return DoCullingJobs(jobLog, CullingStrategyAction, workDone)
                .ResumeWhenOtherCoroutineIsCompleted();
            yield return new ResumeAfterTicks(ticksBetweenOperations);
        }

        // handle training
        yield return DoTrainingJobs(jobLog: jobLog, workDone: workDone)
            .ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        // area restrictions
        // skip for roamers
        if (!(TriggerPawnKind.pawnKind?.RaceProps.Roamer ?? true))
        {
            yield return DoAreaRestrictions(jobLog, workDone).ResumeWhenOtherCoroutineIsCompleted();
            yield return new ResumeAfterTicks(ticksBetweenOperations);
        }

        // follow settings
        DoFollowSettings(jobLog, workDone);
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        // handle taming
        if (TryTameMore)
        {
            yield return DoTamingJobs(jobLog, workDone).ResumeWhenOtherCoroutineIsCompleted();
            yield return new ResumeAfterTicks(ticksBetweenOperations);
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

    [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
    internal Coroutine DoTrainingJobs(
        Boxed<bool> workDone,
        ManagerLog? jobLog = null,
        bool assign = true
    )
    {
        if (TriggerPawnKind.pawnKind == null)
        {
            yield break;
        }

        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(DoTrainingJobs);

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
                        if (assign)
                        {
                            animal.training.SetWanted(def, trainingDef);
                            jobLog?.AddDetail(
                                (
                                    trainingDef
                                        ? "ColonyManagerRedux.Livestock.Logs.AddTraining"
                                        : "ColonyManagerRedux.Livestock.Logs.RemoveTraining"
                                ).Translate(def.label, animal.Label),
                                animal
                            );
                        }

                        workDone.Value = true;
                    }
                }
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine DoAreaRestrictions(ManagerLog jobLog, Boxed<bool> workDone)
    {
        if (TriggerPawnKind.pawnKind == null)
        {
            yield break;
        }

        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            DoAreaRestrictions
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                DoAreaRestrictions
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
                void SetArea(Area? area)
                {
                    animal.playerSettings.AreaRestrictionInPawnCurrentMap = area;
                }

                // culling
                if (SendToCullingArea && CullingStrategyAction.IsAlreadyCulling(animal))
                {
                    workDone.Value |= currentArea != CullingArea;
                    SetArea(CullingArea);
                    jobLog.AddDetail(
                        "ColonyManagerRedux.Livestock.Logs.AssigningToAreaFor".Translate(
                            animal,
                            AreaUtility.AreaAllowedLabel_Area(CullingArea),
                            "ColonyManagerRedux.Livestock.Culling".Translate()
                        ),
                        animal
                    );
                }
                // milking
                else if (
                    SendToMilkingArea
                    && animal.GetComp<CompMilkable>() != null
                    && animal.GetComp<CompMilkable>().TicksTillHarvestable() < UpdateInterval.Ticks
                )
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
                            ),
                            animal
                        );
                    }
                }
                // shearing
                else if (
                    SendToShearingArea
                    && animal.GetComp<CompShearable>() != null
                    && animal.GetComp<CompShearable>().TicksTillHarvestable() < UpdateInterval.Ticks
                )
                {
                    if (currentArea != ShearArea)
                    {
                        workDone.Value = true;
                        SetArea(ShearArea);
                        jobLog.AddDetail(
                            "ColonyManagerRedux.Livestock.Logs.AssigningToAreaFor".Translate(
                                animal,
                                AreaUtility.AreaAllowedLabel_Area(ShearArea),
                                ManagerWorkGiverDefOf.Shear.gerund
                            ),
                            animal
                        );
                    }
                }
                // training
                else if (SendToTrainingArea && animal.training.NextTrainableToTrain() != null)
                {
                    if (currentArea != TrainingArea)
                    {
                        workDone.Value = true;
                        SetArea(TrainingArea);
                        jobLog.AddDetail(
                            "ColonyManagerRedux.Livestock.Logs.AssigningToAreaFor".Translate(
                                animal,
                                AreaUtility.AreaAllowedLabel_Area(TrainingArea),
                                ManagerWorkGiverDefOf.Train.gerund
                            ),
                            animal
                        );
                    }
                }
                // trained
                else if (SendToTrainedArea && animal.training.NextTrainableToTrain() == null)
                {
                    if (currentArea != TrainedArea)
                    {
                        workDone.Value = true;
                        SetArea(TrainedArea);
                        jobLog.AddDetail(
                            "ColonyManagerRedux.Livestock.Logs.AssigningToAreaFor".Translate(
                                animal,
                                AreaUtility.AreaAllowedLabel_Area(TrainedArea),
                                "ColonyManagerRedux.Livestock.Trained".Translate()
                            ),
                            animal
                        );
                    }
                }
                // all
                else if (RestrictToArea && currentArea != RestrictArea[i])
                {
                    workDone.Value = true;
                    SetArea(RestrictArea[i]);
                    jobLog.AddDetail(
                        "ColonyManagerRedux.Livestock.Logs.AssigningToArea".Translate(
                            animal,
                            AreaUtility.AreaAllowedLabel_Area(RestrictArea[i])
                        ),
                        animal
                    );
                }

                if (++animalCounter > 0 && animalCounter % operationsPerTick == 0)
                {
                    yield return new ResumeAfterTicks(ticksBetweenOperations);
                }
            }
        }
    }

    private readonly List<Designation> _tmpDesignations = [];

    [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
    private Coroutine DoCullingJobs(
        ManagerLog jobLog,
        CullingAction cullingAction,
        Boxed<bool> workDone
    )
    {
        if (TriggerPawnKind.pawnKind == null)
        {
            yield break;
        }

        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(DoCullingJobs);

        using var _ = new DoOnDispose(_tmpDesignations.Clear);
        foreach (var ageSex in Utilities_Livestock.AgeSexArray)
        {
            // too many animals?
            var animalCount =
                TriggerPawnKind.pawnKind.GetTame(Manager, ageSex, includeGuests: false).Count()
                - cullingAction.GetAlreadyCulledForAgeSex(ageSex);
            var alreadyCulling = cullingAction.GetAlreadyCullingCountForAgeSex(ageSex);
            var target = TriggerPawnKind.CountTargets[(int)ageSex];
            var targetDifference = animalCount - alreadyCulling - target;

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
                        !cullingAction.IsAlreadyCulling(p)
                        && !cullingAction.IsAlreadyCulled(p)
                        && (CullTrained || !p.training.HasLearned(TrainableDefOf.Obedience))
                        && (CullPregnant || !p.VisiblyPregnant())
                        && (CullBonded || !p.BondedWithColonist())
                        && (
                            !AvoidCullingMilkable
                            || p.GetMilkFullness() < AvoidCullingMilkableThreshold
                        )
                        && (
                            !AvoidCullingShearable
                            || p.GetWoolFullness() < AvoidCullingShearableThreshold
                        )
                    );
                var animals = CullingPawnSorter(ageSex, animalsUnsorted);

                var animalsEnumerator = animals.GetEnumerator();

                for (var i = 0; i < targetDifference && animalsEnumerator.MoveNext(); i++)
                {
                    var animal = animalsEnumerator.Current;
                    cullingAction.Cull(animal);
                    animalCount--;
                    jobLog.AddDetail(
                        "ColonyManagerRedux.Livestock.Logs.AddDesignation".Translate(
                            cullingAction.ActionText,
                            animal.Label,
                            ageSex.GetLabel(),
                            animalCount,
                            target
                        ),
                        animal
                    );
                    workDone.Value = true;
                }
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }

            // remove extra designations
            var didRemove = false;
            while (targetDifference < 0)
            {
                if (cullingAction.TryStopCulling(ageSex, out var animal))
                {
                    workDone.Value = true;
                    targetDifference++;
                    animalCount++;
                    didRemove = true;

                    jobLog.AddDetail(
                        "ColonyManagerRedux.Livestock.Logs.RemoveDesignation".Translate(
                            cullingAction.ActionText,
                            animal.Label,
                            ageSex.GetLabel(),
                            animalCount,
                            target
                        ),
                        animal
                    );
                }
                else
                {
                    break;
                }
            }
            if (didRemove)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    [CoroutineSettingsMethod]
    private Coroutine DoTamingJobs(ManagerLog jobLog, Boxed<bool> workDone)
    {
        if (TriggerPawnKind.pawnKind == null)
        {
            yield break;
        }

        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            DoTamingJobs
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(DoTamingJobs);

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
            var targetDifference = target - animalCount - alreadyTaming;

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
                // get the 'home' position
                var position = Manager.map.GetBaseCenter();

                // get list of animals in sorted by youngest weighted by distance.
                List<Pawn> sortedAnimals = [];
                yield return GetThingsSorted(
                        TriggerPawnKind.pawnKind.GetWild(Manager, ageSex),
                        sortedAnimals,
                        p =>
                            p != null
                            && p.Spawned
                            && Manager.map.designationManager.DesignationOn(p) == null
                            && (TameArea == null || TameArea.ActiveCells.Contains(p.Position))
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

                    AddDesignation(new(animal, DesignationDefOf.Tame));
                    animalCount++;
                    jobLog.AddDetail(
                        "ColonyManagerRedux.Livestock.Logs.AddDesignation".Translate(
                            DesignationDefOf.Tame.ActionText(),
                            ageSex.GetLabel(),
                            animal.Label,
                            animalCount,
                            target
                        ),
                        animal
                    );
                    workDone.Value = true;

                    if (++animalCounter > 0 && animalCounter % operationsPerTick == 0)
                    {
                        yield return new ResumeAfterTicks(ticksBetweenOperations);
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

                    jobLog.AddDetail(
                        "ColonyManagerRedux.Livestock.Logs.RemoveDesignation".Translate(
                            DesignationDefOf.Tame.ActionText(),
                            ageSex.GetLabel(),
                            animal.Label,
                            animalCount,
                            target
                        ),
                        animal
                    );
                }
                else
                {
                    break;
                }

                if (++animalCounter > 0 && animalCounter % operationsPerTick == 0)
                {
                    yield return new ResumeAfterTicks(ticksBetweenOperations);
                }
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

        var followerCounts = masters
            .Select(p => p.GetFollowers(TriggerPawnKind.pawnKind).EnumerableCount())
            .ToArray();
        var greatestDifferenceInFollowerCount = followerCounts.Max() - followerCounts.Min();
        return greatestDifferenceInFollowerCount <= 1;
    }

    private bool TryRemoveDesignation(
        AgeAndSex ageSex,
        DesignationDef def,
        [NotNullWhen(true)] out Pawn? animal
    )
    {
        animal = null;

        if (TriggerPawnKind.pawnKind == null)
        {
            return false;
        }

        // get current designations
        using var _disposeDesignations = new DoOnDispose(_tmpDesignations.Clear);
        DesignationsOfOn(def, ageSex, _tmpDesignations);

        // if none, return false
        if (_tmpDesignations.Count == 0)
        {
            return false;
        }

        // else, remove one from the game as well as our managed list. (delete last - this should be the youngest/oldest).
        var designation = _tmpDesignations.Last();
        animal = (Pawn)designation.target.Thing;
        _ = _designations.Remove(designation);
        designation.Delete();
        return true;
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
