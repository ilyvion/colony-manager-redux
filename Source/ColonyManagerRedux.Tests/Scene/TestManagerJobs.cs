// TestManagerJobs.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Tests;

/// <summary>
/// Scripted <see cref="ManagerJob"/> for driving <see cref="JobTracker"/> in tests. It borrows the
/// Logs manager's def, which has no job comps and a tab whose labels need nothing from the job.
/// </summary>
internal abstract class TestManagerJob : ManagerJob
{
    /// <summary>What the job's phases do.</summary>
    public enum Mode
    {
        /// <summary>Two-phase job that reports work done.</summary>
        Work,

        /// <summary>Two-phase job that reports no work done.</summary>
        NoWork,

        /// <summary>Throws while setting up the gather coroutine.</summary>
        ThrowOnGatherSetup,

        /// <summary>Throws while the gather coroutine runs.</summary>
        ThrowMidGather,

        /// <summary>Throws while the execute coroutine runs.</summary>
        ThrowOnExecute,

        /// <summary>Implements neither the two-phase nor the legacy API.</summary>
        NoApi,

        /// <summary>Only implements the legacy <c>TryDoJobCoroutine</c>.</summary>
        Legacy,
    }

    public Mode Behavior = Mode.Work;
    public int GatherCount;
    public int ExecuteCount;
    public int LegacyCount;
    public bool ThrowOnCleanUp { get; set; }
    public int CleanUpCount;
    public ManagerJobState StateAfterRun = ManagerJobState.Active;

    protected TestManagerJob(Manager manager)
        : base(manager)
    {
        _def = DefDatabase<ManagerDef>.GetNamed("CM_LogsManager");
    }

    public List<string> TargetList { get; } = [];
    public int TargetsEnumerations;
    public bool ThrowOnTick { get; set; }
    public int TickCount;

    public void SetTrigger(Trigger? trigger) => Trigger = trigger;

    public override IEnumerable<string> Targets
    {
        get
        {
            TargetsEnumerations++;
            return TargetList;
        }
    }

    public override void Tick()
    {
        TickCount++;
        if (ThrowOnTick)
        {
            throw new InvalidOperationException("Tick failed on purpose");
        }
    }

    public void CleanDead(List<Designation> designations, DesignationDef? def, ManagerLog? log) =>
        CleanDeadDesignations(designations, def, log);

    public static void CleanUpAll(List<Designation> designations, ManagerLog? log) =>
        CleanUpDesignations(designations, log);

    public static List<Designation> PlanDisallowed(
        IEnumerable<Designation> designations,
        ICollection<Designation> alreadyRemoving,
        Func<Designation, bool> isDisallowed
    ) => PlanDisallowedDesignations(designations, alreadyRemoving, isDisallowed);

    public static void ExecuteDisallowed(
        List<Designation> designations,
        IEnumerable<Designation> toRemove,
        Func<Designation, bool> isDisallowed,
        ManagerLog log
    ) => ExecuteDisallowedDesignations(designations, toRemove, isDisallowed, "things", log);

    public override WorkTypeDef? WorkTypeDef => null;

    public ManagerJobState State
    {
        get => JobState;
        set => JobState = value;
    }

    public override void CleanUp(ManagerLog? jobLog = null)
    {
        CleanUpCount++;
        if (ThrowOnCleanUp)
        {
            throw new InvalidOperationException("CleanUp failed on purpose");
        }
    }

    public override Coroutine? GatherJobDataCoroutine(ManagerLog jobLog, AnyBoxed<object?> data) =>
        Behavior switch
        {
            Mode.NoApi or Mode.Legacy => null,
            Mode.ThrowOnGatherSetup => throw new InvalidOperationException(
                "Gather setup failed on purpose"
            ),
            Mode.Work or Mode.NoWork or Mode.ThrowMidGather or Mode.ThrowOnExecute => Gather(data),
            _ => throw new InvalidOperationException("Unknown behavior"),
        };

    private Coroutine Gather(AnyBoxed<object?> data)
    {
        GatherCount++;
        if (Behavior == Mode.ThrowMidGather)
        {
            throw new InvalidOperationException("Gather failed on purpose");
        }
        data.Value = "gathered";
        yield return ResumeImmediately.Singleton;
    }

    public override Coroutine? ExecuteJobDataCoroutine(
        ManagerLog jobLog,
        object? data,
        Boxed<bool> workDone
    ) => Execute(workDone);

    private Coroutine Execute(Boxed<bool> workDone)
    {
        ExecuteCount++;
        if (Behavior == Mode.ThrowOnExecute)
        {
            throw new InvalidOperationException("Execute failed on purpose");
        }
        workDone.Value = Behavior == Mode.Work;
        JobState = StateAfterRun;
        yield return ResumeImmediately.Singleton;
    }

#pragma warning disable CS0672,CS0618 // Member overrides obsolete member
    public override Coroutine? TryDoJobCoroutine(ManagerLog jobLog, Boxed<bool> workDone) =>
        Behavior == Mode.Legacy ? Legacy(workDone) : null;
#pragma warning restore CS0672,CS0618

    private Coroutine Legacy(Boxed<bool> workDone)
    {
        LegacyCount++;
        workDone.Value = true;
        yield return ResumeImmediately.Singleton;
    }
}

internal sealed class TestManagerJobA(Manager manager) : TestManagerJob(manager);

internal sealed class TestManagerJobB(Manager manager) : TestManagerJob(manager);
