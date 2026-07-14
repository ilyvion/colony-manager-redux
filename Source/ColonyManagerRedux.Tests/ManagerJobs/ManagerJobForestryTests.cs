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

    [Test]
    public static void DesignationWithNoThingIsRemovedForAreaCleanup() =>
        // A designation whose target has already lost its Thing (e.g. it was harvested by
        // some other means) is always cleaned up, regardless of area.
        Assert
            .That(
                ManagerJob_Forestry.ShouldRemoveForAreaCleanup(
                    hasThing: false,
                    inAllowedArea: false
                )
            )
            .Is.True();

    [Test]
    public static void DesignationInAllowedAreaIsNotRemoved() =>
        Assert
            .That(
                ManagerJob_Forestry.ShouldRemoveForAreaCleanup(hasThing: true, inAllowedArea: true)
            )
            .Is.False();

    [Test]
    public static void DesignationOutsideAllowedAreaIsRemoved() =>
        // Regression: the logging area may shrink or be reassigned after a tree was
        // designated, in which case the stale designation must be cleaned up even though its
        // target thing still exists.
        Assert
            .That(
                ManagerJob_Forestry.ShouldRemoveForAreaCleanup(hasThing: true, inAllowedArea: false)
            )
            .Is.True();
}
