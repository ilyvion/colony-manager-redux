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

        Assert.That(chosen).Is.EqualTo("Alice");
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

        Assert.That(chosen).Is.EqualTo("Bob");
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

        Assert.That(chosen).Is.EqualTo("Bob");
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

        Assert.That(chosen).Is.EqualTo("Bob");
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

    [Test]
    public static void FormatAgeSexBucketOmitsCulledSuffixWhenCullingRemovesAnimals() =>
        Assert
            .That(
                ManagerJob_Livestock.FormatAgeSexBucket(
                    tame: 5,
                    culled: 2,
                    target: 5,
                    cullingRemovesAnimals: true
                )
            )
            .Is.EqualTo("3/5, ");

    [Test]
    public static void FormatAgeSexBucketShowsCulledSuffixWhenCullingDoesNotRemoveAnimals() =>
        // When culling doesn't actually remove the animal (e.g. it's marked for slaughter but
        // still alive/present), the still-pending cull count is surfaced via a "(+N)" suffix so
        // the label doesn't silently undercount what's actually on the map.
        Assert
            .That(
                ManagerJob_Livestock.FormatAgeSexBucket(
                    tame: 5,
                    culled: 2,
                    target: 5,
                    cullingRemovesAnimals: false
                )
            )
            .Is.EqualTo("3/5(+2), ");

    [Test]
    public static void FormatAgeSexBucketWithNoCullsOmitsSuffixEvenWhenNotRemoving() =>
        Assert
            .That(
                ManagerJob_Livestock.FormatAgeSexBucket(
                    tame: 5,
                    culled: 0,
                    target: 5,
                    cullingRemovesAnimals: false
                )
            )
            .Is.EqualTo("5/5(+0), ");

    private static PawnKindDef PawnKind(RaceProperties raceProps) =>
        new() { race = new ThingDef { race = raceProps } };

    [Test]
    public static void UntrainableTagRejectsAndHides()
    {
        var pawnKind = PawnKind(new RaceProperties { untrainableTags = ["Sit"] });
        var td = new TrainableDef { defName = "Sit", defaultTrainable = true };

        var report = ManagerJob_Livestock.CanBeTrained(
            odysseyActive: false,
            pawnKind,
            td,
            out var visible
        );

        Assert.That(report.Accepted).Is.False();
        Assert.That(visible).Is.False();
    }

#if !v1_5
    [Test]
    public static void OdysseySpecialTrainableNotListedRejectsAndHides()
    {
        var pawnKind = PawnKind(new RaceProperties { specialTrainables = [] });
        var td = new TrainableDef { defName = "Fly", specialTrainable = true };

        var report = ManagerJob_Livestock.CanBeTrained(
            odysseyActive: true,
            pawnKind,
            td,
            out var visible
        );

        Assert.That(report.Accepted).Is.False();
        Assert.That(visible).Is.False();
    }

    [Test]
    public static void OdysseySpecialTrainableListedPassesGate()
    {
        var td = new TrainableDef { defName = "Fly", specialTrainable = true };
        var pawnKind = PawnKind(new RaceProperties { specialTrainables = [td] });

        var report = ManagerJob_Livestock.CanBeTrained(
            odysseyActive: true,
            pawnKind,
            td,
            out var visible
        );

        Assert.That(report.Accepted).Is.True();
        Assert.That(visible).Is.True();
    }
#endif

    [Test]
    public static void TrainableTagMatchButTooSmallIsVisibleButRejected()
    {
        var pawnKind = PawnKind(
            new RaceProperties { trainableTags = ["Obedience"], baseBodySize = 0.5f }
        );
        var td = new TrainableDef
        {
            defName = "Obedience",
            defaultTrainable = true,
            minBodySize = 1f,
        };

        var report = ManagerJob_Livestock.CanBeTrained(
            odysseyActive: false,
            pawnKind,
            td,
            out var visible
        );

        Assert.That(report.Accepted).Is.False();
        Assert.That(visible).Is.True();
    }

    [Test]
    public static void NotDefaultOrSpecialTrainableRejectsAndHides()
    {
        var pawnKind = PawnKind(new RaceProperties());
        var td = new TrainableDef
        {
            defName = "Unrelated",
            defaultTrainable = false,
#if !v1_5
            specialTrainable = false,
#endif
        };

        var report = ManagerJob_Livestock.CanBeTrained(
            odysseyActive: false,
            pawnKind,
            td,
            out var visible
        );

        Assert.That(report.Accepted).Is.False();
        Assert.That(visible).Is.False();
    }

    [Test]
    public static void TooSmallForDefaultTrainableIsVisibleButRejected()
    {
        var pawnKind = PawnKind(new RaceProperties { baseBodySize = 0.5f });
        var td = new TrainableDef
        {
            defName = "Obedience",
            defaultTrainable = true,
            minBodySize = 1f,
        };

        var report = ManagerJob_Livestock.CanBeTrained(
            odysseyActive: false,
            pawnKind,
            td,
            out var visible
        );

        Assert.That(report.Accepted).Is.False();
        Assert.That(visible).Is.True();
    }

    [Test]
    public static void NotSmartEnoughIsVisibleButRejected()
    {
        var pawnKind = PawnKind(
            new RaceProperties
            {
                baseBodySize = 1f,
                trainability = new TrainabilityDef { intelligenceOrder = 1 },
            }
        );
        var td = new TrainableDef
        {
            defName = "Obedience",
            defaultTrainable = true,
            minBodySize = 1f,
            requiredTrainability = new TrainabilityDef { intelligenceOrder = 2 },
        };

        var report = ManagerJob_Livestock.CanBeTrained(
            odysseyActive: false,
            pawnKind,
            td,
            out var visible
        );

        Assert.That(report.Accepted).Is.False();
        Assert.That(visible).Is.True();
    }

    [Test]
    public static void FullyEligibleCombinationIsAccepted()
    {
        var pawnKind = PawnKind(
            new RaceProperties
            {
                baseBodySize = 1f,
                trainability = new TrainabilityDef { intelligenceOrder = 2 },
            }
        );
        var td = new TrainableDef
        {
            defName = "Obedience",
            defaultTrainable = true,
            minBodySize = 1f,
            requiredTrainability = new TrainabilityDef { intelligenceOrder = 1 },
        };

        var report = ManagerJob_Livestock.CanBeTrained(
            odysseyActive: false,
            pawnKind,
            td,
            out var visible
        );

        Assert.That(report.Accepted).Is.True();
        Assert.That(visible).Is.True();
    }

    // Regression guard for the gather/execute split (issue #27): stopping a culling/taming
    // designation used to remove the game's own List<Designation>.Last() repeatedly in a single
    // synchronous pass. Since the gather phase can no longer mutate the game to observe the
    // effect of each removal before picking the next one, TakeLastReversed reproduces that same
    // "always take from the end" order up front as a pure decision.

    [Test]
    public static void TakeLastReversedPicksFromTheEndInReverseOrder()
    {
        var result = ManagerJob_Livestock.TakeLastReversed(
            ["a", "b", "c", "d"],
            count: 4,
            selector: s => s
        );

        Assert.ThatCollection(result).Has.Count(4);
        Assert.That(result[0]).Is.EqualTo("d");
        Assert.That(result[1]).Is.EqualTo("c");
        Assert.That(result[2]).Is.EqualTo("b");
        Assert.That(result[3]).Is.EqualTo("a");
    }

    [Test]
    public static void TakeLastReversedClampsToAvailableCount()
    {
        var result = ManagerJob_Livestock.TakeLastReversed(["a", "b"], count: 10, selector: s => s);

        Assert.ThatCollection(result).Has.Count(2);
        Assert.That(result[0]).Is.EqualTo("b");
        Assert.That(result[1]).Is.EqualTo("a");
    }

    [Test]
    public static void TakeLastReversedWithZeroCountReturnsEmpty()
    {
        var result = ManagerJob_Livestock.TakeLastReversed(
            ["a", "b", "c"],
            count: 0,
            selector: s => s
        );

        Assert.ThatCollection(result).Is.Empty();
    }

    [Test]
    public static void TakeLastReversedOnEmptyListReturnsEmpty()
    {
        var result = ManagerJob_Livestock.TakeLastReversed(
            Array.Empty<string>(),
            count: 5,
            selector: s => s
        );

        Assert.ThatCollection(result).Is.Empty();
    }
}
