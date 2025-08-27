// RimWorld_GravshipUtility_PreLaunchConfirmation.cs
// Copyright (c) 2025 Alexander Krivács Schrøder

#if !v1_5
using System.Reflection.Emit;

namespace ColonyManagerRedux;

[HarmonyPatch(typeof(GravshipUtility), nameof(GravshipUtility.PreLaunchConfirmation))]
internal static class RimWorld_GravshipUtility_PreLaunchConfirmation
{
    private static readonly MethodInfo Find_WindowStack_MethodInfo = AccessTools.PropertyGetter(
        typeof(Find),
        nameof(Find.WindowStack)
    );

    private static readonly MethodInfo _methodAddColonyManagerReduxLaunchConfirmationText =
        AccessTools.Method(
            typeof(RimWorld_GravshipUtility_PreLaunchConfirmation),
            nameof(AddColonyManagerReduxLaunchConfirmationText)
        );

    private static TaggedString AddColonyManagerReduxLaunchConfirmationText(
        TaggedString text,
        Building_GravEngine gravEngine
    )
    {
        var hasManagerDatabase = gravEngine.ManagerDatabase() != null;
        var manager = Manager.For(gravEngine.Map);
        if (hasManagerDatabase || manager.JobTracker.JobList.Count <= 0)
        {
            return text;
        }
        text +=
            "\n\n"
            + ("GravEngineWarning".Translate() + ": ").Colorize(ColorLibrary.RedReadable)
            + "ColonyManagerRedux.Misc.NoManagerDatabaseOnShip".Translate().Resolve();
        return text;
    }

    internal static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        var original = instructions.ToList();

        var codeMatcher = new CodeMatcher(original, generator);

        _ = codeMatcher.End();

        _ = codeMatcher.SearchBackwards(i =>
            i.opcode == OpCodes.Call
            && i.operand is MethodInfo m
            && m == Find_WindowStack_MethodInfo
        );
        if (!codeMatcher.IsValid)
        {
            ColonyManagerReduxMod.Instance.LogError(
                "Could not patch GravshipUtility.PreLaunchConfirmation, "
                    + "IL does not match expectations: call to Find.WindowStack not found."
            );
            return original;
        }
        _ = codeMatcher.Advance(1);

        // Insert
        //   text = AddColonyManagerReduxLaunchConfirmationText(text);
        // directly before
        _ = codeMatcher.Insert(
            new(OpCodes.Ldloc_0),
            new(OpCodes.Ldarg_0),
            new(OpCodes.Call, _methodAddColonyManagerReduxLaunchConfirmationText),
            new(OpCodes.Stloc_0)
        );

        return codeMatcher.Instructions();
    }
}
#endif // v1_6
