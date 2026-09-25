// ManagerJobLifecycleSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// Creation, initialisation, import and ticking of manager jobs, and the manager's job-ID counter.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobLifecycleSceneTests
{
    private sealed class FirstComp : ManagerJobComp;

    private sealed class SecondComp : ManagerJobComp;

    private sealed class ThrowingInitComp : ManagerJobComp
    {
        protected internal override void Initialize() =>
            throw new InvalidOperationException("Initialize failed on purpose");
    }

    private sealed class ThrowingCtorComp : ManagerJobComp
    {
        public ThrowingCtorComp()
        {
            throw new InvalidOperationException("Constructor failed on purpose");
        }
    }

    private sealed class ThrowingCtorJob : TestManagerJob
    {
        public ThrowingCtorJob(Manager manager)
            : base(manager)
        {
            throw new InvalidOperationException("Job constructor failed on purpose");
        }
    }

    private static ManagerDef MakeDef(Type? jobClass, params Type[] compClasses) =>
        new()
        {
            defName = "CMRTest_" + Guid.NewGuid().ToString("N"),
            managerJobClass = jobClass,
            jobComps = [.. compClasses.Select(c => new ManagerJobCompProperties { compClass = c })],
        };

    // ManagerDefMaker.MakeManagerJob / Manager.NewJob

    [Test]
    public static void NewJobOfTheWrongTypeThrows()
    {
        using var scene = new ManagerScene();
        var def = MakeDef(typeof(TestManagerJobA));

        _ = Expect.Throws<ArgumentException>(() => scene.Manager.NewJob<TestManagerJobB>(def));
    }

    [Test]
    public static void NewJobWithANullDefThrows()
    {
        using var scene = new ManagerScene();

        _ = Expect.Throws<ArgumentNullException>(() =>
            scene.Manager.NewJob<TestManagerJobA>(null!)
        );
    }

    [Test]
    public static void DefWithoutAJobClassMakesNoJobAndNewJobThrows()
    {
        using var scene = new ManagerScene();
        var def = MakeDef(null);

        Expect.IsNull(ManagerDefMaker.MakeManagerJob(def, scene.Manager));
        _ = Expect.Throws<ArgumentException>(() => scene.Manager.NewJob<TestManagerJobA>(def));
    }

    [Test]
    public static void MadeJobHasItsDefAndItsCompsInitialised()
    {
        using var scene = new ManagerScene();
        var def = MakeDef(typeof(TestManagerJobA), typeof(FirstComp));

        var job = scene.Manager.NewJob<TestManagerJobA>(def);

        Expect.ReferencesAreEqual(def, job.Def);
        var comp = job.CompOfType<FirstComp>();
        Expect.IsNotNull(comp);
        Expect.ReferencesAreEqual(job, comp!.Parent);
        Expect.IsNotNull(comp.Props);
    }

    [Test]
    [ErrorsAllowed("Could not create")]
    public static void ThrowingJobConstructorIsLoggedAndMakesNoJob()
    {
        using var scene = new ManagerScene();
        var def = MakeDef(typeof(ThrowingCtorJob));

        Expect.IsNull(ManagerDefMaker.MakeManagerJob(def, scene.Manager));
    }

    // ManagerJob.Initialize

    [Test]
    [ErrorsAllowed("Could not instantiate or initialize")]
    public static void CompThatThrowsInInitializeIsDroppedAndTheOthersSurvive()
    {
        using var scene = new ManagerScene();
        var def = MakeDef(
            typeof(TestManagerJobA),
            typeof(FirstComp),
            typeof(ThrowingInitComp),
            typeof(SecondComp)
        );

        var job = scene.Manager.NewJob<TestManagerJobA>(def);

        Expect.IsNotNull(job.CompOfType<FirstComp>());
        Expect.IsNull(job.CompOfType<ThrowingInitComp>());
        Expect.IsNotNull(job.CompOfType<SecondComp>());
    }

    [Test]
    [ErrorsAllowed("Could not instantiate or initialize")]
    public static void CompThatThrowsInItsConstructorIsDroppedAndTheOthersSurvive()
    {
        using var scene = new ManagerScene();
        var def = MakeDef(
            typeof(TestManagerJobA),
            typeof(FirstComp),
            typeof(ThrowingCtorComp),
            typeof(SecondComp)
        );

        var job = scene.Manager.NewJob<TestManagerJobA>(def);

        Expect.IsNotNull(job.CompOfType<FirstComp>());
        Expect.IsNull(job.CompOfType<ThrowingCtorComp>());
        Expect.IsNotNull(job.CompOfType<SecondComp>());
    }

    // ManagerJob.PostImportInt

    private static void WithNewJobsImmediatelyOutdated(bool value, Action action)
    {
        var settings = ColonyManagerReduxMod.Settings;
        var previous = settings.NewJobsAreImmediatelyOutdated;
        settings.NewJobsAreImmediatelyOutdated = value;
        try
        {
            action();
        }
        finally
        {
            settings.NewJobsAreImmediatelyOutdated = previous;
        }
    }

    [Test]
    public static void ImportedJobStaysOutdatedWhenNewJobsAreImmediatelyOutdated()
    {
        using var scene = new ManagerScene();
        WithNewJobsImmediatelyOutdated(
            true,
            () =>
            {
                var job = new TestManagerJobA(scene.Manager);

                job.PostImportInt();

                Expect.IsTrue(job.IsManaged);
                Expect.IsFalse(job.HasBeenUpdated);
            }
        );
    }

    [Test]
    public static void ImportedJobIsTouchedWhenNewJobsAreNotImmediatelyOutdated()
    {
        using var scene = new ManagerScene();
        TestManagerJobA job = null!;
        WithNewJobsImmediatelyOutdated(true, () => job = new TestManagerJobA(scene.Manager));
        Expect.IsFalse(job.HasBeenUpdated);

        WithNewJobsImmediatelyOutdated(false, job.PostImportInt);

        Expect.IsTrue(job.HasBeenUpdated);
    }

    [Test]
    public static void ImportPointsTheTriggerAtTheImportedJob()
    {
        using var scene = new ManagerScene();
        var original = new TestManagerJobA(scene.Manager);
        var imported = new TestManagerJobB(scene.Manager);
        imported.SetTrigger(new Trigger_Threshold(original));

        imported.PostImportInt();

        Expect.ReferencesAreEqual(imported, imported.Trigger!.Job);
    }

    // ManagerJob.TargetsLabel

    [Test]
    public static void TargetsLabelIsStaleUntilTargetsAreReportedChanged()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        job.TargetList.Add("alpha");
        Expect.AreEqual("alpha", job.TargetsLabel);

        job.TargetList.Add("beta");
        Expect.AreEqual("alpha", job.TargetsLabel);

        job.Notify_TargetsChanged();
        Expect.AreEqual("alpha, beta", job.TargetsLabel);
    }

    [Test]
    public static void TargetsLabelForNoTargetsIsThePlaceholder()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();

        var label = job.TargetsLabel;

        Expect.IsFalse(string.IsNullOrEmpty(label));
        job.TargetList.Add("alpha");
        job.Notify_TargetsChanged();
        Expect.AreNotEqual(label, job.TargetsLabel);
    }

    [Test]
    public static void TargetsLabelIsNotRebuiltWhileNothingChanged()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        job.TargetList.Add("alpha");
        _ = job.TargetsLabel;
        var enumerations = job.TargetsEnumerations;

        _ = job.TargetsLabel;
        _ = job.TargetsLabel;

        Expect.AreEqual(enumerations, job.TargetsEnumerations);
    }

    // Manager.MapComponentTick

    [Test]
    [ErrorsAllowed("errored on tick")]
    public static void JobThatThrowsOnTickIsSuspendedAndDoesNotStopOthers()
    {
        using var scene = new ManagerScene();
        var thrower = scene.AddJob<TestManagerJobA>();
        var healthy = scene.AddJob<TestManagerJobB>();
        thrower.ThrowOnTick = true;

        scene.Manager.MapComponentTick();

        Expect.IsTrue(thrower.IsSuspended);
        Expect.IsNotNull(thrower.CausedException);
        Expect.AreEqual(1, healthy.TickCount);
        Expect.IsFalse(healthy.IsSuspended);
    }

    [Test]
    public static void SuspendedJobsAreNotTicked()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        job.IsSuspended = true;

        scene.Manager.MapComponentTick();

        Expect.AreEqual(0, job.TickCount);
    }

    // Manager.GetNextManagerJobID

    private static void WithJobIdCounter(Manager manager, int value, Action action)
    {
        var previous = manager._nextManagerJobID;
        manager._nextManagerJobID = value;
        try
        {
            action();
        }
        finally
        {
            manager._nextManagerJobID = previous;
        }
    }

    [Test]
    public static void JobIdsAreHandedOutInSequence()
    {
        using var scene = new ManagerScene();

        WithJobIdCounter(
            scene.Manager,
            0,
            () =>
            {
                Expect.AreEqual(0, scene.Manager.GetNextManagerJobID());
                Expect.AreEqual(1, scene.Manager.GetNextManagerJobID());
                Expect.AreEqual(2, scene.Manager.GetNextManagerJobID());
            }
        );
    }

    // The safety net for the counter reaching int.MaxValue.
    [Test]
    [WarningsAllowed("max value")]
    public static void JobIdCounterOverflowFallsBackToAnUnusedId()
    {
        using var scene = new ManagerScene();
        WithJobIdCounter(
            scene.Manager,
            0,
            () =>
            {
                _ = scene.AddJob<TestManagerJobA>();
                _ = scene.AddJob<TestManagerJobA>();
                _ = scene.AddJob<TestManagerJobA>();
            }
        );
        var used = scene.Manager.JobTracker.Jobs.Select(j => j.LoadID).ToList();

        WithJobIdCounter(
            scene.Manager,
            int.MaxValue - 1,
            () =>
            {
                Expect.AreEqual(int.MaxValue - 1, scene.Manager.GetNextManagerJobID());
                var next = scene.Manager.GetNextManagerJobID();
                Expect.IsFalse(used.Contains(next), $"ID {next} collides with a tracked job");
                Expect.AreEqual(3, next);
            }
        );
    }

    // Manager.PurgeHistory

    [Test]
    public static void PurgeHistoryClearsEveryJobsHistoryButKeepsTheJobs()
    {
        using var scene = new ManagerScene();
        var chapter = new ManagerJobHistoryChapterDef
        {
            defName = "CMRTest_" + Guid.NewGuid().ToString("N"),
            historyLabel = new DirectHistoryLabel("Test"),
            color = Color.white,
        };
        var def = MakeDef(typeof(TestManagerJobA));
        def.jobComps.Add(
            new CompProperties_ManagerJobHistory
            {
                compClass = typeof(CompManagerJobHistory),
                chapters = [chapter],
            }
        );
        var withHistory = scene.Register(scene.Manager.NewJob<TestManagerJobA>(def));
        var withoutHistory = scene.AddJob<TestManagerJobB>();
        var history = withHistory.CompOfType<CompManagerJobHistory>()!.History;
        history.Update(0, [7], [7]);
        Expect.Any(history._chapters[0].ValuesFor(Period.Day), v => v == 7);

        scene.Manager.PurgeHistory();

        Expect.None(history._chapters[0].ValuesFor(Period.Day), v => v == 7);
        Expect.IsTrue(scene.Manager.JobTracker.HasJob(withHistory));
        Expect.IsTrue(scene.Manager.JobTracker.HasJob(withoutHistory));
    }
}
