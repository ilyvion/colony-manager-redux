// PawnsChangedPatchTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

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
}
