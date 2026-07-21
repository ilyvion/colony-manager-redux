// Managers_Patches.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers.Patches;

[StaticConstructorOnStartup]
internal static class Managers_Patches
{
    static Managers_Patches()
    {
        var harmony = new Harmony($"{ColonyManagerReduxMod.PackageId}.Managers");
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }
}
