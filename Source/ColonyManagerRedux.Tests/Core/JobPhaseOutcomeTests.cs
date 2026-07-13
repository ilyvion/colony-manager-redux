// JobPhaseOutcomeTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class JobPhaseOutcomeTests
{
    [Test]
    public static void ForReturnsReadyWhenGivenACoroutine()
    {
        Coroutine coroutine = [];
        var outcome = JobPhaseOutcome.For(coroutine);

        Assert.That(outcome is JobPhaseOutcome.Ready).Is.True();
    }

    [Test]
    public static void ForReturnsNotImplementedWhenGivenNull()
    {
        var outcome = JobPhaseOutcome.For(null);

        Assert.That(outcome is JobPhaseOutcome.NotImplemented).Is.True();
    }

    [Test]
    public static void ReadyExposesTheOriginalCoroutineUnchanged()
    {
        Coroutine coroutine = [];
        var outcome = JobPhaseOutcome.For(coroutine);

        // Guards against a caller accidentally wrapping/copying instead of forwarding the
        // exact coroutine it was given.
        Assert
            .That(outcome is JobPhaseOutcome.Ready { Value: var value } && value == coroutine)
            .Is.True();
    }
}
