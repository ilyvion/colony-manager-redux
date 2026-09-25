// ManagerJobMiningSafetyTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;
using static ColonyManagerRedux.Managers.ManagerJob_Mining;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ManagerJobMiningSafetyTests
{
    // ---- DecideDrill / ShouldEnableDrills (deep drill planning) ----

    [Test]
    public static void CountedResourceMakesTheDrillFlickable() =>
        Expect.AreEqual(DrillPlan.Flick, DecideDrill(true, () => false, false));

    [Test]
    public static void UncountedResourceWithCountedChunkProductIsStillFlickable() =>
        // Drills that produce chunks are managed when the chunk's products are counted.
        Expect.AreEqual(DrillPlan.Flick, DecideDrill(false, () => true, false));

    [Test]
    public static void UncountedResourceAndChunkProductsAreSkipped() =>
        Expect.AreEqual(DrillPlan.SkipNotCounted, DecideDrill(false, () => false, false));

    [Test]
    public static void ForbiddenDrillIsSkipped() =>
        Expect.AreEqual(DrillPlan.SkipForbidden, DecideDrill(true, () => false, true));

    [Test]
    public static void UncountedTakesPrecedenceOverForbidden() =>
        Expect.AreEqual(DrillPlan.SkipNotCounted, DecideDrill(false, () => false, true));

    [Test]
    public static void ChunkProductsAreOnlyLookedUpWhenTheResourceIsNotCounted()
    {
        var looked = false;

        _ = DecideDrill(
            true,
            () =>
            {
                looked = true;
                return true;
            },
            false
        );

        Expect.IsFalse(looked);
    }

    [Test]
    public static void MissingCountAlwaysPlansDrillsOff() =>
        Expect.IsFalse(ShouldEnableDrills(false, Trigger_Threshold.Directive.Increase));

    [Test]
    public static void OnlyTheIncreaseDirectiveEnablesDrills()
    {
        Expect.IsTrue(ShouldEnableDrills(true, Trigger_Threshold.Directive.Increase));
        Expect.IsFalse(ShouldEnableDrills(true, Trigger_Threshold.Directive.Hold));
        Expect.IsFalse(ShouldEnableDrills(true, Trigger_Threshold.Directive.Decrease));
    }

    // ---- IsAllowedToMineRoof ----

    [TestCase(true, true, ExpectedResult = true)]
    [TestCase(true, false, ExpectedResult = true)]
    [TestCase(false, true, ExpectedResult = false)]
    [TestCase(false, false, ExpectedResult = true)]
    public static bool ThickRoofsNeedPermissionButThinRoofsDoNot(
        bool mineThickRoofs,
        bool roofIsThick
    ) => IsAllowedToMineRoof(mineThickRoofs, roofIsThick);

    [Test]
    public static void NoRoofIsAlwaysAllowed()
    {
        Expect.IsTrue(IsAllowedToMineRoof(false, null));
        Expect.IsTrue(IsAllowedToMineRoof(true, null));
    }

    // ---- WouldCollapse (roof support safety) ----

    private static readonly IntVec3 Position = new(10, 0, 10);
    private static readonly IntVec3 Support = new(11, 0, 10);
    private static readonly IntVec3 Pillar = new(9, 0, 10);
    private static readonly IntVec3 Empty = new(10, 0, 11);

    private static IEnumerable<IntVec3> Radius() => [Position, Support, Pillar, Empty];

    [Test]
    public static void UnroofedCellNeverCollapses() =>
        Expect.IsFalse(WouldCollapse(false, Radius(), Support, _ => false));

    [Test]
    public static void RoofedCellWithNoOtherHolderCollapses() =>
        Expect.IsTrue(WouldCollapse(true, Radius(), Support, cell => cell == Support));

    [Test]
    public static void AnotherHolderInRangeKeepsTheRoofUp() =>
        Expect.IsFalse(WouldCollapse(true, Radius(), Support, cell => cell == Pillar));

    [Test]
    public static void TheSupportBeingRemovedNeverCountsAsAHolder()
    {
        var asked = new List<IntVec3>();

        _ = WouldCollapse(
            true,
            Radius(),
            Support,
            cell =>
            {
                asked.Add(cell);
                return false;
            }
        );

        Expect.IsFalse(asked.Contains(Support));
        Expect.AreEqual(3, asked.Count);
    }

    [Test]
    public static void AskingStopsAtTheFirstHolder()
    {
        var asked = 0;

        _ = WouldCollapse(
            true,
            Radius(),
            Support,
            _ =>
            {
                asked++;
                return true;
            }
        );

        Expect.AreEqual(1, asked);
    }

    [Test]
    public static void RoofedCellWithNoNeighboursCollapses() =>
        Expect.IsTrue(WouldCollapse(true, [], Support, _ => true));
}
