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
    public static void CombineAndOrderPawnKindSourcesHandlesAllEmptySources() =>
        Assert
            .ThatCollection(
                Utilities_Hunting.CombineAndOrderPawnKindSources(wild: [], visible: [], corpses: [])
            )
            .Is.Empty();
}
