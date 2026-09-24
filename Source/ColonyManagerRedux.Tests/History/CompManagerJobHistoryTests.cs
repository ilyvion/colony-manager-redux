// CompManagerJobHistoryTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class CompManagerJobHistoryTests
{
    [Test]
    public static void SkipsWhenPreviousUpdateIsStillQueuedFromAnEarlierTick() =>
        Expect.IsTrue(CompManagerJobHistory.ShouldSkipUpdate(false, 1, 100, 200));

    [Test]
    public static void SkipsWhenQueuedAndNoUpdateHasStartedYet() =>
        Expect.IsTrue(CompManagerJobHistory.ShouldSkipUpdate(false, 2, null, 200));

    [Test]
    public static void DoesNotSkipWhenNothingIsQueued() =>
        Expect.IsFalse(CompManagerJobHistory.ShouldSkipUpdate(false, 0, 100, 200));

    [Test]
    public static void DoesNotSkipWhenTheQueuedUpdateStartedThisTick() =>
        Expect.IsFalse(CompManagerJobHistory.ShouldSkipUpdate(false, 1, 200, 200));

    // Regression guard: the warning and skip happen once; afterwards updates proceed instead
    // of being skipped forever.
    [Test]
    public static void DoesNotSkipAgainOnceReported() =>
        Expect.IsFalse(CompManagerJobHistory.ShouldSkipUpdate(true, 1, 100, 200));
}
