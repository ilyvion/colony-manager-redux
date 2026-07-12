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
}
