// JobStack.cs
// Copyright Karel Kroeze, 2017-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
///     Full jobstack, in order of assignment
/// </summary>
public class JobStack(Manager manager) : IExposable
{
    public Manager manager = manager;

    private List<ManagerJob> jobStack = [];

    /// <summary>
    ///     Jobstack of jobs that are available now
    /// </summary>
    public List<ManagerJob> CurStack
    {
        get { return jobStack.Where(mj => mj.ShouldDoNow).OrderBy(mj => mj.Priority).ToList(); }
    }

    /// <summary>
    ///     Highest priority available job
    /// </summary>
    public ManagerJob NextJob => CurStack.FirstOrDefault();

    public void ExposeData()
    {
        Scribe_Collections.Look(ref jobStack, "jobStack", LookMode.Deep, manager);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (jobStack.Any(j => !j.IsValid))
            {
                Log.Error(
                    $"Colony Manager :: Removing {jobStack.Count(j => !j.IsValid)} invalid manager jobs. If this keeps happening, please report it.");
                jobStack = jobStack.Where(job => job.IsValid).ToList();
            }
        }
    }

    public void Add(ManagerJob job)
    {
        job.Priority = jobStack.Count + 1;
        jobStack.Add(job);
    }

    /// <summary>
    ///     Add job to the stack with bottom priority.
    /// </summary>
    /// <param name="job"></param>
    public void BottomPriority<T>(T job) where T : ManagerJob
    {
        // get list of priorities for this type.
        var jobsOfType = jobStack.OfType<T>().OrderBy(j => j.Priority).ToList();
        var priorities = jobsOfType.Select(j => j.Priority).ToList();

        // make sure our job is on the bottom.
        job.Priority = jobStack.Count + 10;

        // re-sort
        jobsOfType = jobsOfType.OrderBy(j => j.Priority).ToList();

        // fill in priorities, making sure we don't affect other types.
        for (var i = 0; i < jobsOfType.Count; i++)
        {
            jobsOfType[i].Priority = priorities[i];
        }
        CleanPriorities();
    }

    public void DecreasePriority<T>(T job) where T : ManagerJob
    {
        ManagerJob jobB = jobStack.OfType<T>()
                                .OrderBy(mj => mj.Priority)
                                .First(mj => mj.Priority > job.Priority);
        SwitchPriorities(job, jobB);
        CleanPriorities();
    }

    /// <summary>
    ///     Cleanup job, delete from stack and update priorities.
    /// </summary>
    /// <param name="job"></param>
    public void Delete(ManagerJob job, bool cleanup = true)
    {
        if (cleanup)
        {
            job.CleanUp();
        }

        jobStack.Remove(job);
        CleanPriorities();
    }

    /// <summary>
    ///     Jobs of type T in jobstack, in order of priority
    /// </summary>
    public List<T> FullStack<T>() where T : ManagerJob
    {
        return jobStack.OrderBy(job => job.Priority).OfType<T>().ToList();
    }

    /// <summary>
    ///     Jobs in jobstack, in order of priority
    /// </summary>
    public List<ManagerJob> FullStack()
    {
        return jobStack.OrderBy(job => job.Priority).ToList();
    }

    public void IncreasePriority<T>(T job) where T : ManagerJob
    {
        ManagerJob jobB =
            jobStack.OfType<T>().OrderByDescending(mj => mj.Priority).First(mj => mj.Priority < job.Priority);
        SwitchPriorities(job, jobB);
        CleanPriorities();
    }

    public void SwitchPriorities(ManagerJob a, ManagerJob b)
    {
        (b.Priority, a.Priority) = (a.Priority, b.Priority);
    }

    public void TopPriority<T>(T job) where T : ManagerJob
    {
        // get list of priorities for this type.
        var jobsOfType = jobStack.OfType<T>().OrderBy(j => j.Priority).ToList();
        var priorities = jobsOfType.Select(j => j.Priority).ToList();

        // make sure our job is on top.
        job.Priority = -1;

        // re-sort
        jobsOfType = jobsOfType.OrderBy(j => j.Priority).ToList();

        // fill in priorities, making sure we don't affect other types.
        for (var i = 0; i < jobsOfType.Count; i++)
        {
            jobsOfType[i].Priority = priorities[i];
        }
        CleanPriorities();
    }

    /// <summary>
    ///     Call the worker for the next available job
    /// </summary>
    public bool TryDoNextJob()
    {
        var job = NextJob;
        if (job == null)
        {
            return false;
        }

        // update lastAction
        job.Touch();

        // perform next job if no action was taken
        if (!job.TryDoJob())
        {
            return TryDoNextJob();
        }

        return true;
    }

    /// <summary>
    ///     Normalize priorities
    /// </summary>
    private void CleanPriorities()
    {
        foreach (var (job, priority) in jobStack.OrderBy(mj => mj.Priority).Select((j, i) => (j, i)))
        {
            job.Priority = priority;
        }
    }
}
