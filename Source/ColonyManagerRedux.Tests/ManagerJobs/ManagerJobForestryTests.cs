// ManagerJobForestryTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ManagerJobForestryTests
{
    private sealed class Sentinel;

    private static readonly Sentinel WoodLog = new();
    private static readonly Sentinel OtherHarvest = new();

    [Test]
    public static void ComputeSetAllowReturnsNullForNullHarvestedThingDef() =>
        // Regression guard for commit 04a87f5 ("fix: handle null harvestedThingDef in forestry
        // job processing"): a null harvestedThingDef must signal "nothing to sync", not be passed
        // through to ThresholdFilter.SetAllow.
        Expect.IsTrue(ManagerJob_Forestry.ComputeSetAllow(null, [WoodLog, OtherHarvest]) == null);

    [Test]
    public static void ComputeSetAllowIsFalseWhenNoAllowedTreeMatches() =>
        Expect.AreEqual(false, ManagerJob_Forestry.ComputeSetAllow(WoodLog, [OtherHarvest]));

    [Test]
    public static void ComputeSetAllowIsTrueWhenAnAllowedTreeMatches() =>
        Expect.AreEqual(
            true,
            ManagerJob_Forestry.ComputeSetAllow(WoodLog, [OtherHarvest, WoodLog])
        );

    [Test]
    public static void ComputeSetAllowIsFalseForEmptyAllowedTrees() =>
        Expect.AreEqual(false, ManagerJob_Forestry.ComputeSetAllow(WoodLog, (Sentinel[])[]));

    [Test]
    public static void DesignationWithNoThingIsRemovedForAreaCleanup() =>
        // A designation whose target has already lost its Thing (e.g. it was harvested by
        // some other means) is always cleaned up, regardless of area.
        Expect.IsTrue(
            ManagerJob_Forestry.ShouldRemoveForAreaCleanup(hasThing: false, inAllowedArea: false)
        );

    [Test]
    public static void DesignationInAllowedAreaIsNotRemoved() =>
        Expect.IsFalse(
            ManagerJob_Forestry.ShouldRemoveForAreaCleanup(hasThing: true, inAllowedArea: true)
        );

    [Test]
    public static void DesignationOutsideAllowedAreaIsRemoved() =>
        // Regression: the logging area may shrink or be reassigned after a tree was
        // designated, in which case the stale designation must be cleaned up even though its
        // target thing still exists.
        Expect.IsTrue(
            ManagerJob_Forestry.ShouldRemoveForAreaCleanup(hasThing: true, inAllowedArea: false)
        );
}
