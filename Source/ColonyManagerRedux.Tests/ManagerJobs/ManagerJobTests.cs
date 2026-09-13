// ManagerJobTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class ManagerJobTests
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

        Assert.ThatCollection(result).Has.Count(3);
        Assert.That(result[0]).Is.EqualTo("c");
        Assert.That(result[1]).Is.EqualTo("b");
        Assert.That(result[2]).Is.EqualTo("a");
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

        Assert.ThatCollection(result).Is.Empty();
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

        Assert.ThatCollection(result).Has.Count(1);
        Assert.That(result[0]).Is.EqualTo("only");
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

        Assert.That(result[0]).Is.EqualTo("close-low-yield");
        Assert.That(result[1]).Is.EqualTo("far-high-yield");
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

        Assert.That(descending[0]).Is.EqualTo("high");
        Assert.That(ascending[0]).Is.EqualTo("low");
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

        Assert.That(elapsed).Is.EqualTo(500);
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

        Assert.That(elapsed).Is.EqualTo(300);
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

        Assert.That(elapsed).Is.EqualTo(0);
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

        Assert.That(elapsed).Is.EqualTo(300);
    }
}
