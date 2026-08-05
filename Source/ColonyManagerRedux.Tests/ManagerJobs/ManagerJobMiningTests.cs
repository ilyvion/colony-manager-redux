// ManagerJobMiningTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class ManagerJobMiningTests
{
    [Test]
    public static void OriginIsARoofSupport() =>
        Assert.That(ManagerJob_Mining.IsARoofSupport_Basic(new IntVec3(0, 0, 0))).Is.True();

    [Test]
    public static void CellOnBothGridLinesIsARoofSupport() =>
        Assert.That(ManagerJob_Mining.IsARoofSupport_Basic(new IntVec3(5, 0, 5))).Is.True();

    [Test]
    public static void CellOffXGridLineIsNotARoofSupport() =>
        Assert.That(ManagerJob_Mining.IsARoofSupport_Basic(new IntVec3(4, 0, 5))).Is.False();

    [Test]
    public static void CellOffZGridLineIsNotARoofSupport() =>
        Assert.That(ManagerJob_Mining.IsARoofSupport_Basic(new IntVec3(5, 0, 4))).Is.False();

    [Test]
    public static void NegativeCoordinatesOnGridLinesAreARoofSupport() =>
        // C#'s % operator preserves the sign of the dividend, but for coordinates that are
        // exact multiples of the grid spacing (positive or negative), -5 % 5 == 0 still holds,
        // so the grid alignment check remains correct on the negative side of the origin.
        Assert.That(ManagerJob_Mining.IsARoofSupport_Basic(new IntVec3(-5, 0, 0))).Is.True();

    [Test]
    public static void NegativeCoordinatesOffGridLinesAreNotARoofSupport() =>
        Assert.That(ManagerJob_Mining.IsARoofSupport_Basic(new IntVec3(-4, 0, 0))).Is.False();

    private static ChunkProcessingKind Accumulate(
        params (bool hasButcherProducts, bool hasSmeltProducts)[] chunks
    ) => ManagerJob_Mining.AccumulateChunkProcessingKind(chunks);

    [Test]
    public static void NoChunksYieldsNeither() =>
        Assert.That(Accumulate()).Is.EqualTo(ChunkProcessingKind.Neither);

    [Test]
    public static void OnlyButcherProductsYieldsStonecutting() =>
        Assert.That(Accumulate((true, false))).Is.EqualTo(ChunkProcessingKind.Stonecutting);

    [Test]
    public static void OnlySmeltProductsYieldsSmelting() =>
        Assert.That(Accumulate((false, true))).Is.EqualTo(ChunkProcessingKind.Smelting);

    [Test]
    public static void ButcherThenSmeltAcrossChunksYieldsBoth() =>
        Assert.That(Accumulate((true, false), (false, true))).Is.EqualTo(ChunkProcessingKind.Both);

    [Test]
    public static void SingleChunkWithBothProductsYieldsBoth() =>
        Assert.That(Accumulate((true, true))).Is.EqualTo(ChunkProcessingKind.Both);

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

        Assert.That(result).Is.EqualTo(ChunkProcessingKind.Both);
        Assert.That(visitCount).Is.EqualTo(1);
    }

    [Test]
    public static void ClampScaledCountPassesThroughWhenBelowOriginal() =>
        Assert.That(ManagerJob_Mining.ClampScaledCount(3, 10)).Is.EqualTo(3);

    [Test]
    public static void ClampScaledCountClampsToOriginalWhenRoundedExceedsIt() =>
        // Regression: GenMath.RoundRandom is stochastic and can round a scaled count up past
        // the original count; the clamp must always cap the result at the original.
        Assert.That(ManagerJob_Mining.ClampScaledCount(11, 10)).Is.EqualTo(10);

    [Test]
    public static void ClampScaledCountAtExactlyOriginalIsUnchanged() =>
        Assert.That(ManagerJob_Mining.ClampScaledCount(10, 10)).Is.EqualTo(10);

    private static int SumScaled(float fraction, params (int count, bool counted)[] items) =>
        ManagerJob_Mining.SumScaledCounts(items, fraction);

    [Test]
    public static void SumScaledCountsWithZeroFractionIsZero() =>
        Assert.That(SumScaled(0f, (100, true))).Is.EqualTo(0);

    [Test]
    public static void SumScaledCountsWithFullFractionIsOriginalCount() =>
        Assert.That(SumScaled(1f, (100, true))).Is.EqualTo(100);

    [Test]
    public static void SumScaledCountsExcludesUncountedItems() =>
        Assert.That(SumScaled(1f, (100, true), (50, false))).Is.EqualTo(100);

    [Test]
    public static void SumScaledCountsSumsAcrossMultipleCountedItems() =>
        Assert.That(SumScaled(0.5f, (100, true), (50, true))).Is.EqualTo(75);

    [Test]
    public static void ChunkYieldWithZeroDropChanceIsZero() =>
        Assert
            .That(
                ManagerJob_Mining.CalculateMineralYield(
                    isChunk: true,
                    chunkCount: 10,
                    dropChance: 0f,
                    mineableYield: 0f,
                    mineYieldFactor: 0f,
                    counted: false
                )
            )
            .Is.EqualTo(0);

    [Test]
    public static void ChunkYieldWithFullDropChanceIsFullChunkCount() =>
        Assert
            .That(
                ManagerJob_Mining.CalculateMineralYield(
                    isChunk: true,
                    chunkCount: 10,
                    dropChance: 1f,
                    mineableYield: 0f,
                    mineYieldFactor: 0f,
                    counted: false
                )
            )
            .Is.EqualTo(10);

    [Test]
    public static void ChunkYieldTruncatesFractionalDropChanceTowardZero() =>
        // 3 * 0.5 = 1.5, and the (int) cast truncates rather than rounds.
        Assert
            .That(
                ManagerJob_Mining.CalculateMineralYield(
                    isChunk: true,
                    chunkCount: 3,
                    dropChance: 0.5f,
                    mineableYield: 0f,
                    mineYieldFactor: 0f,
                    counted: false
                )
            )
            .Is.EqualTo(1);

    [Test]
    public static void MetalYieldIsZeroWhenNotCounted() =>
        Assert
            .That(
                ManagerJob_Mining.CalculateMineralYield(
                    isChunk: false,
                    chunkCount: 0,
                    dropChance: 1f,
                    mineableYield: 100f,
                    mineYieldFactor: 2f,
                    counted: false
                )
            )
            .Is.EqualTo(0);

    [Test]
    public static void MetalYieldScalesByYieldFactorAndDropChanceWhenCounted() =>
        Assert
            .That(
                ManagerJob_Mining.CalculateMineralYield(
                    isChunk: false,
                    chunkCount: 0,
                    dropChance: 0.5f,
                    mineableYield: 10f,
                    mineYieldFactor: 2f,
                    counted: true
                )
            )
            .Is.EqualTo(10);

    private static readonly DesignationDef Mine = new() { defName = "Mine" };
    private static readonly DesignationDef Deconstruct = new() { defName = "Deconstruct" };
    private static readonly DesignationDef Unrelated = new() { defName = "Unrelated" };

    [Test]
    public static void NoDesignationIsNotDesignatedForRemoval() =>
        Assert.That(ManagerJob_Mining.IsDesignatedForRemoval(null, Mine, Deconstruct)).Is.False();

    [Test]
    public static void UnrelatedDesignationIsNotDesignatedForRemoval() =>
        Assert
            .That(ManagerJob_Mining.IsDesignatedForRemoval(Unrelated, Mine, Deconstruct))
            .Is.False();

    [Test]
    public static void MineDesignationIsDesignatedForRemoval() =>
        Assert.That(ManagerJob_Mining.IsDesignatedForRemoval(Mine, Mine, Deconstruct)).Is.True();

    [Test]
    public static void DeconstructDesignationIsDesignatedForRemoval() =>
        Assert
            .That(ManagerJob_Mining.IsDesignatedForRemoval(Deconstruct, Mine, Deconstruct))
            .Is.True();

    [Test]
    public static void LoadingVarsShouldResetLockedToMapCaches() =>
        // Regression: _mineralsLockedToMap/_buildingsLockedToMap are loaded straight into
        // their backing fields during LoadingVars, bypassing the property setters that
        // invalidate AllMinerals/AllDeconstructibleBuildings. Without a reset here, imported
        // jobs kept the constructor-time cache and PostImport silently dropped allowed
        // minerals/buildings that weren't spawned on the map at construction time.
        Assert
            .That(ManagerJob_Mining.ShouldResetLockedToMapCaches(LoadSaveMode.LoadingVars))
            .Is.True();

    [Test]
    public static void SavingDoesNotResetLockedToMapCaches() =>
        Assert.That(ManagerJob_Mining.ShouldResetLockedToMapCaches(LoadSaveMode.Saving)).Is.False();

    [Test]
    public static void PostLoadInitDoesNotResetLockedToMapCaches() =>
        Assert
            .That(ManagerJob_Mining.ShouldResetLockedToMapCaches(LoadSaveMode.PostLoadInit))
            .Is.False();

    [Test]
    public static void InactiveDoesNotResetLockedToMapCaches() =>
        Assert
            .That(ManagerJob_Mining.ShouldResetLockedToMapCaches(LoadSaveMode.Inactive))
            .Is.False();
}
