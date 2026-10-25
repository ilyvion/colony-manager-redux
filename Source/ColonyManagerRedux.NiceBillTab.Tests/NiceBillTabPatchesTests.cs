// NiceBillTabPatchesTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.NiceBillTab.Patches;
using DevTools.Testing;
using NiceBillTab;

namespace ColonyManagerRedux.NiceBillTab.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class NiceBillTabPatchesTests
{
    private static List<CodeInstruction> CurrentInstructions(string methodName) =>
        PatchProcessor.GetCurrentInstructions(
            AccessTools.Method(typeof(TabBillsDrawer), methodName)
        );

    [Test]
    public static void DrawBillQueueDrawsBillsThroughTheManagedBillCheck()
    {
        var instructions = CurrentInstructions(nameof(TabBillsDrawer.DrawBillQueue));

        Expect.IsTrue(
            instructions.Any(i =>
                i.Calls(NiceBillTab_TabBillsDrawer_DrawBillQueue.OurDrawBillPreview)
            )
        );
        Expect.IsFalse(
            instructions.Any(i =>
                i.Calls(NiceBillTab_TabBillsDrawer_DrawBillQueue.DrawBillPreviewMethod)
            )
        );
    }

    [Test]
    public static void DrawBillQueueFiltersTheBillMenuThroughTheManagedBillCheck()
    {
        var instructions = CurrentInstructions(nameof(TabBillsDrawer.DrawBillQueue));

        Expect.IsTrue(
            instructions.Any(i => i.Calls(NiceBillTab_TabBillsDrawer_DrawBillQueue.OurAny))
        );
        Expect.IsFalse(
            instructions.Any(i => i.Calls(NiceBillTab_TabBillsDrawer_DrawBillQueue.AnyMethod))
        );
    }

    [Test]
    public static void DrawIngredientsReadsTheSelectedBillThroughTheManagedBillCheck()
    {
        var instructions = CurrentInstructions(nameof(TabBillsDrawer.DrawIngredients));

        Expect.IsTrue(
            instructions.Any(i =>
                i.Calls(NiceBillTab_TabBillsDrawer_DrawIngredients.OurSelectedBill)
            )
        );
        Expect.IsFalse(
            instructions.Any(i =>
                i.LoadsField(NiceBillTab_TabBillsDrawer_DrawIngredients.SelectedBillField)
            )
        );
    }

    [Test]
    public static void DrawRecipeInfoShowsTheMaterialMenuThroughTheManagedBillCheck()
    {
        var instructions = CurrentInstructions(nameof(TabBillsDrawer.DrawRecipeInfo));

        Expect.IsTrue(
            instructions.Any(i =>
                i.Calls(NiceBillTab_TabBillsDrawer_DrawRecipeInfo.OurMadeFromStuff)
            )
        );
        Expect.IsFalse(
            instructions.Any(i =>
                i.Calls(NiceBillTab_TabBillsDrawer_DrawRecipeInfo.MadeFromStuffGetter)
            )
        );
    }

    [Test]
    public static void ManagedIconTakesTheDeleteButtonsPlace()
    {
        var rect = NiceBillTab_TabBillsDrawer_DrawBillQueue.ManagedIconRect(
            new Rect(0f, 100f, 300f, 44f),
            20f
        );

        Expect.AreEqual(new Rect(270f, 110f, 20f, 20f), rect);
    }

    [Test]
    public static void RemoveBillSettingsOptionsKeepsOptionsThatLeaveTheBillAlone()
    {
        var copy = new FloatMenuOption("CopyBillTip".Translate(), () => { });
        var pasteHere = new FloatMenuOption("PasteBillHere".Translate(), () => { });
        var options = new List<FloatMenuOption> { copy, pasteHere };

        NiceBillTab_TabBillsDrawer_DrawBillQueue.RemoveBillSettingsOptions(options);

        Expect.AreEqual(1, options.Count);
        Expect.ReferencesAreEqual(pasteHere, options.Single());
    }

    [Test]
    public static void UnmanagedSelectedBillPassesAnUnmanagedSelectionThrough()
    {
        var selection = new RecipeSelection();

        Expect.IsNull(NiceBillTab_TabBillsDrawer_DrawIngredients.UnmanagedSelectedBill(selection));
    }

    [Test]
    public static void MadeFromStuffUnlessManagedFollowsTheDefForAnUnmanagedSelection()
    {
        var selection = new RecipeSelection();
        var stuffed = new ThingDef { stuffCategories = [StuffCategoryDefOf.Metallic] };
        var unstuffed = new ThingDef();

        Expect.IsTrue(
            NiceBillTab_TabBillsDrawer_DrawRecipeInfo.MadeFromStuffUnlessManaged(stuffed, selection)
        );
        Expect.IsFalse(
            NiceBillTab_TabBillsDrawer_DrawRecipeInfo.MadeFromStuffUnlessManaged(
                unstuffed,
                selection
            )
        );
    }
}
