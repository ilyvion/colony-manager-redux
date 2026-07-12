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

    [Test]
    public static void ComputeReduceCountRemovesNothingWhenRemovalWouldDropBelowTarget() =>
        // "At least 8" threshold: removing even the first item would drop count from 10 to 7,
        // below target, so the loop must break before removing anything.
        Assert
            .That(
                Utilities_Plants.ComputeReduceCount(
                    startingCount: 10,
                    sortedYields: [3, 3, 3],
                    startingDesignationCount: 3,
                    countMeetsTarget: c => c >= 8,
                    shouldRemoveMoreDesignations: _ => false
                )
            )
            .Is.EqualTo(0);

    [Test]
    public static void ComputeReduceCountStopsAsSoonAsFurtherRemovalWouldDropBelowTarget() =>
        // "At least 8" threshold, starting count 15: removing the first two 3-yield items keeps
        // count at/above target (12, then 9); removing a third would drop it to 6, below target -
        // stop there, leaving the third item's designation untouched ("just above target").
        Assert
            .That(
                Utilities_Plants.ComputeReduceCount(
                    startingCount: 15,
                    sortedYields: [3, 3, 3],
                    startingDesignationCount: 3,
                    countMeetsTarget: c => c >= 8,
                    shouldRemoveMoreDesignations: _ => false
                )
            )
            .Is.EqualTo(2);

    [Test]
    public static void ComputeReduceCountRemovesAllWhenTargetStaysMetThroughout() =>
        Assert
            .That(
                Utilities_Plants.ComputeReduceCount(
                    startingCount: 10,
                    sortedYields: [3, 3, 3],
                    startingDesignationCount: 3,
                    countMeetsTarget: _ => true,
                    shouldRemoveMoreDesignations: _ => false
                )
            )
            .Is.EqualTo(3);

    [Test]
    public static void ComputeReduceCountHonorsShouldRemoveMoreDesignationsOverride()
    {
        // A non-"at least" threshold type (e.g. "not equal to") can keep reporting the target as
        // unmet forever; ShouldRemoveMoreDesignations is the independent escape hatch that must
        // still allow the loop to keep removing designations. This mirrors the equivalent Mining
        // bug fixed before af20ec1 ("Mining jobs using a threshold type other than 'at least' ...
        // would keep removing ... past target").
        var designationCountsSeen = new List<int>();
        Assert
            .That(
                Utilities_Plants.ComputeReduceCount(
                    startingCount: 10,
                    sortedYields: [3, 3, 3],
                    startingDesignationCount: 3,
                    countMeetsTarget: _ => false,
                    shouldRemoveMoreDesignations: c =>
                    {
                        designationCountsSeen.Add(c);
                        return c > 1;
                    }
                )
            )
            .Is.EqualTo(2);
        Assert.ThatCollection(designationCountsSeen).Has.Count(3);
    }

    [Test]
    public static void ComputeNumberToDesignateAddsNoneWhenAlreadyAtTarget() =>
        Assert
            .That(
                Utilities_Plants.ComputeNumberToDesignate(
                    startingCount: 10,
                    sortedYields: [3, 3, 3],
                    startingDesignationCount: 0,
                    countMeetsTarget: c => c >= 10,
                    canAddMoreDesignations: _ => true
                )
            )
            .Is.EqualTo(0);

    [Test]
    public static void ComputeNumberToDesignateStopsAsSoonAsTargetIsMet() =>
        Assert
            .That(
                Utilities_Plants.ComputeNumberToDesignate(
                    startingCount: 0,
                    sortedYields: [3, 3, 3],
                    startingDesignationCount: 0,
                    countMeetsTarget: c => c >= 5,
                    canAddMoreDesignations: _ => true
                )
            )
            .Is.EqualTo(2);

    [Test]
    public static void ComputeNumberToDesignateStopsWhenRunningOutOfCandidates() =>
        Assert
            .That(
                Utilities_Plants.ComputeNumberToDesignate(
                    startingCount: 0,
                    sortedYields: [3, 3, 3],
                    startingDesignationCount: 0,
                    countMeetsTarget: c => c >= 100,
                    canAddMoreDesignations: _ => true
                )
            )
            .Is.EqualTo(3);

    [Test]
    public static void ComputeNumberToDesignateStopsWhenCanAddMoreDesignationsBecomesFalse() =>
        // Exercises the loop's *use* of the CanAddMoreDesignations predicate (already covered
        // standalone by SettingsTests), not just the predicate itself.
        Assert
            .That(
                Utilities_Plants.ComputeNumberToDesignate(
                    startingCount: 0,
                    sortedYields: [3, 3, 3],
                    startingDesignationCount: 0,
                    countMeetsTarget: _ => false,
                    canAddMoreDesignations: c => c < 2
                )
            )
            .Is.EqualTo(2);
}
