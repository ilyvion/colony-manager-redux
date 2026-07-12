// Trigger_PawnKind.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024–2025 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class Trigger_PawnKind : Trigger
{
    private readonly CachedValue<string> _cachedTooltip;
    private readonly CachedValue<bool> _cachedState = new(false);

    public int[] CountTargets;
    public PawnKindDef? pawnKind;

    public string ExpectedPawnKindName
    {
        get => $"[PawnKindDef was saved as '{ExpectedPawnKindNameRaw ?? "?"}']";
        private set => ExpectedPawnKindNameRaw = value;
    }
    public string? ExpectedPawnKindNameRaw { get; private set; }

#pragma warning disable CS8618 // Set by using class
    public Trigger_PawnKind(ManagerJob job)
        : base(job)
#pragma warning restore CS8618
    {
        CountTargets = [.. Utilities_Livestock.AgeSexArray.Select(_ => 5)];

        _cachedTooltip = new CachedValue<string>(GetTooltip);
    }

    public int[] Counts =>
        ComputeRemainingCounts(
            [
                .. Utilities_Livestock.AgeSexArray.Select(ageSex =>
                    pawnKind?.GetTame(Job.Manager, ageSex, includeGuests: false).Count() ?? 0
                ),
            ],
            [
                .. Utilities_Livestock.AgeSexArray.Select(
                    Job.CullingStrategyAction.GetAlreadyCulledForAgeSex
                ),
            ]
        );

    internal static int[] ComputeRemainingCounts(
        IReadOnlyList<int> tameCounts,
        IReadOnlyList<int> alreadyCulled
    ) => [.. tameCounts.Zip(alreadyCulled, (tame, culled) => tame - culled)];

    public int GetCountFor(AgeAndSex ageAndSex, bool cached = true) =>
        (pawnKind?.GetTame(Job.Manager, ageAndSex, cached, false).Count() ?? 0)
        - Job.CullingStrategyAction.GetAlreadyCulledForAgeSex(ageAndSex);

    public int GetTargetFor(AgeAndSex ageAndSex) => CountTargets[(int)ageAndSex];

    private static Texture2D GetProgressBarTextureFor(AgeAndSex ageAndSex) =>
        ageAndSex switch
        {
            AgeAndSex.AdultFemale => Resources.AdultFemaleTexture,
            AgeAndSex.AdultMale => Resources.AdultMaleTexture,
            AgeAndSex.JuvenileFemale => Resources.JuvenileFemaleTexture,
            AgeAndSex.JuvenileMale => Resources.JuvenileMaleTexture,
            _ => throw new ArgumentOutOfRangeException(
                nameof(ageAndSex),
                ageAndSex,
                $"Unknown AgeAndSex value '{ageAndSex}'"
            ),
        };

    public new ManagerJob_Livestock Job
    {
        get => (ManagerJob_Livestock)base.Job;
        set => base.Job = value;
    }

    public override bool State
    {
        get
        {
            bool state;
            if (pawnKind == null)
            {
                state = true;
            }
            else if (!_cachedState.TryGetValue(out state))
            {
                var actualCounts = Utilities_Livestock.AgeSexArray.Select(ageSex =>
                    pawnKind.GetTame(Job.Manager, ageSex).Count()
                );
                state = AllTargetsMet(CountTargets, [.. actualCounts]) && AllTrainingWantedSet();
                _ = _cachedState.Update(state);
            }

            return state;
        }
    }

    internal static bool AllTargetsMet(
        IReadOnlyList<int> targets,
        IReadOnlyList<int> actualCounts
    ) => targets.Zip(actualCounts, (target, actual) => target == actual).All(matched => matched);

    public override string StatusTooltip => _cachedTooltip.Value;

    public override void DrawVerticalProgressBars(Rect progressRect, bool active)
    {
        progressRect.xMin += progressRect.width - 10;
        foreach (var ageAndSex in Utilities_Livestock.AgeSexArray)
        {
            var c = GetCountFor(ageAndSex);
            var t = GetTargetFor(ageAndSex);
            DrawVerticalProgressBar(
                progressRect,
                c,
                t,
                Job.CullingStrategyAction.CullingRemovesAnimals
                    ? "ColonyManagerRedux.Livestock.ListEntryAgeAndSexCount".Translate(
                        c,
                        t,
                        ageAndSex.GetLabel(true)
                    )
                    : "ColonyManagerRedux.Livestock.ListEntryAgeAndSexCount.WithCulling".Translate(
                        c,
                        t,
                        ageAndSex.GetLabel(true),
                        Job.CullingStrategyAction.GetAlreadyCulledForAgeSex(ageAndSex),
                        Job.CullingStrategyAction.ActionText
                    ),
                active,
                GetProgressBarTextureFor(ageAndSex)
            );

            progressRect.x -= Constants.Margin + 10;
        }
    }

    public const float PawnKindProgressBarHeight = 10f;

    public override void DrawHorizontalProgressBars(Rect progressRect, bool active)
    {
        //var eachHeight = progressRect.height / Utilities_Livestock.AgeSexArray.Length;
        var eachRect = new Rect(progressRect) { height = PawnKindProgressBarHeight };
        foreach (var ageAndSex in Utilities_Livestock.AgeSexArray)
        {
            var c = GetCountFor(ageAndSex);
            var t = GetTargetFor(ageAndSex);
            DrawHorizontalProgressBar(
                eachRect,
                c,
                t,
                Job.CullingStrategyAction.CullingRemovesAnimals
                    ? "ColonyManagerRedux.Livestock.ListEntryAgeAndSexCount".Translate(
                        c,
                        t,
                        ageAndSex.GetLabel(true)
                    )
                    : "ColonyManagerRedux.Livestock.ListEntryAgeAndSexCount.WithCulling".Translate(
                        c,
                        t,
                        ageAndSex.GetLabel(true),
                        Job.CullingStrategyAction.GetAlreadyCulledForAgeSex(ageAndSex),
                        Job.CullingStrategyAction.ActionText
                    ),
                active,
                GetProgressBarTextureFor(ageAndSex)
            );

            eachRect.y += PawnKindProgressBarHeight + (Constants.Margin / 2);
        }
    }

    public override void DrawTriggerConfig(
        ref Vector2 cur,
        float width,
        float entryHeight,
        string? label = null,
        string? tooltip = null,
        List<Designation>? targets = null,
        Action? onOpenFilterDetails = null,
        Func<Designation, string>? designationLabelGetter = null
    ) { }

    public override void ExposeData()
    {
        base.ExposeData();
        foreach (var ageAndSex in Utilities_Livestock.AgeSexArray)
        {
            Scribe_Values.Look(
                ref CountTargets[(int)ageAndSex],
                $"{ageAndSex.ToString().UncapitalizeFirst()}TargetCount"
            );
        }
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            var subNode = Scribe.loader.curXmlParent["pawnKind"];
            if (subNode != null && subNode.InnerText != null && subNode.InnerText != "null")
            {
                ExpectedPawnKindName = BackCompatibility.BackCompatibleDefName(
                    typeof(PawnKindDef),
                    subNode.InnerText,
                    forDefInjections: false,
                    subNode
                );
            }
        }
        Scribe_Defs.Look(ref pawnKind, "pawnKind");
    }

    private string GetTooltip()
    {
        if (pawnKind == null)
        {
            return "This job is in an error state because the game could not find the PawnKindDef it expected for this job. "
                + "The likeliest causes for this is that the PawnKindDef was renamed by a mod, removed from a mod, or that the mod that added it itself was disabled or removed.";
        }
        var tooltipArgs = new List<NamedArgument> { pawnKind.Named("PAWNKIND") };
        tooltipArgs.AddRange(
            Counts
                .Zip(CountTargets, (c, t) => (c, t))
                .Zip(
                    Utilities_Livestock.AgeSexArray,
                    (v, ageAndSex) =>
                        new NamedArgument(
                            Job.CullingStrategyAction.CullingRemovesAnimals
                                ? "ColonyManagerRedux.Livestock.ListEntryAgeAndSexCount".Translate(
                                    v.c,
                                    v.t,
                                    ageAndSex.GetLabel(true)
                                )
                                : "ColonyManagerRedux.Livestock.ListEntryAgeAndSexCount.WithCulling".Translate(
                                    v.c,
                                    v.t,
                                    ageAndSex.GetLabel(true),
                                    Job.CullingStrategyAction.GetAlreadyCulledForAgeSex(ageAndSex),
                                    Job.CullingStrategyAction.ActionText
                                ),
                            null
                        )
                )
        );
        tooltipArgs.Add(
            "ColonyManagerRedux.Livestock.WildCount".Translate(
                pawnKind.GetWild(Job.Manager).Count()
            )
        );
        return "ColonyManagerRedux.Livestock.ListEntryTooltip"
            .Translate(tooltipArgs.ToArray())
            .Resolve()
            .CapitalizeFirst();
    }

    private bool AllTrainingWantedSet()
    {
        // do a dry run of the training assignment (no assignments are set).
        // this is ridiculously expensive, and should never be called every tick.
        Boxed<bool> actionTaken = new(false);
        Job.DoTrainingJobs(actionTaken, assign: false).RunImmediatelyToCompletion();
        return !actionTaken;
    }
}
