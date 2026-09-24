// HistoryUpdateMismatchTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class HistoryUpdateMismatchTests
{
    private static History NewHistory(int chapters) =>
        new([
            .. Enumerable
                .Range(0, chapters)
                .Select(i => (HistoryLabel)new DirectHistoryLabel($"c{i}")),
        ]);

    [Test]
    public static void UpdateWithMatchingLengthsAddsToEveryChapter()
    {
        var history = NewHistory(2);

        history.Update(0, [3, 4], [30, 40]);

        Expect.AreEqual((3, 30), history._chapters[0].Last(Period.Day));
        Expect.AreEqual((4, 40), history._chapters[1].Last(Period.Day));
    }

    [Test]
    [WarningsAllowed("incorrect number of chapters")]
    public static void UpdateWithFewerInputsThanChaptersUpdatesAPrefix()
    {
        var history = NewHistory(3);

        history.Update(0, [3], [30]);

        Expect.AreEqual((3, 30), history._chapters[0].Last(Period.Day));
    }

    // Pins the current behavior: excess inputs run off the end of the chapter list.
    [Test]
    [WarningsAllowed("incorrect number of chapters")]
    public static void UpdateWithMoreInputsThanChaptersThrows()
    {
        var history = NewHistory(1);

        _ = Expect.Throws<ArgumentOutOfRangeException>(() => history.Update(0, [1, 2], [1, 2]));
    }

    [Test]
    [WarningsAllowed("incorrect number of chapters")]
    public static void UpdateMaxWithFewerInputsUpdatesAPrefix()
    {
        var history = NewHistory(2);

        history.UpdateMax(50);

        Expect.AreEqual(50, history._chapters[0].TrueMax);
    }

    [Test]
    [WarningsAllowed("incorrect number of chapters")]
    public static void UpdateMaxWithMoreInputsThanChaptersThrows()
    {
        var history = NewHistory(1);

        _ = Expect.Throws<ArgumentOutOfRangeException>(() => history.UpdateMax(1, 2));
    }

    [Test]
    [WarningsAllowed("incorrect number of chapters")]
    public static void UpdateThingCountsWithMoreInputsThanChaptersThrows()
    {
        var history = new History([new ThingDefCount(new ThingDef { defName = "A" }, 0)]);

        _ = Expect.Throws<ArgumentOutOfRangeException>(() => history.UpdateThingCounts(1, 2));
    }

    [Test]
    public static void NullArgumentsThrow()
    {
        var history = NewHistory(1);

        _ = Expect.Throws<ArgumentNullException>(() => history.Update(0, null!, [1]));
        _ = Expect.Throws<ArgumentNullException>(() => history.Update(0, [1], null!));
        _ = Expect.Throws<ArgumentNullException>(() => history.UpdateMax(null!));
        _ = Expect.Throws<ArgumentNullException>(() => history.UpdateThingCounts(null!));
    }
}
