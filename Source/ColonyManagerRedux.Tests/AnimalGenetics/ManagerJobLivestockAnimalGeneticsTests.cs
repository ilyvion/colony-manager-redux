// ManagerJobLivestockAnimalGeneticsTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.AnimalGenetics.Core;
using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class ManagerJobLivestockAnimalGeneticsTests
{
    private const float Min = 0.0001f;
    private const float Max = 0.999f;

    private static Dictionary<string, float> Rebalance(
        Dictionary<string, float> values,
        string changedKey,
        float newValue
    ) =>
        ManagerJob_Livestock_AnimalGenetics.RebalanceAfterSliderChange(
            values,
            changedKey,
            newValue,
            Min,
            Max
        );

    [Test]
    public static void ClampingDoesNotBreakFullNormalization()
    {
        // Regression test for commit d3700fa: driving one gene's slider low enough that the
        // proportional redistribution would push another gene below Min forced a clamp, which
        // used to leave the set summing to less than 1.0 instead of renormalizing.
        var values = new Dictionary<string, float> { ["A"] = 0.5f, ["B"] = 0.5f };

        var result = Rebalance(values, "A", 0.0001f);

        var sum = result.Values.Sum();
        Assert.That(sum).Is.BetweenInclusive(0.999f, 1.001f);
    }

    [Test]
    public static void ThreeGeneRedistributionStaysProportional()
    {
        var values = new Dictionary<string, float>
        {
            ["A"] = 0.5f,
            ["B"] = 0.3f,
            ["C"] = 0.2f,
        };

        var result = Rebalance(values, "A", 0.6f);

        // B and C should retain their 3:2 ratio (within float error) while summing to 0.4.
        var ratio = result["B"] / result["C"];
        Assert.That(ratio).Is.BetweenInclusive(1.49f, 1.51f);
        Assert.That(result["B"] + result["C"]).Is.BetweenInclusive(0.399f, 0.401f);
    }

    [Test]
    public static void MaxBoundaryLeavesOthersSummingToNearZero()
    {
        var values = new Dictionary<string, float> { ["A"] = 0.5f, ["B"] = 0.5f };

        var result = Rebalance(values, "A", Max);

        Assert.That(result["A"]).Is.EqualTo(Max);
        Assert.That(result["B"]).Is.BetweenInclusive(0.0f, 0.002f);
    }

    [Test]
    public static void ManyGenesNeverProduceNonPositiveSum()
    {
        var values = new Dictionary<string, float>
        {
            ["A"] = 0.2f,
            ["B"] = 0.2f,
            ["C"] = 0.2f,
            ["D"] = 0.2f,
            ["E"] = 0.2f,
        };

        var result = Rebalance(values, "A", 0.95f);

        Assert.That(result.Values.Sum()).Is.BetweenInclusive(0.999f, 1.001f);
        foreach (var value in result.Values)
        {
            Assert.That(value).Is.GreaterThan(0f);
        }
    }
}
