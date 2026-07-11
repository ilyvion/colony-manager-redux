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
}
