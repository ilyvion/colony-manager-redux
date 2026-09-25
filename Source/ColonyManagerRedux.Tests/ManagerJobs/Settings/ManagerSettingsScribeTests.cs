// ManagerSettingsScribeTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// Reflection-driven save/load checks for every <see cref="ManagerSettings"/> and
/// <see cref="ManagerDefaultSettings"/> subclass: each public setting must survive a round trip,
/// and a document that lacks the setting must load to the same value a fresh instance has.
/// </summary>
[TestFixture(TestType.MainMenu)]
internal sealed class ManagerSettingsScribeTests
{
    private static IEnumerable<FieldInfo> SettingFields(object target) =>
        target
            .GetType()
            .GetFields(BindingFlags.Public | BindingFlags.Instance)
            .Where(f => !f.IsInitOnly);

    /// <summary>
    /// Returns a value of <paramref name="field"/>'s type that differs from
    /// <paramref name="current"/>.
    /// </summary>
    private static object NonDefaultValue(FieldInfo field, object? current)
    {
        var type = field.FieldType;
        if (type == typeof(bool))
        {
            return !(bool)current!;
        }
        if (type == typeof(int))
        {
            return (int)current! + 7;
        }
        if (type == typeof(float))
        {
            return (float)current! + 0.125f;
        }
        if (type.IsEnum)
        {
            return Enum.GetValues(type).Cast<object>().First(v => !v.Equals(current));
        }
        if (type == typeof(int[]))
        {
            return ((int[])current!).Select((v, i) => v + i + 1).ToArray();
        }
        if (type == typeof(HashSet<TrainableDef>))
        {
            return new HashSet<TrainableDef>(DefDatabase<TrainableDef>.AllDefsListForReading);
        }
        if (type == typeof(Dictionary<ThingDef, int>))
        {
            return new Dictionary<ThingDef, int> { [ThingDefOf.Steel] = 42, [ThingDefOf.Gold] = 7 };
        }
        if (
            type.IsGenericType
            && type.GetGenericTypeDefinition() == typeof(List<>)
            && type.GetGenericArguments()[0].IsEnum
        )
        {
            var all = Enum.GetValues(type.GetGenericArguments()[0]).Cast<object>().Reverse();
            var list = (IList)Activator.CreateInstance(type)!;
            foreach (var value in all)
            {
                _ = list.Add(value);
            }
            return list;
        }
        throw new NotSupportedException(
            $"No non-default value generator for {type} ({field.DeclaringType!.Name}.{field.Name}); add one."
        );
    }

    private static string Describe(object? value)
    {
        if (value is null)
        {
            return "null";
        }
        if (value is IDictionary dictionary)
        {
            return "{"
                + string.Join(
                    ", ",
                    dictionary
                        .Keys.Cast<object>()
                        .Select(k => $"{Describe(k)}={Describe(dictionary[k])}")
                        .OrderBy(s => s, StringComparer.Ordinal)
                )
                + "}";
        }
        if (value is IEnumerable enumerable and not string)
        {
            var items = enumerable.Cast<object?>().Select(Describe);
            return "["
                + string.Join(
                    ", ",
                    value is ISet<TrainableDef>
                        ? items.OrderBy(s => s, StringComparer.Ordinal)
                        : items
                )
                + "]";
        }
        return value is Def def
            ? def.defName
            : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
    }

    private static List<string> Differences(object expected, object actual)
    {
        List<string> differences = [];
        foreach (var field in SettingFields(expected))
        {
            var expectedText = Describe(field.GetValue(expected));
            var actualText = Describe(field.GetValue(actual));
            if (expectedText != actualText)
            {
                differences.Add(
                    $"{field.DeclaringType.Name}.{field.Name}: expected {expectedText}, got {actualText}"
                );
            }
        }
        return differences;
    }

    /// <summary>
    /// Sets every public setting of <paramref name="target"/> to a value that differs from its
    /// current one, and returns how many settings that was.
    /// </summary>
    private static int FillWithNonDefaults(object target)
    {
        var count = 0;
        foreach (var field in SettingFields(target))
        {
            var before = Describe(field.GetValue(target));
            field.SetValue(target, NonDefaultValue(field, field.GetValue(target)));
            Expect.AreNotEqual(
                before,
                Describe(field.GetValue(target)),
                $"{field.Name} was not changed by the fill"
            );
            count++;
        }
        return count;
    }

    private const string EmptyValueDocument =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?><root><value /></root>";

    /// <summary>
    /// Saves <paramref name="saved"/> and loads it back as a deep-saved object, so
    /// <c>PostLoadInit</c> runs like it does in a real save.
    /// </summary>
    private static T RoundTrip<T>(T saved)
        where T : class, IExposable
    {
        var loaded = new ScribeRoundTrip.DeepHarness<T>();
        ScribeRoundTrip.Run(new ScribeRoundTrip.DeepHarness<T> { Value = saved }, loaded);
        Expect.IsNotNull(loaded.Value);
        return loaded.Value!;
    }

    /// <summary>
    /// Loads a deep-saved <typeparamref name="T"/> from a node that holds none of its settings.
    /// </summary>
    private static T LoadWithNoSettingNodes<T>()
        where T : class, IExposable
    {
        var loaded = new ScribeRoundTrip.DeepHarness<T>();
        ScribeRoundTrip.Load(EmptyValueDocument, loaded);
        Expect.IsNotNull(loaded.Value);
        return loaded.Value!;
    }

    private static void ExpectSettingsRoundTrip<T>()
        where T : class, IExposable, new()
    {
        var saved = new T();
        Expect.GreaterThan(FillWithNonDefaults(saved), 0);
        Expect.AreEqual("", string.Join("; ", Differences(saved, RoundTrip(saved))));
    }

    private static void ExpectMissingNodesLoadFreshDefaults<T>()
        where T : class, IExposable, new() =>
        Expect.AreEqual("", string.Join("; ", Differences(new T(), LoadWithNoSettingNodes<T>())));

    [Test]
    public static void ForagingSettingsRoundTrip() =>
        ExpectSettingsRoundTrip<ManagerDefaultSettings_Foraging>();

    [Test]
    public static void ForestrySettingsRoundTrip() =>
        ExpectSettingsRoundTrip<ManagerDefaultSettings_Forestry>();

    [Test]
    public static void HuntingSettingsRoundTrip() =>
        ExpectSettingsRoundTrip<ManagerDefaultSettings_Hunting>();

    [Test]
    public static void MiningSettingsRoundTrip() =>
        ExpectSettingsRoundTrip<ManagerDefaultSettings_Mining>();

    [Test]
    public static void LogsSettingsRoundTrip() => ExpectSettingsRoundTrip<ManagerSettings_Logs>();

    [Test]
    public static void PowerSettingsRoundTrip() => ExpectSettingsRoundTrip<ManagerSettings_Power>();

    [Test]
    public static void ProductionSettingsRoundTrip() =>
        ExpectSettingsRoundTrip<ManagerSettings_Production>();

    [Test]
    public static void ForagingSettingsLoadFreshDefaultsWhenNodesAreMissing() =>
        ExpectMissingNodesLoadFreshDefaults<ManagerDefaultSettings_Foraging>();

    [Test]
    public static void ForestrySettingsLoadFreshDefaultsWhenNodesAreMissing() =>
        ExpectMissingNodesLoadFreshDefaults<ManagerDefaultSettings_Forestry>();

    [Test]
    public static void HuntingSettingsLoadFreshDefaultsWhenNodesAreMissing() =>
        ExpectMissingNodesLoadFreshDefaults<ManagerDefaultSettings_Hunting>();

    [Test]
    public static void MiningSettingsLoadFreshDefaultsWhenNodesAreMissing() =>
        ExpectMissingNodesLoadFreshDefaults<ManagerDefaultSettings_Mining>();

    [Test]
    public static void LogsSettingsLoadFreshDefaultsWhenNodesAreMissing() =>
        ExpectMissingNodesLoadFreshDefaults<ManagerSettings_Logs>();

    [Test]
    public static void PowerSettingsLoadFreshDefaultsWhenNodesAreMissing() =>
        ExpectMissingNodesLoadFreshDefaults<ManagerSettings_Power>();

    [Test]
    public static void ProductionSettingsLoadFreshDefaultsWhenNodesAreMissing() =>
        ExpectMissingNodesLoadFreshDefaults<ManagerSettings_Production>();

    [Test]
    public static void MiningDefaultAllowMiningSurvivesRoundTrip()
    {
        var saved = new ManagerDefaultSettings_Mining { DefaultAllowMining = false };

        Expect.IsFalse(RoundTrip(saved).DefaultAllowMining);
    }

    [Test]
    public static void MiningTaskPriorityOrderMissingValuesAreAppendedOnLoad()
    {
        var saved = new ManagerDefaultSettings_Mining
        {
            DefaultTaskPriorityOrder = [ManagerJob_Mining.Task.Mine],
        };
        var loaded = RoundTrip(saved);

        Expect.AreEqual(ManagerJob_Mining.Task.Mine, loaded.DefaultTaskPriorityOrder[0]);
        Expect.AreEqual(
            Enum.GetValues(typeof(ManagerJob_Mining.Task)).Length,
            loaded.DefaultTaskPriorityOrder.Distinct().Count()
        );
    }

    [Test]
    public static void LivestockDefaultsRoundTrip()
    {
        var unrelatedPawnKind = new PawnKindDef { defName = "ScribeTestUnrelatedPawnKind" };
        var saved = new ManagerDefaultSettings_Livestock();
        Expect.GreaterThan(FillWithNonDefaults(saved.GetSettingsFor(unrelatedPawnKind)), 0);

        var loaded = RoundTrip(saved);

        Expect.AreEqual(
            "",
            string.Join(
                "; ",
                Differences(
                    saved.GetSettingsFor(unrelatedPawnKind),
                    loaded.GetSettingsFor(unrelatedPawnKind)
                )
            )
        );
        Expect.ReferencesAreEqual(loaded, loaded.GetSettingsFor(unrelatedPawnKind).settings);
    }

    [Test]
    public static void LivestockOverridesRoundTripAndKeepTheirBackReference()
    {
        var pawnKind = PawnKindDefOf.Muffalo;
        var unrelatedPawnKind = new PawnKindDef { defName = "ScribeTestUnrelatedPawnKind2" };
        var saved = WithOverride(pawnKind);
        Expect.GreaterThan(FillWithNonDefaults(saved.GetSettingsFor(pawnKind)), 0);

        var loaded = RoundTrip(saved);

        var loadedOverride = loaded.GetSettingsFor(pawnKind);
        Expect.AreEqual(
            "",
            string.Join("; ", Differences(saved.GetSettingsFor(pawnKind), loadedOverride))
        );
        Expect.ReferencesAreEqual(loaded, loadedOverride.settings);
        Expect.ReferencesAreEqual(loaded, loaded.GetSettingsFor(unrelatedPawnKind).settings);
        Expect.AreNotEqual(
            Describe(loadedOverride.DefaultCountTargets),
            Describe(loaded.GetSettingsFor(unrelatedPawnKind).DefaultCountTargets)
        );
    }

    [Test]
    public static void LivestockSettingsLoadFreshDefaultsWhenNodesAreMissing()
    {
        var pawnKind = PawnKindDefOf.Muffalo;
        var loaded = LoadWithNoSettingNodes<ManagerDefaultSettings_Livestock>();

        var settings = loaded.GetSettingsFor(pawnKind);

        Expect.AreEqual(
            "",
            string.Join(
                "; ",
                Differences(
                    new ManagerDefaultSettings_Livestock().GetSettingsFor(pawnKind),
                    settings
                )
            )
        );
        Expect.ReferencesAreEqual(loaded, settings.settings);
    }

    private static ManagerDefaultSettings_Livestock WithOverride(PawnKindDef pawnKind)
    {
        var legacy = new ManagerSettings_Livestock();
        legacy.overrides[pawnKind] = new LegacyPawnKindSettings_Livestock();
        var settings = new ManagerDefaultSettings_Livestock();
        Expect.IsTrue(settings.MigrateFrom(legacy));
        return settings;
    }
}
