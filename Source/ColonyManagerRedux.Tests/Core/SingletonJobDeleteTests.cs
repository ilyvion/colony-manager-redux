// SingletonJobDeleteTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The Power job is a map-wide singleton; deleting it (as the gravship import does)
/// must succeed without raising.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class SingletonJobDeleteTests
{
    [Test]
    public static void DeletingPowerJobDirectlyRemovesIt()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<ManagerJob_Power>("CM_PowerManager");

        job.Delete();

        Expect.IsFalse(scene.Manager.JobTracker.HasJob(job));
    }

    [Test]
    public static void DeletingThroughTheTrackerLogsNoError()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<ManagerJob_Power>("CM_PowerManager");

        scene.Manager.JobTracker.Delete(job);

        Expect.IsFalse(scene.Manager.JobTracker.HasJob(job));
    }
}
