// PawnsChangedPatchTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The pawns-changed patch notifies every manager tab of the current map and isolates failures
/// per tab.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class PawnsChangedPatchTests
{
    private sealed class CountingTab(Manager manager) : ManagerTab(manager)
    {
        public int Notified { get; private set; }

        protected internal override void Notify_PawnsChanged() => Notified++;
    }

    private sealed class ThrowingTab(Manager manager) : ManagerTab(manager)
    {
        protected internal override void Notify_PawnsChanged() =>
            throw new InvalidOperationException("boom");
    }

    [Test]
    public static void EveryTabIsNotified()
    {
        using var scene = new ManagerScene();
        var first = new CountingTab(scene.Manager);
        var second = new CountingTab(scene.Manager);

        RimWorld_MainTabWindowUtility_NotifyAllPawnTables_PawnsChanged.NotifyTabs([first, second]);

        Expect.AreEqual(1, first.Notified, "first");
        Expect.AreEqual(1, second.Notified, "second");
    }

    [Test]
    [ErrorsAllowed("Notify_PawnsChanged")]
    public static void ThrowingTabDoesNotStopLaterTabs()
    {
        using var scene = new ManagerScene();
        var after = new CountingTab(scene.Manager);

        RimWorld_MainTabWindowUtility_NotifyAllPawnTables_PawnsChanged.NotifyTabs([
            new ThrowingTab(scene.Manager),
            after,
        ]);

        Expect.AreEqual(1, after.Notified, "tab after the throwing one");
    }

    [Test]
    public static void VanillaNotificationReachesTheRealTabsWithoutThrowing()
    {
        using var scene = new ManagerScene();

        MainTabWindowUtility.NotifyAllPawnTables_PawnsChanged();

        Expect.IsNotEmpty(scene.Manager.Tabs, "tabs present");
    }

    private static PawnTable NewCleanTable()
    {
        var table = new PawnTable(new PawnTableDef(), () => [], 100, 100) { dirty = false };
        return table;
    }

    [Test]
    public static void PostfixWithoutCurrentMapDoesNotThrow()
    {
        var game = Current.Game;
        var index = game.currentMapIndex;
        try
        {
            game.currentMapIndex = -1;
            Expect.IsNull(Find.CurrentMap, "no current map");

            RimWorld_MainTabWindowUtility_NotifyAllPawnTables_PawnsChanged.Postfix();
        }
        finally
        {
            game.currentMapIndex = index;
        }
    }

    [Test]
    public static void OverviewTableIsDirtyAfterVanillaNotification()
    {
        using var scene = new ManagerScene();
        var overview = scene.Manager.Tabs.OfType<ManagerTab_Overview>().Single();
        var previous = overview.pawnOverviewTable;
        try
        {
            var table = NewCleanTable();
            overview.pawnOverviewTable = table;

            MainTabWindowUtility.NotifyAllPawnTables_PawnsChanged();

            Expect.IsTrue(table.dirty, "overview table dirty");
        }
        finally
        {
            overview.pawnOverviewTable = previous;
        }
    }

    [Test]
    public static void LivestockTablesAreDirtyOnlyWhenAJobIsSelected()
    {
        using var scene = new ManagerScene();
        var livestock = scene.Manager.Tabs.OfType<ManagerTab_Livestock>().Single();
        var job = scene.AddJob<ManagerJob_Livestock>("CM_LivestockManager");
        var previousTame = livestock.animalsTameTable;
        var previousWild = livestock.animalsWildTable;
        var previousSelected = livestock.Selected;
        try
        {
            var tame = NewCleanTable();
            var wild = NewCleanTable();
            livestock.Selected = null;
            livestock.animalsTameTable = tame;
            livestock.animalsWildTable = wild;

            MainTabWindowUtility.NotifyAllPawnTables_PawnsChanged();

            Expect.IsFalse(tame.dirty, "tame table without selection");
            Expect.IsFalse(wild.dirty, "wild table without selection");

            livestock.Selected = job;
            tame.dirty = false;
            wild.dirty = false;

            MainTabWindowUtility.NotifyAllPawnTables_PawnsChanged();

            Expect.IsTrue(tame.dirty, "tame table with selection");
            Expect.IsTrue(wild.dirty, "wild table with selection");
        }
        finally
        {
            livestock.Selected = previousSelected;
            livestock.animalsTameTable = previousTame;
            livestock.animalsWildTable = previousWild;
        }
    }
}
