// RimWorld_BillProduction_DoConfigInterface.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers.Patches;

[HarmonyPatch(typeof(Bill_Production), "DoConfigInterface")]
internal static class RimWorld_BillProduction_DoConfigInterface
{
    private static bool RedirectedButtonText(
        WidgetRow row,
        string label,
        string? tooltip,
        bool drawBackground,
        bool doMouseoverSound,
        bool active,
        float? fixedWidth
    ) =>
        !RimWorld_Bill_DoInterface.SuppressManagedBillControls
        && row.ButtonText(label, tooltip, drawBackground, doMouseoverSound, active, fixedWidth);

    private static bool RedirectedButtonIcon(
        WidgetRow row,
        Texture2D tex,
        string? tooltip,
        Color? mouseoverColor,
        Color? backgroundColor,
        Color? mouseoverBackgroundColor,
        bool doMouseoverSound,
        float overrideSize
    ) =>
        !RimWorld_Bill_DoInterface.SuppressManagedBillControls
        && row.ButtonIcon(
            tex,
            tooltip,
            mouseoverColor,
            backgroundColor,
            mouseoverBackgroundColor,
            doMouseoverSound,
            overrideSize
        );

    private static readonly MethodInfo ButtonTextMethod = AccessTools.Method(
        typeof(WidgetRow),
        nameof(WidgetRow.ButtonText)
    );

    private static readonly MethodInfo ButtonIconMethod = AccessTools.Method(
        typeof(WidgetRow),
        nameof(WidgetRow.ButtonIcon)
    );

    private static readonly MethodInfo OurButtonText = AccessTools.Method(
        typeof(RimWorld_BillProduction_DoConfigInterface),
        nameof(RedirectedButtonText)
    );

    private static readonly MethodInfo OurButtonIcon = AccessTools.Method(
        typeof(RimWorld_BillProduction_DoConfigInterface),
        nameof(RedirectedButtonIcon)
    );

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        if (
            ButtonTextMethod == null
            || ButtonIconMethod == null
            || OurButtonText == null
            || OurButtonIcon == null
        )
        {
            ColonyManagerReduxMod.Instance.LogError(
                "Could not patch Bill_Production.DoConfigInterface to suppress managed-bill controls, "
                    + "WidgetRow.ButtonText/ButtonIcon was not found."
            );
            return instructions;
        }

        return instructions.Select(instruction =>
        {
            if (instruction.Calls(ButtonTextMethod))
            {
                instruction.operand = OurButtonText;
            }
            else if (instruction.Calls(ButtonIconMethod))
            {
                instruction.operand = OurButtonIcon;
            }
            return instruction;
        });
    }
}
