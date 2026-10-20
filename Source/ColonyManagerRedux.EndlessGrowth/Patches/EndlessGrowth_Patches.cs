// EndlessGrowth_Patches.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using SlimeSenpai.EndlessGrowth;

namespace ColonyManagerRedux.EndlessGrowth.Patches;

[StaticConstructorOnStartup]
internal static class EndlessGrowth_Patches
{
    static EndlessGrowth_Patches()
    {
        UseEndlessGrowthMaxLevel();

        var harmony = new Harmony($"{ColonyManagerReduxMod.PackageId}.EndlessGrowth");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }

    private static void UseEndlessGrowthMaxLevel() =>
        ManagerJob_Production.MaxAllowedSkillLevel = CommonPatches.GetMaxLevelForBill;
}

/// <summary>
/// EndlessGrowth raises the maximum of every bill whose maximum sits at the old cap when a pawn
/// reaches a new highest skill level; this does the same for production jobs.
/// </summary>
[HarmonyPatch(typeof(CommonPatches), nameof(CommonPatches.CheckMaxLevel))]
internal static class EndlessGrowth_CommonPatches_CheckMaxLevel_Patches
{
    internal static void Prefix(out int __state) => __state = CommonPatches.GetMaxLevelForBill();

    internal static void Postfix(int __state)
    {
        var newCap = CommonPatches.GetMaxLevelForBill();
        if (newCap == __state || Current.Game == null)
        {
            return;
        }

        foreach (var map in Find.Maps)
        {
            var manager = map.GetComponent<Manager>();
            if (manager == null)
            {
                continue;
            }

            foreach (var job in manager.JobTracker.JobsOfType<ManagerJob_Production>())
            {
                job.OnMaxAllowedSkillLevelChanged(__state, newCap);
            }
        }
    }
}
