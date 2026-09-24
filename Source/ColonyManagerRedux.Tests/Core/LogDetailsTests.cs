// LogDetailsTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class LogDetailsTests
{
    private static LocalTargetInfo Cell(int x) => new(new IntVec3(x, 0, 0));

    [Test]
    public static void NextTargetIndexCyclesThroughAllTargets()
    {
        var details = new LogDetails("text", Cell(1), Cell(2), Cell(3));

        Expect.AreEqual(0, details.NextTargetIndex);
        Expect.AreEqual(1, details.NextTargetIndex);
        Expect.AreEqual(2, details.NextTargetIndex);
        Expect.AreEqual(0, details.NextTargetIndex);
    }

    [Test]
    public static void SingleTargetAlwaysReturnsZero()
    {
        var details = new LogDetails("text", Cell(1));

        Expect.AreEqual(0, details.NextTargetIndex);
        Expect.AreEqual(0, details.NextTargetIndex);
    }

    // Pins the current behavior: with no targets the index is 0, which is out of range for the
    // empty list, so callers must check Targets.Count first.
    [Test]
    public static void NoTargetsReturnsZero()
    {
        var details = new LogDetails("text");

        Expect.AreEqual(0, details.Targets.Count);
        Expect.AreEqual(0, details.NextTargetIndex);
    }

    [Test]
    public static void InvalidTargetsAreFilteredOut()
    {
        var details = new LogDetails("text", LocalTargetInfo.Invalid, Cell(1));

        Expect.AreEqual(1, details.Targets.Count);
    }

    [Test]
    public static void ParamsAndEnumerableOverloadsAgree()
    {
        var fromParams = new LogDetails("text", Cell(1), LocalTargetInfo.Invalid, Cell(2));
        var fromEnumerable = new LogDetails(
            "text",
            (IEnumerable<LocalTargetInfo>)[Cell(1), LocalTargetInfo.Invalid, Cell(2)]
        );

        Expect.That(() => fromParams.Targets.SequenceEqual(fromEnumerable.Targets));
    }
}
