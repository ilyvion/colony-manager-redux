// ManagerDefTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ManagerDefTests
{
    [Test]
    public static void AllValidTypesProduceNoErrors() =>
        Expect.IsEmpty(
            ManagerDef.ValidateManagerDefTypes(
                typeof(ManagerJob),
                typeof(ManagerTab),
                typeof(ManagerSettings),
                typeof(ManagerDefaultSettings)
            )
        );

    [Test]
    public static void NullManagerJobClassIsValid() =>
        Expect.IsEmpty(
            ManagerDef.ValidateManagerDefTypes(
                null,
                typeof(ManagerTab),
                typeof(ManagerSettings),
                typeof(ManagerDefaultSettings)
            )
        );

    [Test]
    public static void NonManagerJobTypeProducesError() =>
        Expect.AreEqual(
            1,
            ManagerDef
                .ValidateManagerDefTypes(
                    typeof(object),
                    typeof(ManagerTab),
                    typeof(ManagerSettings),
                    typeof(ManagerDefaultSettings)
                )
                .Count()
        );

    [Test]
    public static void NullManagerTabClassProducesError() =>
        Expect.AreEqual(
            1,
            ManagerDef
                .ValidateManagerDefTypes(
                    null,
                    null,
                    typeof(ManagerSettings),
                    typeof(ManagerDefaultSettings)
                )
                .Count()
        );

    [Test]
    public static void NonManagerTabTypeProducesError() =>
        Expect.AreEqual(
            1,
            ManagerDef
                .ValidateManagerDefTypes(
                    null,
                    typeof(object),
                    typeof(ManagerSettings),
                    typeof(ManagerDefaultSettings)
                )
                .Count()
        );

    [Test]
    public static void NullManagerSettingsClassIsValid() =>
        Expect.IsEmpty(
            ManagerDef.ValidateManagerDefTypes(
                null,
                typeof(ManagerTab),
                null,
                typeof(ManagerDefaultSettings)
            )
        );

    [Test]
    public static void NonManagerSettingsTypeProducesError() =>
        Expect.AreEqual(
            1,
            ManagerDef
                .ValidateManagerDefTypes(
                    null,
                    typeof(ManagerTab),
                    typeof(object),
                    typeof(ManagerDefaultSettings)
                )
                .Count()
        );

    [Test]
    public static void NullManagerDefaultSettingsClassIsValid() =>
        Expect.IsEmpty(
            ManagerDef.ValidateManagerDefTypes(
                null,
                typeof(ManagerTab),
                typeof(ManagerSettings),
                null
            )
        );

    [Test]
    public static void NonManagerDefaultSettingsTypeProducesError() =>
        Expect.AreEqual(
            1,
            ManagerDef
                .ValidateManagerDefTypes(
                    null,
                    typeof(ManagerTab),
                    typeof(ManagerSettings),
                    typeof(object)
                )
                .Count()
        );

    [Test]
    public static void AllInvalidTypesProduceFourErrors() =>
        Expect.AreEqual(
            4,
            ManagerDef
                .ValidateManagerDefTypes(typeof(object), null, typeof(object), typeof(object))
                .Count()
        );
}
