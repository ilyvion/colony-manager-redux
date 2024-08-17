// JobDriver_ManagingAtManagingStation.cs
// Copyright Karel Kroeze, 2018-2020

using Verse.AI;

namespace ColonyManagerRedux;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Microsoft.Performance",
    "CA1812:AvoidUninstantiatedInternalClasses",
    Justification = "Class is instantiated via reflection")]
internal sealed class JobDriver_ManagingAtManagingStation : JobDriver
{
    private float workDone;
    private float workNeeded;

    private bool hadNoWork;
    private CoroutineHandle? handle;
    private int? coroutineStartTick;
    private int? coroutineEndTick;

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref workNeeded, "workNeeded", 100);
        Scribe_Values.Look(ref workDone, "workDone");
        Scribe_Values.Look(ref hadNoWork, "hadNoWork", false);
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(job.targetA, job);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOn(() => Manager.For(pawn.Map).JobTracker.NextJob == null && handle == null);
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
        var manage = Manage(TargetIndex.A);
        if (manage == null)
        {
            yield break;
        }
        yield return manage;

        // if made to by player, keep doing that untill we're out of jobs
        yield return Toils_Jump.JumpIf(
            manage, () => GetActor().CurJob.playerForced && Manager.For(Map).JobTracker.NextJob != null);
    }

    private Toil? Manage(TargetIndex targetIndex)
    {
        if (GetActor().jobs.curJob.GetTarget(targetIndex).Thing is not Building_ManagerStation station)
        {
            ColonyManagerReduxMod.Instance
                .LogError("Target of manager job was not a manager station.");
            return null;
        }

        var comp = station.GetComp<CompManagerStation>();
        if (comp == null)
        {
            ColonyManagerReduxMod.Instance
                .LogError("Target of manager job does not have manager station comp. " +
                    "This should never happen.");
            return null;
        }

        var intlSkill = pawn.skills.GetSkill(SkillDefOf.Intellectual);
        var managingSpeed = pawn.GetStatValue(ManagerStatDefOf.ManagingSpeed);
        var toil = new Toil
        {
            defaultCompleteMode = ToilCompleteMode.Never,
            initAction = () =>
            {
                workDone = 0;
                workNeeded = comp.Props.speed;

                hadNoWork = false;
                handle = null;
                coroutineStartTick = null;
                coroutineEndTick = null;
            },
            tickAction = () =>
            {
                if (!hadNoWork && workDone > workNeeded / 2 && handle == null)
                {
                    var coroutine = Manager.For(pawn.Map).TryDoWork();
                    if (coroutine == null)
                    {
                        hadNoWork = true;
                    }
                    else
                    {
                        coroutineStartTick = Find.TickManager.TicksGame;
                        handle = MultiTickCoroutineManager.StartCoroutine(
                            coroutine,
                            () => coroutineEndTick = Find.TickManager.TicksGame,
                            debugHandle: "JobDriver_ManagingAtManagingStation.Manage");
                    }
                }
                if (workDone < workNeeded)
                {
                    // learn a bit
                    intlSkill.Learn(0.11f);

                    // update counter
                    workDone += managingSpeed;
                }
                else if (handle != null && handle.IsCompleted)
                {
                    var tickCount = coroutineEndTick!.Value - coroutineStartTick!.Value;
                    ColonyManagerReduxMod.Instance.LogDebug(
                        $"TryDoWork took {tickCount} ticks to complete");

                    ReadyForNextToil();
                }
                else if (handle == null)
                {
                    ReadyForNextToil();
                }
            }
        };

        toil.WithProgressBar(TargetIndex.A, () => workDone / workNeeded);
        return toil;
    }
}
