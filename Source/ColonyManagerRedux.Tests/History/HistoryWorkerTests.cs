// HistoryWorkerTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class HistoryWorkerTests
{
    private static readonly ManagerJobHistoryChapterDef chapter = new() { defName = "TestChapter" };

    private sealed class DefaultWorker : HistoryWorker;

    private sealed class DefaultGenericWorker : HistoryWorker<TestManagerJobA>;

#pragma warning disable CS0672,CS0618 // Member overrides obsolete member
    private sealed class LegacyWorker : HistoryWorker
    {
        public override int GetCountForHistoryChapter(
            ManagerJob managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef
        ) => 7;

        public override int GetTargetForHistoryChapter(
            ManagerJob managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef
        ) => 8;

        public override int GetMaxForHistoryChapter(
            ManagerJob managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef
        ) => 9;
    }
#pragma warning restore CS0672,CS0618

    private static void Drain(Coroutine coroutine)
    {
        foreach (var _ in coroutine) { }
    }

    private static int Count(HistoryWorker worker)
    {
        var box = new Boxed<int> { Value = -1 };
        Drain(worker.GetCountForHistoryChapterCoroutine(null!, 0, chapter, box));
        return box.Value;
    }

    private static int Target(HistoryWorker worker)
    {
        var box = new Boxed<int> { Value = -1 };
        Drain(worker.GetTargetForHistoryChapterCoroutine(null!, 0, chapter, box));
        return box.Value;
    }

    private static int Max(HistoryWorker worker)
    {
        var box = new Boxed<int> { Value = -1 };
        Drain(worker.GetMaxForHistoryChapterCoroutine(null!, 0, chapter, box));
        return box.Value;
    }

    [Test]
    [WarningsAllowed("returning a count of 0 for TestChapter in .*DefaultWorker")]
    public static void UnimplementedCountIsZeroAndWarnsWithChapterAndWorkerType() =>
        Expect.AreEqual(0, Count(new DefaultWorker()));

    [Test]
    [WarningsAllowed("returning a target count of 0 for TestChapter in .*DefaultWorker")]
    public static void UnimplementedTargetIsZeroAndWarnsWithChapterAndWorkerType() =>
        Expect.AreEqual(0, Target(new DefaultWorker()));

    [Test]
    [WarningsAllowed("returning a max count of 0 for TestChapter in .*DefaultWorker")]
    public static void UnimplementedMaxIsZeroAndWarnsWithChapterAndWorkerType() =>
        Expect.AreEqual(0, Max(new DefaultWorker()));

    [Test]
    [WarningsAllowed("returning a count of 0 for TestChapter in .*DefaultGenericWorker")]
    public static void UnimplementedGenericCountIsZeroAndWarns() =>
        Expect.AreEqual(0, Count(new DefaultGenericWorker()));

    [Test]
    [WarningsAllowed("returning a target count of 0 for TestChapter in .*DefaultGenericWorker")]
    public static void UnimplementedGenericTargetIsZeroAndWarns() =>
        Expect.AreEqual(0, Target(new DefaultGenericWorker()));

    // The generic max variant used to swallow the missing implementation silently.
    [Test]
    [WarningsAllowed("returning a max count of 0 for TestChapter in .*DefaultGenericWorker")]
    public static void UnimplementedGenericMaxIsZeroAndWarns() =>
        Expect.AreEqual(0, Max(new DefaultGenericWorker()));

    [Test]
    public static void LegacySynchronousOverridesAreUsed()
    {
        var worker = new LegacyWorker();

        Expect.AreEqual(7, Count(worker));
        Expect.AreEqual(8, Target(worker));
        Expect.AreEqual(9, Max(worker));
    }

    [Test]
    public static void NullArgumentsThrow()
    {
        var worker = new DefaultWorker();
        var box = new Boxed<int>();

        _ = Expect.Throws<ArgumentNullException>(() =>
            Drain(worker.GetCountForHistoryChapterCoroutine(null!, 0, chapter, null!))
        );
        _ = Expect.Throws<ArgumentNullException>(() =>
            Drain(worker.GetCountForHistoryChapterCoroutine(null!, 0, null!, box))
        );
        _ = Expect.Throws<ArgumentNullException>(() =>
            Drain(worker.GetTargetForHistoryChapterCoroutine(null!, 0, chapter, null!))
        );
        _ = Expect.Throws<ArgumentNullException>(() =>
            Drain(worker.GetTargetForHistoryChapterCoroutine(null!, 0, null!, box))
        );
        _ = Expect.Throws<ArgumentNullException>(() =>
            Drain(worker.GetMaxForHistoryChapterCoroutine(null!, 0, chapter, null!))
        );
        _ = Expect.Throws<ArgumentNullException>(() =>
            Drain(worker.GetMaxForHistoryChapterCoroutine(null!, 0, null!, box))
        );
    }

    [Test]
    public static void WorkersHaveNoHistoryUpdateCoroutineAndDoNotUpdateMaxByDefault()
    {
        var worker = new DefaultWorker();

        Expect.IsNull(worker.HistoryUpdateCoroutine(null!, 0));
        Expect.IsFalse(worker.UpdatesMax);
    }
}
