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
        if (
            !ShouldWarn(hasManagerDatabase, manager.JobTracker.JobList.Count(j => j.IsTransferable))
        )
        {
            return text;
        }
        text +=
            "\n\n"
            + ("GravEngineWarning".Translate() + ": ").Colorize(ColorLibrary.RedReadable)
            + "ColonyManagerRedux.Misc.NoManagerDatabaseOnShip".Translate().Resolve();
        return text;
    }

    /// <summary>
    /// Whether launching should warn that the ship would leave its jobs behind: it has no manager
    /// database, yet the map has jobs that would have travelled with one.
    /// </summary>
    internal static bool ShouldWarn(bool hasManagerDatabase, int transferableJobCount) =>
        !hasManagerDatabase && transferableJobCount > 0;

    internal static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    ) =>
        TranspilerUtility.PatchOrOriginal(
            instructions,
            "patch GravshipUtility.PreLaunchConfirmation",
            ColonyManagerReduxMod.Instance.LogError,
            codeMatcher =>
            {
                _ = codeMatcher.End();

                _ = codeMatcher.ExpectBackwards(
                    i =>
                        i.opcode == OpCodes.Call
                        && i.operand is MethodInfo m
                        && m == Find_WindowStack_MethodInfo,
                    "call to Find.WindowStack"
                );
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
            },
            generator
        );
}
#endif // v1_6
