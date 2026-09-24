// SettingsReconcileTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class SettingsReconcileTests
{
    private sealed class Entry(ManagerDef? def) : IManagerDefOwnedSettings
    {
        public ManagerDef Def { get; } = def!;
    }

    private static ManagerDef NewDef(string name, int order) =>
        new() { defName = name, order = order };

    [Test]
    [WarningsAllowed("is null", "no such ManagerDef")]
    public static void PrunesNullEntriesNullDefsAndUnknownDefsThenCreatesMissing()
    {
        var a = NewDef("A", 1);
        var b = NewDef("B", 2);
        var unknown = NewDef("Unknown", 3);
        var valid = new Entry(a);
        List<Entry> list = [null!, new Entry(null), new Entry(unknown), valid];

        Settings.EnsureManagerDefSettingsAreCorrect(list, [a, b], d => new Entry(d));

        Expect.AreEqual(2, list.Count);
        Expect.ReferencesAreEqual(valid, list[0]);
        Expect.ReferencesAreEqual(b, list[1].Def);
    }

    [Test]
    public static void ResultIsSortedByDefOrder()
    {
        var a = NewDef("A", 5);
        var b = NewDef("B", 1);
        var c = NewDef("C", 3);
        List<Entry> list = [new Entry(a)];

        Settings.EnsureManagerDefSettingsAreCorrect(list, [a, b, c], d => new Entry(d));

        Expect.That(() => list.Select(e => e.Def).SequenceEqual([b, c, a]));
    }

    [Test]
    public static void CompleteListIsLeftUntouched()
    {
        var a = NewDef("A", 1);
        var entry = new Entry(a);
        List<Entry> list = [entry];
        var created = 0;

        Settings.EnsureManagerDefSettingsAreCorrect(
            list,
            [a],
            d =>
            {
                created++;
                return new Entry(d);
            }
        );

        Expect.AreEqual(0, created);
        Expect.AreEqual(1, list.Count);
    }

    [Test]
    public static void EmptyListIsFilledWithOneEntryPerDef()
    {
        List<Entry> list = [];

        Settings.EnsureManagerDefSettingsAreCorrect(
            list,
            [NewDef("A", 1), NewDef("B", 2)],
            d => new Entry(d)
        );

        Expect.AreEqual(2, list.Count);
    }
}
