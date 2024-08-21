// JobTracker.cs
// Copyright Karel Kroeze, 2017-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Buffers;
using System.Text;
using ilyvion.Laboratory.Extensions;
using LudeonTK;

namespace ColonyManagerRedux;

[HotSwappable]
public class JobTracker(Manager manager) : IExposable
{
    private readonly Manager _manager = manager;

    private List<ManagerJob> jobs = [];
    internal List<ManagerJob> JobLists
    {
        get
        {
            if (jobs == null)
            {
                ColonyManagerReduxMod.Instance.LogError("The jobs collection was null. This " +
                    "should never happen, but it means that any configured jobs you had will " +
                    "have been lost. If this happens repeatedly, there's something seriously " +
                    "wrong with your game somewhere.");
                jobs = [];
            }
            return jobs;
        }
    }

    public IEnumerable<ManagerJob> Jobs => JobLists;

    private IEnumerable<ManagerJob> JobsInOrderOfPriority =>
        Jobs.Where(mj => !mj.IsSuspended && mj.ShouldDoNow).OrderBy(mj => mj.Priority);

    public bool HasNoJobs => JobLists.Count == 0;

    public int MaxPriority => JobLists.Count - 1;

    /// <summary>
    ///     Highest priority available job
    /// </summary>
    public ManagerJob? NextJob => JobsInOrderOfPriority.FirstOrDefault();

    [DebugOutput("Colony Manager Redux", true)]
    public static void JobStatuses()
    {
        var manager = Manager.For(Find.CurrentMap);
        var jobTracker = manager.JobTracker;

        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine("Job count: " + jobTracker.JobLists.Count);
        stringBuilder.AppendLine("Has no jobs: " + jobTracker.HasNoJobs);
        stringBuilder.AppendLine("NextJob: " + jobTracker.NextJob?.ToString() ?? "<none>");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine("Jobs: ");
        foreach (var job in jobTracker.Jobs)
        {
            stringBuilder.AppendLine(job.ToString());
        }
        stringBuilder.AppendLine();

        stringBuilder.AppendLine("Jobs in order of priority: ");
        foreach (var job in jobTracker.JobsInOrderOfPriority)
        {
            stringBuilder.AppendLine(job.ToString());
        }
        ColonyManagerReduxMod.Instance.LogDevMessage(stringBuilder.ToString());
    }

    public void ExposeData()
    {
        Scribe_Collections.Look(ref jobs, "jobs", LookMode.Deep, _manager);

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            if (jobs == null)
            {
                jobs = [];
                ColonyManagerReduxMod.Instance.LogError("The jobs collection was null on load. " +
                    "This means it wasn't saved properly and any configured jobs you had will " +
                    "have been lost. If this happens repeatedly, there's something seriously " +
                    "wrong with your game somewhere.");
            }
        }

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (jobs.Any(j => !j.IsValid))
            {
                ColonyManagerReduxMod.Instance.LogError(
                    $"Removing {jobs.Count(j => !j.IsValid)} invalid manager jobs. " +
                    "If this keeps happening, please report it.");
                jobs = jobs.Where(job => job.IsValid).ToList();
            }
            CleanPriorities();
        }
    }

    public void Add(ManagerJob job)
    {
        if (job == null)
        {
            throw new ArgumentNullException(nameof(job));
        }

        job.Priority = MaxPriority + 1;
        JobLists.Add(job);
    }

    /// <summary>
    ///     Cleanup job, delete from stack and update priorities.
    /// </summary>
    /// <param name="job"></param>
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

        JobLists.Remove(job);
        CleanPriorities();
    }

    public IEnumerable<T> JobsOfType<T>() => Jobs.OrderBy(job => job.Priority).OfType<T>();

    internal (int lowest, int highest) GetBoundsForJobsOfType<T>()
        where T : ManagerJob => Jobs.OfType<T>().Select(j => j.Priority).MinAndMax();

    public bool HasJob(ManagerJob job) => JobLists.Contains(job);

    private bool _isRunningJobs;
    public bool IsRunningJobs => _isRunningJobs;

    /// <summary>
    ///     Call the worker for the next available job
    /// </summary>
    internal Coroutine? TryDoNextJob()
    {
        var job = NextJob;
        if (job == null)
        {
            return null;
        }

        return TryDoNextJobInner();

        Coroutine TryDoNextJobInner()
        {
            bool responsibleForFlag = !_isRunningJobs;
            _isRunningJobs = true;

            // perform next job if no action was taken
            string jobLogLabel = null!;
            try
            {
                jobLogLabel = job.Tab.GetMainLabel(job) + " (" + job.Tab.GetSubLabel(job) + ")";
            }
            catch (Exception err)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    "Suspending manager job because it errored on " +
                    $"{nameof(TryDoNextJob)}: \n{err}");
                job.IsSuspended = true;
                job.CausedException = err;
            }
            if (job.CausedException != null)
            {
                yield return (TryDoNextJob() ?? []).ResumeWhenOtherCoroutineIsCompleted(
                    debugHandle: $"TryDoNextJobAfterException1({job.GetUniqueLoadID()})");
                if (responsibleForFlag)
                {
                    _isRunningJobs = false;
                }
                yield break;
            }

            ManagerLog log = new(job)
            {
                LogLabel = jobLogLabel
            };
            Boxed<bool> workDone = new(false);

            var wasCompleted = job.JobState == ManagerJobState.Completed;
            Coroutine coroutine = null!;
            try
            {
                coroutine = job.TryDoJobCoroutine(log, workDone)
                    ?? TryDoJobTheOldWay(job, log, workDone);
            }
            catch (Exception err)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    "Suspending manager job because it errored on " +
                    $"{nameof(TryDoNextJob)}: \n{err}");
                job.IsSuspended = true;
                job.CausedException = err;
            }
            if (job.CausedException != null)
            {
                yield return (TryDoNextJob() ?? []).ResumeWhenOtherCoroutineIsCompleted(
                    debugHandle: $"TryDoNextJobAfterException2({job.GetUniqueLoadID()})");
                if (responsibleForFlag)
                {
                    _isRunningJobs = false;
                }
                yield break;
            }

            CoroutineHandle handle = MultiTickCoroutineManager.StartCoroutine(coroutine,
                debugHandle: $"TryDoNextJob({job.GetUniqueLoadID()})");
            yield return handle.ResumeWhenOtherCoroutineIsCompleted();

            if (handle.Exception is Exception err2)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    "Suspending manager job because it errored on " +
                    $"{nameof(TryDoNextJob)}: \n{err2}");
                job.IsSuspended = true;
                job.CausedException = err2;

                yield return (TryDoNextJob() ?? []).ResumeWhenOtherCoroutineIsCompleted(
                    debugHandle: $"TryDoNextJobAfterException3({job.GetUniqueLoadID()})");
                if (responsibleForFlag)
                {
                    _isRunningJobs = false;
                }
                yield break;
            }

            if (!wasCompleted || job.JobState != ManagerJobState.Completed)
            {
                // Don't log jobs where the state is Completed both before and after TryDoJob;
                // those TryDoJobs are only for checking whether a job should be resumed again, and
                // it was decided we weren't about to resume yet.
                log._workDone = workDone;
                foreach (var jobLogger in _manager.CompsOfType<IJobLogger>())
                {
                    jobLogger.AddLog(log);
                }
            }

            // mark job as dealt with
            job.Touch();

            if (!workDone)
            {
                yield return (TryDoNextJob() ?? []).ResumeWhenOtherCoroutineIsCompleted(
                    debugHandle: $"TryDoNextJobAfterNoWorkDone({job.GetUniqueLoadID()})");
            }

            if (responsibleForFlag)
            {
                _isRunningJobs = false;
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

    private static void SwitchPriorities(ManagerJob a, ManagerJob b)
    {
        (b.Priority, a.Priority) = (a.Priority, b.Priority);
    }

    private void Reprioritize<T>(T job, int newPriority) where T : ManagerJob
    {

        // get list of priorities for this type.
        // Use ArrayPool<T> and stackalloc to reduce GC pressure
        var jobsOfTypeCount = Jobs.OfType<T>().Count();
        using var jobsOfType = ArrayPool<ManagerJob>.Shared.RentWithSelfReturn(jobsOfTypeCount);
        Span<int> priorities = jobsOfTypeCount < Constants.MaxStackallocSize
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

    internal void TopPriority<T>(T job) where T : ManagerJob
    {
        Reprioritize(job, -1);
    }

    internal void BottomPriority<T>(T job) where T : ManagerJob
    {
        Reprioritize(job, MaxPriority + 1);
    }

    internal void IncreasePriority<T>(T job) where T : ManagerJob
    {
        ManagerJob jobB = Jobs
            .OfType<T>()
            .OrderByDescending(mj => mj.Priority)
            .First(mj => mj.Priority < job.Priority);
        SwitchPriorities(job, jobB);
        CleanPriorities();
    }

    internal void DecreasePriority<T>(T job) where T : ManagerJob
    {
        ManagerJob jobB = Jobs
            .OfType<T>()
            .OrderBy(mj => mj.Priority)
            .First(mj => mj.Priority > job.Priority);
        SwitchPriorities(job, jobB);
        CleanPriorities();
    }
}
