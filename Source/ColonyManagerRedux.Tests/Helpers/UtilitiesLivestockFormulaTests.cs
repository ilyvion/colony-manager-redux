// UtilitiesLivestockFormulaTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class UtilitiesLivestockFormulaTests
{
    private static MasterMode Compose(
        bool hunting = false,
        bool handling = false,
        bool hasPrimary = false,
        bool melee = false,
        bool ranged = false,
        bool violentDisabled = false
    ) =>
        Utilities_Livestock.ComposeMasterMode(
            hunting,
            handling,
            hasPrimary,
            melee,
            ranged,
            violentDisabled
        );

    [Test]
    public static void NoWeaponCountsAsMeleeNotRanged() =>
        Expect.AreEqual(MasterMode.Melee | MasterMode.Violent, Compose());

    [Test]
    public static void RangedWeaponIsRangedOnly() =>
        Expect.AreEqual(
            MasterMode.Ranged | MasterMode.Violent,
            Compose(hasPrimary: true, ranged: true)
        );

    [Test]
    public static void MeleeWeaponIsMelee() =>
        Expect.AreEqual(
            MasterMode.Melee | MasterMode.Violent,
            Compose(hasPrimary: true, melee: true)
        );

    [Test]
    public static void ViolentDisabledIsNonViolentNotViolent() =>
        Expect.AreEqual(MasterMode.Melee | MasterMode.NonViolent, Compose(violentDisabled: true));

    [Test]
    public static void WorkTypesAddHuntersAndTrainers() =>
        Expect.AreEqual(
            MasterMode.Hunters | MasterMode.Trainers | MasterMode.Melee | MasterMode.Violent,
            Compose(hunting: true, handling: true)
        );

    [Test]
    public static void MinimumHandlingSkillAtLowWildnessIsZero() =>
        Expect.AreApproximatelyEqual(0f, Utilities_Livestock.MinimumHandlingSkill(0.3f));

    [Test]
    public static void MinimumHandlingSkillAtMaxWildnessIsNine() =>
        Expect.AreApproximatelyEqual(9f, Utilities_Livestock.MinimumHandlingSkill(1f));

    [Test]
    public static void MinimumHandlingSkillClampsBelowZero() =>
        Expect.AreApproximatelyEqual(0f, Utilities_Livestock.MinimumHandlingSkill(0f));

    [Test]
    public static void MinimumHandlingSkillClampsAtTwenty() =>
        Expect.AreApproximatelyEqual(20f, Utilities_Livestock.MinimumHandlingSkill(10f));

    [Test]
    public static void EmptyFullnessTakesTheFullInterval() =>
        Expect.AreEqual(
            2 * GenDate.TicksPerDay,
            Utilities_Livestock.TicksTillHarvestable(2f, 0f, 1f)
        );

    [Test]
    public static void FullFullnessIsHarvestableNow() =>
        Expect.AreEqual(0, Utilities_Livestock.TicksTillHarvestable(2f, 1f, 1f));

    [Test]
    public static void OverfullIsNegative() =>
        Expect.LessThan(Utilities_Livestock.TicksTillHarvestable(2f, 1.5f, 1f), 0);

    [Test]
    public static void HalfGrowthSpeedDoublesTheTime() =>
        Expect.AreEqual(
            2 * GenDate.TicksPerDay,
            Utilities_Livestock.TicksTillHarvestable(1f, 0f, 0.5f)
        );
}
