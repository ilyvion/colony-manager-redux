// GravshipLaunchConfirmationTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

#if !v1_5
using System.Reflection.Emit;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The launch-confirmation warning rule and the transpiler that inserts it into vanilla's
/// <c>GravshipUtility.PreLaunchConfirmation</c>.
/// </summary>
[TestFixture(TestType.MainMenu)]
internal sealed class GravshipLaunchConfirmationTests
{
    private const string TranspilerError = "call to Find.WindowStack not found";

    private static readonly MethodInfo PreLaunchConfirmation = AccessTools.Method(
        typeof(GravshipUtility),
        nameof(GravshipUtility.PreLaunchConfirmation)
    );

    private static List<CodeInstruction> Original() =>
        PatchProcessor.GetOriginalInstructions(PreLaunchConfirmation);

    private static List<CodeInstruction> Transpile(List<CodeInstruction> instructions) =>
        [
            .. RimWorld_GravshipUtility_PreLaunchConfirmation.Transpiler(
                instructions,
                new DynamicMethod("test", typeof(void), Type.EmptyTypes).GetILGenerator()
            ),
        ];

    [Test]
    public static void WarnsOnlyWithoutADatabaseAndWithTransferableJobs()
    {
        Expect.IsTrue(RimWorld_GravshipUtility_PreLaunchConfirmation.ShouldWarn(false, 1));
        Expect.IsFalse(RimWorld_GravshipUtility_PreLaunchConfirmation.ShouldWarn(true, 1));
        Expect.IsFalse(RimWorld_GravshipUtility_PreLaunchConfirmation.ShouldWarn(false, 0));
        Expect.IsFalse(RimWorld_GravshipUtility_PreLaunchConfirmation.ShouldWarn(true, 0));
    }

    [Test]
    public static void TranspilerInsertsFourInstructionsCallingTheTextMethod()
    {
        var original = Original();

        var result = Transpile(original);

        Expect.AreEqual(original.Count + 4, result.Count);
        var call = result.Single(i =>
            i.opcode == OpCodes.Call
            && i.operand is MethodInfo m
            && m.Name == "AddColonyManagerReduxLaunchConfirmationText"
        );
        var index = result.IndexOf(call);
        Expect.AreEqual(OpCodes.Ldloc_0, result[index - 2].opcode);
        Expect.AreEqual(OpCodes.Ldarg_0, result[index - 1].opcode);
        Expect.AreEqual(OpCodes.Stloc_0, result[index + 1].opcode);
        // Inserted directly after the last Find.WindowStack getter call.
        Expect.IsTrue(
            result[index - 3].operand is MethodInfo w
                && w == AccessTools.PropertyGetter(typeof(Find), nameof(Find.WindowStack))
        );
    }

    [Test]
    public static void InsertedInstructionsMatchTheLocalAndArgumentTypes()
    {
        // The inserted code loads local 0 as the text and argument 0 as the engine.
        var locals = (
            PreLaunchConfirmation.GetMethodBody()
            ?? throw new InvalidOperationException("PreLaunchConfirmation has no method body")
        ).LocalVariables;
        Expect.AreEqual(typeof(TaggedString), locals[0].LocalType);
        Expect.AreEqual(
            typeof(Building_GravEngine),
            PreLaunchConfirmation.GetParameters()[0].ParameterType
        );
        Expect.IsTrue(PreLaunchConfirmation.IsStatic);
    }

    [Test]
    [ErrorsAllowed(TranspilerError)]
    public static void TranspilerReturnsTheOriginalWhenNoWindowStackCallExists()
    {
        List<CodeInstruction> instructions = [new(OpCodes.Nop), new(OpCodes.Ret)];

        var result = Transpile(instructions);

        Expect.AreEqual(2, result.Count);
        Expect.AreEqual(OpCodes.Nop, result[0].opcode);
        Expect.AreEqual(OpCodes.Ret, result[1].opcode);
    }
}
#endif // !v1_5
