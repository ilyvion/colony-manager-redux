// CompManagerJobHistorySceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The history update coroutine of <see cref="CompManagerJobHistory"/> and the static state it
/// shares between jobs.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class CompManagerJobHistorySceneTests
{
    private sealed class ScriptedWorker : HistoryWorker
    {
        public string Name = "";
        public List<string> Events = [];
        public bool Open = true;
        public bool ThrowOnUpdate;
        public bool UpdatesMaxValue;
        public int CountCalls;

        public override bool UpdatesMax => UpdatesMaxValue;

        public override Coroutine? HistoryUpdateCoroutine(ManagerJob managerJob, int tick) =>
            ThrowOnUpdate ? throw new InvalidOperationException("Update failed on purpose") : null;

        public override Coroutine GetCountForHistoryChapterCoroutine(
            ManagerJob managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> count
        ) => Gated(chapterDef, count, 10, "count");

        public override Coroutine GetTargetForHistoryChapterCoroutine(
            ManagerJob managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> target
        ) => Gated(chapterDef, target, 100, "target");

        public override Coroutine GetMaxForHistoryChapterCoroutine(
            ManagerJob managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> max
        ) => Gated(chapterDef, max, 1000, "max");

        private Coroutine Gated(
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> result,
            int factor,
            string kind
        )
        {
            CountCalls++;
            yield return new ResumeWhenTrue(() => Open);
            Events.Add($"{Name}:{kind}:{chapterDef.defName}");
            result.Value = factor * (chapterDef.defName == "First" ? 1 : 2);
        }
    }

    private static ManagerJobHistoryChapterDef Chapter(string name) =>
        new()
        {
            defName = name,
            historyLabel = new DirectHistoryLabel(name),
            color = Color.white,
        };

    private static (CompManagerJobHistory comp, ScriptedWorker worker) NewComp(
        ManagerJob job,
        string name,
        List<string> events
    )
    {
        var props = new CompProperties_ManagerJobHistory
        {
            workerClass = typeof(ScriptedWorker),
            chapters = [Chapter("First"), Chapter("Second")],
        };
        var worker = (ScriptedWorker)props.Worker;
        worker.Name = name;
        worker.Events = events;

        var comp = new CompManagerJobHistory { Parent = job };
        comp.InitializeInt(props);
        return (comp, worker);
    }

    private static void ResetSharedState()
    {
        CompManagerJobHistory._isRecordingHistory = false;
        CompManagerJobHistory._queuedToRecord = 0;
    }

    private static void TickCoroutines() =>
        Current.Game.GetComponent<MultiTickCoroutineManager>().GameComponentTick();

    private static void RunTest(Action<ManagerScene> test)
    {
        var speed = Find.TickManager.CurTimeSpeed;
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        ResetSharedState();
        try
        {
            using var scene = new ManagerScene();
            test(scene);
        }
        finally
        {
            ResetSharedState();
            Find.TickManager.CurTimeSpeed = speed;
        }
    }

    [Test]
    public static void FinalizeInitResetsTheStaticRecordingState() =>
        RunTest(scene =>
        {
            var (comp, _) = NewComp(scene.AddJob<TestManagerJobA>(), "A", []);
            CompManagerJobHistory._isRecordingHistory = true;
            CompManagerJobHistory._queuedToRecord = 3;

            comp.FinalizeInit();

            Expect.IsFalse(CompManagerJobHistory._isRecordingHistory);
            Expect.AreEqual(0, CompManagerJobHistory._queuedToRecord);
        });

    [Test]
    public static void UpdateRecordsCountAndTargetForEveryChapter() =>
        RunTest(scene =>
        {
            var (comp, _) = NewComp(scene.AddJob<TestManagerJobA>(), "A", []);

            ManagerScene.RunToCompletion(comp.DoHistoryUpdateCoroutine(comp.Props.Worker, 0));

            Expect.AreEqual((10, 100), comp.History._chapters[0].Last(Period.Day));
            Expect.AreEqual((20, 200), comp.History._chapters[1].Last(Period.Day));
            Expect.IsFalse(CompManagerJobHistory._isRecordingHistory);
            Expect.AreEqual(0, CompManagerJobHistory._queuedToRecord);
        });

    [Test]
    public static void UpdatesMaxWorkerRecordsTheMaxPerChapter() =>
        RunTest(scene =>
        {
            var (comp, worker) = NewComp(scene.AddJob<TestManagerJobA>(), "A", []);
            worker.UpdatesMaxValue = true;

            ManagerScene.RunToCompletion(comp.DoHistoryUpdateCoroutine(worker, 0));

            Expect.AreEqual(1000, comp.History._chapters[0].TrueMax);
            Expect.AreEqual(2000, comp.History._chapters[1].TrueMax);
        });

    [Test]
    public static void WorkerWithoutUpdatesMaxNeverAsksForTheMax() =>
        RunTest(scene =>
        {
            var (comp, worker) = NewComp(scene.AddJob<TestManagerJobA>(), "A", []);

            ManagerScene.RunToCompletion(comp.DoHistoryUpdateCoroutine(worker, 0));

            Expect.IsFalse(worker.Events.Any(e => e.Contains(":max:", StringComparison.Ordinal)));
        });

    [Test]
    [WarningsAllowed("caused exception on coroutine")]
    public static void ExceptionInTheWorkerStillResetsTheSharedState() =>
        RunTest(scene =>
        {
            var (comp, worker) = NewComp(scene.AddJob<TestManagerJobA>(), "A", []);
            worker.ThrowOnUpdate = true;

            ManagerScene.RunToCompletion(comp.DoHistoryUpdateCoroutine(worker, 0));

            Expect.IsFalse(CompManagerJobHistory._isRecordingHistory);
            Expect.AreEqual(0, CompManagerJobHistory._queuedToRecord);
            Expect.IsFalse(comp._reportedSkippedUpdateTick);
            Expect.IsFalse(comp._currentUpdateTick.HasValue);
        });

    [Test]
    public static void SecondUpdateStartedWhileOneIsRunningWaitsForItThenBothFinish() =>
        RunTest(scene =>
        {
            var job = scene.AddJob<TestManagerJobA>();
            var events = new List<string>();
            var (compA, workerA) = NewComp(job, "A", events);
            var (compB, workerB) = NewComp(job, "B", events);
            workerA.Open = false;

            var handleA = MultiTickCoroutineManager.StartCoroutine(
                compA.DoHistoryUpdateCoroutine(workerA, 0)
            );
            TickCoroutines();
            Expect.IsTrue(CompManagerJobHistory._isRecordingHistory, "A is recording");
            Expect.AreEqual(0, CompManagerJobHistory._queuedToRecord);

            var handleB = MultiTickCoroutineManager.StartCoroutine(
                compB.DoHistoryUpdateCoroutine(workerB, 0)
            );
            TickCoroutines();
            TickCoroutines();
            Expect.AreEqual(1, CompManagerJobHistory._queuedToRecord, "B is queued");
            Expect.AreEqual(0, workerB.CountCalls, "B must not start while A is recording");

            workerA.Open = true;
            for (var i = 0; i < 2000 && !(handleA.IsCompleted && handleB.IsCompleted); i++)
            {
                TickCoroutines();
            }

            Expect.IsTrue(handleA.IsCompleted, "A completed");
            Expect.IsTrue(handleB.IsCompleted, "B completed");
            Expect.IsFalse(CompManagerJobHistory._isRecordingHistory);
            Expect.AreEqual(0, CompManagerJobHistory._queuedToRecord);
            Expect.AreEqual((10, 100), compA.History._chapters[0].Last(Period.Day));
            Expect.AreEqual((10, 100), compB.History._chapters[0].Last(Period.Day));
            Expect.That(
                () =>
                    events.FindLastIndex(e => e.StartsWith("A:", StringComparison.Ordinal))
                    < events.FindIndex(e => e.StartsWith("B:", StringComparison.Ordinal)),
                "All of A's work happens before any of B's."
            );
        });

    [Test]
    public static void QueueingResetsTheSkipBookkeepingOnceTheQueueDrains() =>
        RunTest(scene =>
        {
            var (comp, _) = NewComp(scene.AddJob<TestManagerJobA>(), "A", []);
            comp._reportedSkippedUpdateTick = true;
            comp._currentUpdateTick = 500;

            ManagerScene.RunToCompletion(comp.DoHistoryUpdateCoroutine(comp.Props.Worker, 0));

            Expect.IsFalse(comp._reportedSkippedUpdateTick);
            Expect.IsFalse(comp._currentUpdateTick.HasValue);
        });
}
