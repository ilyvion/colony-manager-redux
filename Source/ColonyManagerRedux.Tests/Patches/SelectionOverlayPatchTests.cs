// SelectionOverlayPatchTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// <see cref="RimWorld_SelectionDrawer_DrawSelectionOverlays.RunAll"/> runs every registered
/// action in order, isolates failures and tolerates actions that change the list.
/// </summary>
[TestFixture(TestType.MainMenu)]
internal sealed class SelectionOverlayPatchTests
{
    private static void RunAll(List<Action> actions) =>
        RimWorld_SelectionDrawer_DrawSelectionOverlays.RunAll(actions);

    [Test]
    public static void EmptyListDoesNothing() => RunAll([]);

    [Test]
    public static void ActionsRunInRegistrationOrderOncePerCall()
    {
        var events = new List<string>();

        RunAll([() => events.Add("a"), () => events.Add("b")]);

        Expect.IsTrue(events.SequenceEqual(["a", "b"]), "order and count");
    }

    [Test]
    [ErrorsAllowed("Post-draw selection overlay action")]
    public static void ThrowingActionDoesNotStopLaterActions()
    {
        var ran = false;

        RunAll([() => throw new InvalidOperationException("boom"), () => ran = true]);

        Expect.IsTrue(ran, "later action ran");
    }

    [Test]
    [ErrorsAllowed("Post-draw selection overlay action")]
    public static void ThrowingActionIsLoggedOnlyOnce()
    {
        var actions = new List<Action> { () => throw new InvalidOperationException("boom") };

        RunAll(actions);
        RunAll(actions);
    }

    [Test]
    public static void ActionAddingToTheListDoesNotThrow()
    {
        var actions = new List<Action>();
        var ran = 0;
        actions.Add(() =>
        {
            ran++;
            actions.Add(() => ran++);
        });

        RunAll(actions);

        Expect.AreEqual(1, ran, "actions run");
        Expect.AreEqual(2, actions.Count, "action was added");
    }

    [Test]
    public static void ActionRemovingItselfDoesNotThrow()
    {
        var actions = new List<Action>();
        var ran = false;
        actions.Add(actions.Clear);
        actions.Add(() => ran = true);

        RunAll(actions);

        Expect.IsTrue(ran, "snapshot still runs later action");
    }
}

#if v1_7_OR_GREATER
/// <summary>
/// Registered post-draw selection overlay actions run when vanilla's map interface updates.
/// </summary>
// On 1.7 the JIT can inline the small DrawSelectionOverlays into its vanilla caller, so the patch
// is checked through that caller.
[TestFixture(TestType.Playing)]
internal sealed class SelectionOverlayVanillaCallerTests
{
    [Test]
    public static void ActionsRunWhenTheMapInterfaceUpdates()
    {
        var actions =
            RimWorld_SelectionDrawer_DrawSelectionOverlays.PostDrawSelectionOverlaysActions;
        var ran = false;
        void MarkRan()
        {
            ran = true;
        }
        Action markRan = MarkRan;
        actions.Add(markRan);
        try
        {
            Find.MapUI.MapInterfaceUpdate();

            Expect.IsTrue(ran, "action ran");
        }
        finally
        {
            _ = actions.Remove(markRan);
        }
    }
}
#endif

/// <summary>
/// <see cref="StockpileGUI.ShouldDrawMouseOverZone"/> draws the hovered zone's edges only when it
/// is not selected.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class StockpileMouseOverOverlayTests
{
    [Test]
    public static void NoMouseOverZoneIsNotDrawn() =>
        Expect.IsFalse(StockpileGUI.ShouldDrawMouseOverZone(null), "null zone");

    [Test]
    public static void UnselectedMouseOverZoneIsDrawn()
    {
        using var scene = new ManagerScene();
        var zone = scene.AddStockpile([Find.CurrentMap.Center]);

        Expect.IsTrue(StockpileGUI.ShouldDrawMouseOverZone(zone), "unselected zone");
    }

    [Test]
    public static void SelectedMouseOverZoneIsNotDrawn()
    {
        using var scene = new ManagerScene();
        var zone = scene.AddStockpile([Find.CurrentMap.Center]);
        Find.Selector.Select(zone, playSound: false, forceDesignatorDeselect: false);
        try
        {
            Expect.IsFalse(StockpileGUI.ShouldDrawMouseOverZone(zone), "selected zone");
        }
        finally
        {
            Find.Selector.ClearSelection();
        }
    }
}
