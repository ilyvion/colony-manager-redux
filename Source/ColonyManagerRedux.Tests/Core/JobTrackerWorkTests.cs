// JobTrackerWorkTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The gather/execute state machine of <see cref="JobTracker"/>, driven with scripted jobs.
/// Coroutines are run to completion synchronously, so no game ticks are needed.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class JobTrackerWorkTests
{
    private const string SuspendedLog = "Suspending manager job";
    private const string CoroutineFailedLog = "caused exception on coroutine";

    private static T AddDueJob<T>(ManagerScene scene, TestManagerJob.Mode mode)
        where T : TestManagerJob
    {
        var job = scene.AddJob<T>();
        job.Behavior = mode;
        job.Untouch();
        return job;
    }

    private static void DoWork(ManagerScene scene) =>
        ManagerScene.RunToCompletion(scene.Manager.TryDoWork());

    [Test]
    public static void SingleJobWithWorkIsExecutedTouchedAndLogged()
    {
        using var scene = new ManagerScene();
        var job = AddDueJob<TestManagerJobA>(scene, TestManagerJob.Mode.Work);

        DoWork(scene);

        Expect.AreEqual(1, job.GatherCount);
        Expect.AreEqual(1, job.ExecuteCount);
        Expect.IsFalse(job.IsForceUpdatePending, "Job was touched");
        Expect.IsTrue(job.HasBeenUpdated);
        Expect.IsFalse(job.ShouldDoNow, "Job is no longer due");
        Expect.IsFalse(scene.Manager.JobTracker.IsRunningJobs);
        var logs = scene.NewLogs.Where(l => l.IsForJob(job)).ToList();
        Expect.AreEqual(1, logs.Count);
        Expect.IsTrue(logs.Count == 1 && logs[0].WorkDone);
    }

    [Test]
    public static void NoDueJobsMeansNoWork()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        job.Touch();

        Expect.IsNull(scene.Manager.TryDoWork());
        Expect.AreEqual(0, job.GatherCount);
        Expect.IsFalse(scene.Manager.JobTracker.IsRunningJobs);
    }

    [Test]
    public static void JobWithNoWorkFallsThroughToNextJob()
    {
        using var scene = new ManagerScene();
        var a = AddDueJob<TestManagerJobA>(scene, TestManagerJob.Mode.NoWork);
        var b = AddDueJob<TestManagerJobB>(scene, TestManagerJob.Mode.Work);

        DoWork(scene);

        Expect.AreEqual(1, a.ExecuteCount);
        Expect.AreEqual(1, b.ExecuteCount);
        Expect.IsTrue(a.HasBeenUpdated && !a.ShouldDoNow, "A was touched");
        Expect.IsTrue(b.HasBeenUpdated && !b.ShouldDoNow, "B was touched");
        Expect.IsFalse(scene.Manager.JobTracker.IsRunningJobs);
    }

    [Test]
    public static void OnlyTheHighestPriorityJobRunsWhenItDoesWork()
    {
        using var scene = new ManagerScene();
        var a = AddDueJob<TestManagerJobA>(scene, TestManagerJob.Mode.Work);
        var b = AddDueJob<TestManagerJobB>(scene, TestManagerJob.Mode.Work);

        DoWork(scene);

        Expect.AreEqual(1, a.ExecuteCount);
        Expect.AreEqual(0, b.GatherCount);
        Expect.AreEqual(0, b.ExecuteCount);
        Expect.IsTrue(b.ShouldDoNow, "B is still due");
    }

    [Test]
    [ErrorsAllowed(SuspendedLog)]
    [WarningsAllowed(CoroutineFailedLog)]
    public static void ThrowMidGatherSuspendsJobAndRunsNextOne()
    {
        using var scene = new ManagerScene();
        var a = AddDueJob<TestManagerJobA>(scene, TestManagerJob.Mode.ThrowMidGather);
        var b = AddDueJob<TestManagerJobB>(scene, TestManagerJob.Mode.Work);

        DoWork(scene);

        Expect.IsTrue(a.IsSuspended);
        Expect.IsNotNull(a.CausedException);
        Expect.AreEqual(0, a.ExecuteCount);
        Expect.AreEqual(1, b.ExecuteCount);
        Expect.IsFalse(scene.Manager.JobTracker.IsRunningJobs);
    }

    [Test]
    [ErrorsAllowed(SuspendedLog)]
    public static void ThrowOnGatherSetupSuspendsJobAndRunsNextOne()
    {
        using var scene = new ManagerScene();
        var a = AddDueJob<TestManagerJobA>(scene, TestManagerJob.Mode.ThrowOnGatherSetup);
        var b = AddDueJob<TestManagerJobB>(scene, TestManagerJob.Mode.Work);

        DoWork(scene);

        Expect.IsTrue(a.IsSuspended);
        Expect.IsNotNull(a.CausedException);
        Expect.AreEqual(1, b.ExecuteCount);
        Expect.IsFalse(scene.Manager.JobTracker.IsRunningJobs);
    }

    [Test]
    [ErrorsAllowed(SuspendedLog)]
    [WarningsAllowed(CoroutineFailedLog)]
    public static void ThrowMidGatherWithNoNextJobResetsRunningFlagAndYieldsNoPendingWork()
    {
        using var scene = new ManagerScene();
        var a = AddDueJob<TestManagerJobA>(scene, TestManagerJob.Mode.ThrowMidGather);

        AnyBoxed<JobTracker.PendingJobWork?> pending = new(null);
        var gather = scene.Manager.TryGatherWork(pending);
        Expect.IsNotNull(gather);
        ManagerScene.RunToCompletion(gather);

        Expect.IsTrue(a.IsSuspended);
        Expect.IsNull(pending.Value);
        Expect.IsFalse(scene.Manager.JobTracker.IsRunningJobs);
    }

    [Test]
    [ErrorsAllowed(SuspendedLog)]
    [WarningsAllowed(CoroutineFailedLog)]
    public static void ThrowOnExecuteSuspendsJobAndGathersAndExecutesNextJob()
    {
        using var scene = new ManagerScene();
        var a = AddDueJob<TestManagerJobA>(scene, TestManagerJob.Mode.ThrowOnExecute);
        var b = AddDueJob<TestManagerJobB>(scene, TestManagerJob.Mode.Work);

        DoWork(scene);

        Expect.IsTrue(a.IsSuspended);
        Expect.IsNotNull(a.CausedException);
        Expect.AreEqual(1, b.GatherCount);
        Expect.AreEqual(1, b.ExecuteCount);
        Expect.IsFalse(scene.Manager.JobTracker.IsRunningJobs);
    }

    [Test]
    [ErrorsAllowed("implements neither")]
    public static void JobWithNeitherApiIsSuspendedWithNotImplemented()
    {
        using var scene = new ManagerScene();
        var a = AddDueJob<TestManagerJobA>(scene, TestManagerJob.Mode.NoApi);

        DoWork(scene);

        Expect.IsTrue(a.IsSuspended);
        Expect.IsTrue(a.CausedException is NotImplementedException);
        Expect.IsFalse(scene.Manager.JobTracker.IsRunningJobs);
    }

    [Test]
    public static void LegacyJobRunsOnlyInExecutePhase()
    {
        using var scene = new ManagerScene();
        var a = AddDueJob<TestManagerJobA>(scene, TestManagerJob.Mode.Legacy);

        AnyBoxed<JobTracker.PendingJobWork?> pending = new(null);
        ManagerScene.RunToCompletion(scene.Manager.TryGatherWork(pending));

        Expect.AreEqual(0, a.LegacyCount, "Nothing ran during gather");
        Expect.IsNotNull(pending.Value);
        if (pending.Value == null)
        {
            return;
        }

        ManagerScene.RunToCompletion(scene.Manager.TryExecuteWork(pending.Value));

        Expect.AreEqual(1, a.LegacyCount);
        Expect.AreEqual(0, a.GatherCount);
        Expect.AreEqual(0, a.ExecuteCount);
        Expect.IsFalse(scene.Manager.JobTracker.IsRunningJobs);
    }

    [Test]
    public static void CompletedToCompletedRunsAreNotLogged()
    {
        using var scene = new ManagerScene();
        var a = AddDueJob<TestManagerJobA>(scene, TestManagerJob.Mode.NoWork);
        a.State = ManagerJobState.Completed;
        a.StateAfterRun = ManagerJobState.Completed;

        DoWork(scene);

        Expect.AreEqual(1, a.ExecuteCount);
        Expect.AreEqual(0, scene.NewLogs.Count(l => l.IsForJob(a)));
    }

    [Test]
    public static void ActiveToCompletedRunsAreLogged()
    {
        using var scene = new ManagerScene();
        var a = AddDueJob<TestManagerJobA>(scene, TestManagerJob.Mode.NoWork);
        a.StateAfterRun = ManagerJobState.Completed;

        DoWork(scene);

        Expect.AreEqual(1, scene.NewLogs.Count(l => l.IsForJob(a)));
    }
}
