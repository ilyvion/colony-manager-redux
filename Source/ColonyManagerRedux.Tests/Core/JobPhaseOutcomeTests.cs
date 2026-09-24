// JobPhaseOutcomeTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class JobPhaseOutcomeTests
{
    [Test]
    public static void ForReturnsReadyWhenGivenACoroutine()
    {
        Coroutine coroutine = [];
        var outcome = JobPhaseOutcome.For(coroutine);

        Expect.IsTrue(outcome is JobPhaseOutcome.Ready);
    }

    [Test]
    public static void ForReturnsNotImplementedWhenGivenNull()
    {
        var outcome = JobPhaseOutcome.For(null);

        Expect.IsTrue(outcome is JobPhaseOutcome.NotImplemented);
    }

    [Test]
    public static void ReadyExposesTheOriginalCoroutineUnchanged()
    {
        Coroutine coroutine = [];
        var outcome = JobPhaseOutcome.For(coroutine);

        // Guards against a caller accidentally wrapping/copying instead of forwarding the
        // exact coroutine it was given.
        Expect.IsTrue(outcome is JobPhaseOutcome.Ready { Value: var value } && value == coroutine);
    }
}
