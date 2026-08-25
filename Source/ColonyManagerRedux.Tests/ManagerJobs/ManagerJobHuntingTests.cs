// ManagerJobHuntingTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class ManagerJobHuntingTests
{
    private sealed class Sentinel;

    private static readonly Sentinel Meat = new();
    private static readonly Sentinel Leather = new();

    [Test]
    public static void SelectsMeatDefForMeatTargetResource() =>
        Assert
            .That(
                ReferenceEquals(
                    ManagerJob_Hunting.SelectResourceDef(
                        ManagerJob_Hunting.HuntingTargetResource.Meat,
                        Meat,
                        Leather
                    ),
                    Meat
                )
            )
            .Is.True();

    [Test]
    public static void SelectsLeatherDefForLeatherTargetResource() =>
        Assert
            .That(
                ReferenceEquals(
                    ManagerJob_Hunting.SelectResourceDef(
                        ManagerJob_Hunting.HuntingTargetResource.Leather,
                        Meat,
                        Leather
                    ),
                    Leather
                )
            )
            .Is.True();

    // Regression guard for CHANGELOG 0.5.4: a humanlike race without a leatherDef must resolve to
    // null (not throw or fall back to meatDef) when targeting leather.
    [Test]
    public static void SelectsNullWhenLeatherTargetResourceHasNoLeatherDef() =>
        Assert
            .That(
                ManagerJob_Hunting.SelectResourceDef(
                    ManagerJob_Hunting.HuntingTargetResource.Leather,
                    Meat,
                    null
                )
                    is null
            )
            .Is.True();

    // Regression guard for CHANGELOG 0.5.3: a race without organic flesh (no meatDef) must
    // resolve to null when targeting meat, rather than crashing with a NullReferenceException.
    [Test]
    public static void SelectsNullWhenMeatTargetResourceHasNoMeatDef() =>
        Assert
            .That(
                ManagerJob_Hunting.SelectResourceDef(
                    ManagerJob_Hunting.HuntingTargetResource.Meat,
                    null,
                    Leather
                )
                    is null
            )
            .Is.True();

    // The designation-priority comment at ManagerJob_Hunting.cs:688 says "value = meat /
    // (distance ^ 2)", but the sorter it actually passes to GetTargetsSorted is
    // "yield / distance" (single division, not squared) — see TEST-OPPORTUNITIES.md #7. This
    // pins the real (non-squared) ordering down as a regression guard, using values where the
    // two formulas disagree: yield/d gives {9, 25} (second wins), yield/d^2 gives {9, 6.25}
    // (first would win) — so an accidental "fix" to match the stale comment would flip this
    // ordering and be caught here.
    [Test]
    public static void DesignationPrioritySortsByYieldOverDistanceNotDistanceSquared()
    {
        var result = ManagerJob.SortByScoreDescending(
            origins: ["low-yield-close", "high-yield-far"],
            things: [9, 100],
            distances: [1f, 4f],
            sorter: (yield, distance) => yield / distance
        );

        Assert.That(result[0]).Is.EqualTo("high-yield-far");
        Assert.That(result[1]).Is.EqualTo("low-yield-close");
    }

    [Test]
    public static void DesignationWithNoThingIsRemovedForAreaCleanup() =>
        // A designation whose target has already lost its Thing (e.g. the animal died and its
        // corpse rotted away) is always cleaned up, regardless of area.
        Assert
            .That(
                ManagerJob_Hunting.ShouldRemoveForAreaCleanup(hasThing: false, inAllowedArea: false)
            )
            .Is.True();

    [Test]
    public static void DesignationInAllowedAreaIsNotRemoved() =>
        Assert
            .That(
                ManagerJob_Hunting.ShouldRemoveForAreaCleanup(hasThing: true, inAllowedArea: true)
            )
            .Is.False();

    [Test]
    public static void DesignationOutsideAllowedAreaIsRemoved() =>
        // Regression: the hunting grounds may shrink or be reassigned after an animal was
        // designated, in which case the stale designation must be cleaned up even though its
        // target thing still exists.
        Assert
            .That(
                ManagerJob_Hunting.ShouldRemoveForAreaCleanup(hasThing: true, inAllowedArea: false)
            )
            .Is.True();

    [Test]
    public static void AllowedAnimalCorpseIsAlwaysUnforbidden() =>
        // An animal corpse that's already on the allowed list is unforbidden regardless of the
        // "also unforbid disallowed animals"/"including human corpses" settings.
        Assert
            .That(
                ManagerJob_Hunting.ShouldUnforbidCorpse(
                    unforbidAllCorpses: false,
                    unforbidHumanCorpses: false,
                    isAllowedAnimal: true,
                    isHumanlike: false
                )
            )
            .Is.True();

    [Test]
    public static void DisallowedAnimalCorpseIsUnforbiddenOnlyWhenUnforbidAllCorpsesIsSet() =>
        Assert
            .That(
                ManagerJob_Hunting.ShouldUnforbidCorpse(
                    unforbidAllCorpses: true,
                    unforbidHumanCorpses: false,
                    isAllowedAnimal: false,
                    isHumanlike: false
                )
            )
            .Is.True();

    [Test]
    public static void DisallowedAnimalCorpseIsNotUnforbiddenWithoutUnforbidAllCorpses() =>
        Assert
            .That(
                ManagerJob_Hunting.ShouldUnforbidCorpse(
                    unforbidAllCorpses: false,
                    unforbidHumanCorpses: false,
                    isAllowedAnimal: false,
                    isHumanlike: false
                )
            )
            .Is.False();

    // Regression guard: "also unforbid corpses of disallowed animals" must not implicitly
    // unforbid human corpses too — that requires the separate "including human corpses" toggle.
    [Test]
    public static void HumanCorpseIsNotUnforbiddenByUnforbidAllCorpsesAlone() =>
        Assert
            .That(
                ManagerJob_Hunting.ShouldUnforbidCorpse(
                    unforbidAllCorpses: true,
                    unforbidHumanCorpses: false,
                    isAllowedAnimal: false,
                    isHumanlike: true
                )
            )
            .Is.False();

    [Test]
    public static void HumanCorpseIsUnforbiddenWhenUnforbidHumanCorpsesIsSet() =>
        Assert
            .That(
                ManagerJob_Hunting.ShouldUnforbidCorpse(
                    unforbidAllCorpses: true,
                    unforbidHumanCorpses: true,
                    isAllowedAnimal: false,
                    isHumanlike: true
                )
            )
            .Is.True();
}
