// NiceBillTab_Patches.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using System.Reflection.Emit;
using ColonyManagerRedux.Managers;
using ColonyManagerRedux.Managers.Patches;
using NiceBillTab;

namespace ColonyManagerRedux.NiceBillTab.Patches;

[StaticConstructorOnStartup]
internal static class NiceBillTab_Patches
{
    static NiceBillTab_Patches()
    {
        var harmony = new Harmony($"{ColonyManagerReduxMod.PackageId}.NiceBillTab");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }

    internal static void LogMissingTarget(string method, string target) =>
        ColonyManagerReduxMod.Instance.LogError(
            $"Could not patch Nice Bill Tab's TabBillsDrawer.{method} to suppress managed-bill controls, "
                + $"the expected {target} was not found."
        );
}

/// <summary>
/// Draws bills managed by a production job without Nice Bill Tab's bill controls (suspend,
/// delete, repeat mode, count, details, store mode) and with the "managed by" icon in place of
/// the delete button, and leaves copying and pasting settings out of their right-click menu.
/// </summary>
[HarmonyPatch(typeof(TabBillsDrawer), nameof(TabBillsDrawer.DrawBillQueue))]
internal static class NiceBillTab_TabBillsDrawer_DrawBillQueue
{
    internal static readonly MethodInfo DrawBillPreviewMethod = AccessTools.Method(
        typeof(TabBillsDrawer),
        nameof(TabBillsDrawer.DrawBillPreview)
    );

    internal static readonly MethodInfo AnyMethod = (
        (Func<List<FloatMenuOption>, bool>)GenCollection.Any
    ).Method;

    internal static readonly MethodInfo OurDrawBillPreview = AccessTools.Method(
        typeof(NiceBillTab_TabBillsDrawer_DrawBillQueue),
        nameof(DrawBillPreview)
    );

    internal static readonly MethodInfo OurAny = AccessTools.Method(
        typeof(NiceBillTab_TabBillsDrawer_DrawBillQueue),
        nameof(AnyBillMenuOption)
    );

    private static float DrawBillPreview(
        Rect recipePreviewRect,
        Bill bill,
        bool selected,
        TabBillsDrawer.BillStatus status,
        bool dragging,
        bool drawButtons
    )
    {
        var job = drawButtons ? ManagerJob_Production.FindManagingJob(bill) : null;
        var height = TabBillsDrawer.DrawBillPreview(
            recipePreviewRect,
            bill,
            selected,
            status,
            dragging,
            drawButtons && job == null
        );
        if (job != null)
        {
            RimWorld_Bill_DoInterface.DrawManagedByJobIcon(
                ManagedIconRect(recipePreviewRect, TabBillsDrawer.IngredientIconPreviewSize),
                job
            );
        }
        return height;
    }

    /// <summary>
    /// Where <see cref="TabBillsDrawer.DrawBillPreview"/> puts a bill's delete button.
    /// </summary>
    internal static Rect ManagedIconRect(Rect recipePreviewRect, float size) =>
        new(recipePreviewRect.xMax - 6f - size - 4f, recipePreviewRect.y + 10f, size, size);

    private static bool AnyBillMenuOption(List<FloatMenuOption> options)
    {
        if (ManagerJob_Production.FindManagingJob(ITab_Bills_FillTab_Patch.HoveredBill) != null)
        {
            RemoveBillSettingsOptions(options);
        }
        return options.Any();
    }

    internal static void RemoveBillSettingsOptions(List<FloatMenuOption> options)
    {
        string copy = "CopyBillTip".Translate();
        string? pasteSettings = "IW.PasteBillSettings".CanTranslate()
            ? "IW.PasteBillSettings".Translate()
            : null;
        _ = options.RemoveAll(o => o.Label == copy || o.Label == pasteSettings);
    }

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var foundDrawBillPreview = false;
        var foundAny = false;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(DrawBillPreviewMethod))
            {
                instruction.operand = OurDrawBillPreview;
                foundDrawBillPreview = true;
            }
            else if (instruction.Calls(AnyMethod))
            {
                instruction.operand = OurAny;
                foundAny = true;
            }
            yield return instruction;
        }

        if (!foundDrawBillPreview)
        {
            NiceBillTab_Patches.LogMissingTarget(
                nameof(TabBillsDrawer.DrawBillQueue),
                "TabBillsDrawer.DrawBillPreview call"
            );
        }
        if (!foundAny)
        {
            NiceBillTab_Patches.LogMissingTarget(
                nameof(TabBillsDrawer.DrawBillQueue),
                "Enumerable.Any<FloatMenuOption> call"
            );
        }
    }
}

/// <summary>
/// Leaves the details button out of the recipe panel of a selected bill managed by a production
/// job.
/// </summary>
[HarmonyPatch(typeof(TabBillsDrawer), nameof(TabBillsDrawer.DrawIngredients))]
internal static class NiceBillTab_TabBillsDrawer_DrawIngredients
{
    internal static readonly FieldInfo SelectedBillField = AccessTools.Field(
        typeof(RecipeSelection),
        nameof(RecipeSelection.SelectedBill)
    );

    internal static readonly MethodInfo OurSelectedBill = AccessTools.Method(
        typeof(NiceBillTab_TabBillsDrawer_DrawIngredients),
        nameof(UnmanagedSelectedBill)
    );

    internal static Bill? UnmanagedSelectedBill(RecipeSelection selection) =>
        ManagerJob_Production.FindManagingJob(selection.SelectedBill) == null
            ? selection.SelectedBill
            : null;

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var found = false;
        foreach (var instruction in instructions)
        {
            if (instruction.LoadsField(SelectedBillField))
            {
                instruction.opcode = OpCodes.Call;
                instruction.operand = OurSelectedBill;
                found = true;
            }
            yield return instruction;
        }

        if (!found)
        {
            NiceBillTab_Patches.LogMissingTarget(
                nameof(TabBillsDrawer.DrawIngredients),
                "RecipeSelection.SelectedBill load"
            );
        }
    }
}

/// <summary>
/// Leaves the material menu, which changes the selected bill's ingredients, out of the recipe
/// panel of a selected bill managed by a production job.
/// </summary>
[HarmonyPatch(typeof(TabBillsDrawer), nameof(TabBillsDrawer.DrawRecipeInfo))]
internal static class NiceBillTab_TabBillsDrawer_DrawRecipeInfo
{
    // Resolved through the declaring type, since a getter looked up through a derived type
    // doesn't compare equal to the call site's (BuildableDef declares it on 1.7).
    internal static readonly MethodInfo MadeFromStuffGetter = AccessTools.PropertyGetter(
        AccessTools.Property(typeof(ThingDef), nameof(ThingDef.MadeFromStuff)).DeclaringType,
        nameof(ThingDef.MadeFromStuff)
    );

    internal static readonly MethodInfo OurMadeFromStuff = AccessTools.Method(
        typeof(NiceBillTab_TabBillsDrawer_DrawRecipeInfo),
        nameof(MadeFromStuffUnlessManaged)
    );

    internal static bool MadeFromStuffUnlessManaged(ThingDef def, RecipeSelection selection) =>
        def.MadeFromStuff && ManagerJob_Production.FindManagingJob(selection.SelectedBill) == null;

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var found = false;
        foreach (var instruction in instructions)
        {
            if (instruction.Calls(MadeFromStuffGetter))
            {
                // DrawRecipeInfo(Rect rect, RecipeSelection selection, bool showScrollbar)
                yield return new CodeInstruction(OpCodes.Ldarg_1).WithLabels(
                    instruction.ExtractLabels()
                );
                instruction.opcode = OpCodes.Call;
                instruction.operand = OurMadeFromStuff;
                found = true;
            }
            yield return instruction;
        }

        if (!found)
        {
            NiceBillTab_Patches.LogMissingTarget(
                nameof(TabBillsDrawer.DrawRecipeInfo),
                "ThingDef.MadeFromStuff call"
            );
        }
    }
}
