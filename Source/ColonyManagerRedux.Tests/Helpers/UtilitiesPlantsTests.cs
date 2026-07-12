// UtilitiesPlantsTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class UtilitiesPlantsTests
{
    private sealed class Sentinel;

    private static readonly Sentinel WoodLog = new();
    private static readonly Sentinel OtherHarvest = new();

    [Test]
    public static void ClearAreaAlwaysValidRegardlessOfOtherFields()
    {
        // Regression guard for commit ad9405e ("fix: logic error in
        // Utilities_Plants.GetForestryPlants"): an operator-precedence bug meant clearArea
        // didn't unconditionally short-circuit the rest of the check. With clearArea true,
        // every field combination below (including a zero yield and a null harvested def) must
        // still be considered valid.
        Assert
            .That(
                Utilities_Plants.IsValidForestryPlant(
                    clearArea: true,
                    harvestTag: "Something else",
                    harvestedThingDef: OtherHarvest,
                    woodLogDef: WoodLog,
                    harvestYield: 0f
                )
            )
            .Is.True();

        Assert
            .That(
                Utilities_Plants.IsValidForestryPlant(
                    clearArea: true,
                    harvestTag: null,
                    harvestedThingDef: null,
                    woodLogDef: WoodLog,
                    harvestYield: -1f
                )
            )
            .Is.True();
    }

    // Ties to commit 04a87f5 ("fix: handle null harvestedThingDef in forestry job processing"):
    // even if harvestTag == "Wood", a null harvestedThingDef must exclude the plant.
    [Test]
    public static void NonClearAreaExcludesNullHarvestedThingDef() =>
        Assert
            .That(
                Utilities_Plants.IsValidForestryPlant(
                    clearArea: false,
                    harvestTag: "Wood",
                    harvestedThingDef: null,
                    woodLogDef: WoodLog,
                    harvestYield: 1f
                )
            )
            .Is.False();

    [Test]
    public static void NonClearAreaExcludesZeroYield() =>
        Assert
            .That(
                Utilities_Plants.IsValidForestryPlant(
                    clearArea: false,
                    harvestTag: "Wood",
                    harvestedThingDef: OtherHarvest,
                    woodLogDef: WoodLog,
                    harvestYield: 0f
                )
            )
            .Is.False();

    [Test]
    public static void NonClearAreaIncludesMatchingHarvestedThingDefEvenWithDifferentTag() =>
        Assert
            .That(
                Utilities_Plants.IsValidForestryPlant(
                    clearArea: false,
                    harvestTag: "Something else",
                    harvestedThingDef: WoodLog,
                    woodLogDef: WoodLog,
                    harvestYield: 1f
                )
            )
            .Is.True();

    [Test]
    public static void NonClearAreaExcludesNonWoodTagWithNonMatchingHarvestedThingDef() =>
        Assert
            .That(
                Utilities_Plants.IsValidForestryPlant(
                    clearArea: false,
                    harvestTag: "Something else",
                    harvestedThingDef: OtherHarvest,
                    woodLogDef: WoodLog,
                    harvestYield: 1f
                )
            )
            .Is.False();
}
