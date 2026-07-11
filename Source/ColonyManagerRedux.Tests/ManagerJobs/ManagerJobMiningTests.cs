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
}
