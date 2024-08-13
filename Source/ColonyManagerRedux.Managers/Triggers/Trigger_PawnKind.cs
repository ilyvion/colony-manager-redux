// Trigger_PawnKind.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class Trigger_PawnKind : Trigger
{
    private readonly CachedValue<string> _cachedTooltip;
    private readonly CachedValue<bool> _cachedState = new(false);

    public int[] CountTargets;
    public PawnKindDef pawnKind;

#pragma warning disable CS8618 // Set by using class
    public Trigger_PawnKind(ManagerJob job) : base(job)
#pragma warning restore CS8618
    {
        CountTargets = Utilities_Livestock.AgeSexArray.Select(_ => 5).ToArray();

        _cachedTooltip = new CachedValue<string>("", 250, GetTooltip);
    }

    public int[] Counts
    {
        get
        {
            return Utilities_Livestock.AgeSexArray
                .Select(ageSex => pawnKind.GetTame(Job.Manager, ageSex, includeGuests: false).Count())
                .ToArray();
        }
    }

    public int GetCountFor(AgeAndSex ageAndSex, bool cached = true)
    {
        return pawnKind.GetTame(Job.Manager, ageAndSex, cached, false).Count();
    }

    public int GetTargetFor(AgeAndSex ageAndSex)
    {
        return CountTargets[(int)ageAndSex];
    }

    private static Texture2D GetProgressBarTextureFor(AgeAndSex ageAndSex)
    {
        return ageAndSex switch
        {
            AgeAndSex.AdultFemale => Resources.AdultFemaleTexture,
            AgeAndSex.AdultMale => Resources.AdultMaleTexture,
            AgeAndSex.JuvenileFemale => Resources.JuvenileFemaleTexture,
            AgeAndSex.JuvenileMale => Resources.JuvenileMaleTexture,
            _ => throw new Exception($"Unknown AgeAndSex value '{ageAndSex}'"),
        };
    }

    public new ManagerJob_Livestock Job
    {
        get => (ManagerJob_Livestock)base.Job;
        set => base.Job = value;
    }

    public override bool State
    {
        get
        {
            if (!_cachedState.TryGetValue(out bool state))
            {

                state = Utilities_Livestock.AgeSexArray.All(
                    ageSex => CountTargets[(int)ageSex] ==
                        pawnKind.GetTame(Job.Manager, ageSex).Count())
                     && AllTrainingWantedSet();
                _cachedState.Update(state);
            }

            return state;
        }
    }

    public override string StatusTooltip => _cachedTooltip.Value;

    public override void DrawVerticalProgressBars(Rect progressRect, bool active)
    {
        progressRect.xMin += progressRect.width - 10;
        foreach (var ageAndSex in Utilities_Livestock.AgeSexArray)
        {
            int c = GetCountFor(ageAndSex);
            int t = GetTargetFor(ageAndSex);
            DrawVerticalProgressBar(
                progressRect,
                c,
                t,
                "ColonyManagerRedux.Livestock.ListEntryAgeAndSexCount".Translate(c, t,
                    ageAndSex.GetLabel()),
                active,
                GetProgressBarTextureFor(ageAndSex));

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
            int c = GetCountFor(ageAndSex);
            int t = GetTargetFor(ageAndSex);
            DrawHorizontalProgressBar(
                eachRect,
                c,
                t,
                "ColonyManagerRedux.Livestock.ListEntryAgeAndSexCount".Translate(c, t,
                    ageAndSex.GetLabel()),
                active,
                GetProgressBarTextureFor(ageAndSex));

            eachRect.y += PawnKindProgressBarHeight + Constants.Margin / 2;
        }
    }

    public override void DrawTriggerConfig(ref Vector2 cur, float width, float entryHeight,
        string? label = null, string? tooltip = null,
        List<Designation>? targets = null, Action? onOpenFilterDetails = null,
        Func<Designation, string>? designationLabelGetter = null)
    {
    }

    public override void ExposeData()
    {
        base.ExposeData();
        foreach (var ageAndSex in Utilities_Livestock.AgeSexArray)
        {
            Scribe_Values.Look(ref CountTargets[(int)ageAndSex], $"{ageAndSex.ToString().UncapitalizeFirst()}TargetCount");
        }
        Scribe_Defs.Look(ref pawnKind, "pawnKind");
    }

    private string GetTooltip()
    {
        var tooltipArgs = new List<NamedArgument>
        {
            pawnKind.Named("PAWNKIND")
        };
        tooltipArgs.AddRange(
            Counts.Zip(CountTargets, (c, t) => (c, t))
            .Zip(Utilities_Livestock.AgeSexArray, (v, l) => new NamedArgument(
                "ColonyManagerRedux.Livestock.ListEntryAgeAndSexCount".Translate(v.c, v.t,
                    l.GetLabel()), null)));
        tooltipArgs.Add(
            "ColonyManagerRedux.Livestock.WildCount".Translate(
                pawnKind.GetWild(Job.Manager).Count())
        );
        return "ColonyManagerRedux.Livestock.ListEntryTooltip".Translate(tooltipArgs.ToArray()).Resolve().CapitalizeFirst();
    }

    private bool AllTrainingWantedSet()
    {
        // do a dry run of the training assignment (no assignments are set).
        // this is rediculously expensive, and should never be called on tick.
        var actionTaken = false;
        Job.DoTrainingJobs(ref actionTaken, assign: false);
        return !actionTaken;
    }
}
