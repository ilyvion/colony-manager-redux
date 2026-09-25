// PawnColumnHeaderTranspilerTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using System.Reflection.Emit;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The transpilers that turn vanilla <c>PawnColumnWorker.DoHeader</c> into the custom-label and
/// custom-icon header drawers.
/// </summary>
[TestFixture(TestType.MainMenu)]
internal sealed class PawnColumnHeaderTranspilerTests
{
    private static readonly MethodInfo DoHeader = AccessTools.Method(
        typeof(PawnColumnWorker),
        nameof(PawnColumnWorker.DoHeader)
    );

    private static readonly MethodInfo WidgetsLabel = AccessTools.Method(
        typeof(Widgets),
        nameof(Widgets.Label),
        [typeof(Rect), typeof(string)]
    );

    private static readonly MethodInfo GuiDrawTexture = AccessTools.Method(
        typeof(GUI),
        nameof(GUI.DrawTexture),
        [typeof(Rect), typeof(Texture)]
    );

    private static List<CodeInstruction> Original() =>
        PatchProcessor.GetOriginalInstructions(DoHeader);

    private static ILGenerator Generator() =>
        new DynamicMethod("test", typeof(void), Type.EmptyTypes).GetILGenerator();

    private static List<CodeInstruction> Label(List<CodeInstruction> instructions) =>
        [.. RimWorld_PawnColumnWorker_DoHeader.LabelTranspiler(instructions, Generator())];

    private static List<CodeInstruction> Icon(List<CodeInstruction> instructions) =>
        [.. RimWorld_PawnColumnWorker_DoHeader.IconTranspiler(instructions, Generator())];

    private static bool Calls(List<CodeInstruction> instructions, MethodInfo method) =>
        instructions.Any(i => i.opcode == OpCodes.Call && i.operand is MethodInfo m && m == method);

    private static int CallCount(List<CodeInstruction> instructions, MethodInfo method) =>
        instructions.Count(i =>
            i.opcode == OpCodes.Call && i.operand is MethodInfo m && m == method
        );

    private static bool InvokesAction(List<CodeInstruction> instructions, Type actionType) =>
        instructions.Any(i =>
            i.opcode == OpCodes.Callvirt
            && i.operand is MethodInfo m
            && m.DeclaringType == actionType
            && m.Name == "Invoke"
        );

    [Test]
    public static void VanillaHeaderCallsLabelAndDrawTexture()
    {
        var original = Original();

        Expect.IsTrue(Calls(original, WidgetsLabel), "Widgets.Label");
        Expect.IsTrue(Calls(original, GuiDrawTexture), "GUI.DrawTexture");
    }

    [Test]
    public static void LabelTranspilerReplacesTheLabelCallWithTheAction()
    {
        var original = Original();

        var result = Label(original);

        Expect.IsFalse(Calls(result, WidgetsLabel), "Widgets.Label removed");
        Expect.IsTrue(
            InvokesAction(result, typeof(Action<Rect, string, PawnTable, PawnColumnWorker>)),
            "Action invoked"
        );
        // Removes one call, inserts ldarg.2, ldarg.0, callvirt and ldarg.3.
        Expect.AreEqual(original.Count + 3, result.Count);
    }

    [Test]
    public static void LabelTranspilerLoadsTheActionBeforeItsArguments()
    {
        var result = Label(Original());

        var invoke = result.FindIndex(i =>
            i.opcode == OpCodes.Callvirt
            && i.operand is MethodInfo m
            && m.DeclaringType == typeof(Action<Rect, string, PawnTable, PawnColumnWorker>)
        );
        Expect.AreEqual(OpCodes.Ldarg_0, result[invoke - 1].opcode);
        Expect.AreEqual(OpCodes.Ldarg_2, result[invoke - 2].opcode);
        var actionLoad = result.FindLastIndex(invoke, i => i.opcode == OpCodes.Ldarg_3);
        Expect.IsTrue(actionLoad >= 0 && actionLoad < invoke - 2, "Action loaded first");
    }

    [Test]
    public static void IconTranspilerReplacesTheDrawTextureCallWithTheAction()
    {
        var original = Original();

        var result = Icon(original);

        Expect.AreEqual(
            CallCount(original, GuiDrawTexture) - 1,
            CallCount(result, GuiDrawTexture),
            "One GUI.DrawTexture call replaced"
        );
        Expect.IsTrue(
            InvokesAction(result, typeof(Action<Rect, Texture, PawnTable, PawnColumnWorker>)),
            "Action invoked"
        );
    }

    [Test]
    public static void TranspilersLeaveTheirInputInstructionsUntouched()
    {
        var instructions = Original();
        var before = instructions.Select(i => (i.opcode, i.operand)).ToList();

        _ = Label(instructions);
        _ = Icon(instructions);

        Expect.IsTrue(
            instructions.Select(i => (i.opcode, i.operand)).SequenceEqual(before),
            "Input list is unchanged"
        );
    }

    private const string BrtrueError = "brtrue.s";

    [Test]
    [ErrorsAllowed(BrtrueError)]
    public static void LabelTranspilerReturnsTheOriginalWithoutABranch()
    {
        var result = Label([new(OpCodes.Nop), new(OpCodes.Ret)]);

        Expect.AreEqual(2, result.Count);
        Expect.AreEqual(OpCodes.Nop, result[0].opcode);
        Expect.AreEqual(OpCodes.Ret, result[1].opcode);
    }

    [Test]
    [ErrorsAllowed(BrtrueError)]
    public static void IconTranspilerReturnsTheOriginalWithoutABranch()
    {
        var result = Icon([new(OpCodes.Nop), new(OpCodes.Ret)]);

        Expect.AreEqual(2, result.Count);
        Expect.AreEqual(OpCodes.Nop, result[0].opcode);
    }

    [Test]
    [ErrorsAllowed("call to Widgets.Label not found")]
    public static void LabelTranspilerReturnsTheUnmodifiedOriginalWithoutTheLabelCall()
    {
        var generator = Generator();
        var target = generator.DefineLabel();
        List<CodeInstruction> instructions =
        [
            new(OpCodes.Ldarg_0),
            new(OpCodes.Brtrue_S, target),
            new(OpCodes.Nop) { labels = [target] },
            new(OpCodes.Ret),
        ];

        var result = Label(instructions);

        Expect.AreEqual(4, result.Count);
        Expect.AreEqual(OpCodes.Brtrue_S, result[1].opcode);
    }

    [Test]
    [ErrorsAllowed("call to GUI.DrawTexture not found")]
    public static void IconTranspilerReturnsTheUnmodifiedOriginalWithoutTheDrawCall()
    {
        var generator = Generator();
        var target = generator.DefineLabel();
        List<CodeInstruction> instructions =
        [
            new(OpCodes.Ldarg_0),
            new(OpCodes.Brtrue_S, target),
            new(OpCodes.Ldarg_1),
            new(OpCodes.Brfalse_S, target),
            new(OpCodes.Nop) { labels = [target] },
            new(OpCodes.Ret),
        ];

        var result = Icon(instructions);

        Expect.AreEqual(6, result.Count);
        Expect.AreEqual(OpCodes.Brtrue_S, result[1].opcode);
        Expect.AreEqual(OpCodes.Brfalse_S, result[3].opcode);
    }
}
