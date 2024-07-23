// RimWorld_PawnColumnWorker_DoHeader.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Reflection;
using System.Reflection.Emit;

namespace ColonyManagerRedux;

[HarmonyPatch(typeof(PawnColumnWorker), nameof(PawnColumnWorker.DoHeader))]
internal static class RimWorld_PawnColumnWorker_DoHeader
{
    private static readonly MethodInfo Widgets_Label_MethodInfo
        = AccessTools.Method(typeof(Widgets), nameof(Widgets.Label), [typeof(Rect), typeof(string)]);

    private static readonly MethodInfo Action_Invoke
        = AccessTools.Method(typeof(Action<Rect, string, PawnTable, PawnColumnWorker_Label>), nameof(Action.Invoke));

    [HarmonyReversePatch]
    internal static void CustomLabelDoHeader(PawnColumnWorker_Label @this, Rect rect, PawnTable table, Action<Rect, string, PawnTable, PawnColumnWorker_Label> customDoHeaderAction)
    {
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var original = instructions.ToList();

            var codeMatcher = new CodeMatcher(original, generator);

            codeMatcher.SearchForward(i => i.opcode == OpCodes.Brtrue_S);
            codeMatcher.Instruction.opcode = OpCodes.Pop;
            codeMatcher.Instruction.operand = null;

            codeMatcher.SearchForward(i => i.opcode == OpCodes.Call && i.operand is MethodInfo m && m == Widgets_Label_MethodInfo);
            if (!codeMatcher.IsValid)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    "Could not reverse patch PawnColumnWorker.DoHeader, " +
                    "IL does not match expectations: call to Widgets.Label not found.");
                return original;
            }

            codeMatcher.RemoveInstruction();

            codeMatcher.Insert([
                new(OpCodes.Ldarg_2),
                new(OpCodes.Ldarg_0),
                new(OpCodes.Callvirt, Action_Invoke),
            ]);

            codeMatcher.SearchBackwards(i => i.opcode == OpCodes.Ldloc_0);
            if (!codeMatcher.IsValid)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    "Could not reverse patch PawnColumnWorker.DoHeader, " +
                    "IL does not match expectations: [ldloc.0] not found.");
                return original;
            }

            codeMatcher.Insert([
                new(OpCodes.Ldarg_3),
            ]);

            return codeMatcher.Instructions();
        }

        // Make compiler happy. This gets patched out anyway.
        _ = @this;
        _ = rect;
        _ = table;
        _ = customDoHeaderAction;
        Transpiler(null!, null!);
    }
}
