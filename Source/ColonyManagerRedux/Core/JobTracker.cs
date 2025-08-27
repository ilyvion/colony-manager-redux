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
            job.CleanUp();
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
    ///     Call the worker for the next available job
    /// </summary>
    internal Coroutine? TryDoNextJob()
    {
        var job = NextJob;
        ColonyManagerReduxMod.Instance.LogVerboseMessage(
            $"Manager.TryDoWork called with {job} as next job."
        );
        return job == null ? null : TryDoNextJobInner();

        Coroutine TryDoNextJobInner()
        {
            var responsibleForFlag = !IsRunningJobs;
            IsRunningJobs = true;

            // perform next job if no action was taken
            string jobLogLabel = null!;
            try
            {
                jobLogLabel = job.Tab.GetMainLabel(job) + " (" + job.Tab.GetSubLabel(job) + ")";
            }
            catch (Exception err)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    "Suspending manager job because it errored on "
                        + $"{nameof(TryDoNextJob)}: \n{err}"
                );
                job.IsSuspended = true;
                job.CausedException = err;
            }
            if (job.CausedException != null)
            {
                yield return (TryDoNextJob() ?? []).ResumeWhenOtherCoroutineIsCompleted(
                    debugHandle: $"TryDoNextJobAfterException1({job.GetUniqueLoadID()})"
                );
                if (responsibleForFlag)
                {
                    IsRunningJobs = false;
                }
                yield break;
            }

            ManagerLog log = new(job) { LogLabel = jobLogLabel };
            Boxed<bool> workDone = new(false);

            var wasCompleted = job.JobState == ManagerJobState.Completed;
            Coroutine coroutine = null!;
            try
            {
                ColonyManagerReduxMod.Instance.LogVerboseMessage($"Setting up job's coroutine.");
                coroutine =
                    job.TryDoJobCoroutine(log, workDone) ?? TryDoJobTheOldWay(job, log, workDone);
            }
            catch (Exception err)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    "Suspending manager job because it errored on setting up "
                        + $"{nameof(TryDoNextJob)}: \n{err}"
                );
                job.IsSuspended = true;
                job.CausedException = err;
            }
            if (job.CausedException != null)
            {
                ColonyManagerReduxMod.Instance.LogVerboseMessage(
                    $"Since setting up the job caused an exception, let's try to do the next job."
                );
                yield return (TryDoNextJob() ?? []).ResumeWhenOtherCoroutineIsCompleted(
                    debugHandle: $"TryDoNextJobAfterException2({job.GetUniqueLoadID()})"
                );
                ColonyManagerReduxMod.Instance.LogVerboseMessage(
                    $"Back from the next job after the exception."
                );
                if (responsibleForFlag)
                {
                    IsRunningJobs = false;
                }
                yield break;
            }

            ColonyManagerReduxMod.Instance.LogVerboseMessage(
                $"Waiting for job's coroutine to complete."
            );
            var handle = MultiTickCoroutineManager.StartCoroutine(
                coroutine,
                debugHandle: $"TryDoNextJob({job.GetUniqueLoadID()})"
            );
            yield return handle.ResumeWhenOtherCoroutineIsCompleted();
            ColonyManagerReduxMod.Instance.LogVerboseMessage($"Job's coroutine completed.");

            if (handle.Exception is Exception err2)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    "Suspending manager job because it errored on running "
                        + $"{nameof(TryDoNextJob)}: \n{err2}"
                );
                job.IsSuspended = true;
                job.CausedException = err2;

                ColonyManagerReduxMod.Instance.LogVerboseMessage(
                    $"Since running the job caused an exception, let's try to do the next job."
                );
                yield return (TryDoNextJob() ?? []).ResumeWhenOtherCoroutineIsCompleted(
                    debugHandle: $"TryDoNextJobAfterException3({job.GetUniqueLoadID()})"
                );
                ColonyManagerReduxMod.Instance.LogVerboseMessage(
                    $"Back from the next job after the exception."
                );
                if (responsibleForFlag)
                {
                    IsRunningJobs = false;
                }
                yield break;
            }

            if (!wasCompleted || job.JobState != ManagerJobState.Completed)
            {
                // Don't log jobs where the state is Completed both before and after TryDoJob;
                // those TryDoJobs are only for checking whether a job should be resumed again, and
                // it was decided we weren't about to resume yet.
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
            ColonyManagerReduxMod.Instance.LogVerboseMessage(
                $"Mark the job as having been updated."
            );
            job.Touch();

            if (!workDone)
            {
                ColonyManagerReduxMod.Instance.LogVerboseMessage(
                    $"Since the job did not do any work, let's try to do the next job."
                );
                yield return (TryDoNextJob() ?? []).ResumeWhenOtherCoroutineIsCompleted(
                    debugHandle: $"TryDoNextJobAfterNoWorkDone({job.GetUniqueLoadID()})"
                );
                ColonyManagerReduxMod.Instance.LogVerboseMessage(
                    $"Back from the next job after the one with no work done."
                );
            }

            if (responsibleForFlag)
            {
                IsRunningJobs = false;
            }
        }

        static Coroutine TryDoJobTheOldWay(ManagerJob job, ManagerLog log, Boxed<bool> workDone)
        {
#pragma warning disable CS0618 // This is the one place we are allowed to call it
            workDone.Value = job.TryDoJob(log);
#pragma warning restore CS0618
            yield break;
        }
    }

    private void CleanPriorities()
    {
        foreach (var (job, priority) in Jobs.OrderBy(mj => mj.Priority).Select((j, i) => (j, i)))
        {
            job.Priority = priority;
        }
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
        foreach (var (j, i) in Jobs.OfType<T>().OrderBy(j => j.Priority).Select((j, i) => (j, i)))
        {
            jobsOfType[i] = j;
            priorities[i] = j.Priority;
        }

        // make sure our job is on top.
        job.Priority = newPriority;

        // re-sort
        IlyvionArray.SortBy(jobsOfType.Arr, 0, jobsOfTypeCount, j => j.Priority);

        // fill in priorities, making sure we don't affect other types.
        for (var i = 0; i < jobsOfTypeCount; i++)
        {
            jobsOfType[i].Priority = priorities[i];
        }
        CleanPriorities();
    }

    internal void TopPriority<T>(T job)
        where T : ManagerJob => Reprioritize(job, -1);

    internal void BottomPriority<T>(T job)
        where T : ManagerJob => Reprioritize(job, MaxPriority + 1);

    internal void IncreasePriority<T>(T job)
        where T : ManagerJob
    {
        ManagerJob jobB = Jobs.OfType<T>()
            .OrderByDescending(mj => mj.Priority)
            .First(mj => mj.Priority < job.Priority);
        SwitchPriorities(job, jobB);
        CleanPriorities();
    }

    internal void DecreasePriority<T>(T job)
        where T : ManagerJob
    {
        ManagerJob jobB = Jobs.OfType<T>()
            .OrderBy(mj => mj.Priority)
            .First(mj => mj.Priority > job.Priority);
        SwitchPriorities(job, jobB);
        CleanPriorities();
    }
}
