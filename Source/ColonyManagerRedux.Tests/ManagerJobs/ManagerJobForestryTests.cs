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

    // ---- ShouldPlanClearCut ----

    private static bool ClearCut(
        bool hasPlant = true,
        bool alreadyDesignated = false,
        bool defAllowed = true,
        bool inGrowZone = false,
        bool hasPlantGrower = false
    ) =>
        ManagerJob_Forestry.ShouldPlanClearCut(
            hasPlant,
            () => alreadyDesignated,
            () => defAllowed,
            () => inGrowZone,
            () => hasPlantGrower
        );

    [Test]
    public static void AllowedUndesignatedPlantInTheOpenIsCut() => Expect.IsTrue(ClearCut());

    [Test]
    public static void NoPlantMeansNothingToCut() => Expect.IsFalse(ClearCut(hasPlant: false));

    [Test]
    public static void AlreadyDesignatedPlantIsNotCutAgain() =>
        Expect.IsFalse(ClearCut(alreadyDesignated: true));

    [Test]
    public static void PlantOutsideTheAllowedTreesIsLeftAlone() =>
        Expect.IsFalse(ClearCut(defAllowed: false));

    [Test]
    public static void PlantInAGrowingZoneIsLeftAlone() =>
        Expect.IsFalse(ClearCut(inGrowZone: true));

    [Test]
    public static void PlantInAPlantGrowerIsLeftAlone() =>
        Expect.IsFalse(ClearCut(hasPlantGrower: true));

    [Test]
    public static void ChecksAreLazyAndStopAtTheFirstRejection()
    {
        var calls = new List<string>();
        Func<bool> Spy(string name, bool result)
        {
            return () =>
            {
                calls.Add(name);
                return result;
            };
        }

        // No plant: none of the (more expensive) checks may run.
        _ = ManagerJob_Forestry.ShouldPlanClearCut(
            false,
            Spy("designated", false),
            Spy("allowed", true),
            Spy("zone", false),
            Spy("grower", false)
        );
        Expect.IsEmpty(calls);

        // Already designated: stops before the allowed-def check.
        _ = ManagerJob_Forestry.ShouldPlanClearCut(
            true,
            Spy("designated", true),
            Spy("allowed", true),
            Spy("zone", false),
            Spy("grower", false)
        );
        Expect.That(() => calls.SequenceEqual(["designated"]));

        // Everything passes: asked in ladder order.
        calls.Clear();
        _ = ManagerJob_Forestry.ShouldPlanClearCut(
            true,
            Spy("designated", false),
            Spy("allowed", true),
            Spy("zone", false),
            Spy("grower", false)
        );
        Expect.That(() => calls.SequenceEqual(["designated", "allowed", "zone", "grower"]));
    }
}
