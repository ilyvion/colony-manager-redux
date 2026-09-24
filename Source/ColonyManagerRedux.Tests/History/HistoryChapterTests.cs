// HistoryChapterTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class HistoryChapterTests
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
        Expect.AreEqual(3, chapter.ValuesFor(Period.Day).Length);
        Expect.AreEqual(0, chapter.ValuesFor(Period.Day)[0]);
        Expect.AreEqual(10, chapter.ValuesFor(Period.Day)[1]);
        Expect.AreEqual(20, chapter.ValuesFor(Period.Day)[2]);
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
        Expect.AreEqual(3, values.Length);
        Expect.AreEqual(20, values[0]);
        Expect.AreEqual(30, values[1]);
        Expect.AreEqual(40, values[2]);
    }

    [Test]
    public static void MaxIncludesTargetValue()
    {
        var chapter = NewChapter(5);
        chapter.Add(10, 100, 0 * interval);
        chapter.Add(20, 0, 1 * interval);

        Expect.AreEqual(100, chapter.Max(Period.Day));
    }

    [Test]
    public static void HasTargetsIsFalseWhenTargetNeverChangesFromZero()
    {
        var chapter = NewChapter(5);
        chapter.Add(10, 0, 0 * interval);
        chapter.Add(20, 0, 1 * interval);

        Expect.IsFalse(chapter.HasTargets(Period.Day));
        Expect.IsTrue(chapter.TargetsFor(Period.Day) is null);
    }

    [Test]
    public static void TargetsForExpandsSparseTargetChangesToEveryPosition()
    {
        var chapter = NewChapter(5);
        chapter.Add(10, 0, 0 * interval);
        chapter.Add(20, 0, 1 * interval);
        chapter.Add(30, 100, 2 * interval); // target changes to 100 from this point on
        chapter.Add(40, 100, 3 * interval);

        Expect.IsTrue(chapter.HasTargets(Period.Day));

        var targets = chapter.TargetsFor(Period.Day)!;
        Expect.AreEqual(5, targets.Length);
        Expect.AreEqual(0, targets[0]);
        Expect.AreEqual(0, targets[1]);
        Expect.AreEqual(0, targets[2]);
        Expect.AreEqual(100, targets[3]);
        Expect.AreEqual(100, targets[4]);
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
        Expect.AreEqual(5, values.Length);
        Expect.AreEqual(20, values[0]);
        Expect.AreEqual(60, values[4]);

        Expect.AreEqual(5, targets.Length);
        Expect.AreEqual(0, targets[0]);
        Expect.AreEqual(100, targets[1]);
        Expect.AreEqual(100, targets[4]);
    }

    [Test]
    public static void ClearResetsValuesToFreshChapterState()
    {
        var chapter = NewChapter(5);
        chapter.Add(10, 0, 0 * interval);
        chapter.Add(20, 0, 1 * interval);
        chapter.Add(30, 0, 2 * interval);

        chapter.Clear();

        var values = chapter.ValuesFor(Period.Day);
        Expect.AreEqual(1, values.Length);
        Expect.AreEqual(0, values[0]);
    }

    [Test]
    public static void ClearResetsTrueMaxToDefault()
    {
        var chapter = NewChapter(5);
        chapter.Add(10, 100, 0 * interval);
        chapter.Add(20, 0, 1 * interval);

        chapter.Clear();

        // With no recorded data, TrueMax falls back to its floor of 1.
        Expect.AreEqual(1, chapter.TrueMax);
    }

    [Test]
    public static void ClearResetsTargetsSoHasTargetsIsFalse()
    {
        var chapter = NewChapter(5);
        chapter.Add(10, 0, 0 * interval);
        chapter.Add(20, 100, 1 * interval);

        Expect.IsTrue(chapter.HasTargets(Period.Day));

        chapter.Clear();

        Expect.IsFalse(chapter.HasTargets(Period.Day));
        Expect.IsTrue(chapter.TargetsFor(Period.Day) is null);
    }
}
