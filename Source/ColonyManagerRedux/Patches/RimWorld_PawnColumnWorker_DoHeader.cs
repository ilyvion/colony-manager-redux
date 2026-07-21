// RimWorld_PawnColumnWorker_DoHeader.cs
// Copyright (c) 2024–2026 Alexander Krivács Schrøder

using System.Reflection.Emit;

namespace ColonyManagerRedux;

/// <summary>
/// Extension methods for customizing the header rendering of PawnColumnWorker columns in RimWorld pawn tables.
/// </summary>
public static class PawnColumnWorkerDoHeaderExtensions
{
    /// <summary>
    /// Allows custom label rendering in the header of a pawn column.
    /// </summary>
    /// <param name="pawnColumnWorker">The pawn column worker instance.</param>
    /// <param name="rect">The rectangle in which to draw.</param>
    /// <param name="table">The pawn table.</param>
    /// <param name="customDoHeaderAction">The custom action to perform for label rendering.</param>
    public static void CustomLabelDoHeader(
        this PawnColumnWorker pawnColumnWorker,
        Rect rect,
        PawnTable table,
        Action<Rect, string, PawnTable, PawnColumnWorker> customDoHeaderAction
    ) =>
        RimWorld_PawnColumnWorker_DoHeader.CustomLabelDoHeader(
            pawnColumnWorker,
            rect,
            table,
            customDoHeaderAction
        );

    /// <summary>
    /// Allows custom icon rendering in the header of a pawn column.
    /// </summary>
    /// <param name="pawnColumnWorker">The pawn column worker instance.</param>
    /// <param name="rect">The rectangle in which to draw.</param>
    /// <param name="table">The pawn table.</param>
    /// <param name="customDoHeaderAction">The custom action to perform for icon rendering.</param>
    public static void CustomIconDoHeader(
        this PawnColumnWorker pawnColumnWorker,
        Rect rect,
        PawnTable table,
        Action<Rect, Texture, PawnTable, PawnColumnWorker> customDoHeaderAction
    ) =>
        RimWorld_PawnColumnWorker_DoHeader.CustomIconDoHeader(
            pawnColumnWorker,
            rect,
            table,
            customDoHeaderAction
        );
}

[HarmonyPatch(typeof(PawnColumnWorker), nameof(PawnColumnWorker.DoHeader))]
internal static class RimWorld_PawnColumnWorker_DoHeader
{
    private static readonly MethodInfo Widgets_Label_MethodInfo = AccessTools.Method(
        typeof(Widgets),
        nameof(Widgets.Label),
        [typeof(Rect), typeof(string)]
    );

    private static readonly MethodInfo Action_Invoke = AccessTools.Method(
        typeof(Action<Rect, string, PawnTable, PawnColumnWorker>),
        nameof(Action.Invoke)
    );

    [HarmonyReversePatch]
    internal static void CustomLabelDoHeader(
        PawnColumnWorker @this,
        Rect rect,
        PawnTable table,
        Action<Rect, string, PawnTable, PawnColumnWorker> customDoHeaderAction
    )
    {
#pragma warning disable IDE0062 // Make local function 'static'
        IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator generator
        )
        {
            var original = instructions.ToList();

            var codeMatcher = new CodeMatcher(original, generator);

            _ = codeMatcher.SearchForward(i => i.opcode == OpCodes.Brtrue_S);
            codeMatcher.Instruction.opcode = OpCodes.Pop;
            codeMatcher.Instruction.operand = null;

            _ = codeMatcher.SearchForward(i =>
                i.opcode == OpCodes.Call
                && i.operand is MethodInfo m
                && m == Widgets_Label_MethodInfo
            );
            if (!codeMatcher.IsValid)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    "Could not reverse patch PawnColumnWorker.DoHeader, "
                        + "IL does not match expectations: call to Widgets.Label not found."
                );
                return original;
            }

            _ = codeMatcher.RemoveInstruction();

            _ = codeMatcher.Insert([
                new(OpCodes.Ldarg_2),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Callvirt, Action_Invoke),
            ]);

            _ = codeMatcher.SearchBackwards(i => i.opcode == OpCodes.Ldloc_0);
            if (!codeMatcher.IsValid)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    "Could not reverse patch PawnColumnWorker.DoHeader, "
                        + "IL does not match expectations: [ldloc.0] not found."
                );
                return original;
            }

            _ = codeMatcher.Insert([new(OpCodes.Ldarg_3)]);

            return codeMatcher.Instructions();
        }
#pragma warning restore IDE0062 // Make local function 'static'

        // Make compiler happy. This gets patched out anyway.
        _ = @this;
        _ = rect;
        _ = table;
        _ = customDoHeaderAction;
        _ = Transpiler(null!, null!);
    }

    private static readonly MethodInfo GUI_DrawTexture_MethodInfo = AccessTools.Method(
        typeof(GUI),
        nameof(GUI.DrawTexture),
        [typeof(Rect), typeof(Texture)]
    );

    private static readonly MethodInfo Action_Invoke2 = AccessTools.Method(
        typeof(Action<Rect, Texture, PawnTable, PawnColumnWorker>),
        nameof(Action.Invoke)
    );

    [HarmonyReversePatch]
    internal static void CustomIconDoHeader(
        PawnColumnWorker @this,
        Rect rect,
        PawnTable table,
        Action<Rect, Texture, PawnTable, PawnColumnWorker> customDoHeaderAction
    )
    {
#pragma warning disable IDE0062 // Make local function 'static'
        IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions,
            ILGenerator generator
        )
        {
            var original = instructions.ToList();

            var codeMatcher = new CodeMatcher(original, generator);

            _ = codeMatcher.SearchForward(i => i.opcode == OpCodes.Brtrue_S);
            codeMatcher.Instruction.opcode = OpCodes.Br;
            _ = codeMatcher.Insert([new(OpCodes.Pop)]);

            _ = codeMatcher.SearchForward(i => i.opcode == OpCodes.Brfalse_S);
            codeMatcher.Instruction.opcode = OpCodes.Pop;
            codeMatcher.Instruction.operand = null;

            _ = codeMatcher.SearchForward(i =>
                i.opcode == OpCodes.Call
                && i.operand is MethodInfo m
                && m == GUI_DrawTexture_MethodInfo
            );
            if (!codeMatcher.IsValid)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    "Could not reverse patch PawnColumnWorker.DoHeader, "
                        + "IL does not match expectations: call to Widgets.Label not found."
                );
                return original;
            }

            _ = codeMatcher.RemoveInstruction();

            _ = codeMatcher.Insert([
                new(OpCodes.Ldarg_2),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Callvirt, Action_Invoke2),
            ]);

            _ = codeMatcher.SearchBackwards(i => i.opcode == OpCodes.Stloc_3);
            if (!codeMatcher.IsValid)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    "Could not reverse patch PawnColumnWorker.DoHeader, "
                        + "IL does not match expectations: [ldloc.0] not found."
                );
                return original;
            }
            _ = codeMatcher.Advance(1);

            _ = codeMatcher.Insert([new(OpCodes.Ldarg_3)]);

            return codeMatcher.Instructions();
        }
#pragma warning restore IDE0062 // Make local function 'static'

        // Make compiler happy. This gets patched out anyway.
        _ = @this;
        _ = rect;
        _ = table;
        _ = customDoHeaderAction;
        _ = Transpiler(null!, null!);
    }
}
