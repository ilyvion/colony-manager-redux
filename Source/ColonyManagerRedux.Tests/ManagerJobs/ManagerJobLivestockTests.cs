// ManagerJobLivestockTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ManagerJobLivestockTests
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

        Expect.AreEqual(2, order.Length);
        Expect.AreEqual("Untrained", order[0]);
        Expect.AreEqual("Trained", order[1]);
    }

    [Test]
    public static void AdultsAreCulledOldestFirst()
    {
        var order = Order(
            oldestFirst: true,
            new Animal("Young", learnedTraitCount: 0, ageBiologicalTicks: 100),
            new Animal("Old", learnedTraitCount: 0, ageBiologicalTicks: 200)
        );

        Expect.AreEqual(2, order.Length);
        Expect.AreEqual("Old", order[0]);
        Expect.AreEqual("Young", order[1]);
    }

    [Test]
    public static void JuvenilesAreCulledYoungestFirst()
    {
        var order = Order(
            oldestFirst: false,
            new Animal("Young", learnedTraitCount: 0, ageBiologicalTicks: 100),
            new Animal("Old", learnedTraitCount: 0, ageBiologicalTicks: 200)
        );

        Expect.AreEqual(2, order.Length);
        Expect.AreEqual("Young", order[0]);
        Expect.AreEqual("Old", order[1]);
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

        Expect.AreEqual(2, order.Length);
        Expect.AreEqual("UntrainedYoung", order[0]);
        Expect.AreEqual("TrainedOld", order[1]);
    }

    [Test]
    public static void EligibleAnimalIsIncludedWhenAllTogglesAreOff() =>
        Expect.IsTrue(
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
                woolThreshold: 0f,
                avoidNamed: false,
                isNamed: false
            )
        );

    [Test]
    public static void AlreadyCullingOrCulledAnimalsAreAlwaysExcluded()
    {
        Expect.IsFalse(
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
                woolThreshold: 1f,
                avoidNamed: false,
                isNamed: false
            )
        );

        Expect.IsFalse(
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
                woolThreshold: 1f,
                avoidNamed: false,
                isNamed: false
            )
        );
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
                woolThreshold: 1f,
                avoidNamed: false,
                isNamed: false
            );
        }

        Expect.IsFalse(Eligible(false, false, false));
        Expect.IsFalse(Eligible(true, false, false));
        Expect.IsFalse(Eligible(true, true, false));
        Expect.IsTrue(Eligible(true, true, true));
    }

    [Test]
    public static void NamedAnimalsExcludedUnlessToggleAllows()
    {
        static bool Eligible(bool avoidNamed, bool isNamed)
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
                avoidMilkable: false,
                milkFullness: 0f,
                milkThreshold: 1f,
                avoidShearable: false,
                woolFullness: 0f,
                woolThreshold: 1f,
                avoidNamed,
                isNamed
            );
        }

        Expect.IsTrue(Eligible(false, true));
        Expect.IsTrue(Eligible(true, false));
        Expect.IsFalse(Eligible(true, true));
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
                woolThreshold,
                avoidNamed: false,
                isNamed: false
            );
        }

        // Exactly at threshold is not "below", so avoidance still excludes the animal.
        Expect.IsFalse(Eligible(1f, 1f, 0f, 1f));
        Expect.IsFalse(Eligible(0f, 1f, 1f, 1f));

        // Strictly below both thresholds is included.
        Expect.IsTrue(Eligible(0.5f, 1f, 0.5f, 1f));
    }

    [Test]
    public static void CullingTargetDifferenceMath()
    {
        Expect.AreEqual(0, ManagerJob_Livestock.CalculateCullingTargetDifference(10, 0, 10));
        Expect.AreEqual(5, ManagerJob_Livestock.CalculateCullingTargetDifference(15, 0, 10));
        Expect.AreEqual(-8, ManagerJob_Livestock.CalculateCullingTargetDifference(5, 3, 10));
        Expect.AreEqual(0, ManagerJob_Livestock.CalculateCullingTargetDifference(12, 2, 10));
    }

    [Test]
    public static void TamingTargetDifferenceMath()
    {
        Expect.AreEqual(0, ManagerJob_Livestock.CalculateTamingTargetDifference(10, 10, 0));
        Expect.AreEqual(6, ManagerJob_Livestock.CalculateTamingTargetDifference(10, 4, 0));
        Expect.AreEqual(-3, ManagerJob_Livestock.CalculateTamingTargetDifference(10, 8, 5));
    }

    [Test]
    public static void RoughlyEquallyDistributedFollowerCounts()
    {
        Expect.IsTrue(ManagerJob_Livestock.IsRoughlyEquallyDistributed([3, 3, 3]));
        Expect.IsTrue(ManagerJob_Livestock.IsRoughlyEquallyDistributed([3, 4]));
        Expect.IsFalse(ManagerJob_Livestock.IsRoughlyEquallyDistributed([3, 5]));
        Expect.IsTrue(ManagerJob_Livestock.IsRoughlyEquallyDistributed([7]));
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

        Expect.AreEqual("Alice", chosen);
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

        Expect.AreEqual("Bob", chosen);
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

        Expect.AreEqual("Bob", chosen);
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

        Expect.AreEqual("Bob", chosen);
    }

    [Test]
    public static void PruneIntervalGating()
    {
        // Initial state (_lastPruneTick starts at -PruneIntervalTicks) should trigger a prune
        // immediately at tick 0.
        Expect.IsTrue(ManagerJob_Livestock.LivestockCachesComp.ShouldPrune(0, -5000, 5000));

        Expect.IsTrue(ManagerJob_Livestock.LivestockCachesComp.ShouldPrune(5000, 0, 5000));
        Expect.IsFalse(ManagerJob_Livestock.LivestockCachesComp.ShouldPrune(4999, 0, 5000));
    }

    [Test]
    public static void FormatAgeSexBucketOmitsCulledSuffixWhenCullingRemovesAnimals() =>
        Expect.AreEqual(
            "3/5, ",
            ManagerJob_Livestock.FormatAgeSexBucket(
                tame: 5,
                culled: 2,
                target: 5,
                cullingRemovesAnimals: true
            )
        );

    [Test]
    public static void FormatAgeSexBucketShowsCulledSuffixWhenCullingDoesNotRemoveAnimals() =>
        // When culling doesn't actually remove the animal (e.g. it's marked for slaughter but
        // still alive/present), the still-pending cull count is surfaced via a "(+N)" suffix so
        // the label doesn't silently undercount what's actually on the map.
        Expect.AreEqual(
            "3/5(+2), ",
            ManagerJob_Livestock.FormatAgeSexBucket(
                tame: 5,
                culled: 2,
                target: 5,
                cullingRemovesAnimals: false
            )
        );

    [Test]
    public static void FormatAgeSexBucketWithNoCullsOmitsSuffixEvenWhenNotRemoving() =>
        Expect.AreEqual(
            "5/5(+0), ",
            ManagerJob_Livestock.FormatAgeSexBucket(
                tame: 5,
                culled: 0,
                target: 5,
                cullingRemovesAnimals: false
            )
        );

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

        Expect.IsFalse(report.Accepted);
        Expect.IsFalse(visible);
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

        Expect.IsFalse(report.Accepted);
        Expect.IsFalse(visible);
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

        Expect.IsTrue(report.Accepted);
        Expect.IsTrue(visible);
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

        Expect.IsFalse(report.Accepted);
        Expect.IsTrue(visible);
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

        Expect.IsFalse(report.Accepted);
        Expect.IsFalse(visible);
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

        Expect.IsFalse(report.Accepted);
        Expect.IsTrue(visible);
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

        Expect.IsFalse(report.Accepted);
        Expect.IsTrue(visible);
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

        Expect.IsTrue(report.Accepted);
        Expect.IsTrue(visible);
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

        Expect.AreEqual(4, result.Count);
        Expect.AreEqual("d", result[0]);
        Expect.AreEqual("c", result[1]);
        Expect.AreEqual("b", result[2]);
        Expect.AreEqual("a", result[3]);
    }

    [Test]
    public static void TakeLastReversedClampsToAvailableCount()
    {
        var result = ManagerJob_Livestock.TakeLastReversed(["a", "b"], count: 10, selector: s => s);

        Expect.AreEqual(2, result.Count);
        Expect.AreEqual("b", result[0]);
        Expect.AreEqual("a", result[1]);
    }

    [Test]
    public static void TakeLastReversedWithZeroCountReturnsEmpty()
    {
        var result = ManagerJob_Livestock.TakeLastReversed(
            ["a", "b", "c"],
            count: 0,
            selector: s => s
        );

        Expect.IsEmpty(result);
    }

    [Test]
    public static void TakeLastReversedOnEmptyListReturnsEmpty()
    {
        var result = ManagerJob_Livestock.TakeLastReversed(
            Array.Empty<string>(),
            count: 5,
            selector: s => s
        );

        Expect.IsEmpty(result);
    }

    private sealed class Trainee(
        string name,
        long age,
        bool trained = false,
        bool eligible = true,
        bool named = false,
        bool bonded = false,
        bool ignored = false
    )
    {
        public string Name { get; } = name;
        public long Age { get; } = age;
        public bool Trained { get; } = trained;
        public bool Eligible { get; } = eligible;
        public bool Named { get; } = named;
        public bool Bonded { get; } = bonded;
        public bool Ignored { get; } = ignored;
    }

    private static string[] SelectForLimit(
        int limit,
        ManagerJob_Livestock.TrainingPriority priority,
        bool prioritizeNamed,
        bool prioritizeBonded,
        params Trainee[] animals
    ) =>
        [
            .. ManagerJob_Livestock
                .SelectAnimalsForTrainingLimit(
                    limit,
                    animals,
                    a => a.Trained,
                    a => a.Eligible,
                    a => a.Ignored,
                    a => a.Named,
                    a => a.Bonded,
                    a => a.Age,
                    priority,
                    prioritizeNamed,
                    prioritizeBonded
                )
                .Select(a => a.Name),
        ];

    [Test]
    public static void TrainingLimitKeepsAlreadyTrainedAnimalsAndFillsRemainingSlots()
    {
        var result = SelectForLimit(
            2,
            ManagerJob_Livestock.TrainingPriority.Oldest,
            false,
            false,
            new Trainee("Old", 300),
            new Trainee("Trained", 100, trained: true),
            new Trainee("Young", 200)
        );

        Expect.AreEqual(2, result.Length);
        Expect.AreEqual("Trained", result[0]);
        Expect.AreEqual("Old", result[1]);
    }

    [Test]
    public static void TrainingLimitNeverDropsTrainedAnimalsOverTheLimit()
    {
        var result = SelectForLimit(
            1,
            ManagerJob_Livestock.TrainingPriority.Oldest,
            false,
            false,
            new Trainee("A", 100, trained: true),
            new Trainee("B", 200, trained: true),
            new Trainee("C", 300)
        );

        Expect.AreEqual(2, result.Length);
        Expect.IsFalse(result.Contains("C"));
    }

    [Test]
    public static void TrainingLimitSkipsIneligibleAnimals()
    {
        var result = SelectForLimit(
            2,
            ManagerJob_Livestock.TrainingPriority.Oldest,
            false,
            false,
            new Trainee("Ineligible", 500, eligible: false),
            new Trainee("Eligible", 100)
        );

        Expect.AreEqual(1, result.Length);
        Expect.AreEqual("Eligible", result[0]);
    }

    [Test]
    public static void TrainingLimitYoungestPriorityPicksYoungestFirst()
    {
        var result = SelectForLimit(
            1,
            ManagerJob_Livestock.TrainingPriority.Youngest,
            false,
            false,
            new Trainee("Old", 300),
            new Trainee("Young", 100)
        );

        Expect.AreEqual("Young", result[0]);
    }

    [Test]
    public static void TrainingLimitPrioritizesBondedThenNamedOverAge()
    {
        var result = SelectForLimit(
            2,
            ManagerJob_Livestock.TrainingPriority.Oldest,
            true,
            true,
            new Trainee("Oldest", 900),
            new Trainee("Named", 100, named: true),
            new Trainee("Bonded", 50, bonded: true)
        );

        Expect.AreEqual(2, result.Length);
        Expect.AreEqual("Bonded", result[0]);
        Expect.AreEqual("Named", result[1]);
    }

    [Test]
    public static void TrainingLimitIgnoredAnimalsDoNotCountTowardTheLimit()
    {
        var result = SelectForLimit(
            2,
            ManagerJob_Livestock.TrainingPriority.Oldest,
            false,
            false,
            new Trainee("Ancient", 900, trained: true, ignored: true),
            new Trainee("Trained", 100, trained: true),
            new Trainee("Young", 200)
        );

        Expect.AreEqual(2, result.Length);
        Expect.IsFalse(result.Contains("Ancient"));
        Expect.AreEqual("Young", result[1]);
    }

    [Test]
    public static void TrainingLimitIgnoredAnimalsAreNotPicked()
    {
        var result = SelectForLimit(
            2,
            ManagerJob_Livestock.TrainingPriority.Oldest,
            false,
            false,
            new Trainee("Ancient", 900, ignored: true),
            new Trainee("Young", 200)
        );

        Expect.AreEqual(1, result.Length);
        Expect.AreEqual("Young", result[0]);
    }
}
