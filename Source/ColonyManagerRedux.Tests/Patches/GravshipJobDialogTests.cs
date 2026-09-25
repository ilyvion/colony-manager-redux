// GravshipJobDialogTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

#if !v1_5
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The selection state of <see cref="Dialog_GravshipJobPicker"/> and the modal flags of both
/// gravship job dialogs.
/// </summary>
[TestFixture(TestType.MainMenu)]
internal sealed class GravshipJobDialogTests
{
    [Test]
    public static void KeptJobsComeBackInTheOriginalListOrder()
    {
        string[] all = ["d", "a", "c", "b"];
        var kept = new HashSet<string> { "b", "d", "c" };

        Expect.AreEqual("d,c,b", string.Join(",", Dialog_GravshipJobPicker.KeptInOrder(all, kept)));
    }

    [Test]
    public static void NothingKeptGivesAnEmptyList() =>
        Expect.IsEmpty(Dialog_GravshipJobPicker.KeptInOrder(["a", "b"], new HashSet<string>()));

    [Test]
    public static void KeptJobsThatAreNotInTheListAreIgnored() =>
        Expect.AreEqual(
            "a",
            string.Join(
                ",",
                Dialog_GravshipJobPicker.KeptInOrder(["a", "b"], new HashSet<string> { "a", "z" })
            )
        );

    [Test]
    public static void HeaderToggleOfAnEmptyListIsOffAndNotOn()
    {
        var (allOn, allOff) = Utilities.ToggleState([], new HashSet<string>());

        Expect.IsFalse(allOn);
        Expect.IsTrue(allOff);
    }

    // The player cannot dismiss the dialog without choosing; if it could, the gravship's jobs
    // would be silently lost.
    [Test]
    public static void ConflictDialogCannotBeDismissedWithoutChoosing()
    {
        var dialog = new Dialog_GravshipJobConflict(
            1,
            1,
            () => { },
            () => { },
            () => { },
            () => { }
        );

        dialog.PreOpen();

        Expect.IsFalse(dialog.closeOnCancel, "closeOnCancel");
        Expect.IsFalse(dialog.closeOnClickedOutside, "closeOnClickedOutside");
        Expect.IsTrue(dialog.forcePause, "forcePause");
        Expect.IsTrue(dialog.absorbInputAroundWindow, "absorbInputAroundWindow");
    }

    [Test]
    public static void PickerDialogCannotBeDismissedWithoutConfirming()
    {
        var dialog = new Dialog_GravshipJobPicker([], [], (_, _) => { });

        dialog.PreOpen();

        Expect.IsFalse(dialog.closeOnCancel, "closeOnCancel");
        Expect.IsFalse(dialog.closeOnClickedOutside, "closeOnClickedOutside");
        Expect.IsTrue(dialog.forcePause, "forcePause");
        Expect.IsTrue(dialog.absorbInputAroundWindow, "absorbInputAroundWindow");
    }
}
#endif // !v1_5
