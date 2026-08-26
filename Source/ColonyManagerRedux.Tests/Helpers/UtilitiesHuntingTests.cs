// UtilitiesHuntingTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class UtilitiesHuntingTests
{
    private static PawnKindDef Kind(string label) => new() { label = label };

    [Test]
    public static void CombineAndOrderPawnKindSourcesDeduplicatesAcrossAllThreeSources()
    {
        // Same PawnKindDef instance showing up as both a wild animal and a visible pawn (and
        // again as a corpse) must collapse to a single entry.
        var shared = Kind("Shared");
        var wildOnly = Kind("WildOnly");

        var result = Utilities_Hunting.CombineAndOrderPawnKindSources(
            wild: [shared, wildOnly],
            visible: [shared],
            corpses: [shared]
        );

        Assert.ThatCollection(result).Has.Count(2);
    }

    [Test]
    public static void CombineAndOrderPawnKindSourcesOrdersByLabel()
    {
        var zebra = Kind("Zebra");
        var alpaca = Kind("Alpaca");
        var muffalo = Kind("Muffalo");

        var result = Utilities_Hunting
            .CombineAndOrderPawnKindSources(wild: [zebra], visible: [alpaca], corpses: [muffalo])
            .ToList();

        Assert.ThatCollection(result).Has.Count(3);
        Assert.That(ReferenceEquals(result[0], alpaca)).Is.True();
        Assert.That(ReferenceEquals(result[1], muffalo)).Is.True();
        Assert.That(ReferenceEquals(result[2], zebra)).Is.True();
    }

    [Test]
    public static void GetWildAnimalsSafelyReturnsResultWhenAccessorSucceeds()
    {
        var muffalo = Kind("Muffalo");

        var result = Utilities_Hunting.GetWildAnimalsSafely(() => [muffalo]);

        Assert.ThatCollection(result).Has.Count(1);
    }

    // Regression test for a crash reported against maps (e.g. abandoned camps) whose world tile
    // isn't registered yet: map.Biome throws ArgumentOutOfRangeException, which used to abort
    // the whole Manager map component load instead of just leaving the wild animal list empty.
    [Test]
    public static void GetWildAnimalsSafelyReturnsEmptyWhenAccessorThrowsArgumentOutOfRange() =>
        Assert
            .ThatCollection(
                Utilities_Hunting.GetWildAnimalsSafely(IEnumerable<PawnKindDef> () =>
                    throw new ArgumentOutOfRangeException("tile")
                )
            )
            .Is.Empty();

    [Test]
    public static void CombineAndOrderPawnKindSourcesHandlesAllEmptySources() =>
        Assert
            .ThatCollection(
                Utilities_Hunting.CombineAndOrderPawnKindSources(wild: [], visible: [], corpses: [])
            )
            .Is.Empty();

    [Test]
    public static void ManhunterIconColorIsGrayWhenAnimalNotAllowed() =>
        Assert
            .That(
                Utilities_Hunting.GetManhunterIconColor(
                    allowed: false,
                    manhunterOnDamageChance: 0.9f
                ) == Color.gray
            )
            .Is.True();

    [Test]
    public static void ManhunterIconColorIsRedAboveQuarterChanceWhenAllowed() =>
        Assert
            .That(
                Utilities_Hunting.GetManhunterIconColor(
                    allowed: true,
                    manhunterOnDamageChance: 0.26f
                ) == Color.red
            )
            .Is.True();

    [Test]
    public static void ManhunterIconColorIsOrangeAtOrBelowQuarterChanceWhenAllowed() =>
        Assert
            .That(
                Utilities_Hunting.GetManhunterIconColor(
                    allowed: true,
                    manhunterOnDamageChance: 0.25f
                ) == Resources.Orange
            )
            .Is.True();

    [Test]
    public static void VeneratedIconColorIsGrayWhenAnimalNotAllowed() =>
        Assert
            .That(
                Utilities_Hunting.GetVeneratedIconColor(allowed: false, allVenerated: true)
                    == Color.gray
            )
            .Is.True();

    [Test]
    public static void VeneratedIconColorIsRedWhenAllColonistsVenerateAndAllowed() =>
        Assert
            .That(
                Utilities_Hunting.GetVeneratedIconColor(allowed: true, allVenerated: true)
                    == Color.red
            )
            .Is.True();

    [Test]
    public static void VeneratedIconColorIsOrangeWhenOnlySomeColonistsVenerateAndAllowed() =>
        Assert
            .That(
                Utilities_Hunting.GetVeneratedIconColor(allowed: true, allVenerated: false)
                    == Resources.Orange
            )
            .Is.True();
}
