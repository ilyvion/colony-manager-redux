// ManagerJobForestryTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class ManagerJobForestryTests
{
    private sealed class Sentinel;

    private static readonly Sentinel WoodLog = new();
    private static readonly Sentinel OtherHarvest = new();

    [Test]
    public static void ComputeSetAllowReturnsNullForNullHarvestedThingDef() =>
        // Regression guard for commit 04a87f5 ("fix: handle null harvestedThingDef in forestry
        // job processing"): a null harvestedThingDef must signal "nothing to sync", not be passed
        // through to ThresholdFilter.SetAllow.
        Assert
            .That(ManagerJob_Forestry.ComputeSetAllow(null, [WoodLog, OtherHarvest]))
            .Is.Null();

    [Test]
    public static void ComputeSetAllowIsFalseWhenNoAllowedTreeMatches() =>
        Assert.That(ManagerJob_Forestry.ComputeSetAllow(WoodLog, [OtherHarvest])).Is.EqualTo(false);

    [Test]
    public static void ComputeSetAllowIsTrueWhenAnAllowedTreeMatches() =>
        Assert
            .That(ManagerJob_Forestry.ComputeSetAllow(WoodLog, [OtherHarvest, WoodLog]))
            .Is.EqualTo(true);

    [Test]
    public static void ComputeSetAllowIsFalseForEmptyAllowedTrees() =>
        Assert.That(ManagerJob_Forestry.ComputeSetAllow(WoodLog, (Sentinel[])[])).Is.EqualTo(false);
}
