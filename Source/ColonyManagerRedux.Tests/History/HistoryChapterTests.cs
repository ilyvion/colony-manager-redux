// HistoryChapterTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class HistoryChapterTests
{
    private static History.Chapter NewChapter(int entriesPerInterval) =>
        new(new DirectHistoryLabel("test"), entriesPerInterval, Color.white);

    private static readonly int interval = History.PeriodTickInterval(Period.Day);

    [Test]
    public static void ValuesForReflectsPushedCounts()
    {
        var chapter = NewChapter(5);
        chapter.Add(10, 0, 0 * interval);
        chapter.Add(20, 0, 1 * interval);

        // The buffer starts with a single default (0) entry, then each Add appends.
        Assert.ThatCollection(chapter.ValuesFor(Period.Day)).Has.Count(3);
        Assert.That(chapter.ValuesFor(Period.Day)[0]).Is.EqualTo(0);
        Assert.That(chapter.ValuesFor(Period.Day)[1]).Is.EqualTo(10);
        Assert.That(chapter.ValuesFor(Period.Day)[2]).Is.EqualTo(20);
    }

    [Test]
    public static void ValuesForEvictsOldestOnceBufferIsFull()
    {
        var chapter = NewChapter(3);
        chapter.Add(10, 0, 0 * interval);
        chapter.Add(20, 0, 1 * interval);
        chapter.Add(30, 0, 2 * interval); // buffer full: [0, 10, 20], then 30 pushed
        chapter.Add(40, 0, 3 * interval); // evicts the oldest (0): [10, 20, 30] -> [20, 30, 40]

        var values = chapter.ValuesFor(Period.Day);
        Assert.ThatCollection(values).Has.Count(3);
        Assert.That(values[0]).Is.EqualTo(20);
        Assert.That(values[1]).Is.EqualTo(30);
        Assert.That(values[2]).Is.EqualTo(40);
    }

    [Test]
    public static void MaxIncludesTargetValue()
    {
        var chapter = NewChapter(5);
        chapter.Add(10, 100, 0 * interval);
        chapter.Add(20, 0, 1 * interval);

        Assert.That(chapter.Max(Period.Day)).Is.EqualTo(100);
    }

    [Test]
    public static void HasTargetsIsFalseWhenTargetNeverChangesFromZero()
    {
        var chapter = NewChapter(5);
        chapter.Add(10, 0, 0 * interval);
        chapter.Add(20, 0, 1 * interval);

        Assert.That(chapter.HasTargets(Period.Day)).Is.False();
        Assert.That(chapter.TargetsFor(Period.Day) is null).Is.True();
    }

    [Test]
    public static void TargetsForExpandsSparseTargetChangesToEveryPosition()
    {
        var chapter = NewChapter(5);
        chapter.Add(10, 0, 0 * interval);
        chapter.Add(20, 0, 1 * interval);
        chapter.Add(30, 100, 2 * interval); // target changes to 100 from this point on
        chapter.Add(40, 100, 3 * interval);

        Assert.That(chapter.HasTargets(Period.Day)).Is.True();

        var targets = chapter.TargetsFor(Period.Day)!;
        Assert.ThatCollection(targets).Has.Count(5);
        Assert.That(targets[0]).Is.EqualTo(0);
        Assert.That(targets[1]).Is.EqualTo(0);
        Assert.That(targets[2]).Is.EqualTo(0);
        Assert.That(targets[3]).Is.EqualTo(100);
        Assert.That(targets[4]).Is.EqualTo(100);
    }

    [Test]
    public static void TargetsForStaysAlignedWithValuesAfterBufferShifts()
    {
        // Regression test for the sparse (position, target) bookkeeping in Chapter.Add:
        // once the counts buffer starts evicting old entries, every stored target position
        // must be decremented to stay aligned with the shifted values.
        var chapter = NewChapter(5);
        chapter.Add(10, 0, 0 * interval);
        chapter.Add(20, 0, 1 * interval);
        chapter.Add(30, 100, 2 * interval);
        chapter.Add(40, 100, 3 * interval);
        chapter.Add(50, 100, 4 * interval); // buffer full now; next Add evicts the oldest entry
        chapter.Add(60, 100, 5 * interval); // evicts the leading 0 entry, shifting target positions

        var values = chapter.ValuesFor(Period.Day);
        var targets = chapter.TargetsFor(Period.Day)!;
        Assert.ThatCollection(values).Has.Count(5);
        Assert.That(values[0]).Is.EqualTo(20);
        Assert.That(values[4]).Is.EqualTo(60);

        Assert.ThatCollection(targets).Has.Count(5);
        Assert.That(targets[0]).Is.EqualTo(0);
        Assert.That(targets[1]).Is.EqualTo(100);
        Assert.That(targets[4]).Is.EqualTo(100);
    }
}
