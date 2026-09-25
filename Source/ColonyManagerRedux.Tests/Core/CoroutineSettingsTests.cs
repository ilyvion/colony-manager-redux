// CoroutineSettingsTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// Every <see cref="CoroutineSettingsMethodAttribute"/> method must own one setting key, and
/// that key must be the one <see cref="Settings"/> derives from a delegate to the method.
/// </summary>
[TestFixture(TestType.MainMenu)]
internal sealed class CoroutineSettingsTests
{
    private const BindingFlags AllDeclared =
        BindingFlags.DeclaredOnly
        | BindingFlags.Public
        | BindingFlags.NonPublic
        | BindingFlags.Instance
        | BindingFlags.Static;

    private static List<MethodInfo> AttributedMethods() =>
        [
            .. CoroutineSettingsTypeAttribute
                .AllTypesWithAttribute.SelectMany(type => type.GetMethods(AllDeclared))
                .Where(method => method.HasAttribute<CoroutineSettingsMethodAttribute>()),
        ];

    private static string Key(MethodInfo method) =>
        $"{method.DeclaringType.FullName}.{method.Name}";

    [Test]
    public static void CoroutineSettingsMethodsExist() =>
        Expect.GreaterThan(AttributedMethods().Count, 0);

    [Test]
    public static void EverySettingKeyIsUnique()
    {
        var duplicates = AttributedMethods()
            .GroupBy(Key)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        Expect.AreEqual("", string.Join(", ", duplicates), "keys shared by several methods");
    }

    [Test]
    public static void EveryAttributedMethodReturnsACoroutine()
    {
        var wrong = AttributedMethods()
            .Where(method => method.ReturnType != typeof(Coroutine))
            .Select(Key)
            .ToList();

        Expect.AreEqual("", string.Join(", ", wrong), "methods not returning Coroutine");
    }

    [Test]
    public static void ADelegateResolvesToTheAttributedMethodsKey()
    {
        var method = typeof(ManagerJob_Power.HistoryWorker).GetMethod(
            nameof(ManagerJob_Power.HistoryWorker.HistoryUpdateCoroutine),
            [typeof(ManagerJob_Power), typeof(int)]
        );
        var key = Key(method);
        var settings = new Settings
        {
            ShowAdvancedPerformanceSettings = true,
            TicksBetweenOperations = 3,
        };
        settings._coroutineTicksBetweenOperations[key] = 11;

        var worker = new ManagerJob_Power.HistoryWorker();
        var resolved = settings.GetTicksBetweenOperationsForCoroutine(
            (Func<ManagerJob_Power, int, Coroutine>)worker.HistoryUpdateCoroutine
        );

        Expect.AreEqual(11, resolved);
    }

    [Test]
    public static void NullDelegatesAreRejected()
    {
        var settings = new Settings();
        var threwOperations = false;
        var threwTicks = false;
        try
        {
            _ = settings.GetOperationsPerTickForCoroutine(null!);
        }
        catch (ArgumentNullException)
        {
            threwOperations = true;
        }
        try
        {
            _ = settings.GetTicksBetweenOperationsForCoroutine(null!);
        }
        catch (ArgumentNullException)
        {
            threwTicks = true;
        }

        Expect.IsTrue(threwOperations, "operations per tick");
        Expect.IsTrue(threwTicks, "ticks between operations");
    }
}
