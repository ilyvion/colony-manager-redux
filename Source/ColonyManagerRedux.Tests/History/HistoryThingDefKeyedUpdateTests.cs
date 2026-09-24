// HistoryThingDefKeyedUpdateTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class HistoryThingDefKeyedUpdateTests
{
    private static ThingDef Def(string defName) => new() { defName = defName };

    private static History.Chapter NewChapter(ThingDef def) =>
        new(new ThingDefCountClass(def, 0), History.EntriesPerInterval, Color.white);

    // Regression guard: ManagerJob_Power's TraderDefs ordering is recomputed from
    // DefDatabase<ThingDef> every session and can differ from the order chapters were created
    // in (e.g. after a mod adds/removes/reorders power buildings). Update/UpdateThingCountAndMax
    // must attach each value to the chapter matching its ThingDef, not to whichever chapter
    // happens to sit at the same array index.
    [Test]
    public static void UpdateAttachesValueToChapterMatchingThingDefEvenWhenChaptersAreOutOfOrder()
    {
        var gas = Def("GasGenerator");
        var uranium = Def("UraniumArc");
        var history = new History();
        // Chapters were created in the opposite order to how the caller now enumerates defs.
        history._chapters.Add(NewChapter(uranium));
        history._chapters.Add(NewChapter(gas));

        history.Update(0, (gas, 3, 0), (uranium, 7, 0));

        var gasChapter = history._chapters.Single(c => c.ThingDefCount.thingDef == gas);
        var uraniumChapter = history._chapters.Single(c => c.ThingDefCount.thingDef == uranium);
        Expect.AreEqual(3, gasChapter.Last(Period.Day).count);
        Expect.AreEqual(7, uraniumChapter.Last(Period.Day).count);
    }

    [Test]
    public static void UpdateThingCountAndMaxAttachesValueToChapterMatchingThingDefEvenWhenChaptersAreOutOfOrder()
    {
        var gas = Def("GasGenerator");
        var uranium = Def("UraniumArc");
        var history = new History();
        history._chapters.Add(NewChapter(uranium));
        history._chapters.Add(NewChapter(gas));

        history.UpdateThingCountAndMax([(gas, 3, 300), (uranium, 1, 100)]);

        var gasChapter = history._chapters.Single(c => c.ThingDefCount.thingDef == gas);
        var uraniumChapter = history._chapters.Single(c => c.ThingDefCount.thingDef == uranium);
        Expect.AreEqual(3, gasChapter.ThingDefCount.count);
        Expect.AreEqual(1, uraniumChapter.ThingDefCount.count);
    }
}
