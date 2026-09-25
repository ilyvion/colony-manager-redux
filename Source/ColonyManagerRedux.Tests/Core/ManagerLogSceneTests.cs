// ManagerLogSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// <see cref="ManagerLog"/> details, labels, job association and how a log saves the job it
/// belongs to.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerLogSceneTests
{
    private sealed class LogAndJobsHarness(Manager manager, List<ManagerJob> jobs, ManagerLog? log)
        : IExposable
    {
        public List<ManagerJob> Jobs = jobs;
        public ManagerLog? Log = log;

        public void ExposeData()
        {
            Scribe_Collections.Look(ref Jobs, "jobs", LookMode.Deep, manager);
            Scribe_Deep.Look(ref Log, "log");
        }
    }

    [Test]
    public static void LogWithoutWorkOrDetailsShowsOnePlaceholder()
    {
        using var scene = new ManagerScene();
        var log = new ManagerLog(scene.AddJob<TestManagerJobA>());

        Expect.AreEqual(1, log.Details.Count());
    }

    [Test]
    public static void AddingADetailReplacesThePlaceholder()
    {
        using var scene = new ManagerScene();
        var log = new ManagerLog(scene.AddJob<TestManagerJobA>());
        var placeholder = log.Details.Single().Text;

        log.AddDetail("did a thing");

        var detail = log.Details.Single();
        Expect.AreEqual("did a thing", detail.Text);
        Expect.AreNotEqual(placeholder, detail.Text);
    }

    [Test]
    public static void LogThatDidWorkWithoutDetailsShowsNone()
    {
        using var scene = new ManagerScene();
        var log = new ManagerLog(scene.AddJob<TestManagerJobA>()) { _workDone = true };

        Expect.IsEmpty(log.Details);
    }

    [Test]
    public static void LogLabelFallsBackToTheJobLabelUntilSet()
    {
        using var scene = new ManagerScene();
        var log = new ManagerLog(scene.AddJob<TestManagerJobA>());
        Expect.AreEqual(log.JobLabel, log.LogLabel);

        log.LogLabel = "Custom";

        Expect.AreEqual("Custom", log.LogLabel);
    }

    [Test]
    public static void LogKnowsWhetherItsJobIsStillTracked()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        var log = new ManagerLog(job);
        Expect.IsTrue(log.IsForJob(job));
        Expect.IsTrue(log.HasJob);

        scene.Manager.JobTracker.Delete(job);

        Expect.IsTrue(log.IsForJob(job));
        Expect.IsFalse(log.HasJob);
    }

    [Test]
    public static void ForgettingAnotherJobDoesNothingAndForgettingItsOwnJobReleasesIt()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        var other = scene.AddJob<TestManagerJobB>();
        var log = new ManagerLog(job);

        log.ForgetJob(other);
        Expect.IsTrue(log.IsForJob(job));

        log.ForgetJob(job);
        Expect.IsFalse(log.IsForJob(job));
        Expect.IsFalse(log.HasJob);
    }

    // Regression guard: saving a log whose job has been deleted raised a 'referenced but not
    // saved' warning; the reference must be dropped instead.
    [Test]
    public static void LogOfADeletedJobIsSavedWithoutAJobReference()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        var log = new ManagerLog(job);
        scene.Manager.JobTracker.Delete(job);

        var xml = ScribeRoundTrip.Save(
            new ScribeRoundTrip.ManagerHarness(
                scene.Manager,
                new ScribeRoundTrip.DeepHarness<ManagerLog> { Value = log }
            )
        );
        var loaded = ScribeRoundTrip.RoundTripDeep(scene.Manager, log);

        Expect.IsFalse(xml.Contains(job.GetUniqueLoadID(), StringComparison.Ordinal));
        Expect.IsNotNull(loaded);
        Expect.IsFalse(loaded!.HasJob);
        Expect.IsFalse(loaded.IsForJob(job));
    }

    [Test]
    public static void LogOfATrackedJobKeepsItsJobReferenceAcrossASave()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        var log = new ManagerLog(job);
        var loaded = new LogAndJobsHarness(scene.Manager, [], null);

        ScribeRoundTrip.Run(
            new ScribeRoundTrip.ManagerHarness(
                scene.Manager,
                new LogAndJobsHarness(scene.Manager, [job], log)
            ),
            new ScribeRoundTrip.ManagerHarness(scene.Manager, loaded)
        );

        Expect.AreEqual(1, loaded.Jobs.Count);
        Expect.IsNotNull(loaded.Log);
        Expect.IsTrue(loaded.Log!.IsForJob(loaded.Jobs[0]));
    }
}
