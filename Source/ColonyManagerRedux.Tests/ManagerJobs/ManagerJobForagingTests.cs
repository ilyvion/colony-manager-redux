// ManagerJobForagingTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ManagerJobForagingTests
{
    [Test]
    public static void DesignationWithNoThingIsRemovedForAreaCleanup() =>
        // A designation whose target has already lost its Thing (e.g. it was harvested by
        // some other means) is always cleaned up, regardless of area.
        Expect.IsTrue(
            ManagerJob_Foraging.ShouldRemoveForAreaCleanup(hasThing: false, inAllowedArea: false)
        );

    [Test]
    public static void DesignationInAllowedAreaIsNotRemoved() =>
        Expect.IsFalse(
            ManagerJob_Foraging.ShouldRemoveForAreaCleanup(hasThing: true, inAllowedArea: true)
        );

    [Test]
    public static void DesignationOutsideAllowedAreaIsRemoved() =>
        // Regression: the foraging area may shrink or be reassigned after a plant was
        // designated, in which case the stale designation must be cleaned up even though its
        // target thing still exists.
        Expect.IsTrue(
            ManagerJob_Foraging.ShouldRemoveForAreaCleanup(hasThing: true, inAllowedArea: false)
        );
}
