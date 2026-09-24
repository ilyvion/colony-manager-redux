// ManagerJobTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ManagerJobTests
{
    [Test]
    public static void SortsByScoreDescendingForSimpleIntSorter()
    {
        var result = ManagerJob.SortByScoreDescending(
            origins: ["a", "b", "c"],
            things: [1, 2, 3],
            distances: [0f, 0f, 0f],
            sorter: (thing, _) => thing
        );

        Expect.AreEqual(3, result.Count);
        Expect.AreEqual("c", result[0]);
        Expect.AreEqual("b", result[1]);
        Expect.AreEqual("a", result[2]);
    }

    [Test]
    public static void EmptyInputProducesEmptyOutput()
    {
        var result = ManagerJob.SortByScoreDescending<string, int, int>(
            origins: [],
            things: [],
            distances: [],
            sorter: (thing, _) => thing
        );

        Expect.IsEmpty(result);
    }

    [Test]
    public static void SingleItemInputIsReturnedUnchanged()
    {
        var result = ManagerJob.SortByScoreDescending(
            origins: ["only"],
            things: [42],
            distances: [10f],
            sorter: (thing, distance) => thing / distance
        );

        Expect.AreEqual(1, result.Count);
        Expect.AreEqual("only", result[0]);
    }

    [Test]
    public static void ScoreCombinesThingAndDistance()
    {
        // Mirrors the "-yield/distance" reduction-ordering pattern used by hunting/mining
        // (see TEST-OPPORTUNITIES.md #11): a far-away high-yield item can score lower than a
        // close low-yield one once distance is factored in.
        var result = ManagerJob.SortByScoreDescending(
            origins: ["far-high-yield", "close-low-yield"],
            things: [100, 10],
            distances: [50f, 1f],
            sorter: (yield, distance) => yield / distance
        );

        Expect.AreEqual("close-low-yield", result[0]);
        Expect.AreEqual("far-high-yield", result[1]);
    }

    [Test]
    public static void InvertedSignFlipsOrderingForReductionScoring()
    {
        // The negated-score pattern used to order "remove worst first" (see #11): flipping the
        // sign of an otherwise-identical sorter must reverse the resulting order.
        var descending = ManagerJob.SortByScoreDescending(
            origins: ["low", "high"],
            things: [1, 5],
            distances: [1f, 1f],
            sorter: (yield, distance) => yield / distance
        );
        var ascending = ManagerJob.SortByScoreDescending(
            origins: ["low", "high"],
            things: [1, 5],
            distances: [1f, 1f],
            sorter: (yield, distance) => -(yield / distance)
        );

        Expect.AreEqual("high", descending[0]);
        Expect.AreEqual("low", ascending[0]);
    }

    [Test]
    public static void UntouchedStateReportsElapsedSinceCreation()
    {
        var elapsed = ManagerJob.ComputeTicksSinceLastUpdate(
            lastActionTick: -1,
            jobCreatedTick: 1000,
            forceUpdateRequested: false,
            currentTick: 1500
        );

        Expect.AreEqual(500, elapsed);
    }

    [Test]
    public static void TouchedStateReportsElapsedSinceLastAction()
    {
        var elapsed = ManagerJob.ComputeTicksSinceLastUpdate(
            lastActionTick: 1200,
            jobCreatedTick: 1000,
            forceUpdateRequested: false,
            currentTick: 1500
        );

        Expect.AreEqual(300, elapsed);
    }

    [Test]
    public static void ForceUpdateStateReportsNoElapsedTimeForLongDormantJob()
    {
        // Regression guard: Untouch() (the "Force update job" action) used to leave
        // jobCreatedTick as its original, possibly years-old value, so a long-dormant job would
        // report years of elapsed time via the jobCreatedTick fallback below, instantly
        // tripping the "jobs not updating" alert. Untouch() now sets forceUpdateRequested
        // instead, which this simulates directly, without touching jobCreatedTick at all.
        var elapsed = ManagerJob.ComputeTicksSinceLastUpdate(
            lastActionTick: -1,
            jobCreatedTick: 1000,
            forceUpdateRequested: true,
            currentTick: 5_000_000
        );

        Expect.AreEqual(0, elapsed);
    }

    [Test]
    public static void TouchedStateIgnoresForceUpdateRequested()
    {
        // Touch() clears forceUpdateRequested, but even if it didn't, a non-negative
        // lastActionTick must always take priority over the force-update fallback.
        var elapsed = ManagerJob.ComputeTicksSinceLastUpdate(
            lastActionTick: 1200,
            jobCreatedTick: 1000,
            forceUpdateRequested: true,
            currentTick: 1500
        );

        Expect.AreEqual(300, elapsed);
    }
}
