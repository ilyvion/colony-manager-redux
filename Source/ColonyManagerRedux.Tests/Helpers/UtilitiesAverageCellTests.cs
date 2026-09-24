// UtilitiesAverageCellTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class UtilitiesAverageCellTests
{
    [Test]
    public static void AveragingSingleCellReturnsThatCell()
    {
        var result = Utilities.AverageCell([new IntVec3(3, 0, 7)]);

        Expect.AreEqual(3, result.x);
        Expect.AreEqual(7, result.z);
    }

    // Integer division truncates toward zero rather than rounding, so an uneven sum doesn't
    // round up to the nearest cell.
    [Test]
    public static void AveragingCellsThatDoNotDivideEvenlyTruncates()
    {
        IntVec3[] cells = [new IntVec3(0, 0, 0), new IntVec3(1, 0, 0), new IntVec3(1, 0, 0)];

        var result = Utilities.AverageCell(cells);

        // sum.x == 2, count == 3, 2 / 3 == 0 (truncated), not 1 (rounded).
        Expect.AreEqual(0, result.x);
    }

    [Test]
    public static void AveragingMultipleCellsSumsPerAxisIndependently()
    {
        IntVec3[] cells = [new IntVec3(0, 0, 10), new IntVec3(10, 0, 0)];

        var result = Utilities.AverageCell(cells);

        Expect.AreEqual(5, result.x);
        Expect.AreEqual(5, result.z);
    }
}
