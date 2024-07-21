// JobDriver_ManagingAtManagingStation.cs
// Copyright Karel Kroeze, 2018-2020

using Verse.AI;

namespace ColonyManagerRedux;

internal class JobDriver_ManagingAtManagingStation : JobDriver
{
    private float workDone;
    private float workNeeded;

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref workNeeded, "workNeeded", 100);
        Scribe_Values.Look(ref workDone, "workDone");
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(job.targetA, job);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
        var manage = Manage(TargetIndex.A);
        if (manage == null)
        {
            yield break;
        }
        yield return manage;

        // if made to by player, keep doing that untill we're out of jobs
        yield return Toils_Jump.JumpIf(
            manage, () => GetActor().CurJob.playerForced && Manager.For(Map).JobStack.NextJob != null);
    }

    private Toil? Manage(TargetIndex targetIndex)
    {
        if (GetActor().jobs.curJob.GetTarget(targetIndex).Thing is not Building_ManagerStation station)
        {
            Log.Error("Target of manager job was not a manager station.");
            return null;
        }

        var comp = station.GetComp<CompManagerStation>();
        if (comp == null)
        {
            Log.Error("Target of manager job does not have manager station comp. This should never happen.");
            return null;
        }

        var toil = new Toil
        {
            defaultCompleteMode = ToilCompleteMode.Never,
            initAction = () =>
            {
                workDone = 0;
                workNeeded = comp.Props.speed;
            },
            tickAction = () =>
            {
                // learn a bit
                pawn.skills.GetSkill(SkillDefOf.Intellectual).Learn(0.11f);

                // update counter
                workDone += pawn.GetStatValue(ManagerStatDefOf.ManagingSpeed);

                // are we done yet?
                if (workDone > workNeeded)
                {
                    Manager.For(pawn.Map).TryDoWork();
                    ReadyForNextToil();
                }
            }
        };

        toil.WithProgressBar(TargetIndex.A, () => workDone / workNeeded);
        return toil;
    }
}
