// ManagerJobHuntingTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class ManagerJobHuntingTests
{
    private sealed class Sentinel;

    private static readonly Sentinel Meat = new();
    private static readonly Sentinel Leather = new();

    [Test]
    public static void SelectsMeatDefForMeatTargetResource() =>
        Assert
            .That(
                ReferenceEquals(
                    ManagerJob_Hunting.SelectResourceDef(
                        ManagerJob_Hunting.HuntingTargetResource.Meat,
                        Meat,
                        Leather
                    ),
                    Meat
                )
            )
            .Is.True();

    [Test]
    public static void SelectsLeatherDefForLeatherTargetResource() =>
        Assert
            .That(
                ReferenceEquals(
                    ManagerJob_Hunting.SelectResourceDef(
                        ManagerJob_Hunting.HuntingTargetResource.Leather,
                        Meat,
                        Leather
                    ),
                    Leather
                )
            )
            .Is.True();

    // Regression guard for CHANGELOG 0.5.4: a humanlike race without a leatherDef must resolve to
    // null (not throw or fall back to meatDef) when targeting leather.
    [Test]
    public static void SelectsNullWhenLeatherTargetResourceHasNoLeatherDef() =>
        Assert
            .That(
                ManagerJob_Hunting.SelectResourceDef(
                    ManagerJob_Hunting.HuntingTargetResource.Leather,
                    Meat,
                    null
                )
                    is null
            )
            .Is.True();

    // Regression guard for CHANGELOG 0.5.3: a race without organic flesh (no meatDef) must
    // resolve to null when targeting meat, rather than crashing with a NullReferenceException.
    [Test]
    public static void SelectsNullWhenMeatTargetResourceHasNoMeatDef() =>
        Assert
            .That(
                ManagerJob_Hunting.SelectResourceDef(
                    ManagerJob_Hunting.HuntingTargetResource.Meat,
                    null,
                    Leather
                )
                    is null
            )
            .Is.True();

    // The designation-priority comment at ManagerJob_Hunting.cs:688 says "value = meat /
    // (distance ^ 2)", but the sorter it actually passes to GetTargetsSorted is
    // "yield / distance" (single division, not squared) — see TEST-OPPORTUNITIES.md #7. This
    // pins the real (non-squared) ordering down as a regression guard, using values where the
    // two formulas disagree: yield/d gives {9, 25} (second wins), yield/d^2 gives {9, 6.25}
    // (first would win) — so an accidental "fix" to match the stale comment would flip this
    // ordering and be caught here.
    [Test]
    public static void DesignationPrioritySortsByYieldOverDistanceNotDistanceSquared()
    {
        var result = ManagerJob.SortByScoreDescending(
            origins: ["low-yield-close", "high-yield-far"],
            things: [9, 100],
            distances: [1f, 4f],
            sorter: (yield, distance) => yield / distance
        );

        Assert.That(result[0]).Is.EqualTo("high-yield-far");
        Assert.That(result[1]).Is.EqualTo("low-yield-close");
    }
}
