// ManagerJobMiningTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ManagerJobMiningTests
{
    [Test]
    public static void OriginIsARoofSupport() =>
        Expect.IsTrue(ManagerJob_Mining.IsARoofSupport_Basic(new IntVec3(0, 0, 0)));

    [Test]
    public static void CellOnBothGridLinesIsARoofSupport() =>
        Expect.IsTrue(ManagerJob_Mining.IsARoofSupport_Basic(new IntVec3(5, 0, 5)));

    [Test]
    public static void CellOffXGridLineIsNotARoofSupport() =>
        Expect.IsFalse(ManagerJob_Mining.IsARoofSupport_Basic(new IntVec3(4, 0, 5)));

    [Test]
    public static void CellOffZGridLineIsNotARoofSupport() =>
        Expect.IsFalse(ManagerJob_Mining.IsARoofSupport_Basic(new IntVec3(5, 0, 4)));

    [Test]
    public static void NegativeCoordinatesOnGridLinesAreARoofSupport() =>
        // C#'s % operator preserves the sign of the dividend, but for coordinates that are
        // exact multiples of the grid spacing (positive or negative), -5 % 5 == 0 still holds,
        // so the grid alignment check remains correct on the negative side of the origin.
        Expect.IsTrue(ManagerJob_Mining.IsARoofSupport_Basic(new IntVec3(-5, 0, 0)));

    [Test]
    public static void NegativeCoordinatesOffGridLinesAreNotARoofSupport() =>
        Expect.IsFalse(ManagerJob_Mining.IsARoofSupport_Basic(new IntVec3(-4, 0, 0)));

    private static ChunkProcessingKind Accumulate(
        params (bool hasButcherProducts, bool hasSmeltProducts)[] chunks
    ) => ManagerJob_Mining.AccumulateChunkProcessingKind(chunks);

    [Test]
    public static void NoChunksYieldsNeither() =>
        Expect.AreEqual(ChunkProcessingKind.Neither, Accumulate());

    [Test]
    public static void OnlyButcherProductsYieldsStonecutting() =>
        Expect.AreEqual(ChunkProcessingKind.Stonecutting, Accumulate((true, false)));

    [Test]
    public static void OnlySmeltProductsYieldsSmelting() =>
        Expect.AreEqual(ChunkProcessingKind.Smelting, Accumulate((false, true)));

    [Test]
    public static void ButcherThenSmeltAcrossChunksYieldsBoth() =>
        Expect.AreEqual(ChunkProcessingKind.Both, Accumulate((true, false), (false, true)));

    [Test]
    public static void SingleChunkWithBothProductsYieldsBoth() =>
        Expect.AreEqual(ChunkProcessingKind.Both, Accumulate((true, true)));

    [Test]
    public static void ChunksAfterBothIsReachedAreNotVisited()
    {
        var visitCount = 0;

        IEnumerable<(bool, bool)> Chunks()
        {
            visitCount++;
            yield return (true, true);
            visitCount++;
            yield return (true, false);
        }

        var result = ManagerJob_Mining.AccumulateChunkProcessingKind(Chunks());

        Expect.AreEqual(ChunkProcessingKind.Both, result);
        Expect.AreEqual(1, visitCount);
    }

    [Test]
    public static void ClampScaledCountPassesThroughWhenBelowOriginal() =>
        Expect.AreEqual(3, ManagerJob_Mining.ClampScaledCount(3, 10));

    [Test]
    public static void ClampScaledCountClampsToOriginalWhenRoundedExceedsIt() =>
        // Regression: GenMath.RoundRandom is stochastic and can round a scaled count up past
        // the original count; the clamp must always cap the result at the original.
        Expect.AreEqual(10, ManagerJob_Mining.ClampScaledCount(11, 10));

    [Test]
    public static void ClampScaledCountAtExactlyOriginalIsUnchanged() =>
        Expect.AreEqual(10, ManagerJob_Mining.ClampScaledCount(10, 10));

    private static int SumScaled(float fraction, params (int count, bool counted)[] items) =>
        ManagerJob_Mining.SumScaledCounts(items, fraction);

    [Test]
    public static void SumScaledCountsWithZeroFractionIsZero() =>
        Expect.AreEqual(0, SumScaled(0f, (100, true)));

    [Test]
    public static void SumScaledCountsWithFullFractionIsOriginalCount() =>
        Expect.AreEqual(100, SumScaled(1f, (100, true)));

    [Test]
    public static void SumScaledCountsExcludesUncountedItems() =>
        Expect.AreEqual(100, SumScaled(1f, (100, true), (50, false)));

    [Test]
    public static void SumScaledCountsSumsAcrossMultipleCountedItems() =>
        Expect.AreEqual(75, SumScaled(0.5f, (100, true), (50, true)));

    [Test]
    public static void ChunkYieldWithZeroDropChanceIsZero() =>
        Expect.AreEqual(
            0,
            ManagerJob_Mining.CalculateMineralYield(
                isChunk: true,
                chunkCount: 10,
                dropChance: 0f,
                mineableYield: 0f,
                mineYieldFactor: 0f,
                counted: false
            )
        );

    [Test]
    public static void ChunkYieldWithFullDropChanceIsFullChunkCount() =>
        Expect.AreEqual(
            10,
            ManagerJob_Mining.CalculateMineralYield(
                isChunk: true,
                chunkCount: 10,
                dropChance: 1f,
                mineableYield: 0f,
                mineYieldFactor: 0f,
                counted: false
            )
        );

    [Test]
    public static void ChunkYieldTruncatesFractionalDropChanceTowardZero() =>
        // 3 * 0.5 = 1.5, and the (int) cast truncates rather than rounds.
        Expect.AreEqual(
            1,
            ManagerJob_Mining.CalculateMineralYield(
                isChunk: true,
                chunkCount: 3,
                dropChance: 0.5f,
                mineableYield: 0f,
                mineYieldFactor: 0f,
                counted: false
            )
        );

    [Test]
    public static void MetalYieldIsZeroWhenNotCounted() =>
        Expect.AreEqual(
            0,
            ManagerJob_Mining.CalculateMineralYield(
                isChunk: false,
                chunkCount: 0,
                dropChance: 1f,
                mineableYield: 100f,
                mineYieldFactor: 2f,
                counted: false
            )
        );

    [Test]
    public static void MetalYieldScalesByYieldFactorAndDropChanceWhenCounted() =>
        Expect.AreEqual(
            10,
            ManagerJob_Mining.CalculateMineralYield(
                isChunk: false,
                chunkCount: 0,
                dropChance: 0.5f,
                mineableYield: 10f,
                mineYieldFactor: 2f,
                counted: true
            )
        );

    private static readonly DesignationDef Mine = new() { defName = "Mine" };
    private static readonly DesignationDef Deconstruct = new() { defName = "Deconstruct" };
    private static readonly DesignationDef Unrelated = new() { defName = "Unrelated" };

    [Test]
    public static void NoDesignationIsNotDesignatedForRemoval() =>
        Expect.IsFalse(ManagerJob_Mining.IsDesignatedForRemoval(null, Mine, Deconstruct));

    [Test]
    public static void UnrelatedDesignationIsNotDesignatedForRemoval() =>
        Expect.IsFalse(ManagerJob_Mining.IsDesignatedForRemoval(Unrelated, Mine, Deconstruct));

    [Test]
    public static void MineDesignationIsDesignatedForRemoval() =>
        Expect.IsTrue(ManagerJob_Mining.IsDesignatedForRemoval(Mine, Mine, Deconstruct));

    [Test]
    public static void DeconstructDesignationIsDesignatedForRemoval() =>
        Expect.IsTrue(ManagerJob_Mining.IsDesignatedForRemoval(Deconstruct, Mine, Deconstruct));

    [Test]
    public static void LoadingVarsShouldResetLockedToMapCaches() =>
        // Regression: _mineralsLockedToMap/_buildingsLockedToMap are loaded straight into
        // their backing fields during LoadingVars, bypassing the property setters that
        // invalidate AllMinerals/AllDeconstructibleBuildings. Without a reset here, imported
        // jobs kept the constructor-time cache and PostImport silently dropped allowed
        // minerals/buildings that weren't spawned on the map at construction time.
        Expect.IsTrue(ManagerJob_Mining.ShouldResetLockedToMapCaches(LoadSaveMode.LoadingVars));

    [Test]
    public static void SavingDoesNotResetLockedToMapCaches() =>
        Expect.IsFalse(ManagerJob_Mining.ShouldResetLockedToMapCaches(LoadSaveMode.Saving));

    [Test]
    public static void PostLoadInitDoesNotResetLockedToMapCaches() =>
        Expect.IsFalse(ManagerJob_Mining.ShouldResetLockedToMapCaches(LoadSaveMode.PostLoadInit));

    [Test]
    public static void InactiveDoesNotResetLockedToMapCaches() =>
        Expect.IsFalse(ManagerJob_Mining.ShouldResetLockedToMapCaches(LoadSaveMode.Inactive));
}
