// TriggerPawnKindTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class TriggerPawnKindTests
{
    [Test]
    public static void ComputeRemainingCountsSubtractsCulledFromTame() =>
        Assert
            .ThatCollection(Trigger_PawnKind.ComputeRemainingCounts([5, 5, 5, 5], [1, 2, 3, 4]))
            .Does.Contain(4);

    [Test]
    public static void ComputeRemainingCountsMatchesElementwise()
    {
        var result = Trigger_PawnKind.ComputeRemainingCounts([5, 5, 5, 5], [1, 2, 3, 4]);

        Assert.ThatCollection(result).Has.Count(4);
        Assert.That(result[0]).Is.EqualTo(4);
        Assert.That(result[1]).Is.EqualTo(3);
        Assert.That(result[2]).Is.EqualTo(2);
        Assert.That(result[3]).Is.EqualTo(1);
    }

    [Test]
    public static void ComputeRemainingCountsCanGoNegativeWhenCulledExceedsTame() =>
        // No clamping in the original code - pin down that this is intentional (used for progress
        // bars, which are expected to tolerate a temporary over-culled state).
        Assert.That(Trigger_PawnKind.ComputeRemainingCounts([2], [5])[0]).Is.EqualTo(-3);

    [Test]
    public static void AllTargetsMetTrueForEmptyArrays() =>
        Assert.That(Trigger_PawnKind.AllTargetsMet([], [])).Is.True();

    [Test]
    public static void AllTargetsMetTrueWhenEveryTargetMatches() =>
        Assert.That(Trigger_PawnKind.AllTargetsMet([5, 5, 5, 5], [5, 5, 5, 5])).Is.True();

    [Test]
    public static void AllTargetsMetFalseOnSingleMismatch() =>
        Assert.That(Trigger_PawnKind.AllTargetsMet([5, 5, 5, 5], [5, 5, 4, 5])).Is.False();

    [Test]
    public static void AllTargetsMetFalseWhenAllMismatched() =>
        Assert.That(Trigger_PawnKind.AllTargetsMet([5, 5, 5, 5], [1, 2, 3, 4])).Is.False();
}
