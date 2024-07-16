// TODO: Nullability uncertain; rewrite?
#nullable disable

// Trigger_PawnKind.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public class Trigger_PawnKind : Trigger
{
    private readonly Utilities.CachedValue<string> _cachedTooltip;
    private readonly Utilities.CachedValue<bool> _state = new(false);

    public int[] CountTargets;
    public PawnKindDef pawnKind;

    public Trigger_PawnKind(ManagerJob job) : base(job)
    {
        CountTargets = Utilities_Livestock.AgeSexArray.Select(_ => 5).ToArray();

        _cachedTooltip = new Utilities.CachedValue<string>("", 250, _getTooltip);
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

    public ManagerJob_Livestock Job
    {
        get
        {
            return job.Manager.JobStack.FullStack<ManagerJob_Livestock>()
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
                                            string label = null, string tooltip = null,
                                            List<Designation> targets = null, Action onOpenFilterDetails = null,
                                            Func<Designation, string> designationLabelGetter = null)
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

    private string _getTooltip()
    {
        var tooltipArgs = new List<NamedArgument>
        {
            pawnKind.LabelCap
        };
        tooltipArgs.AddRange(CountTargets.Select(v => new NamedArgument(v.ToString(), null)));
        tooltipArgs.AddRange(Counts.Select(x => new NamedArgument(x.ToString(), null)));
        return "ColonyManagerRedux.Livestock.ListEntryTooltip".Translate(tooltipArgs.ToArray());
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
