// UiHelpersTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The viewport-culling rule of <see cref="Widgets_Section"/> and the tri-state group toggle
/// state of <see cref="Utilities.ToggleState{T}"/>.
/// </summary>
[TestFixture(TestType.MainMenu)]
internal sealed class UiHelpersTests
{
    // Viewport covers y in [100, 200].

    private static bool Outside(float y, float height) =>
        Widgets_Section.IsOutsideViewport(y, height, 100f, 100f);

    [Test]
    public static void EntryInsideTheViewportIsNotCulled() => Expect.IsFalse(Outside(120f, 20f));

    [Test]
    public static void EntryWhoseBottomEdgeTouchesTheScrollOffsetIsNotCulled() =>
        Expect.IsFalse(Outside(80f, 20f));

    [Test]
    public static void EntryOnePixelAboveTheViewportIsCulled() => Expect.IsTrue(Outside(79f, 20f));

    [Test]
    public static void EntryStartingAtTheBottomEdgeIsNotCulled() =>
        Expect.IsFalse(Outside(200f, 20f));

    [Test]
    public static void EntryStartingOnePixelBelowTheViewportIsCulled() =>
        Expect.IsTrue(Outside(201f, 20f));

    [Test]
    public static void ZeroHeightEntryFollowsTheSameEdges()
    {
        Expect.IsFalse(Outside(100f, 0f));
        Expect.IsTrue(Outside(99f, 0f));
        Expect.IsFalse(Outside(200f, 0f));
        Expect.IsTrue(Outside(201f, 0f));
    }

    [Test]
    public static void EntryLargerThanTheViewportIsNotCulled() =>
        Expect.IsFalse(Outside(50f, 300f));

    [Test]
    public static void NothingIsCulledWithoutAnActiveSectionColumn() =>
        Expect.IsFalse(Widgets_Section.CanCull(-1000f, 1f));

    [Test]
    public static void ToggleStateIsAllOnWhenEveryOptionIsSelected()
    {
        var (allOn, allOff) = Utilities.ToggleState([1, 2], new HashSet<int> { 1, 2, 3 });

        Expect.IsTrue(allOn);
        Expect.IsFalse(allOff);
    }

    [Test]
    public static void ToggleStateIsAllOffWhenNoOptionIsSelected()
    {
        var (allOn, allOff) = Utilities.ToggleState([1, 2], new HashSet<int> { 3 });

        Expect.IsFalse(allOn);
        Expect.IsTrue(allOff);
    }

    [Test]
    public static void ToggleStateIsPartialWhenSomeOptionsAreSelected()
    {
        var (allOn, allOff) = Utilities.ToggleState([1, 2], new HashSet<int> { 1 });

        Expect.IsFalse(allOn);
        Expect.IsFalse(allOff);
    }

    // An empty group has nothing to switch on, so it shows as unchecked rather than as both
    // fully on and fully off.
    [Test]
    public static void ToggleStateOfAnEmptyGroupIsOff()
    {
        var (allOn, allOff) = Utilities.ToggleState([], new HashSet<int> { 1 });

        Expect.IsFalse(allOn);
        Expect.IsTrue(allOff);
    }

    [Test]
    public static void ToggleStateRejectsNullArguments()
    {
        var threwForOptions = false;
        try
        {
            _ = Utilities.ToggleState(null!, new HashSet<int>());
        }
        catch (ArgumentNullException)
        {
            threwForOptions = true;
        }

        var threwForSelected = false;
        try
        {
            _ = Utilities.ToggleState([1], null!);
        }
        catch (ArgumentNullException)
        {
            threwForSelected = true;
        }

        Expect.IsTrue(threwForOptions, "options");
        Expect.IsTrue(threwForSelected, "selected");
    }
}
