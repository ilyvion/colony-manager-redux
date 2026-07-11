// ManagerJobLivestockTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class ManagerJobLivestockTests
{
    private readonly struct Animal(string name, int learnedTraitCount, long ageBiologicalTicks)
    {
        public string Name { get; } = name;
        public int LearnedTraitCount { get; } = learnedTraitCount;
        public long AgeBiologicalTicks { get; } = ageBiologicalTicks;
    }

    private static string[] Order(bool oldestFirst, params Animal[] animals) =>
        [
            .. ManagerJob_Livestock
                .OrderForCulling(
                    oldestFirst,
                    animals,
                    a => a.LearnedTraitCount,
                    a => a.AgeBiologicalTicks
                )
                .Select(a => a.Name),
        ];

    [Test]
    public static void UntrainedAnimalsAreCulledBeforeTrainedOnes()
    {
        var order = Order(
            oldestFirst: true,
            new Animal("Trained", learnedTraitCount: 2, ageBiologicalTicks: 100),
            new Animal("Untrained", learnedTraitCount: 0, ageBiologicalTicks: 100)
        );

        Assert.ThatCollection(order).Has.Count(2);
        Assert.That(order[0]).Is.EqualTo("Untrained");
        Assert.That(order[1]).Is.EqualTo("Trained");
    }

    [Test]
    public static void AdultsAreCulledOldestFirst()
    {
        var order = Order(
            oldestFirst: true,
            new Animal("Young", learnedTraitCount: 0, ageBiologicalTicks: 100),
            new Animal("Old", learnedTraitCount: 0, ageBiologicalTicks: 200)
        );

        Assert.ThatCollection(order).Has.Count(2);
        Assert.That(order[0]).Is.EqualTo("Old");
        Assert.That(order[1]).Is.EqualTo("Young");
    }

    [Test]
    public static void JuvenilesAreCulledYoungestFirst()
    {
        var order = Order(
            oldestFirst: false,
            new Animal("Young", learnedTraitCount: 0, ageBiologicalTicks: 100),
            new Animal("Old", learnedTraitCount: 0, ageBiologicalTicks: 200)
        );

        Assert.ThatCollection(order).Has.Count(2);
        Assert.That(order[0]).Is.EqualTo("Young");
        Assert.That(order[1]).Is.EqualTo("Old");
    }

    [Test]
    public static void TrainingTakesPriorityOverAge()
    {
        // An untrained young animal should be culled before a trained, much older one.
        var order = Order(
            oldestFirst: true,
            new Animal("TrainedOld", learnedTraitCount: 1, ageBiologicalTicks: 1000),
            new Animal("UntrainedYoung", learnedTraitCount: 0, ageBiologicalTicks: 10)
        );

        Assert.ThatCollection(order).Has.Count(2);
        Assert.That(order[0]).Is.EqualTo("UntrainedYoung");
        Assert.That(order[1]).Is.EqualTo("TrainedOld");
    }

    [Test]
    public static void EligibleAnimalIsIncludedWhenAllTogglesAreOff() =>
        Assert
            .That(
                ManagerJob_Livestock.IsEligibleForCulling(
                    alreadyCulling: false,
                    alreadyCulled: false,
                    cullTrained: false,
                    isTrained: false,
                    cullPregnant: false,
                    isPregnant: false,
                    cullBonded: false,
                    isBonded: false,
                    avoidMilkable: false,
                    milkFullness: 1f,
                    milkThreshold: 0f,
                    avoidShearable: false,
                    woolFullness: 1f,
                    woolThreshold: 0f
                )
            )
            .Is.True();

    [Test]
    public static void AlreadyCullingOrCulledAnimalsAreAlwaysExcluded()
    {
        Assert
            .That(
                ManagerJob_Livestock.IsEligibleForCulling(
                    alreadyCulling: true,
                    alreadyCulled: false,
                    cullTrained: true,
                    isTrained: false,
                    cullPregnant: true,
                    isPregnant: false,
                    cullBonded: true,
                    isBonded: false,
                    avoidMilkable: false,
                    milkFullness: 0f,
                    milkThreshold: 1f,
                    avoidShearable: false,
                    woolFullness: 0f,
                    woolThreshold: 1f
                )
            )
            .Is.False();

        Assert
            .That(
                ManagerJob_Livestock.IsEligibleForCulling(
                    alreadyCulling: false,
                    alreadyCulled: true,
                    cullTrained: true,
                    isTrained: false,
                    cullPregnant: true,
                    isPregnant: false,
                    cullBonded: true,
                    isBonded: false,
                    avoidMilkable: false,
                    milkFullness: 0f,
                    milkThreshold: 1f,
                    avoidShearable: false,
                    woolFullness: 0f,
                    woolThreshold: 1f
                )
            )
            .Is.False();
    }

    [Test]
    public static void TrainedPregnantBondedAnimalsExcludedUnlessToggleAllows()
    {
        static bool Eligible(bool cullTrained, bool cullPregnant, bool cullBonded)
        {
            return ManagerJob_Livestock.IsEligibleForCulling(
                alreadyCulling: false,
                alreadyCulled: false,
                cullTrained,
                isTrained: true,
                cullPregnant,
                isPregnant: true,
                cullBonded,
                isBonded: true,
                avoidMilkable: false,
                milkFullness: 0f,
                milkThreshold: 1f,
                avoidShearable: false,
                woolFullness: 0f,
                woolThreshold: 1f
            );
        }

        Assert.That(Eligible(false, false, false)).Is.False();
        Assert.That(Eligible(true, false, false)).Is.False();
        Assert.That(Eligible(true, true, false)).Is.False();
        Assert.That(Eligible(true, true, true)).Is.True();
    }

    [Test]
    public static void MilkAndWoolThresholdsExcludeAtOrAboveThreshold()
    {
        static bool Eligible(
            float milkFullness,
            float milkThreshold,
            float woolFullness,
            float woolThreshold
        )
        {
            return ManagerJob_Livestock.IsEligibleForCulling(
                alreadyCulling: false,
                alreadyCulled: false,
                cullTrained: true,
                isTrained: false,
                cullPregnant: true,
                isPregnant: false,
                cullBonded: true,
                isBonded: false,
                avoidMilkable: true,
                milkFullness,
                milkThreshold,
                avoidShearable: true,
                woolFullness,
                woolThreshold
            );
        }

        // Exactly at threshold is not "below", so avoidance still excludes the animal.
        Assert.That(Eligible(1f, 1f, 0f, 1f)).Is.False();
        Assert.That(Eligible(0f, 1f, 1f, 1f)).Is.False();

        // Strictly below both thresholds is included.
        Assert.That(Eligible(0.5f, 1f, 0.5f, 1f)).Is.True();
    }

    [Test]
    public static void CullingTargetDifferenceMath()
    {
        Assert.That(ManagerJob_Livestock.CalculateCullingTargetDifference(10, 0, 10)).Is.EqualTo(0);
        Assert.That(ManagerJob_Livestock.CalculateCullingTargetDifference(15, 0, 10)).Is.EqualTo(5);
        Assert.That(ManagerJob_Livestock.CalculateCullingTargetDifference(5, 3, 10)).Is.EqualTo(-8);
        Assert.That(ManagerJob_Livestock.CalculateCullingTargetDifference(12, 2, 10)).Is.EqualTo(0);
    }

    [Test]
    public static void TamingTargetDifferenceMath()
    {
        Assert.That(ManagerJob_Livestock.CalculateTamingTargetDifference(10, 10, 0)).Is.EqualTo(0);
        Assert.That(ManagerJob_Livestock.CalculateTamingTargetDifference(10, 4, 0)).Is.EqualTo(6);
        Assert.That(ManagerJob_Livestock.CalculateTamingTargetDifference(10, 8, 5)).Is.EqualTo(-3);
    }

    [Test]
    public static void RoughlyEquallyDistributedFollowerCounts()
    {
        Assert.That(ManagerJob_Livestock.IsRoughlyEquallyDistributed([3, 3, 3])).Is.True();
        Assert.That(ManagerJob_Livestock.IsRoughlyEquallyDistributed([3, 4])).Is.True();
        Assert.That(ManagerJob_Livestock.IsRoughlyEquallyDistributed([3, 5])).Is.False();
        Assert.That(ManagerJob_Livestock.IsRoughlyEquallyDistributed([7])).Is.True();
    }

    [Test]
    public static void ChooseMasterKeepsCurrentMasterWhenValidAndBalanced()
    {
        var chosen = ManagerJob_Livestock.ChooseMaster(
            currentMaster: "Alice",
            options: ["Alice", "Bob"],
            currentOptionsRoughlyEquallyDistributed: true,
            followerCount: _ => 3
        );

        Assert.That(chosen!).Is.EqualTo("Alice");
    }

    [Test]
    public static void ChooseMasterSwitchesWhenDistributionIsUneven()
    {
        // Regression guard for the stale-cache bug fixed by de32088 (#24): even though "Alice"
        // is still a valid current master, an uneven follower spread must force a re-pick.
        var chosen = ManagerJob_Livestock.ChooseMaster(
            currentMaster: "Alice",
            options: ["Alice", "Bob"],
            currentOptionsRoughlyEquallyDistributed: false,
            followerCount: name => name == "Alice" ? 5 : 1
        );

        Assert.That(chosen!).Is.EqualTo("Bob");
    }

    [Test]
    public static void ChooseMasterSwitchesWhenCurrentMasterNotInOptions()
    {
        var chosen = ManagerJob_Livestock.ChooseMaster(
            currentMaster: "Carol",
            options: ["Alice", "Bob"],
            currentOptionsRoughlyEquallyDistributed: true,
            followerCount: name => name == "Alice" ? 2 : 1
        );

        Assert.That(chosen!).Is.EqualTo("Bob");
    }

    [Test]
    public static void ChooseMasterPicksLeastFollowersWhenNoCurrentMaster()
    {
        var chosen = ManagerJob_Livestock.ChooseMaster(
            currentMaster: null,
            options: ["Alice", "Bob", "Carol"],
            currentOptionsRoughlyEquallyDistributed: true,
            followerCount: name =>
                name switch
                {
                    "Alice" => 4,
                    "Bob" => 1,
                    _ => 9,
                }
        );

        Assert.That(chosen!).Is.EqualTo("Bob");
    }

    [Test]
    public static void PruneIntervalGating()
    {
        // Initial state (_lastPruneTick starts at -PruneIntervalTicks) should trigger a prune
        // immediately at tick 0.
        Assert
            .That(ManagerJob_Livestock.LivestockCachesComp.ShouldPrune(0, -5000, 5000))
            .Is.True();

        Assert.That(ManagerJob_Livestock.LivestockCachesComp.ShouldPrune(5000, 0, 5000)).Is.True();
        Assert.That(ManagerJob_Livestock.LivestockCachesComp.ShouldPrune(4999, 0, 5000)).Is.False();
    }
}
