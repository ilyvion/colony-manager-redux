// HistoryUpdateThingDefsTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class HistoryUpdateThingDefsTests
{
    private static ThingDef Def(string defName) => new() { defName = defName };

    private static History.Chapter NewChapter(ThingDef def) =>
        new(new ThingDefCountClass(def, 0), History.EntriesPerInterval, Color.white);

    // Regression guard for CHANGELOG 0.14.7/0.5.1: a chapter whose def is no longer in the new
    // set (e.g. a power trader/battery building that no longer exists) must be removed, not
    // crash or leave the list in a state with the wrong chapter count.
    [Test]
    public static void RemovesChapterForDefNoLongerPresent()
    {
        var stays = Def("Stays");
        var removed = Def("Removed");
        var history = new History();
        history._chapters.Add(NewChapter(stays));
        history._chapters.Add(NewChapter(removed));

        history.UpdateThingDefs([stays]);

        Assert.ThatCollection(history._chapters).Has.Count(1);
        Assert.That(ReferenceEquals(history._chapters[0].ThingDefCount.thingDef, stays)).Is.True();
    }

    [Test]
    public static void AddsChapterForNewDefBackfilledWithZeroesMatchingExistingBufferLength()
    {
        var existing = Def("Existing");
        var history = new History();
        var chapter = NewChapter(existing);
        history._chapters.Add(chapter);

        // Grow the existing chapter's Day buffer to 3 entries: [0, 10, 20].
        var interval = History.PeriodTickInterval(Period.Day);
        chapter.Add(10, 0, 0 * interval);
        chapter.Add(20, 0, 1 * interval);

        var newDef = Def("New");
        history.UpdateThingDefs([existing, newDef]);

        Assert.ThatCollection(history._chapters).Has.Count(2);
        var newChapter = history._chapters[1];
        Assert.That(ReferenceEquals(newChapter.ThingDefCount.thingDef, newDef)).Is.True();

        var values = newChapter.ValuesFor(Period.Day);
        Assert.ThatCollection(values).Has.Count(3);
        Assert.That(values[0]).Is.EqualTo(0);
        Assert.That(values[1]).Is.EqualTo(0);
        Assert.That(values[2]).Is.EqualTo(0);
    }

    [Test]
    public static void HandlesSimultaneousRemovalAndAdditionInSingleCall()
    {
        var stays = Def("Stays");
        var removed = Def("Removed");
        var added = Def("Added");
        var history = new History();
        history._chapters.Add(NewChapter(stays));
        history._chapters.Add(NewChapter(removed));

        history.UpdateThingDefs([stays, added]);

        Assert.ThatCollection(history._chapters).Has.Count(2);
        Assert
            .That(history._chapters.Any(c => ReferenceEquals(c.ThingDefCount.thingDef, stays)))
            .Is.True();
        Assert
            .That(history._chapters.Any(c => ReferenceEquals(c.ThingDefCount.thingDef, added)))
            .Is.True();
        Assert
            .That(history._chapters.Any(c => ReferenceEquals(c.ThingDefCount.thingDef, removed)))
            .Is.False();
    }

    // Chapters added or removed after construction must be reflected in the shown-chapters set,
    // or the plot never displays new chapters and keeps drawing removed ones.
    [Test]
    public static void ShownChaptersTrackAddedAndRemovedChapters()
    {
        var stays = Def("Stays");
        var removed = Def("Removed");
        var added = Def("Added");
        var history = new History();
        var staysChapter = NewChapter(stays);
        history._chapters.Add(staysChapter);
        history._chapters.Add(NewChapter(removed));
        history._chaptersShown.AddRange(history._chapters);

        history.UpdateThingDefs([stays, added]);

        Assert.ThatCollection(history._chaptersShown).Has.Count(2);
        Assert.That(history._chaptersShown.Contains(staysChapter)).Is.True();
        Assert.That(history._chaptersShown.All(history._chapters.Contains)).Is.True();
    }

    // A chapter the player has hidden via the legend must stay hidden across an update.
    [Test]
    public static void HiddenChapterStaysHiddenAcrossUpdate()
    {
        var first = Def("First");
        var second = Def("Second");
        var history = new History();
        var firstChapter = NewChapter(first);
        var secondChapter = NewChapter(second);
        history._chapters.Add(firstChapter);
        history._chapters.Add(secondChapter);
        history._chaptersShown.Add(firstChapter);

        history.UpdateThingDefs([first, second, Def("Third")]);

        Assert.That(history._chaptersShown.Contains(secondChapter)).Is.False();
        Assert.That(history._chaptersShown.Contains(firstChapter)).Is.True();
    }

    // A no-op call (new set exactly matches existing chapters' defs) should still leave the
    // chapter count unchanged, even though the reconciliation loop walks/removes/re-adds nothing.
    [Test]
    public static void ChapterCountIsStableWhenNewDefsExactlyMatchExisting()
    {
        var first = Def("First");
        var second = Def("Second");
        var history = new History();
        history._chapters.Add(NewChapter(first));
        history._chapters.Add(NewChapter(second));

        history.UpdateThingDefs([first, second]);

        Assert.ThatCollection(history._chapters).Has.Count(2);
        Assert.That(ReferenceEquals(history._chapters[0].ThingDefCount.thingDef, first)).Is.True();
        Assert.That(ReferenceEquals(history._chapters[1].ThingDefCount.thingDef, second)).Is.True();
    }

    // When exactly one def is passed in and there are no surviving chapters, the "single chapter"
    // color default (Color.white, not a 1-element HSV rainbow range) is used.
    [Test]
    public static void SingleNewChapterGetsWhiteWhenNoColorsSupplied()
    {
        var only = Def("Only");
        var history = new History();

        history.UpdateThingDefs([only]);

        Assert.ThatCollection(history._chapters).Has.Count(1);
        Assert.That(history._chapters[0].LineColor == Color.white).Is.True();
    }

    // Explicit colors shorter than the total chapter count must wrap around via
    // (currentChapterCount + i) % colors.Length rather than throwing an IndexOutOfRange.
    [Test]
    public static void SuppliedColorsWrapAroundForChaptersBeyondArrayLength()
    {
        var existing = Def("Existing");
        var history = new History();
        history._chapters.Add(NewChapter(existing));

        var addedA = Def("AddedA");
        var addedB = Def("AddedB");
        Color[] colors = [Color.red, Color.green];

        // currentChapterCount == 1 at the time new chapters are added, so addedA gets
        // colors[1 % 2] == green and addedB gets colors[2 % 2] == red.
        history.UpdateThingDefs([existing, addedA, addedB], colors);

        Assert.ThatCollection(history._chapters).Has.Count(3);
        Assert.That(history._chapters[1].LineColor == Color.green).Is.True();
        Assert.That(history._chapters[2].LineColor == Color.red).Is.True();
    }
}
