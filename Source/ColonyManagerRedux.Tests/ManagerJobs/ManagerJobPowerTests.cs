// ManagerJobPowerTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ManagerJobPowerTests
{
    [Test]
    public static void ShouldStartSuspendedIsFalseWhenSettingIsDisabled() =>
        // Old behavior, kept as the default: the auto-created Power job never starts suspended
        // unless the player opts in via the setting - see GitHub issue #18.
        Expect.IsFalse(ManagerTab_Power.ShouldStartSuspended(false, mapIsPlayerHome: false));

    [Test]
    public static void ShouldStartSuspendedIsFalseOnHomeMapsEvenWhenSettingIsEnabled() =>
        Expect.IsFalse(ManagerTab_Power.ShouldStartSuspended(true, mapIsPlayerHome: true));

    [Test]
    public static void ShouldStartSuspendedIsTrueOnNonHomeMapsWhenSettingIsEnabled() =>
        // A quest site or other map the player is just visiting shouldn't start nagging about an
        // auto-created Power job the moment it exists - see GitHub issue #18.
        Expect.IsTrue(ManagerTab_Power.ShouldStartSuspended(true, mapIsPlayerHome: false));

    [Test]
    public static void MapIsActuallyPlayerHomeIsTrueWhenPlayerOwnsTheMapParent() =>
        Expect.IsTrue(
            ManagerTab_Power.MapIsActuallyPlayerHome(
                hasPlayerOwnedHomeMapParent: true,
                playerHasGravEngineOnMap: false
            )
        );

    [Test]
    public static void MapIsActuallyPlayerHomeIsTrueWhenGravEngineIsPhysicallyOnTheMap() =>
        Expect.IsTrue(
            ManagerTab_Power.MapIsActuallyPlayerHome(
                hasPlayerOwnedHomeMapParent: false,
                playerHasGravEngineOnMap: true
            )
        );

    [Test]
    public static void MapIsActuallyPlayerHomeIsFalseForAHostileEventSiteAGravshipLandedOn() =>
        // A map's world object doesn't become player-owned just because a gravship landed on or
        // was diverted to it (e.g. a hostile quest/event site) - unlike Map.IsPlayerHome, which
        // unconditionally reports true for any such map via wasSpawnedViaGravShipLanding.
        Expect.IsFalse(
            ManagerTab_Power.MapIsActuallyPlayerHome(
                hasPlayerOwnedHomeMapParent: false,
                playerHasGravEngineOnMap: false
            )
        );

    [Test]
    public static void TrimListToIsNoOpWhenListAlreadyAtTargetLength()
    {
        List<int> list = [1, 2, 3];
        ManagerJob_Power.TrimListTo(list, 3);

        Expect.AreEqual(3, list.Count);
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

        Expect.AreEqual(3, list.Count);
        Expect.IsTrue(list.Contains(1));
        Expect.IsTrue(list.Contains(2));
        Expect.IsTrue(list.Contains(3));
        Expect.IsFalse(list.Contains(4));
    }

    [Test]
    public static void TrimListToRemovesManyExcessEntries()
    {
        List<int> list = [1, 2, 3, 4, 5];
        ManagerJob_Power.TrimListTo(list, 1);

        Expect.AreEqual(1, list.Count);
        Expect.AreEqual(1, list[0]);
    }

    [Test]
    public static void TrimListToAllowsClearingToZero()
    {
        List<int> list = [1, 2, 3];
        ManagerJob_Power.TrimListTo(list, 0);

        Expect.IsEmpty(list);
    }

    [Test]
    public static void PickSurvivorKeepsCurrentWhenThereAreNoOthers()
    {
        var survivor = ManagerJob_Power.PickSurvivor("this", [], _ => true);
        Expect.AreEqual("this", survivor);
    }

    [Test]
    public static void PickSurvivorSwitchesToTheOnlineOtherJob()
    {
        var survivor = ManagerJob_Power.PickSurvivor("this", ["other"], j => j == "other");
        Expect.AreEqual("other", survivor);
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
        Expect.AreEqual("online1", survivor);
    }

    [Test]
    public static void PickSurvivorKeepsCurrentWhenNoOtherIsOnline()
    {
        var survivor = ManagerJob_Power.PickSurvivor("this", ["offline1", "offline2"], _ => false);
        Expect.AreEqual("this", survivor);
    }

    [Test]
    public static void DeterminePowerJobOutcomeIsNoPoweredStationOnlineWhenNoneOnline() =>
        Expect.AreEqual(
            ManagerJob_Power.PowerJobOutcome.NoPoweredStationOnline,
            ManagerJob_Power.DeterminePowerJobOutcome(
                anyPoweredStationOnline: false,
                recordHistoricalData: true
            )
        );

    [Test]
    public static void DeterminePowerJobOutcomeIsNoPoweredStationOnlineEvenWhenHistoricalDataDisabled() =>
        // No online station takes priority over the historical-data setting - matches the old
        // TryDoJobCoroutine's ordering, where the "any station online" check yield-broke first.
        Expect.AreEqual(
            ManagerJob_Power.PowerJobOutcome.NoPoweredStationOnline,
            ManagerJob_Power.DeterminePowerJobOutcome(
                anyPoweredStationOnline: false,
                recordHistoricalData: false
            )
        );

    [Test]
    public static void DeterminePowerJobOutcomeIsHistoricalDataRecordingDisabledWhenOnlineButSettingOff() =>
        Expect.AreEqual(
            ManagerJob_Power.PowerJobOutcome.HistoricalDataRecordingDisabled,
            ManagerJob_Power.DeterminePowerJobOutcome(
                anyPoweredStationOnline: true,
                recordHistoricalData: false
            )
        );

    [Test]
    public static void DeterminePowerJobOutcomeIsRefreshListsWhenOnlineAndRecordingEnabled() =>
        Expect.AreEqual(
            ManagerJob_Power.PowerJobOutcome.RefreshLists,
            ManagerJob_Power.DeterminePowerJobOutcome(
                anyPoweredStationOnline: true,
                recordHistoricalData: true
            )
        );

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

        Expect.AreEqual(0, producers);
        Expect.AreEqual(0, consumers);
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

        Expect.AreEqual(2, producers);
        Expect.AreEqual(0, consumers);
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

        Expect.AreEqual(0, producers);
        Expect.AreEqual(2, consumers);
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

        Expect.AreEqual(2, producers);
        Expect.AreEqual(2, consumers);
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

        Expect.AreEqual(1, producers);
        Expect.AreEqual(1, consumers);
    }

    private readonly struct Battery(float storedEnergyMax)
    {
        public float StoredEnergyMax { get; } = storedEnergyMax;
    }

    [Test]
    public static void SumNestedOfEmptyOuterListIsZero() =>
        Expect.AreEqual(0f, ManagerJob_Power.SumNested<Battery>([], b => b.StoredEnergyMax));

    [Test]
    public static void SumNestedOfEmptyInnerListsIsZero() =>
        Expect.AreEqual(
            0f,
            ManagerJob_Power.SumNested<Battery>(
                [
                    [],
                    [],
                ],
                b => b.StoredEnergyMax
            )
        );

    [Test]
    public static void SumNestedOfSingleGroupSingleItemReturnsThatValue() =>
        Expect.AreEqual(
            500f,
            ManagerJob_Power.SumNested(
                [
                    [new Battery(500f)],
                ],
                b => b.StoredEnergyMax
            )
        );

    [Test]
    public static void SumNestedAcrossMultipleGroupsSumsAllItems() =>
        // Mirrors the batteries-max history chapter: multiple battery-type groups, each
        // containing multiple batteries, must all contribute to a single flat sum.
        Expect.AreEqual(
            2000f,
            ManagerJob_Power.SumNested(
                [
                    [new Battery(500f), new Battery(500f)],
                    [new Battery(1000f)],
                ],
                b => b.StoredEnergyMax
            )
        );
}
