// ManagerJobForagingTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class ManagerJobForagingTests
{
    [Test]
    public static void DesignationWithNoThingIsRemovedForAreaCleanup() =>
        // A designation whose target has already lost its Thing (e.g. it was harvested by
        // some other means) is always cleaned up, regardless of area.
        Assert
            .That(
                ManagerJob_Foraging.ShouldRemoveForAreaCleanup(
                    hasThing: false,
                    inAllowedArea: false
                )
            )
            .Is.True();

    [Test]
    public static void DesignationInAllowedAreaIsNotRemoved() =>
        Assert
            .That(
                ManagerJob_Foraging.ShouldRemoveForAreaCleanup(hasThing: true, inAllowedArea: true)
            )
            .Is.False();

    [Test]
    public static void DesignationOutsideAllowedAreaIsRemoved() =>
        // Regression: the foraging area may shrink or be reassigned after a plant was
        // designated, in which case the stale designation must be cleaned up even though its
        // target thing still exists.
        Assert
            .That(
                ManagerJob_Foraging.ShouldRemoveForAreaCleanup(hasThing: true, inAllowedArea: false)
            )
            .Is.True();
}
