// JobDriver_ManagingAtManagingStation.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024–2026 Alexander Krivács Schrøder

using Verse.AI;

namespace ColonyManagerRedux;

[HotSwappable]
internal sealed class JobDriver_ManagingAtManagingStation : JobDriver
{
    // The fraction of workNeeded reserved for the gather phase; the execute phase may only
    // start once this fraction of the timer has elapsed (and the gather phase has finished).
    internal const float GatherPhaseFraction = 0.95f;

    private float workDone;
    private float workNeeded;

    private JobDriverPhase phase = new JobDriverPhase.NotStarted();

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref workNeeded, "workNeeded", 100);
        Scribe_Values.Look(ref workDone, "workDone");
        // phase itself isn't persisted, matching the pre-tagged-union code (its handle/pending-work
        // fields were never persisted either, since a CoroutineHandle can't survive a save/load);
        // resuming from NotStarted just re-runs the side-effect-free gather phase, which is safe.
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed) =>
        pawn.Reserve(job.targetA, job);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        _ = this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        _ = this.FailOn(() =>
            Manager.For(pawn.Map).JobTracker.NextJob == null
            && phase is JobDriverPhase.NotStarted or JobDriverPhase.NoWorkFound
        );
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);
        var manage = Manage(TargetIndex.A);
        if (manage == null)
        {
            yield break;
        }
        yield return manage;

        // if made to by player, keep doing that untill we're out of jobs
        yield return Toils_Jump.JumpIf(
            manage,
            () => GetActor().CurJob.playerForced && Manager.For(Map).JobTracker.NextJob != null
        );
    }

    /// <summary>
    /// Given how much of the pawn's work timer has elapsed and whether the gather phase has
    /// completed, decides whether the execute phase is allowed to start. The execute phase is
    /// held back until at least <see cref="GatherPhaseFraction"/> of <paramref name="workNeeded"/>
    /// has elapsed, but is never held back further just because the gather phase took longer
    /// than that fraction.
    /// </summary>
    internal static bool ShouldStartExecutePhase(
        bool gatherCompleted,
        float workDone,
        float workNeeded
    ) => gatherCompleted && workDone >= workNeeded * GatherPhaseFraction;

    /// <summary>
    /// Caps how far <paramref name="workDone"/> may advance while waiting for the execute
    /// phase to be allowed to start, so the progress bar visibly holds at
    /// <see cref="GatherPhaseFraction"/> instead of running all the way to completion before
    /// the job's real work is actually done.
    /// </summary>
    internal static float AdvanceWorkDoneWhileWaiting(
        float workDone,
        float managingSpeed,
        float workNeeded
    ) => Math.Min(workDone + managingSpeed, workNeeded * GatherPhaseFraction);

    /// <summary>
    /// The execute phase is meant to finish instantly rather than making the pawn stand
    /// around for the rest of the timer, but the pawn should still receive the full skill
    /// experience that timer would have granted; this computes the lump-sum catch-up amount.
    /// </summary>
    internal static float ComputeCatchUpSkillGain(float workDone, float workNeeded) =>
        Math.Max(0f, workNeeded - workDone) * 0.11f;

    private Toil? Manage(TargetIndex targetIndex)
    {
        if (
            GetActor().jobs.curJob.GetTarget(targetIndex).Thing
            is not Building_ManagerStation station
        )
        {
            ColonyManagerReduxMod.Instance.LogError(
                "Target of manager job was not a manager station."
            );
            return null;
        }

        var comp = station.GetComp<CompManagerStation>();
        if (comp == null)
        {
            ColonyManagerReduxMod.Instance.LogError(
                "Target of manager job does not have manager station comp. "
                    + "This should never happen."
            );
            return null;
        }

        var intlSkill = pawn.skills.GetSkill(SkillDefOf.Intellectual);
        var managingSpeed =
            pawn.GetStatValue(ManagerStatDefOf.ManagingSpeed)
            * station.GetStatValue(StatDefOf.WorkTableEfficiencyFactor);
        var toil = new Toil
        {
            defaultCompleteMode = ToilCompleteMode.Never,
            initAction = () =>
            {
                ColonyManagerReduxMod.Instance.LogVerboseMessage(
                    $"Pawn {pawn.Name} began toiling with managing at {station.Label}."
                );

                workDone = 0;
                workNeeded = comp.Props.speed;

                phase = new JobDriverPhase.NotStarted();
            },
            tickAction = () =>
            {
                // Gathering doesn't change anything in the game, so it can start immediately
                // instead of waiting for the pawn's timer to reach any particular point. Falls
                // through (no return) so the resulting phase is handled in this same tick.
                if (phase is JobDriverPhase.NotStarted)
                {
                    phase = StartGathering();
                }

                switch (phase)
                {
                    case JobDriverPhase.Gathering gathering:
                        TickGathering(gathering);
                        break;
                    case JobDriverPhase.NoWorkFound:
                        TickNoWorkFound();
                        break;
                    case JobDriverPhase.Executing executing:
                        TickExecuting(executing);
                        break;
                    default:
                        // NotStarted: unreachable, just transitioned away from this above.
                        break;
                }

                JobDriverPhase StartGathering()
                {
                    ColonyManagerReduxMod.Instance.LogVerboseMessage(
                        $"Setting up a job's gather phase due to pawn {pawn.Name} toiling with managing at {station.Label} having managing speed {managingSpeed}..."
                    );
                    AnyBoxed<JobTracker.PendingJobWork?> pendingWork = new(null);
                    var coroutine = Manager.For(pawn.Map).TryGatherWork(pendingWork);
                    if (coroutine == null)
                    {
                        ColonyManagerReduxMod.Instance.LogVerboseMessage(
                            $"...there was no job to do."
                        );
                        return new JobDriverPhase.NoWorkFound();
                    }

                    var startTick = Find.TickManager.TicksGame;
                    ColonyManagerReduxMod.Instance.LogVerboseMessage(
                        $"...job's gather phase started @ game tick {startTick}."
                    );
                    var handle = MultiTickCoroutineManager.StartCoroutine(
                        coroutine,
                        debugHandle: "JobDriver_ManagingAtManagingStation.Gather"
                    );
                    return new JobDriverPhase.Gathering(handle, pendingWork, startTick);
                }

                // Advances the progress bar/skill learning while waiting for the execute phase
                // to be allowed to start, or reports that it's time to move on. Shared between
                // the Gathering and NoWorkFound phases, which both just wait out the same gate.
                bool ReadyToAdvance(bool gatherCompleted)
                {
                    if (ShouldStartExecutePhase(gatherCompleted, workDone, workNeeded))
                    {
                        return true;
                    }
                    intlSkill.Learn(managingSpeed * 0.11f);
                    workDone = AdvanceWorkDoneWhileWaiting(workDone, managingSpeed, workNeeded);
                    return false;
                }

                void TickGathering(JobDriverPhase.Gathering gathering)
                {
                    if (!ReadyToAdvance(gathering.Handle.IsCompleted))
                    {
                        return;
                    }

                    if (gathering.PendingWork.Value == null)
                    {
                        // The gather phase's recursive "try next job" chain can bottom out
                        // without ever populating PendingWork.Value (e.g. every remaining
                        // candidate job errored out during gather) - treat that the same as
                        // "no work" rather than trying to execute a null pending-work value.
                        phase = new JobDriverPhase.NoWorkFound();
                        return;
                    }

                    ColonyManagerReduxMod.Instance.LogVerboseMessage(
                        $"Setting up a job's execute phase due to pawn {pawn.Name} toiling with managing at {station.Label}..."
                    );
                    var executeCoroutine = Manager
                        .For(pawn.Map)
                        .TryExecuteWork(gathering.PendingWork.Value);
                    var executeHandle = MultiTickCoroutineManager.StartCoroutine(
                        executeCoroutine,
                        debugHandle: "JobDriver_ManagingAtManagingStation.Execute"
                    );
                    phase = new JobDriverPhase.Executing(
                        executeHandle,
                        gathering.PendingWork.Value,
                        gathering.StartTick
                    );
                }

                void TickNoWorkFound()
                {
                    if (ReadyToAdvance(gatherCompleted: true))
                    {
                        FinishInstantly();
                    }
                }

                void TickExecuting(JobDriverPhase.Executing executing)
                {
                    if (!executing.Handle.IsCompleted)
                    {
                        return;
                    }

                    var tickCount = Find.TickManager.TicksGame - executing.StartTick;
                    ColonyManagerReduxMod.Instance.LogVerboseMessage(
                        $"Pawn {pawn.Name} toiling with managing at {station.Label} having managing speed {managingSpeed} took {tickCount} ticks to complete"
                    );

                    FinishInstantly();
                }

                void FinishInstantly()
                {
                    intlSkill.Learn(ComputeCatchUpSkillGain(workDone, workNeeded));
                    workDone = workNeeded;
                    ReadyForNextToil();
                }
            },
        };
        toil.AddFinishAction(() =>
        {
            switch (phase)
            {
                case JobDriverPhase.Executing { Handle.IsCompleted: false } executing:
                    ColonyManagerReduxMod.Instance.LogVerboseMessage(
                        $"Cancelling managing job's execute phase because pawn {pawn.Name} toiling with managing at {station.Label} was interrupted."
                    );
                    executing.Handle.Cancel();
                    break;
                case JobDriverPhase.Gathering { Handle.IsCompleted: false } gathering:
                    ColonyManagerReduxMod.Instance.LogVerboseMessage(
                        $"Cancelling managing job's gather phase because pawn {pawn.Name} toiling with managing at {station.Label} was interrupted."
                    );
                    gathering.Handle.Cancel();
                    break;
                default:
                    // NotStarted (nothing to cancel yet), NoWorkFound (no coroutine involved),
                    // or a completed handle (nothing left to cancel).
                    break;
            }
        });

        return toil.WithEffect(EffecterDefOf.Research, TargetIndex.A)
            .WithProgressBar(TargetIndex.A, () => workDone / workNeeded);
    }
}
