// ManagerTabNavigationSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// <see cref="MainTabWindow_Manager.GoTo"/> hook order and the per-type job-order bounds of
/// <see cref="ManagerTab{T}"/> versus the whole-list bounds of the base <see cref="ManagerTab"/>.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerTabNavigationSceneTests
{
    private sealed class SpyTab(Manager manager, string name, List<string> events)
        : ManagerTab(manager)
    {
        public override void PreClose() => events.Add($"{name}.PreClose");

        public override void PostClose() => events.Add($"{name}.PostClose");

        public override void PreOpen() => events.Add($"{name}.PreOpen");

        public override void PostOpen() => events.Add($"{name}.PostOpen");
    }

    private sealed class TabForB(Manager manager) : ManagerTab<TestManagerJobB>(manager);

    [Test]
    public static void GoToRunsHooksInOrderAndSwitchesTab()
    {
        using var scene = new ManagerScene();
        var events = new List<string>();
        var old = new SpyTab(scene.Manager, "old", events);
        var next = new SpyTab(scene.Manager, "new", events);
        var original = MainTabWindow_Manager.CurrentTab;
        try
        {
            MainTabWindow_Manager.CurrentTab = old;

            MainTabWindow_Manager.GoTo(next);

            Expect.That(
                () =>
                    events.SequenceEqual([
                        "old.PreClose",
                        "new.PreOpen",
                        "old.PostClose",
                        "new.PostOpen",
                    ]),
                "Hooks fire in order"
            );
            Expect.ReferencesAreEqual(next, MainTabWindow_Manager.CurrentTab);
        }
        finally
        {
            MainTabWindow_Manager.CurrentTab = original;
        }
    }

    [Test]
    public static void GoToNullTabThrows() =>
        _ = Expect.Throws<ArgumentNullException>(() => MainTabWindow_Manager.GoTo(null!));

    [Test]
    public static void TypedTabBoundsAreRelativeToItsOwnJobType()
    {
        using var scene = new ManagerScene();
        var tracker = scene.Manager.JobTracker;
        var a = scene.AddJob<TestManagerJobA>();
        var b1 = scene.AddJob<TestManagerJobB>();
        var b2 = scene.AddJob<TestManagerJobB>();
        var tab = new TabForB(scene.Manager);

        Expect.AreEqual((true, false), tab.GetJobOrderBounds(b1, tracker));
        Expect.AreEqual((false, true), tab.GetJobOrderBounds(b2, tracker));
        Expect.AreEqual(0, a.Priority);
    }

    [Test]
    public static void BaseTabBoundsSpanTheWholeJobList()
    {
        using var scene = new ManagerScene();
        var tracker = scene.Manager.JobTracker;
        var a = scene.AddJob<TestManagerJobA>();
        var b = scene.AddJob<TestManagerJobB>();
        var tab = new SpyTab(scene.Manager, "base", []);

        Expect.AreEqual((true, false), tab.GetJobOrderBounds(a, tracker));
        Expect.AreEqual((false, true), tab.GetJobOrderBounds(b, tracker));
    }
}
