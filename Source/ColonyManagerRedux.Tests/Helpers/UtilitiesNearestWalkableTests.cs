// UtilitiesNearestWalkableTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class UtilitiesNearestWalkableTests
{
    private static readonly IntVec3 _origin = new(10, 0, 10);

    [Test]
    public static void WalkablePositionIsReturnedUnchanged() =>
        Expect.AreEqual(_origin, Utilities.FindNearestWalkableCell(_origin, _ => true));

    [Test]
    public static void BlockedPositionFindsAnAdjacentWalkableCell()
    {
        var result = Utilities.FindNearestWalkableCell(_origin, c => c != _origin);

        Expect.AreNotEqual(_origin, result);
        Expect.AreEqual(1, (result - _origin).LengthHorizontalSquared);
    }

    [Test]
    public static void ReturnsNearestWalkableCellWhenNeighboursAreBlocked()
    {
        var walkableCell = _origin + new IntVec3(3, 0, 0);

        Expect.AreEqual(
            walkableCell,
            Utilities.FindNearestWalkableCell(_origin, c => c == walkableCell)
        );
    }

    // Nothing walkable anywhere used to walk off the end of the radial pattern.
    [Test]
    public static void NothingWalkableTerminatesAndReturnsPosition() =>
        Expect.AreEqual(_origin, Utilities.FindNearestWalkableCell(_origin, _ => false));
}
