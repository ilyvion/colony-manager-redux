﻿// Trigger_PawnKind.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HotSwappable]
internal sealed class Trigger_PawnKind : Trigger
{
    private readonly CachedValue<string> _cachedTooltip;
    private readonly CachedValue<bool> _state = new(false);

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
                .Select(ageSex => pawnKind.GetTame(job.Manager, ageSex).Count())
                .ToArray();
        }
    }

    public int GetCountFor(AgeAndSex ageAndSex)
    {
        return pawnKind.GetTame(job.Manager, ageAndSex).Count();
    }

    public int GetTargetFor(AgeAndSex ageAndSex)
    {
        return CountTargets[(int)ageAndSex];
    }

    public ManagerJob_Livestock Job
    {
        get
        {
            return job.Manager.JobTracker.JobsOfType<ManagerJob_Livestock>()
                .FirstOrDefault(job => job.Trigger == this);
        }
    }

    public override bool State
    {
        get
        {
            if (!_state.TryGetValue(out bool state))
            {
                state = Utilities_Livestock.AgeSexArray.All(
                            ageSex => CountTargets[(int)ageSex] ==
                                      pawnKind.GetTame(job.Manager, ageSex).Count())
                     && AllTrainingWantedSet();
                _state.Update(state);
            }

            return state;
        }
    }

    public override string StatusTooltip => _cachedTooltip.Value;

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
                    $"ColonyManagerRedux.AgeAndSex.{l}".Translate()), null)));
        return "ColonyManagerRedux.Livestock.ListEntryTooltip".Translate(tooltipArgs.ToArray()).Resolve().CapitalizeFirst();
    }

    private bool AllTrainingWantedSet()
    {
        // do a dry run of the training assignment (no assignments are set).
        // this is rediculously expensive, and should never be called on tick.
        var actionTaken = false;
        Job.DoTrainingJobs(ref actionTaken, false);
        return actionTaken;
    }
}
