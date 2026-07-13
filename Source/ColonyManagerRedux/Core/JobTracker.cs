// JobTracker.cs
// Copyright Karel Kroeze, 2017-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Buffers;
using System.Text;
using ilyvion.Laboratory.Extensions;
using LudeonTK;

namespace ColonyManagerRedux;

/// <summary>
/// Tracks and manages all manager jobs for a given manager instance.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="JobTracker"/> class for the specified manager.
/// </remarks>
/// <param name="manager">The manager instance this job tracker is associated with.</param>
[HotSwappable]
public class JobTracker(Manager manager) : IExposable
{
    private readonly Manager _manager = manager;

    private List<ManagerJob> jobs = [];

    internal List<ManagerJob> JobList
    {
        get
        {
            if (jobs == null)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    "The jobs collection was null. This "
                        + "should never happen, but it means that any configured jobs you had will "
                        + "have been lost. If this happens repeatedly, there's something seriously "
                        + "wrong with your game somewhere."
                );
                jobs = [];
            }
            return jobs;
        }
    }

    /// <summary>
    /// Gets a read-only collection of all manager jobs associated with this tracker.
    /// </summary>
    public IEnumerable<ManagerJob> Jobs => JobList.AsReadOnly();

    private IEnumerable<ManagerJob> JobsInOrderOfPriority =>
        Jobs.Where(mj => !mj.IsSuspended && mj.ShouldDoNow).OrderBy(mj => mj.Priority);

    /// <summary>
    /// Gets a value indicating whether there are no jobs in the job tracker.
    /// </summary>
    public bool HasNoJobs => JobList.Count == 0;

    /// <summary>
    /// Gets the maximum priority value among the jobs in the job tracker.
    /// </summary>
    public int MaxPriority => JobList.Count - 1;

    /// <summary>
    ///     Highest priority available job
    /// </summary>
    public ManagerJob? NextJob => JobsInOrderOfPriority.FirstOrDefault();

    [DebugOutput("Colony Manager Redux", true)]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static void JobStatuses()
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        var manager = Manager.For(Find.CurrentMap);
        var jobTracker = manager.JobTracker;

        var stringBuilder = new StringBuilder()
            .AppendLine("Job count: " + jobTracker.JobList.Count)
            .AppendLine("Has no jobs: " + jobTracker.HasNoJobs)
            .AppendLine(("Next job: " + jobTracker.NextJob?.GetUniqueLoadID()) ?? "<none>")
            .AppendLine();

        _ = stringBuilder.AppendLine("Next jobs in order of priority: ");
        foreach (var job in jobTracker.JobsInOrderOfPriority)
        {
            _ = stringBuilder.AppendLine(job.GetUniqueLoadID());
        }
        _ = stringBuilder.AppendLine();

        _ = stringBuilder.AppendLine("All jobs: ");
        foreach (var job in jobTracker.Jobs)
        {
            _ = stringBuilder.AppendLine(job.ToString());
        }
        ColonyManagerReduxMod.Instance.LogDevMessage(stringBuilder.ToString());
    }

    /// <inheritdoc/>
    public void ExposeData()
    {
        Scribe_Collections.Look(ref jobs, "jobs", LookMode.Deep, _manager);

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            if (jobs == null)
            {
                jobs = [];
                ColonyManagerReduxMod.Instance.LogError(
                    "The jobs collection was null on load. "
                        + "This means it wasn't saved properly and any configured jobs you had will "
                        + "have been lost. If this happens repeatedly, there's something seriously "
                        + "wrong with your game somewhere."
                );
            }
        }

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (jobs.Any(job => job == null || !job.IsValid))
            {
                ColonyManagerReduxMod.Instance.LogError(
                    $"Removing {jobs.Count(j => j == null || !j.IsValid)} invalid manager jobs. "
                        + "If this keeps happening, please report it."
                );
                jobs.RemoveWhere(job => job == null || !job.IsValid);
            }
            CleanPriorities();
        }
    }

    /// <summary>
    /// Adds a manager job to the job tracker and assigns it the next available priority.
    /// </summary>
    /// <param name="job">The manager job to add.</param>
    /// <exception cref="ArgumentNullException">Thrown if the job is null.</exception>
    public void Add(ManagerJob job)
    {
        if (job == null)
        {
            throw new ArgumentNullException(nameof(job));
        }

        job.Priority = MaxPriority + 1;
        JobList.Add(job);
    }

    /// <summary>
    ///     Cleanup job, delete from stack and update priorities.
    /// </summary>
    /// <param name="job">The manager job to delete.</param>
    /// <param name="cleanup">Whether to perform cleanup on the job before deleting.</param>
    public void Delete(ManagerJob job, bool cleanup = true)
    {
        if (job == null)
        {
            throw new ArgumentNullException(nameof(job));
        }

        if (cleanup)
        {
            try
            {
                job.CleanUp();
            }
            catch (Exception err)
            {
                ColonyManagerReduxMod.Instance.LogException(
                    $"ManagerJob caused exception during {nameof(ManagerJob.CleanUp)}",
                    err
                );
            }
        }

        _ = JobList.Remove(job);
        CleanPriorities();
    }

    /// <summary>
    /// Returns an ordered enumerable of jobs of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of ManagerJob to filter by.</typeparam>
    public IEnumerable<T> JobsOfType<T>() => Jobs.OrderBy(job => job.Priority).OfType<T>();

    internal (int lowest, int highest) GetBoundsForJobsOfType<T>()
        where T : ManagerJob => Jobs.OfType<T>().Select(j => j.Priority).MinAndMax();

    /// <summary>
    /// Determines whether the specified job exists in the job tracker.
    /// </summary>
    /// <param name="job">The manager job to check for existence.</param>
    /// <returns>True if the job exists in the tracker; otherwise, false.</returns>
    public bool HasJob(ManagerJob job) => JobList.Contains(job);

    /// <summary>
    /// Gets a value indicating whether jobs are currently being executed by the job tracker.
    /// </summary>
    public bool IsRunningJobs { get; private set; }

    /// <summary>
    /// Carries state gathered for a job between
    /// <see cref="TryGatherNextJobWork(AnyBoxed{PendingJobWork})"/> and the
    /// later matching call to <see cref="TryExecuteJobWork"/>.
    /// </summary>
#pragma warning disable CA1034 // Nested types should not be visible; this type only makes sense alongside JobTracker
    public sealed class PendingJobWork(
        ManagerJob job,
        ManagerLog log,
        bool wasCompleted,
        bool responsibleForFlag
    )
#pragma warning restore CA1034
    {
        internal ManagerJob Job { get; } = job;
        internal ManagerLog Log { get; } = log;
        internal bool WasCompleted { get; } = wasCompleted;
        internal bool ResponsibleForFlag { get; } = responsibleForFlag;

        // True if this job doesn't implement the two-phase API; its obsolete
        // TryDoJobCoroutine fallback is run in full during the execute step
        // since its body isn't guaranteed to be free of game-state mutation.
        internal bool IsLegacyFallback { get; set; }
        internal object? GatheredData { get; set; }
    }

    /// <summary>
    ///     Gather the information needed for the next available job to do its work, without
    ///     changing anything in the game. Must be followed by a matching call to
    ///     <see cref="TryExecuteJobWork"/> using the same <see cref="PendingJobWork"/>.
    /// </summary>
    internal Coroutine? TryGatherNextJobWork(AnyBoxed<PendingJobWork?> pendingWork) =>
        TryGatherNextJobWork(pendingWork, null);

    // The responsibleForFlag parameter is threaded through explicitly (once computed) rather
    // than recomputed from !IsRunningJobs on every call: the exception-recovery branches below
    // recurse on the same pendingWork chain after IsRunningJobs has already been set true
    // by this same call chain, so recomputing it there would always yield false and the
    // chain would never reset IsRunningJobs once it finally succeeds or bottoms out. It's passed
    // in as a nullable override rather than computed eagerly by the caller, so that - matching
    // the pre-two-phase-split code - it's only read from IsRunningJobs lazily, on the coroutine's
    // first pump, rather than at the moment this method is called (which, since the returned
    // Coroutine isn't necessarily pumped immediately, could otherwise observe a stale value).
    private Coroutine? TryGatherNextJobWork(
        AnyBoxed<PendingJobWork?> pendingWork,
        bool? responsibleForFlagOverride
    )
    {
        var job = NextJob;
        ColonyManagerReduxMod.Instance.LogVerboseMessage(
            $"Manager.TryDoWork called with {job} as next job."
        );
        return job == null ? null : TryGatherNextJobWorkInner();

        Coroutine TryGatherNextJobWorkInner()
        {
            var responsibleForFlag = responsibleForFlagOverride ?? !IsRunningJobs;
            IsRunningJobs = true;

            string jobLogLabel = null!;
            try
            {
                jobLogLabel = job.Tab.GetMainLabel(job) + " (" + job.Tab.GetSubLabel(job) + ")";
            }
            catch (Exception err)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    "Suspending manager job because it errored on "
                        + $"{nameof(TryGatherNextJobWork)}: \n{err}"
                );
                job.IsSuspended = true;
                job.CausedException = err;
            }
            if (job.CausedException != null)
            {
                yield return (
                    TryGatherNextJobWork(pendingWork, responsibleForFlag) ?? []
                ).ResumeWhenOtherCoroutineIsCompleted(
                    debugHandle: $"TryGatherNextJobWorkAfterException1({job.GetUniqueLoadID()})"
                );
                if (responsibleForFlag && pendingWork.Value == null)
                {
                    IsRunningJobs = false;
                }
                yield break;
            }

            ManagerLog log = new(job) { LogLabel = jobLogLabel };
            var wasCompleted = job.JobState == ManagerJobState.Completed;
            var pending = new PendingJobWork(job, log, wasCompleted, responsibleForFlag);

            AnyBoxed<object?> data = new(null);
            Coroutine coroutine = null!;
            try
            {
                ColonyManagerReduxMod.Instance.LogVerboseMessage(
                    $"Setting up job's gather coroutine."
                );
                var outcome = JobPhaseOutcome.For(job.GatherJobDataCoroutine(log, data));
                coroutine = outcome.Match(
                    r => r.Value,
                    _ =>
                    {
                        // Legacy (obsolete TryDoJobCoroutine) jobs mutate game state in a single
                        // step, so none of that can safely run during gather; defer running the
                        // whole thing to the execute phase instead, same as a two-phase job's real
                        // mutations would be.
                        pending.IsLegacyFallback = true;
                        return [];
                    }
                );
            }
            catch (Exception err)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    "Suspending manager job because it errored on setting up "
                        + $"{nameof(TryGatherNextJobWork)}: \n{err}"
                );
                job.IsSuspended = true;
                job.CausedException = err;
            }
            if (job.CausedException != null)
            {
                ColonyManagerReduxMod.Instance.LogVerboseMessage(
                    $"Since setting up the job caused an exception, let's try to do the next job."
                );
                yield return (
                    TryGatherNextJobWork(pendingWork, responsibleForFlag) ?? []
                ).ResumeWhenOtherCoroutineIsCompleted(
                    debugHandle: $"TryGatherNextJobWorkAfterException2({job.GetUniqueLoadID()})"
                );
                if (responsibleForFlag && pendingWork.Value == null)
                {
                    IsRunningJobs = false;
                }
                yield break;
            }

            ColonyManagerReduxMod.Instance.LogVerboseMessage(
                $"Waiting for job's gather coroutine to complete."
            );
            var handle = MultiTickCoroutineManager.StartCoroutine(
                coroutine,
                debugHandle: $"TryGatherNextJobWork({job.GetUniqueLoadID()})"
            );
            yield return handle.ResumeWhenOtherCoroutineIsCompleted();
            ColonyManagerReduxMod.Instance.LogVerboseMessage($"Job's gather coroutine completed.");

            if (handle.Exception is Exception err2)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    "Suspending manager job because it errored on gathering "
                        + $"{nameof(TryGatherNextJobWork)}: \n{err2}"
                );
                job.IsSuspended = true;
                job.CausedException = err2;

                ColonyManagerReduxMod.Instance.LogVerboseMessage(
                    $"Since gathering the job caused an exception, let's try to do the next job."
                );
                yield return (
                    TryGatherNextJobWork(pendingWork, responsibleForFlag) ?? []
                ).ResumeWhenOtherCoroutineIsCompleted(
                    debugHandle: $"TryGatherNextJobWorkAfterException3({job.GetUniqueLoadID()})"
                );
                if (responsibleForFlag && pendingWork.Value == null)
                {
                    IsRunningJobs = false;
                }
                yield break;
            }

            pending.GatheredData = data.Value;
            pendingWork.Value = pending;
        }
    }

    /// <summary>
    ///     Execute the work gathered by a preceding call to
    ///     <see cref="TryGatherNextJobWork(AnyBoxed{PendingJobWork})"/>
    ///     for the same job, applying whatever changes it decided on.
    /// </summary>
    internal Coroutine TryExecuteJobWork(PendingJobWork pending)
    {
        var job = pending.Job;
        var log = pending.Log;
        Boxed<bool> workDone = new(false);

        if (pending.IsLegacyFallback || pending.GatheredData != null)
        {
            Coroutine? executeCoroutine = null;
            try
            {
                ColonyManagerReduxMod.Instance.LogVerboseMessage(
                    $"Setting up job's execute coroutine."
                );
                executeCoroutine = pending.IsLegacyFallback
                    // The obsolete fallback mutates game state in one step, so - unlike a
                    // two-phase job - it only runs now, gated behind the execute phase's
                    // timing, instead of during gather.
#pragma warning disable CS0618 // Type or member is obsolete
                    ? job.TryDoJobCoroutine(log, workDone)
#pragma warning restore CS0618
                    : job.ExecuteJobDataCoroutine(log, pending.GatheredData, workDone);
            }
            catch (Exception err)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    "Suspending manager job because it errored on setting up "
                        + $"{nameof(TryExecuteJobWork)}: \n{err}"
                );
                job.IsSuspended = true;
                job.CausedException = err;
            }

            if (job.CausedException == null)
            {
                var outcome = JobPhaseOutcome.For(executeCoroutine);
                if (outcome is not JobPhaseOutcome.Ready { Value: var readyExecuteCoroutine })
                {
                    ColonyManagerReduxMod.Instance.LogError(
                        $"Manager job {job.GetUniqueLoadID()} ({job.GetType().Name}) "
                            + $"implements neither the two-phase {nameof(ManagerJob.GatherJobDataCoroutine)} "
                            + $"API nor the (obsolete) {nameof(ManagerJob.TryDoJobCoroutine)} "
                            + "fallback, so it cannot do any work. Suspending it."
                    );
                    job.IsSuspended = true;
                    job.CausedException = new NotImplementedException(
                        $"{job.GetType().Name} does not implement "
                            + $"{nameof(ManagerJob.GatherJobDataCoroutine)} or "
                            + $"{nameof(ManagerJob.TryDoJobCoroutine)}."
                    );
                }
                else
                {
                    ColonyManagerReduxMod.Instance.LogVerboseMessage(
                        $"Waiting for job's execute coroutine to complete."
                    );
                    var handle = MultiTickCoroutineManager.StartCoroutine(
                        readyExecuteCoroutine,
                        debugHandle: $"TryExecuteJobWork({job.GetUniqueLoadID()})"
                    );
                    yield return handle.ResumeWhenOtherCoroutineIsCompleted();
                    ColonyManagerReduxMod.Instance.LogVerboseMessage(
                        $"Job's execute coroutine completed."
                    );

                    if (handle.Exception is Exception err2)
                    {
                        ColonyManagerReduxMod.Instance.LogError(
                            "Suspending manager job because it errored on executing "
                                + $"{nameof(TryExecuteJobWork)}: \n{err2}"
                        );
                        job.IsSuspended = true;
                        job.CausedException = err2;
                    }
                }
            }
        }

        if (job.CausedException != null)
        {
            ColonyManagerReduxMod.Instance.LogVerboseMessage(
                $"Since executing the job caused an exception, let's try to do the next job."
            );
            AnyBoxed<PendingJobWork?> nextPending = new(null);
            yield return (
                TryGatherNextJobWork(nextPending) ?? []
            ).ResumeWhenOtherCoroutineIsCompleted(
                debugHandle: $"TryExecuteJobWorkAfterException({job.GetUniqueLoadID()})"
            );
            if (nextPending.Value != null)
            {
                yield return TryExecuteJobWork(nextPending.Value)
                    .ResumeWhenOtherCoroutineIsCompleted();
            }
            if (pending.ResponsibleForFlag)
            {
                IsRunningJobs = false;
            }
            yield break;
        }

        if (ShouldLogJobRun(pending.WasCompleted, job.JobState))
        {
            // Don't log jobs where the state is Completed both before and after; those runs
            // are only for checking whether a job should be resumed again, and it was
            // decided we weren't about to resume yet.
            ColonyManagerReduxMod.Instance.LogVerboseMessage(
                $"Since the job did something, let's log it."
            );
            log._workDone = workDone;
            foreach (var jobLogger in _manager.CompsOfType<IJobLogger>())
            {
                jobLogger.AddLog(log);
            }
        }

        // mark job as dealt with
        ColonyManagerReduxMod.Instance.LogVerboseMessage($"Mark the job as having been updated.");
        job.Touch();

        if (!workDone)
        {
            ColonyManagerReduxMod.Instance.LogVerboseMessage(
                $"Since the job did not do any work, let's try to do the next job."
            );
            AnyBoxed<PendingJobWork?> nextPending = new(null);
            yield return (
                TryGatherNextJobWork(nextPending) ?? []
            ).ResumeWhenOtherCoroutineIsCompleted(
                debugHandle: $"TryExecuteJobWorkAfterNoWorkDone({job.GetUniqueLoadID()})"
            );
            if (nextPending.Value != null)
            {
                yield return TryExecuteJobWork(nextPending.Value)
                    .ResumeWhenOtherCoroutineIsCompleted();
            }
            ColonyManagerReduxMod.Instance.LogVerboseMessage(
                $"Back from the next job after the one with no work done."
            );
        }

        if (pending.ResponsibleForFlag)
        {
            IsRunningJobs = false;
        }
    }

    // Suppresses logging a job run that was Completed both before and after TryDoJob; those
    // runs are only checking whether a job should be resumed and didn't actually do anything.
    internal static bool ShouldLogJobRun(bool wasCompletedBefore, ManagerJobState stateAfter) =>
        !wasCompletedBefore || stateAfter != ManagerJobState.Completed;

    private void CleanPriorities()
    {
        var jobList = JobList;
        var currentPriorities = new int[jobList.Count];
        for (var i = 0; i < jobList.Count; i++)
        {
            currentPriorities[i] = jobList[i].Priority;
        }
        var newPriorities = ComputeCleanedPriorities(currentPriorities);
        for (var i = 0; i < jobList.Count; i++)
        {
            jobList[i].Priority = newPriorities[i];
        }
    }

    // Renumbers priorities densely from 0, preserving the relative order jobs are already in
    // (ties broken by original position, matching the stable sort this replaced).
    internal static int[] ComputeCleanedPriorities(IReadOnlyList<int> currentPrioritiesInOrder)
    {
        var order = Enumerable
            .Range(0, currentPrioritiesInOrder.Count)
            .OrderBy(i => currentPrioritiesInOrder[i])
            .ToArray();
        var result = new int[currentPrioritiesInOrder.Count];
        for (var rank = 0; rank < order.Length; rank++)
        {
            result[order[rank]] = rank;
        }
        return result;
    }

    private static void SwitchPriorities(ManagerJob a, ManagerJob b) =>
        (b.Priority, a.Priority) = (a.Priority, b.Priority);

    private void Reprioritize<T>(T job, int newPriority)
        where T : ManagerJob
    {
        // get list of priorities for this type.
        // Use ArrayPool<T> and stackalloc to reduce GC pressure
        var jobsOfTypeCount = Jobs.OfType<T>().Count();
        using var jobsOfType = ArrayPool<ManagerJob>.Shared.RentWithSelfReturn(jobsOfTypeCount);
        var priorities =
            jobsOfTypeCount < Constants.MaxStackallocSize
                ? stackalloc int[jobsOfTypeCount]
                : new int[jobsOfTypeCount];
        var movedIndex = -1;
        foreach (var (j, i) in Jobs.OfType<T>().OrderBy(j => j.Priority).Select((j, i) => (j, i)))
        {
            jobsOfType[i] = j;
            priorities[i] = j.Priority;
            if (j == job)
            {
                movedIndex = i;
            }
        }

        var newPriorities = ComputeReprioritizedPriorities(
            priorities.ToArray(),
            movedIndex,
            newPriority
        );

        // fill in priorities, making sure we don't affect other types.
        for (var i = 0; i < jobsOfTypeCount; i++)
        {
            jobsOfType[i].Priority = newPriorities[i];
        }
        CleanPriorities();
    }

    // Moves the job at `movedIndex` (within the ascending-by-priority `oldPrioritiesAscending`
    // list) to `newPriorityForMoved`, then redistributes the original set of priority values
    // across the resulting order, so other same-type jobs' priority values are otherwise
    // untouched (they still get renumbered densely afterwards by CleanPriorities).
    internal static int[] ComputeReprioritizedPriorities(
        IReadOnlyList<int> oldPrioritiesAscending,
        int movedIndex,
        int newPriorityForMoved
    )
    {
        var n = oldPrioritiesAscending.Count;
        var currentPriority = new int[n];
        for (var i = 0; i < n; i++)
        {
            currentPriority[i] = oldPrioritiesAscending[i];
        }
        currentPriority[movedIndex] = newPriorityForMoved;

        var order = Enumerable.Range(0, n).OrderBy(i => currentPriority[i]).ToArray();

        var result = new int[n];
        for (var rank = 0; rank < n; rank++)
        {
            result[order[rank]] = oldPrioritiesAscending[rank];
        }
        return result;
    }

    internal void TopPriority<T>(T job)
        where T : ManagerJob => Reprioritize(job, -1);

    internal void BottomPriority<T>(T job)
        where T : ManagerJob => Reprioritize(job, MaxPriority + 1);

    internal void IncreasePriority<T>(T job)
        where T : ManagerJob
    {
        var prioritiesOfType = Jobs.OfType<T>().Select(mj => mj.Priority).ToArray();
        var adjacentPriority = FindAdjacentPriority(prioritiesOfType, job.Priority, lower: true);
        ManagerJob jobB = Jobs.OfType<T>().First(mj => mj.Priority == adjacentPriority);
        SwitchPriorities(job, jobB);
        CleanPriorities();
    }

    internal void DecreasePriority<T>(T job)
        where T : ManagerJob
    {
        var prioritiesOfType = Jobs.OfType<T>().Select(mj => mj.Priority).ToArray();
        var adjacentPriority = FindAdjacentPriority(prioritiesOfType, job.Priority, lower: false);
        ManagerJob jobB = Jobs.OfType<T>().First(mj => mj.Priority == adjacentPriority);
        SwitchPriorities(job, jobB);
        CleanPriorities();
    }

    // Finds the priority value immediately adjacent to `currentPriority` within
    // `prioritiesOfType` (the largest one below it, or the smallest one above it). Throws
    // InvalidOperationException if there's no such value (i.e. currentPriority is already at
    // the top/bottom of its type), matching the previous .First(...) behavior this replaced.
    internal static int FindAdjacentPriority(
        IReadOnlyList<int> prioritiesOfType,
        int currentPriority,
        bool lower
    ) =>
        lower
            ? prioritiesOfType.Where(p => p < currentPriority).Max()
            : prioritiesOfType.Where(p => p > currentPriority).Min();
}
