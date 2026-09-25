// ManagerJobLivestockAnimalGeneticsTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.AnimalGenetics.Core;
using DevTools.Testing;

namespace ColonyManagerRedux.AnimalGenetics.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ManagerJobLivestockAnimalGeneticsTests
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
        Expect.GreaterThanOrEqualTo(sum, 0.999f);
        Expect.LessThanOrEqualTo(sum, 1.001f);
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
        Expect.GreaterThanOrEqualTo(ratio, 1.49f);
        Expect.LessThanOrEqualTo(ratio, 1.51f);
        var remainder = result["B"] + result["C"];
        Expect.GreaterThanOrEqualTo(remainder, 0.399f);
        Expect.LessThanOrEqualTo(remainder, 0.401f);
    }

    [Test]
    public static void MaxBoundaryLeavesOthersSummingToNearZero()
    {
        var values = new Dictionary<string, float> { ["A"] = 0.5f, ["B"] = 0.5f };

        var result = Rebalance(values, "A", Max);

        Expect.AreEqual(Max, result["A"]);
        Expect.GreaterThanOrEqualTo(result["B"], 0.0f);
        Expect.LessThanOrEqualTo(result["B"], 0.002f);
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

        var total = result.Values.Sum();
        Expect.GreaterThanOrEqualTo(total, 0.999f);
        Expect.LessThanOrEqualTo(total, 1.001f);
        foreach (var value in result.Values)
        {
            Expect.GreaterThan(value, 0f);
        }
    }

    private sealed class FakePawn(Dictionary<string, float> genes)
    {
        public Dictionary<string, float> Genes { get; } = genes;
    }

    private static float GetGene(FakePawn pawn, string gene) => pawn.Genes[gene];

    [Test]
    public static void CalculatePreferenceScoreOfSingleGeneReducesToGeneTimesWeight()
    {
        var values = new Dictionary<string, float> { ["A"] = 0.5f };
        var pawn = new FakePawn(new Dictionary<string, float> { ["A"] = 4f });

        var score = ManagerJob_Livestock_AnimalGenetics.CalculatePreferenceScore(
            ["A"],
            values,
            pawn,
            GetGene
        );

        Expect.AreEqual(2f, score);
    }

    [Test]
    public static void CalculatePreferenceScoreSumsAcrossMultipleGenes()
    {
        var values = new Dictionary<string, float>
        {
            ["A"] = 0.5f,
            ["B"] = 0.25f,
            ["C"] = 0.25f,
        };
        var pawn = new FakePawn(
            new Dictionary<string, float>
            {
                ["A"] = 2f,
                ["B"] = 4f,
                ["C"] = 8f,
            }
        );

        var score = ManagerJob_Livestock_AnimalGenetics.CalculatePreferenceScore(
            ["A", "B", "C"],
            values,
            pawn,
            GetGene
        );

        // 2*0.5 + 4*0.25 + 8*0.25 = 1 + 1 + 2 = 4
        Expect.AreEqual(4f, score);
    }

    [Test]
    public static void CalculatePreferenceScoreOfAllZeroWeightsIsZero()
    {
        var values = new Dictionary<string, float> { ["A"] = 0f, ["B"] = 0f };
        var pawn = new FakePawn(new Dictionary<string, float> { ["A"] = 5f, ["B"] = 10f });

        var score = ManagerJob_Livestock_AnimalGenetics.CalculatePreferenceScore(
            ["A", "B"],
            values,
            pawn,
            GetGene
        );

        Expect.AreEqual(0f, score);
    }

    [Test]
    public static void SingleGeneKeepsTheNewValueAndChangesNothingElse()
    {
        var result = Rebalance(new Dictionary<string, float> { ["A"] = 1f }, "A", 0.4f);

        Expect.AreEqual(1, result.Count);
        Expect.AreEqual(0.4f, result["A"]);
    }

    [Test]
    public static void OtherGenesWithNoWeightShareTheRemainderEvenly()
    {
        var values = new Dictionary<string, float>
        {
            ["A"] = 1f,
            ["B"] = 0f,
            ["C"] = 0f,
        };

        var result = Rebalance(values, "A", 0.4f);

        Expect.AreEqual(0.4f, result["A"]);
        Expect.AreApproximatelyEqual(0.3f, result["B"]);
        Expect.AreApproximatelyEqual(0.3f, result["C"]);
    }

    [Test]
    public static void SettingOneGeneToTheMaximumPinsTheOthersToTheirMinimum()
    {
        var values = new Dictionary<string, float> { ["A"] = 0.5f, ["B"] = 0.5f };

        var result = Rebalance(values, "A", 1f);

        Expect.IsFalse(float.IsNaN(result["B"]));
        Expect.AreEqual(1f, result["A"]);
        Expect.LessThanOrEqualTo(result["B"], Min);
    }

    [Test]
    public static void RandomSliderMovesNeverProduceNaNAndKeepTheSumAtOne()
    {
        // A fixed linear congruential sequence keeps the "random" moves reproducible.
        var state = 12345u;
        float NextFraction()
        {
            state = unchecked((state * 1664525u) + 1013904223u);
            return (state >> 8) / (float)(1 << 24);
        }

        var keys = new[] { "A", "B", "C", "D" };
        var values = keys.ToDictionary(k => k, _ => 0.25f);

        for (var move = 0; move < 200; move++)
        {
            var key = keys[(int)(NextFraction() * keys.Length)];
            var newValue = Min + (NextFraction() * (Max - Min));

            values = Rebalance(values, key, newValue);

            Expect.IsFalse(values.Values.Any(float.IsNaN), $"NaN after move {move}");
            Expect.AreApproximatelyEqual(1f, values.Values.Sum(), $"Sum drifted at move {move}");
            Expect.AreEqual(newValue, values[key]);
        }
    }

    [Test]
    public static void InitialGeneWeightsAreEqualAndSumToOne()
    {
        var weights = ManagerJob_Livestock_AnimalGenetics.InitialGeneWeights(["A", "B", "C", "D"]);

        Expect.AreEqual(4, weights.Count);
        Expect.IsTrue(weights.Values.All(w => w == 0.25f));
        Expect.AreApproximatelyEqual(1f, weights.Values.Sum());
    }

    [Test]
    public static void CullingOverrideOffKeepsTheOriginalOrder()
    {
        var original = new[] { 3, 1, 2 };

        var result = ManagerJob_Livestock_AnimalGenetics.OrderForCulling(
            false,
            original,
            i => i,
            () => original
        );

        Expect.AreEqual("3,1,2", string.Join(",", result));
    }

    // A direction mistake here would cull the best animals first.
    [Test]
    public static void CullingOverrideCullsTheLowestScoreFirst()
    {
        var scores = new Dictionary<string, float>
        {
            ["best"] = 9f,
            ["worst"] = 1f,
            ["mid"] = 5f,
        };

        var result = ManagerJob_Livestock_AnimalGenetics
            .OrderForCulling(true, scores.Keys, k => scores[k], () => [])
            .ToList();

        Expect.AreEqual("worst,mid,best", string.Join(",", result));
    }

    [Test]
    public static void TamingScoreUsesTheOriginalWhenTheOverrideIsOff()
    {
        var score = ManagerJob_Livestock_AnimalGenetics.TamingSortScore(false, () => 9f, () => 2f);

        Expect.AreEqual(2f, score);
    }

    [Test]
    public static void TamingScoreIsThePreferenceScoreWhenTheOverrideIsOn()
    {
        var score = ManagerJob_Livestock_AnimalGenetics.TamingSortScore(true, () => 9f, () => 2f);

        Expect.AreEqual(9f, score);
    }

    [Test]
    public static void TamingOverrideDoesNotEvaluateTheOriginalScore()
    {
        var originalEvaluated = false;

        _ = ManagerJob_Livestock_AnimalGenetics.TamingSortScore(
            true,
            () => 9f,
            () =>
            {
                originalEvaluated = true;
                return 2f;
            }
        );

        Expect.IsFalse(originalEvaluated);
    }
}
