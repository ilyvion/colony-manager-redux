// UtilitiesResourceSyncTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class UtilitiesResourceSyncTests
{
    private sealed class Animal(string name, string resource)
    {
        public string Name { get; } = name;
        public string Resource { get; } = resource;
    }

    // Regression guard for CHANGELOG 0.2.0: removing one allowed item that shares a resource
    // with another allowed item must NOT clear the resource's "allow" flag.
    [Test]
    public static void ResourceStaysAllowedWhenAnotherAllowedItemStillProducesIt()
    {
        var remaining = new Animal("Boomalope", "Leather");
        var setAllow = Utilities_ResourceSync.ShouldResourceStayAllowed(
            [remaining],
            a => a.Resource,
            "Leather"
        );

        Assert.That(setAllow).Is.True();
    }

    [Test]
    public static void ResourceIsDisallowedWhenNoRemainingItemProducesIt()
    {
        var other = new Animal("Muffalo", "Wool");
        var setAllow = Utilities_ResourceSync.ShouldResourceStayAllowed(
            [other],
            a => a.Resource,
            "Leather"
        );

        Assert.That(setAllow).Is.False();
    }

    [Test]
    public static void ResourceStaysAllowedForEmptyAllowedSetOnlyIfVacuouslyTrueIsExpected() =>
        // Documents current (LINQ .Any) behavior: an empty allowed set can never satisfy .Any,
        // so the resource is always disallowed once the last producer is removed.
        Assert
            .That(
                Utilities_ResourceSync.ShouldResourceStayAllowed(
                    [],
                    (Animal a) => a.Resource,
                    "Leather"
                )
            )
            .Is.False();
}
