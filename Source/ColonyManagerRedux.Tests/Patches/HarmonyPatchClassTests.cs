// HarmonyPatchClassTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// Every <c>[HarmonyPatch]</c> class in the mod's assemblies contributes at least one patch
/// method, and every such method is installed by Harmony.
/// </summary>
[TestFixture(TestType.MainMenu)]
internal sealed class HarmonyPatchClassTests
{
    private static readonly string[] _patchMethodNames =
    [
        "Prefix",
        "Postfix",
        "Transpiler",
        "Finalizer",
        "ReversePatch",
    ];

    private static readonly Type[] _patchMethodAttributes =
    [
        typeof(HarmonyPrefix),
        typeof(HarmonyPostfix),
        typeof(HarmonyTranspiler),
        typeof(HarmonyFinalizer),
        typeof(HarmonyReversePatch),
    ];

    private const BindingFlags AllDeclared =
        BindingFlags.Public
        | BindingFlags.NonPublic
        | BindingFlags.Static
        | BindingFlags.Instance
        | BindingFlags.DeclaredOnly;

    private static IEnumerable<Assembly> ModAssemblies() =>
        AppDomain
            .CurrentDomain.GetAssemblies()
            .Where(a =>
            {
                var name = a.GetName().Name;
                return name.StartsWith("ColonyManagerRedux", StringComparison.Ordinal)
                    && !name.EndsWith(".Tests", StringComparison.Ordinal);
            });

    private static IEnumerable<Type> PatchClasses() =>
        ModAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsDefined(typeof(HarmonyPatch), inherit: false));

    // Reverse patches are applied to the patch method itself and do not show up in
    // Harmony.GetPatchInfo, so only the other kinds can be checked for installation.
    private static IEnumerable<MethodInfo> InstalledPatchMethods(Type type) =>
        PatchMethods(type)
            .Where(m =>
                m.Name != "ReversePatch"
                && !m.IsDefined(typeof(HarmonyReversePatch), inherit: false)
            );

    private static IEnumerable<MethodInfo> PatchMethods(Type type) =>
        type.GetMethods(AllDeclared)
            .Where(m =>
                _patchMethodNames.Contains(m.Name)
                || _patchMethodAttributes.Any(a => m.IsDefined(a, inherit: false))
            );

    [Test]
    public static void CoreAndManagersAssembliesAreScanned()
    {
        var names = ModAssemblies().Select(a => a.GetName().Name).ToList();

        Expect.IsTrue(names.Contains("ColonyManagerRedux"), "core assembly");
        Expect.IsTrue(names.Contains("ColonyManagerRedux.Managers"), "managers assembly");
        Expect.IsNotEmpty(PatchClasses(), "patch classes found");
    }

    [Test]
    public static void EveryPatchClassDeclaresAPatchMethod()
    {
        var offenders = PatchClasses()
            .Where(t => !PatchMethods(t).Any())
            .Select(t => t.FullName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Expect.IsTrue(
            offenders.Count == 0,
            $"patch classes without a patch method: {string.Join(", ", offenders)}"
        );
    }

    [Test]
    public static void EveryPatchClassHasAnInstalledPatch()
    {
        var installedOwners = Harmony
            .GetAllPatchedMethods()
            .Select(Harmony.GetPatchInfo)
            .SelectMany(info =>
                info.Prefixes.Concat(info.Postfixes)
                    .Concat(info.Transpilers)
                    .Concat(info.Finalizers)
            )
            .Select(p => p.PatchMethod.DeclaringType)
            .ToHashSet();

        var offenders = PatchClasses()
            .Where(t => InstalledPatchMethods(t).Any() && !installedOwners.Contains(t))
            .Select(t => t.FullName)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();

        Expect.IsTrue(
            offenders.Count == 0,
            $"patch classes with no installed patch: {string.Join(", ", offenders)}"
        );
    }
}
