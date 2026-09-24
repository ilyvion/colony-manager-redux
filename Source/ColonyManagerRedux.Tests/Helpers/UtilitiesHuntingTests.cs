// UtilitiesHuntingTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class UtilitiesHuntingTests
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

        Expect.AreEqual(2, result.Count());
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

        Expect.AreEqual(3, result.Count);
        Expect.IsTrue(ReferenceEquals(result[0], alpaca));
        Expect.IsTrue(ReferenceEquals(result[1], muffalo));
        Expect.IsTrue(ReferenceEquals(result[2], zebra));
    }

    [Test]
    public static void GetWildAnimalsSafelyReturnsResultWhenAccessorSucceeds()
    {
        var muffalo = Kind("Muffalo");

        var result = Utilities_Hunting.GetWildAnimalsSafely(() => [muffalo]);

        Expect.AreEqual(1, result.Count());
    }

    // Regression test for a crash reported against maps (e.g. abandoned camps) whose world tile
    // isn't registered yet: map.Biome throws ArgumentOutOfRangeException, which used to abort
    // the whole Manager map component load instead of just leaving the wild animal list empty.
    [Test]
    [WarningsAllowed("Could not determine biome for map")]
    public static void GetWildAnimalsSafelyReturnsEmptyWhenAccessorThrowsArgumentOutOfRange() =>
        Expect.IsEmpty(
            Utilities_Hunting.GetWildAnimalsSafely(IEnumerable<PawnKindDef> () =>
                throw new ArgumentOutOfRangeException("tile")
            )
        );

    [Test]
    public static void CombineAndOrderPawnKindSourcesHandlesAllEmptySources() =>
        Expect.IsEmpty(
            Utilities_Hunting.CombineAndOrderPawnKindSources(wild: [], visible: [], corpses: [])
        );

    [Test]
    public static void ManhunterIconColorIsGrayWhenAnimalNotAllowed() =>
        Expect.IsTrue(
            Utilities_Hunting.GetManhunterIconColor(allowed: false, manhunterOnDamageChance: 0.9f)
                == Color.gray
        );

    [Test]
    public static void ManhunterIconColorIsRedAboveQuarterChanceWhenAllowed() =>
        Expect.IsTrue(
            Utilities_Hunting.GetManhunterIconColor(allowed: true, manhunterOnDamageChance: 0.26f)
                == Color.red
        );

    [Test]
    public static void ManhunterIconColorIsOrangeAtOrBelowQuarterChanceWhenAllowed() =>
        Expect.IsTrue(
            Utilities_Hunting.GetManhunterIconColor(allowed: true, manhunterOnDamageChance: 0.25f)
                == Resources.Orange
        );

    [Test]
    public static void VeneratedIconColorIsGrayWhenAnimalNotAllowed() =>
        Expect.IsTrue(
            Utilities_Hunting.GetVeneratedIconColor(allowed: false, allVenerated: true)
                == Color.gray
        );

    [Test]
    public static void VeneratedIconColorIsRedWhenAllColonistsVenerateAndAllowed() =>
        Expect.IsTrue(
            Utilities_Hunting.GetVeneratedIconColor(allowed: true, allVenerated: true) == Color.red
        );

    [Test]
    public static void VeneratedIconColorIsOrangeWhenOnlySomeColonistsVenerateAndAllowed() =>
        Expect.IsTrue(
            Utilities_Hunting.GetVeneratedIconColor(allowed: true, allVenerated: false)
                == Resources.Orange
        );
}
