// ManagerJobPowerTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class ManagerJobPowerTests
{
    [Test]
    public static void TrimListToIsNoOpWhenListAlreadyAtTargetLength()
    {
        List<int> list = [1, 2, 3];
        ManagerJob_Power.TrimListTo(list, 3);

        Assert.ThatCollection(list).Has.Count(3);
    }

    [Test]
    public static void TrimListToRemovesExactlyOneExcessEntry()
    {
        // Regression guard for commit 265cfdb ("fix: correct off-by-one when trimming power
        // manager's trader/battery lists"): the old code used RemoveRange(count - 1, ...),
        // chopping one item too many/at the wrong start. Trimming from 4 down to 3 must remove
        // only the trailing entry.
        List<int> list = [1, 2, 3, 4];
        ManagerJob_Power.TrimListTo(list, 3);

        Assert.ThatCollection(list).Has.Count(3);
        Assert.ThatCollection(list).Does.Contain(1);
        Assert.ThatCollection(list).Does.Contain(2);
        Assert.ThatCollection(list).Does.Contain(3);
        Assert.ThatCollection(list).Does.Not.Contain(4);
    }

    [Test]
    public static void TrimListToRemovesManyExcessEntries()
    {
        List<int> list = [1, 2, 3, 4, 5];
        ManagerJob_Power.TrimListTo(list, 1);

        Assert.ThatCollection(list).Has.Count(1);
        Assert.That(list[0]).Is.EqualTo(1);
    }

    [Test]
    public static void TrimListToAllowsClearingToZero()
    {
        List<int> list = [1, 2, 3];
        ManagerJob_Power.TrimListTo(list, 0);

        Assert.ThatCollection(list).Is.Empty();
    }

    [Test]
    public static void PickSurvivorKeepsCurrentWhenThereAreNoOthers()
    {
        var survivor = ManagerJob_Power.PickSurvivor("this", [], _ => true);
        Assert.That(survivor).Is.EqualTo("this");
    }

    [Test]
    public static void PickSurvivorSwitchesToTheOnlineOtherJob()
    {
        var survivor = ManagerJob_Power.PickSurvivor("this", ["other"], j => j == "other");
        Assert.That(survivor).Is.EqualTo("other");
    }

    [Test]
    public static void PickSurvivorPicksFirstOnlineAmongMultipleOthers()
    {
        // Regression guard for commit 6118533 ("fix: prevent crash importing saves with 3+
        // power jobs on the same map"): the old code used SingleOrDefault, which throws when
        // more than one other job is online. FirstOrDefault must tolerate multiple online jobs.
        var survivor = ManagerJob_Power.PickSurvivor(
            "this",
            ["offline1", "online1", "online2"],
            j => j.StartsWith("online", StringComparison.Ordinal)
        );
        Assert.That(survivor).Is.EqualTo("online1");
    }

    [Test]
    public static void PickSurvivorKeepsCurrentWhenNoOtherIsOnline()
    {
        var survivor = ManagerJob_Power.PickSurvivor("this", ["offline1", "offline2"], _ => false);
        Assert.That(survivor).Is.EqualTo("this");
    }

    private readonly struct Trader(float powerOutput)
    {
        public float PowerOutput { get; } = powerOutput;
    }

    [Test]
    public static void CountByOutputSignOfEmptyGroupsIsZeroZero()
    {
        var (producers, consumers) = ManagerJob_Power.CountByOutputSign(
            [],
            (Trader t) => t.PowerOutput
        );

        Assert.That(producers).Is.EqualTo(0);
        Assert.That(consumers).Is.EqualTo(0);
    }

    [Test]
    public static void CountByOutputSignCountsAllPositiveAsProducers()
    {
        var (producers, consumers) = ManagerJob_Power.CountByOutputSign(
            [
                [new Trader(5f), new Trader(10f)],
            ],
            t => t.PowerOutput
        );

        Assert.That(producers).Is.EqualTo(2);
        Assert.That(consumers).Is.EqualTo(0);
    }

    [Test]
    public static void CountByOutputSignCountsAllNegativeAsConsumers()
    {
        var (producers, consumers) = ManagerJob_Power.CountByOutputSign(
            [
                [new Trader(-5f), new Trader(-10f)],
            ],
            t => t.PowerOutput
        );

        Assert.That(producers).Is.EqualTo(0);
        Assert.That(consumers).Is.EqualTo(2);
    }

    [Test]
    public static void CountByOutputSignCountsMixedAcrossMultipleGroups()
    {
        var (producers, consumers) = ManagerJob_Power.CountByOutputSign(
            [
                [new Trader(5f), new Trader(-3f)],
                [new Trader(-1f), new Trader(2f)],
            ],
            t => t.PowerOutput
        );

        Assert.That(producers).Is.EqualTo(2);
        Assert.That(consumers).Is.EqualTo(2);
    }

    [Test]
    public static void CountByOutputSignExcludesExactlyZeroOutput()
    {
        // An idle/unpowered trader reporting PowerOutput == 0 must count as neither a producer
        // nor a consumer (strict > / < comparisons).
        var (producers, consumers) = ManagerJob_Power.CountByOutputSign(
            [
                [new Trader(0f), new Trader(5f), new Trader(-5f)],
            ],
            t => t.PowerOutput
        );

        Assert.That(producers).Is.EqualTo(1);
        Assert.That(consumers).Is.EqualTo(1);
    }

    private readonly struct Battery(float storedEnergyMax)
    {
        public float StoredEnergyMax { get; } = storedEnergyMax;
    }

    [Test]
    public static void SumNestedOfEmptyOuterListIsZero() =>
        Assert.That(ManagerJob_Power.SumNested<Battery>([], b => b.StoredEnergyMax)).Is.EqualTo(0f);

    [Test]
    public static void SumNestedOfEmptyInnerListsIsZero() =>
        Assert
            .That(
                ManagerJob_Power.SumNested<Battery>(
                    [
                        [],
                        [],
                    ],
                    b => b.StoredEnergyMax
                )
            )
            .Is.EqualTo(0f);

    [Test]
    public static void SumNestedOfSingleGroupSingleItemReturnsThatValue() =>
        Assert
            .That(
                ManagerJob_Power.SumNested(
                    [
                        [new Battery(500f)],
                    ],
                    b => b.StoredEnergyMax
                )
            )
            .Is.EqualTo(500f);

    [Test]
    public static void SumNestedAcrossMultipleGroupsSumsAllItems() =>
        // Mirrors the batteries-max history chapter: multiple battery-type groups, each
        // containing multiple batteries, must all contribute to a single flat sum.
        Assert
            .That(
                ManagerJob_Power.SumNested(
                    [
                        [new Battery(500f), new Battery(500f)],
                        [new Battery(1000f)],
                    ],
                    b => b.StoredEnergyMax
                )
            )
            .Is.EqualTo(2000f);
}
