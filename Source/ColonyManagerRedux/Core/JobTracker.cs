﻿// JobTracker.cs
// Copyright Karel Kroeze, 2017-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Text;
using LudeonTK;

namespace ColonyManagerRedux;

public class JobTracker(Manager manager) : IExposable
{
    private readonly Manager _manager = manager;

    private List<ManagerJob> jobs = [];
    private IEnumerable<ManagerJob> JobsInOrderOfPriority =>
        jobs.Where(mj => !mj.IsSuspended && mj.ShouldDoNow).OrderBy(mj => mj.Priority);

    public bool HasNoJobs => jobs.Count == 0;

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
        stringBuilder.AppendLine("Job count: " + jobTracker.jobs.Count);
        stringBuilder.AppendLine("Has no jobs: " + jobTracker.HasNoJobs);
        stringBuilder.AppendLine("NextJob: " + jobTracker.NextJob?.ToString() ?? "<none>");
        stringBuilder.AppendLine();
        stringBuilder.AppendLine("Jobs: ");
        foreach (var job in jobTracker.jobs)
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

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (jobs.Any(j => !j.IsValid))
            {
                ColonyManagerReduxMod.Instance.LogError(
                    $"Removing {jobs.Count(j => !j.IsValid)} invalid manager jobs. " +
                    "If this keeps happening, please report it.");
                jobs = jobs.Where(job => job.IsValid).ToList();
            }
        }
    }

    public void Add(ManagerJob job)
    {
        if (job == null)
        {
            throw new ArgumentNullException(nameof(job));
        }

        job.Priority = jobs.Count + 1;
        jobs.Add(job);
    }

    /// <summary>
    ///     Add job to the stack with bottom priority.
    /// </summary>
    internal void BottomPriority<T>(T job) where T : ManagerJob
    {
        // get list of priorities for this type.
        var jobsOfType = jobs.OfType<T>().OrderBy(j => j.Priority).ToList();
        var priorities = jobsOfType.Select(j => j.Priority).ToList();

        // make sure our job is on the bottom.
        job.Priority = jobs.Count + 10;

        // re-sort
        jobsOfType = jobsOfType.OrderBy(j => j.Priority).ToList();

        // fill in priorities, making sure we don't affect other types.
        for (var i = 0; i < jobsOfType.Count; i++)
        {
            jobsOfType[i].Priority = priorities[i];
        }
        CleanPriorities();
    }

    internal void DecreasePriority<T>(T job) where T : ManagerJob
    {
        ManagerJob jobB = jobs.OfType<T>()
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
        if (job == null)
        {
            throw new ArgumentNullException(nameof(job));
        }

        if (cleanup)
        {
            job.CleanUp();
        }

        jobs.Remove(job);
        CleanPriorities();
    }

    public IEnumerable<T> JobsOfType<T>()
    {
        return jobs.OrderBy(job => job.Priority).OfType<T>();
    }

    public bool HasJob(ManagerJob job) => jobs.Contains(job);

    internal void IncreasePriority<T>(T job) where T : ManagerJob
    {
        ManagerJob jobB =
            jobs.OfType<T>().OrderByDescending(mj => mj.Priority).First(mj => mj.Priority < job.Priority);
        SwitchPriorities(job, jobB);
        CleanPriorities();
    }

    private static void SwitchPriorities(ManagerJob a, ManagerJob b)
    {
        (b.Priority, a.Priority) = (a.Priority, b.Priority);
    }

    internal void TopPriority<T>(T job) where T : ManagerJob
    {
        // get list of priorities for this type.
        var jobsOfType = jobs.OfType<T>().OrderBy(j => j.Priority).ToList();
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
    internal bool TryDoNextJob()
    {
        var job = NextJob;
        if (job == null)
        {
            return false;
        }

        try
        {
            // update lastAction
            job.Touch();

            // perform next job if no action was taken
            ManagerLog log = new(job)
            {
                LogLabel = job.Tab.GetMainLabel(job) + " (" + job.Tab.GetSubLabel(job) + ")"
            };
            bool workDone = job.TryDoJob(log);
            log._workDone = workDone;
            foreach (var jobLogger in _manager.CompsOfType<IJobLogger>())
            {
                jobLogger.AddLog(log);
            }

            if (!workDone)
            {
                return TryDoNextJob();
            }
        }
        catch (Exception err)
        {
            ColonyManagerReduxMod.Instance
                .LogError($"Suspending manager job because it errored on TryDoJob: \n{err}");
            job.IsSuspended = true;
            job.CausedException = err;

            return TryDoNextJob();
        }

        return true;
    }

    /// <summary>
    ///     Normalize priorities
    /// </summary>
    private void CleanPriorities()
    {
        foreach (var (job, priority) in jobs.OrderBy(mj => mj.Priority).Select((j, i) => (j, i)))
        {
            job.Priority = priority;
        }
    }
}
