// ManagerDefTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class ManagerDefTests
{
    [Test]
    public static void AllValidTypesProduceNoErrors() =>
        Assert
            .ThatCollection(
                ManagerDef.ValidateManagerDefTypes(
                    typeof(ManagerJob),
                    typeof(ManagerTab),
                    typeof(ManagerSettings)
                )
            )
            .Is.Empty();

    [Test]
    public static void NullManagerJobClassIsValid() =>
        Assert
            .ThatCollection(
                ManagerDef.ValidateManagerDefTypes(
                    null,
                    typeof(ManagerTab),
                    typeof(ManagerSettings)
                )
            )
            .Is.Empty();

    [Test]
    public static void NonManagerJobTypeProducesError() =>
        Assert
            .ThatCollection(
                ManagerDef.ValidateManagerDefTypes(
                    typeof(object),
                    typeof(ManagerTab),
                    typeof(ManagerSettings)
                )
            )
            .Has.Count(1);

    [Test]
    public static void NullManagerTabClassProducesError() =>
        Assert
            .ThatCollection(ManagerDef.ValidateManagerDefTypes(null, null, typeof(ManagerSettings)))
            .Has.Count(1);

    [Test]
    public static void NonManagerTabTypeProducesError() =>
        Assert
            .ThatCollection(
                ManagerDef.ValidateManagerDefTypes(null, typeof(object), typeof(ManagerSettings))
            )
            .Has.Count(1);

    [Test]
    public static void NullManagerSettingsClassIsValid() =>
        Assert
            .ThatCollection(ManagerDef.ValidateManagerDefTypes(null, typeof(ManagerTab), null))
            .Is.Empty();

    [Test]
    public static void NonManagerSettingsTypeProducesError() =>
        Assert
            .ThatCollection(
                ManagerDef.ValidateManagerDefTypes(null, typeof(ManagerTab), typeof(object))
            )
            .Has.Count(1);

    [Test]
    public static void AllInvalidTypesProduceThreeErrors() =>
        Assert
            .ThatCollection(
                ManagerDef.ValidateManagerDefTypes(typeof(object), null, typeof(object))
            )
            .Has.Count(3);
}
