namespace ColonyManagerRedux.Managers.Patches;

[HarmonyPatch(typeof(Bill), nameof(Bill.DoInterface))]
internal static class RimWorld_Bill_DoInterface
{
    // Set by Prefix, read by this class' own Transpiler-redirected button
    // wrappers and by RimWorld_BillProduction_DoConfigInterface's (DoConfigInterface
    // is always called synchronously from within DoInterface's own call frame, so
    // this one flag covers both methods' redirected button calls).
    [ThreadStatic]
    internal static bool SuppressManagedBillControls;

    private static void Prefix(Bill __instance) =>
        SuppressManagedBillControls =
            __instance is Bill_Production bill
            && bill.Map is { } map
            && ManagerJob_Production.FindOwningJob(
                Manager.For(map).JobTracker.JobsOfType<ManagerJob_Production>(),
                j => j.ManagedBills,
                bill
            ) != null;

    private static void Postfix(Bill __instance, Rect __result)
    {
        SuppressManagedBillControls = false;

        if (__instance is not Bill_Production bill || bill.Map is not { } map)
        {
            return;
        }

        var job = ManagerJob_Production.FindOwningJob(
            Manager.For(map).JobTracker.JobsOfType<ManagerJob_Production>(),
            j => j.ManagedBills,
            bill
        );
        if (job == null)
        {
            return;
        }

        var iconRect = new Rect(__result.xMax - 24f, __result.y + 2f, 20f, 20f);
        GUI.color = Color.white;
        GUI.DrawTexture(iconRect, Resources.ManagedByColonyManagerIcon);
        TooltipHandler.TipRegion(
            iconRect,
            "ColonyManagerRedux.Production.ManagedByJob".Translate(job.Recipe!.LabelCap)
        );
    }

    // Vanilla's decompiled source shows e.g. `Widgets.ButtonImage(rect2, TexButton.ReorderUp, color)`,
    // but that hides two trailing default-valued args (doMouseoverSound, tooltip) baked into the call
    // site at compile time — the actual overloads called are the 5-arg and 6-arg ones below, confirmed
    // via get_il on Bill.DoInterface. AccessTools.Method silently returns null for a signature that
    // doesn't match any overload, and Transpiler passing that null into CodeInstruction.Calls throws.
    //
    // ReorderUp, ReorderDown and Suspend all go through this exact same 5-arg overload (confirmed via
    // get_il — same MethodInfo token at all three call sites), so a global call-site redirect can't
    // tell them apart by signature alone. Reordering doesn't override anything a Production job
    // manages, so it should stay usable even on a managed bill; only suppress Suspend.
    private static bool RedirectedButtonImage(
        Rect butRect,
        Texture2D tex,
        Color baseColor,
        bool doMouseoverSound,
        string? tooltip
    ) =>
        (tex == TexButton.ReorderUp || tex == TexButton.ReorderDown || !SuppressManagedBillControls)
        && Widgets.ButtonImage(butRect, tex, baseColor, doMouseoverSound, tooltip);

    private static bool RedirectedButtonImage(
        Rect butRect,
        Texture2D tex,
        Color baseColor,
        Color mouseoverColor,
        bool doMouseoverSound,
        string? tooltip
    ) =>
        !SuppressManagedBillControls
        && Widgets.ButtonImage(butRect, tex, baseColor, mouseoverColor, doMouseoverSound, tooltip);

    private static bool RedirectedButtonImageFitted(Rect butRect, Texture2D tex, Color baseColor) =>
        !SuppressManagedBillControls && Widgets.ButtonImageFitted(butRect, tex, baseColor);

    // Suppressing the buttons above still leaves their trailing TooltipHandler.TipRegionByKey
    // calls in the IL (they're not part of the button-widget call itself), so a hover region
    // with the old "Delete"/"Suspend"/etc. tooltip lingers over the blank space. Redirect these
    // too so no vanilla tooltip shows there once the button is suppressed.
    private static void RedirectedTipRegionByKey(Rect rect, string key)
    {
        if (key is "ReorderBillUpTip" or "ReorderBillDownTip" || !SuppressManagedBillControls)
        {
            TooltipHandler.TipRegionByKey(rect, key);
        }
    }

    private static readonly MethodInfo TipRegionByKeyMethod = AccessTools.Method(
        typeof(TooltipHandler),
        nameof(TooltipHandler.TipRegionByKey),
        [typeof(Rect), typeof(string)]
    );

    private static readonly MethodInfo OurTipRegionByKey = AccessTools.Method(
        typeof(RimWorld_Bill_DoInterface),
        nameof(RedirectedTipRegionByKey)
    );

    private static readonly MethodInfo ButtonImage3 = AccessTools.Method(
        typeof(Widgets),
        nameof(Widgets.ButtonImage),
        [typeof(Rect), typeof(Texture2D), typeof(Color), typeof(bool), typeof(string)]
    );

    private static readonly MethodInfo ButtonImage4 = AccessTools.Method(
        typeof(Widgets),
        nameof(Widgets.ButtonImage),
        [
            typeof(Rect),
            typeof(Texture2D),
            typeof(Color),
            typeof(Color),
            typeof(bool),
            typeof(string),
        ]
    );

    private static readonly MethodInfo ButtonImageFittedMethod = AccessTools.Method(
        typeof(Widgets),
        nameof(Widgets.ButtonImageFitted),
        [typeof(Rect), typeof(Texture2D), typeof(Color)]
    );

    private static readonly MethodInfo OurButtonImage3 = AccessTools.Method(
        typeof(RimWorld_Bill_DoInterface),
        nameof(RedirectedButtonImage),
        [typeof(Rect), typeof(Texture2D), typeof(Color), typeof(bool), typeof(string)]
    );

    private static readonly MethodInfo OurButtonImage4 = AccessTools.Method(
        typeof(RimWorld_Bill_DoInterface),
        nameof(RedirectedButtonImage),
        [
            typeof(Rect),
            typeof(Texture2D),
            typeof(Color),
            typeof(Color),
            typeof(bool),
            typeof(string),
        ]
    );

    private static readonly MethodInfo OurButtonImageFitted = AccessTools.Method(
        typeof(RimWorld_Bill_DoInterface),
        nameof(RedirectedButtonImageFitted)
    );

    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        if (
            ButtonImage3 == null
            || ButtonImage4 == null
            || ButtonImageFittedMethod == null
            || TipRegionByKeyMethod == null
            || OurButtonImage3 == null
            || OurButtonImage4 == null
            || OurButtonImageFitted == null
            || OurTipRegionByKey == null
        )
        {
            ColonyManagerReduxMod.Instance.LogError(
                "Could not patch Bill.DoInterface to suppress managed-bill controls, "
                    + "one or more expected Widgets.ButtonImage(Fitted)/TooltipHandler.TipRegionByKey overloads was not found."
            );
            return instructions;
        }

        return instructions.Select(instruction =>
        {
            if (instruction.Calls(ButtonImage3))
            {
                instruction.operand = OurButtonImage3;
            }
            else if (instruction.Calls(ButtonImage4))
            {
                instruction.operand = OurButtonImage4;
            }
            else if (instruction.Calls(ButtonImageFittedMethod))
            {
                instruction.operand = OurButtonImageFitted;
            }
            else if (instruction.Calls(TipRegionByKeyMethod))
            {
                instruction.operand = OurTipRegionByKey;
            }
            return instruction;
        });
    }
}
