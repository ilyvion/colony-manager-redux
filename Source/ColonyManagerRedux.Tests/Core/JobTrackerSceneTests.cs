// JobTrackerSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// Orchestration of <see cref="JobTracker"/> add/delete/reprioritise on a live map, including the
/// invariant that priorities are shared across job types while moves are per-type.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class JobTrackerSceneTests
{
    private sealed class SpyComp : ManagerComp
    {
        public List<ManagerJob> Deleted { get; } = [];

        public override void Notify_JobDeleted(ManagerJob job) => Deleted.Add(job);
    }

    private static void ExpectDenseAndSorted(JobTracker tracker)
    {
        var priorities = tracker.Jobs.Select(j => j.Priority).ToList();
        Expect.That(
            () => priorities.SequenceEqual(Enumerable.Range(0, priorities.Count)),
            "Priorities are dense 0..n-1 in list order"
        );
    }

    private static void ExpectOrder(IEnumerable<ManagerJob> actual, params ManagerJob[] expected) =>
        Expect.That(() => actual.SequenceEqual(expected), "Jobs are in the expected order");

    [Test]
    public static void AddAssignsNextPriority()
    {
        using var scene = new ManagerScene();
        var a1 = scene.AddJob<TestManagerJobA>();
        var b1 = scene.AddJob<TestManagerJobB>();
        var a2 = scene.AddJob<TestManagerJobA>();

        Expect.AreEqual(0, a1.Priority);
        Expect.AreEqual(1, b1.Priority);
        Expect.AreEqual(2, a2.Priority);
        Expect.AreEqual(2, scene.Manager.JobTracker.MaxPriority);
    }

    [Test]
    public static void AddStartsJobAgeAtTimeOfAdding()
    {
        using var scene = new ManagerScene();
        var job = new TestManagerJobA(scene.Manager)
        {
            IsManaged = true,
            _jobCreatedTick = Find.TickManager.TicksGame - (GenDate.TicksPerDay * 5),
        };

        scene.Manager.JobTracker.Add(job);

        Expect.AreEqual(0, job.TicksSinceLastUpdate);
    }

    [Test]
    public static void AddNullThrows()
    {
        using var scene = new ManagerScene();
        _ = Expect.Throws<ArgumentNullException>(() => scene.Manager.JobTracker.Add(null!));
    }

    [Test]
    public static void IncreasePriorityMovesWithinTypeAndLeavesOtherTypesAlone()
    {
        using var scene = new ManagerScene();
        var tracker = scene.Manager.JobTracker;
        var a1 = scene.AddJob<TestManagerJobA>();
        var b1 = scene.AddJob<TestManagerJobB>();
        var a2 = scene.AddJob<TestManagerJobA>();
        var b2 = scene.AddJob<TestManagerJobB>();

        tracker.IncreasePriority(a2);

        ExpectOrder(tracker.JobsOfType<TestManagerJobA>(), a2, a1);
        ExpectOrder(tracker.JobsOfType<TestManagerJobB>(), b1, b2);
        // The global list is reconciled to priority order: A2 took A1's slot.
        ExpectOrder(tracker.Jobs, a2, b1, a1, b2);
        ExpectDenseAndSorted(tracker);
    }

    [Test]
    public static void DecreasePriorityMovesWithinType()
    {
        using var scene = new ManagerScene();
        var tracker = scene.Manager.JobTracker;
        var a1 = scene.AddJob<TestManagerJobA>();
        var b1 = scene.AddJob<TestManagerJobB>();
        var a2 = scene.AddJob<TestManagerJobA>();

        tracker.DecreasePriority(a1);

        ExpectOrder(tracker.JobsOfType<TestManagerJobA>(), a2, a1);
        ExpectOrder(tracker.Jobs, a2, b1, a1);
        ExpectDenseAndSorted(tracker);
    }

    [Test]
    public static void IncreasePriorityOnTopJobOfTypeThrows()
    {
        using var scene = new ManagerScene();
        var a1 = scene.AddJob<TestManagerJobA>();
        _ = scene.AddJob<TestManagerJobA>();

        _ = Expect.Throws<InvalidOperationException>(() =>
            scene.Manager.JobTracker.IncreasePriority(a1)
        );
    }

    [Test]
    public static void TopAndBottomPriorityMoveWithinTypeOnly()
    {
        using var scene = new ManagerScene();
        var tracker = scene.Manager.JobTracker;
        var a1 = scene.AddJob<TestManagerJobA>();
        var b1 = scene.AddJob<TestManagerJobB>();
        var a2 = scene.AddJob<TestManagerJobA>();
        var a3 = scene.AddJob<TestManagerJobA>();

        tracker.TopPriority(a3);
        ExpectOrder(tracker.JobsOfType<TestManagerJobA>(), a3, a1, a2);
        ExpectDenseAndSorted(tracker);

        tracker.BottomPriority(a3);
        ExpectOrder(tracker.JobsOfType<TestManagerJobA>(), a1, a2, a3);
        ExpectOrder(tracker.JobsOfType<TestManagerJobB>(), b1);
        ExpectDenseAndSorted(tracker);
    }

    [Test]
    public static void DeleteRenumbersAndNotifiesComps()
    {
        using var scene = new ManagerScene();
        var tracker = scene.Manager.JobTracker;
        var spy = new SpyComp { Manager = scene.Manager };
        scene.Manager.AllCompsForReading.Add(spy);
        try
        {
            var a1 = scene.AddJob<TestManagerJobA>();
            var b1 = scene.AddJob<TestManagerJobB>();
            var a2 = scene.AddJob<TestManagerJobA>();

            tracker.Delete(b1);

            ExpectOrder(tracker.Jobs, a1, a2);
            ExpectDenseAndSorted(tracker);
            Expect.AreEqual(1, b1.CleanUpCount);
            ExpectOrder(spy.Deleted, b1);
        }
        finally
        {
            _ = scene.Manager.AllCompsForReading.Remove(spy);
        }
    }

    [Test]
    public static void DeleteWithoutCleanupSkipsCleanUp()
    {
        using var scene = new ManagerScene();
        var a1 = scene.AddJob<TestManagerJobA>();

        scene.Manager.JobTracker.Delete(a1, cleanup: false);

        Expect.AreEqual(0, a1.CleanUpCount);
        Expect.IsFalse(scene.Manager.JobTracker.HasJob(a1));
    }

    [Test]
    [ErrorsAllowed("CleanUp")]
    [WarningsAllowed("CleanUp")]
    public static void DeleteStillRemovesJobWhenCleanUpThrows()
    {
        using var scene = new ManagerScene();
        var a1 = scene.AddJob<TestManagerJobA>();
        a1.ThrowOnCleanUp = true;

        scene.Manager.JobTracker.Delete(a1);

        Expect.AreEqual(1, a1.CleanUpCount);
        Expect.IsFalse(scene.Manager.JobTracker.HasJob(a1));
    }

    [Test]
    public static void DeleteNullThrows()
    {
        using var scene = new ManagerScene();
        _ = Expect.Throws<ArgumentNullException>(() => scene.Manager.JobTracker.Delete(null!));
    }

    [Test]
    public static void BoundsForTypeWithNoJobsAreZero()
    {
        using var scene = new ManagerScene();
        _ = scene.AddJob<TestManagerJobA>();

        var (lowest, highest) = scene.Manager.JobTracker.GetBoundsForJobsOfType<TestManagerJobB>();

        Expect.AreEqual(0, lowest);
        Expect.AreEqual(0, highest);
    }

    [Test]
    public static void BoundsForTypeUseGlobalPriorities()
    {
        using var scene = new ManagerScene();
        _ = scene.AddJob<TestManagerJobB>();
        _ = scene.AddJob<TestManagerJobA>();
        _ = scene.AddJob<TestManagerJobB>();
        _ = scene.AddJob<TestManagerJobA>();

        var (lowest, highest) = scene.Manager.JobTracker.GetBoundsForJobsOfType<TestManagerJobA>();

        Expect.AreEqual(1, lowest);
        Expect.AreEqual(3, highest);
    }

    [Test]
    public static void JobsOfTypeFiltersByType()
    {
        using var scene = new ManagerScene();
        var a1 = scene.AddJob<TestManagerJobA>();
        _ = scene.AddJob<TestManagerJobB>();

        ExpectOrder(scene.Manager.JobTracker.JobsOfType<TestManagerJobA>(), a1);
    }

    [Test]
    [ErrorsAllowed(
        "Removing 1 invalid manager jobs",
        "Exception in ToString",
        "Could not do PostLoadInit on error"
    )]
    [WarningsAllowed("Could not resolve reference")]
    public static void PostLoadRemovesInvalidJobsAndRenumbers()
    {
        using var scene = new ManagerScene();
        var saved = new JobTracker(scene.Manager);
        saved.Add(new TestManagerJobA(scene.Manager) { IsManaged = true });
        saved.Add(new TestManagerJobB(scene.Manager) { IsManaged = true });
        saved.Add(new TestManagerJobA(scene.Manager) { IsManaged = true });
        var xml = ScribeRoundTrip.Save(
            new ScribeRoundTrip.ManagerHarness(
                scene.Manager,
                new ScribeRoundTrip.DeepHarness<JobTracker>(scene.Manager) { Value = saved }
            )
        );

        // Point the middle job at a manager that does not exist, which is what makes a job invalid.
        var managerReference = $"<manager>{scene.Manager.GetUniqueLoadID()}</manager>";
        var firstIndex = xml.IndexOf(managerReference, StringComparison.Ordinal);
        var secondIndex = xml.IndexOf(
            managerReference,
            firstIndex + managerReference.Length,
            StringComparison.Ordinal
        );
        Expect.IsTrue(secondIndex > 0, "The saved XML contains a manager reference per job");
        xml =
            xml[..secondIndex]
            + "<manager>ColonyManagerRedux_9999</manager>"
            + xml[(secondIndex + managerReference.Length)..];

        var loaded = new ScribeRoundTrip.DeepHarness<JobTracker>(scene.Manager);
        ScribeRoundTrip.Load(xml, new ScribeRoundTrip.ManagerHarness(scene.Manager, loaded));

        var tracker = loaded.Value;
        Expect.IsNotNull(tracker);
        if (tracker == null)
        {
            return;
        }
        Expect.AreEqual(2, tracker.Jobs.Count());
        Expect.All(tracker.Jobs, j => j.IsValid);
        Expect.All(tracker.Jobs, j => j is TestManagerJobA);
        ExpectDenseAndSorted(tracker);
    }
}
