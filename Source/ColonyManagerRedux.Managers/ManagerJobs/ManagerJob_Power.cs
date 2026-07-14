// ManagerJob_Mining.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.Extensions;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
[CoroutineSettingsType]
internal sealed class ManagerJob_Power
    : ManagerJob<ManagerSettings_Power, ManagerJob_Power.PowerWorkData>
{
    // What GatherJobDataCoroutine decided about this run. RefreshLists is the only outcome that
    // leads to any work being reported as done; the other two mirror the old TryDoJobCoroutine's
    // early yield breaks (no powered station online / historical data recording disabled).
    internal enum PowerJobOutcome
    {
        NoPoweredStationOnline,
        HistoricalDataRecordingDisabled,
        RefreshLists,
    }

    /// <summary>
    /// Carries the decision made by <see cref="GatherJobDataCoroutine"/> (which doesn't touch
    /// the game) to <see cref="ExecuteJobDataCoroutine"/> (which only needs to report that work
    /// was done - refreshing the trader/battery bookkeeping lists doesn't mutate the game, so
    /// it's done entirely during gather).
    /// </summary>
    internal sealed class PowerWorkData;

    [HotSwappable]
    [CoroutineSettingsType]
    public sealed class HistoryWorker : HistoryWorker<ManagerJob_Power>
    {
        public override bool UpdatesMax => true;

#pragma warning disable IDE0028 // Simplify collection initialization
        private readonly ConditionalWeakTable<
            ManagerJob_Power,
            CachedValue<(int current, int)[]>
        > cachedTrades = new();
#pragma warning restore IDE0028 // Simplify collection initialization

        private CachedValue<(int current, int)[]> GetCachedTradeForJob(
            ManagerJob_Power managerJob
        ) => cachedTrades.GetValue(managerJob, _ => new([]));

        public override Coroutine GetCountForHistoryChapterCoroutine(
            ManagerJob_Power managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> count
        )
        {
            var cachedTrade = GetCachedTradeForJob(managerJob);
            var trade = cachedTrade.Value;

            count.Value =
                chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryProduction
                    ? Utilities.SaturatingIntSum(
                        trade.Where(i => i.current > 0).Select(i => i.current)
                    )
                : chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryConsumption
                    ? Utilities.SaturatingIntSum(
                        trade.Where(i => i.current < 0).Select(i => Utilities.SafeAbs(i.current))
                    )
                : chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryBatteries
                    ? Utilities.SaturatingIntSum(
                        managerJob.GetCurrentBatteries().Select(b => b.current)
                    )
                : throw new ArgumentException($"Unexpected chapterDef value {chapterDef.defName}");

            yield break;
        }

        public override Coroutine GetTargetForHistoryChapterCoroutine(
            ManagerJob_Power managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> target
        )
        {
            target.Value = 0;
            yield break;
        }

        public override Coroutine GetMaxForHistoryChapterCoroutine(
            ManagerJob_Power managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> max
        )
        {
            max.Value =
                chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryBatteries
                    ? (int)SumNested(
                        managerJob._batteries,
                        battery => battery.Props.storedEnergyMax
                    )
                    : 0;
            yield break;
        }

        [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
        public override Coroutine HistoryUpdateCoroutine(ManagerJob_Power managerJob, int tick)
        {
            var ticksBetweenOperations =
                ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                    (Func<ManagerJob_Power, int, Coroutine>)HistoryUpdateCoroutine
                );

            yield return managerJob.RefreshBuildingLists().ResumeWhenOtherCoroutineIsCompleted();
            yield return new ResumeAfterTicks(ticksBetweenOperations);
            yield return managerJob.RefreshCompLists().ResumeWhenOtherCoroutineIsCompleted();
            yield return new ResumeAfterTicks(ticksBetweenOperations);

            var cachedTrade = GetCachedTradeForJob(managerJob);
            if (!cachedTrade.TryGetValue(out var trade))
            {
                trade = managerJob.GetCurrentTrade();
                _ = cachedTrade.Update(trade);
            }
            managerJob.tradingHistory.UpdateThingCountAndMax(
                [.. managerJob._traders.Select(list => list.Count)],
                [.. managerJob._traders.Select(list => 0)]
            );

            managerJob.tradingHistory.Update(tick, trade);
        }
    }

    public static List<ThingDef> BatteryDefs => field ??= [.. GetBatteryDefs()];

    public static List<ThingDef> TraderDefs => field ??= [.. GetTraderDefs()];

    private List<Building> _batteryBuildings = [];
    private List<Building> _traderBuildings = [];
    private readonly List<List<CompPowerBattery>> _batteries = [];
    private readonly List<List<CompPowerTrader>> _traders = [];

    private readonly CachedValue<int[]> cachedTradeCounts = new([]);
    private int[] CachedTradeCounts
    {
        get
        {
            if (!cachedTradeCounts.TryGetValue(out var trade))
            {
                var (producerCount, consumerCount) = CountByOutputSign(
                    _traders,
                    i => i.PowerOutput
                );
                trade = [producerCount, consumerCount];
                _ = cachedTradeCounts.Update(trade);
            }
            return trade;
        }
    }

    /// <summary>
    /// Counts how many items across <paramref name="groups"/> have a positive
    /// (<paramref name="outputSelector"/> &gt; 0, "producer") or negative ("consumer") output.
    /// Items with exactly zero output are counted as neither.
    /// </summary>
    internal static (int producers, int consumers) CountByOutputSign<T>(
        IEnumerable<IEnumerable<T>> groups,
        Func<T, float> outputSelector
    )
    {
        var flattened = groups.SelectMany(g => g).ToList();
        var producers = flattened.Count(i => outputSelector(i) > 0);
        var consumers = flattened.Count(i => outputSelector(i) < 0);
        return (producers, consumers);
    }

    /// <summary>
    /// Sums <paramref name="selector"/> across every item in every group of
    /// <paramref name="groups"/>, e.g. the battery-storage-max history chapter's sum across all
    /// battery-type groups' individual batteries.
    /// </summary>
    internal static float SumNested<T>(
        IEnumerable<IEnumerable<T>> groups,
        Func<T, float> selector
    ) => groups.Sum(group => group.Sum(selector));

    internal int ProducerCount => CachedTradeCounts[0];
    internal int ConsumerCount => CachedTradeCounts[1];

    private readonly CachedValue<int> cachedBatteryCount = new(0);
    private int CachedBatteryCount
    {
        get
        {
            if (!cachedBatteryCount.TryGetValue(out var batteryCount))
            {
                batteryCount = _batteries?.SelectMany(b => b).Count() ?? 0;
                _ = cachedBatteryCount.Update(batteryCount);
            }
            return batteryCount;
        }
    }
    internal int BatteryCount => CachedBatteryCount;

    internal History tradingHistory;

    private readonly CachedValue<bool> _cachedAnyPoweredStationOnline = new(false);
    public bool AnyPoweredStationOnline
    {
        get
        {
            if (_cachedAnyPoweredStationOnline.TryGetValue(out var value))
            {
                return value;
            }

            value = Manager
                .map.listerBuildings.AllBuildingsColonistOfClass<Building_ManagerStation>()
                .Select(t => t.TryGetComp<CompPowerTrader>())
                .Concat(
                    Manager
                        .map.listerBuildings.AllBuildingsColonistOfClass<Building_AIManager>()
                        .Select(t => t.TryGetComp<CompPowerTrader>())
                )
                .Any(c => c != null && c.PowerOn);
            _ = _cachedAnyPoweredStationOnline.Update(value);
            return value;
        }
    }

    public override bool IsTransferable => Manager.ScribeSameGameData;

    public ManagerJob_Power(Manager manager)
        : base(manager)
    {
        tradingHistory =
            Scribe.mode == LoadSaveMode.Inactive
                ? new History(
                    TraderDefs
                        .Select(def => new ThingDefCount(
                            def,
                            manager.map.listerBuildings.AllBuildingsColonistOfDef(def).Count
                        ))
                        .ToArray()
                )
                {
                    DrawOptions = false,
                    DrawInlineLegend = false,
                    YAxisSuffix = "W",
                    DrawTargetLine = false,
                }
                : null!;
    }

    public override string IsCompletedTooltip =>
        "ColonyManagerRedux.Energy.RecordHistoricalDataDisabled".Translate().CapitalizeFirst();

    public override IEnumerable<string> Targets => [];

    public override WorkTypeDef? WorkTypeDef => ManagerWorkTypeDefOf.Managing;

    public override void CleanUp(ManagerLog? jobLog) =>
        // The power job is never removed/cleaned up
        throw new NotImplementedException();

    /// <summary>
    /// Decides what a run of the job should do based on whether any powered manager station is
    /// online and whether historical data recording is enabled, mirroring the early yield-break
    /// branches the old single-phase <c>TryDoJobCoroutine</c> used to have.
    /// </summary>
    internal static PowerJobOutcome DeterminePowerJobOutcome(
        bool anyPoweredStationOnline,
        bool recordHistoricalData
    ) =>
        !anyPoweredStationOnline ? PowerJobOutcome.NoPoweredStationOnline
        : !recordHistoricalData ? PowerJobOutcome.HistoricalDataRecordingDisabled
        : PowerJobOutcome.RefreshLists;

    [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
    protected override Coroutine GatherJobDataCoroutine(
        ManagerLog jobLog,
        AnyBoxed<PowerWorkData?> data
    )
    {
        var outcome = DeterminePowerJobOutcome(
            AnyPoweredStationOnline,
            ColonyManagerReduxMod.Settings.RecordHistoricalData
        );

        if (outcome == PowerJobOutcome.NoPoweredStationOnline)
        {
            yield break;
        }

        if (outcome == PowerJobOutcome.HistoricalDataRecordingDisabled)
        {
            if (JobState != ManagerJobState.Completed)
            {
                JobState = ManagerJobState.Completed;
            }
            yield break;
        }

        JobState = ManagerJobState.Active;

        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                (Func<ManagerLog, AnyBoxed<PowerWorkData?>, Coroutine>)GatherJobDataCoroutine
            );

        // Refreshing these lists only updates the job's own bookkeeping fields (_traderBuildings,
        // _batteryBuildings, _traders, _batteries) from a read-only query of the map's buildings;
        // it doesn't change anything in the game itself, so it's safe to do while gathering.
        yield return RefreshBuildingLists(jobLog).ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);
        yield return RefreshCompLists(jobLog).ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        data.Value = new PowerWorkData();
    }

    [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
    protected override Coroutine ExecuteJobDataCoroutine(
        ManagerLog jobLog,
        PowerWorkData data,
        Boxed<bool> workDone
    )
    {
        // All the actual work happened during gather; there's nothing left to apply.
        workDone.Value = true;
        yield break;
    }

    private static IEnumerable<ThingDef> GetTraderDefs() =>
        from td in DefDatabase<ThingDef>.AllDefsListForReading
        where td.HasCompOrChildCompOf(typeof(CompPowerTrader))
        select td;

    private static IEnumerable<ThingDef> GetBatteryDefs() =>
        from td in DefDatabase<ThingDef>.AllDefsListForReading
        where td.HasCompOrChildCompOf(typeof(CompPowerBattery))
        select td;

    private bool _isRefreshingBuildingLists;

    [CoroutineSettingsMethod]
    private Coroutine RefreshBuildingLists(ManagerLog? jobLog = null)
    {
        if (_isRefreshingBuildingLists)
        {
            yield return new ResumeWhenTrue(() => !_isRefreshingBuildingLists);
            yield break;
        }

        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            RefreshBuildingLists
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                RefreshBuildingLists
            );

        _isRefreshingBuildingLists = true;
        using var _ = new DoOnDispose(() => _isRefreshingBuildingLists = false);

        var buildingsBefore = _traderBuildings.Count;
        var batteriesBefore = _batteryBuildings.Count;

        _traderBuildings.Clear();
        _batteryBuildings.Clear();

        foreach (var (def, i) in TraderDefs.Select((d, i) => (d, i)))
        {
            _traderBuildings.AddRange(Manager.map.listerBuildings.AllBuildingsColonistOfDef(def));
            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }

        foreach (var (def, i) in BatteryDefs.Select((d, i) => (d, i)))
        {
            _batteryBuildings.AddRange(Manager.map.listerBuildings.AllBuildingsColonistOfDef(def));
            if (i > 0 && i % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }

        var buildingsAfter = _traderBuildings.Count;
        var batteriesAfter = _batteryBuildings.Count;

        if (buildingsBefore != buildingsAfter || batteriesBefore != batteriesAfter)
        {
            jobLog?.AddDetail(
                "ColonyManagerRedux.Energy.Logs.InventoriedBuildings".Translate(
                    buildingsBefore,
                    batteriesBefore,
                    buildingsAfter,
                    batteriesAfter
                )
            );
        }
    }

    private bool _isRefreshingCompLists;
    private readonly List<(IEnumerable<CompPowerTrader> traders, int i)> _refreshCompListTraders =
    [];
    private readonly List<Building> _refreshCompListBatteryBuildings = [];
    private readonly List<Building> _refreshCompListTraderBuildings = [];

    [CoroutineSettingsMethod]
    private Coroutine RefreshCompLists(ManagerLog? jobLog = null)
    {
        if (_isRefreshingCompLists)
        {
            yield return new ResumeWhenTrue(() => !_isRefreshingCompLists);
            yield break;
        }

        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            RefreshCompLists
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(RefreshCompLists);

        _isRefreshingCompLists = true;
        using var _ = new DoOnDispose(() => _isRefreshingCompLists = false);
        using var _2 = new DoOnDispose(_refreshCompListTraders.Clear);
        using var _3 = new DoOnDispose(_refreshCompListBatteryBuildings.Clear);
        using var _4 = new DoOnDispose(_refreshCompListTraderBuildings.Clear);

        foreach (var traders in _traders)
        {
            traders.Clear();
        }
        foreach (var batteries in _batteries)
        {
            batteries.Clear();
        }

        // get list of power trader comps per def for consumers and producers.
        var compCounter = -1;

        TrimListTo(_traders, TraderDefs.Count);

        _refreshCompListTraderBuildings.Clear();
        _refreshCompListTraderBuildings.AddRange(_traderBuildings);

        _refreshCompListTraders.Clear();
        _refreshCompListTraders.AddRange(
            TraderDefs.Select(
                (def, i) =>
                    (
                        _refreshCompListTraderBuildings
                            .Where(b => b.def == def)
                            .Select(b => b.GetComp<CompPowerTrader>()),
                        i
                    )
            )
        );
        foreach (var (traders, i) in _refreshCompListTraders)
        {
            if (i == _traders.Count)
            {
                _traders.Add([]);
            }
            foreach (var comp in traders)
            {
                _traders[i].Add(comp);
                if (++compCounter > 0 && compCounter % operationsPerTick == 0)
                {
                    yield return new ResumeAfterTicks(ticksBetweenOperations);
                }
            }
        }

        TrimListTo(_batteries, BatteryDefs.Count);

        _refreshCompListBatteryBuildings.Clear();
        _refreshCompListBatteryBuildings.AddRange(_batteryBuildings);

        foreach (
            var (batteries, i) in BatteryDefs.Select(
                (def, i) =>
                    (
                        _refreshCompListBatteryBuildings
                            .Where(b => b.def == def)
                            .Select(b => b.GetComp<CompPowerBattery>()),
                        i
                    )
            )
        )
        {
            if (i == _batteries.Count)
            {
                _batteries.Add([]);
            }
            foreach (var comp in batteries)
            {
                _batteries[i].Add(comp);
                if (++compCounter > 0 && compCounter % operationsPerTick == 0)
                {
                    yield return new ResumeAfterTicks(ticksBetweenOperations);
                }
            }
        }

        if (jobLog != null)
        {
            var tradersPerType = _traders
                .Where(cl => cl.Count > 0)
                .Select(cl => $" - {cl[0].parent.def.LabelCap}: {cl.Count}");
            var batteriesPerType = _batteries
                .Where(cl => cl.Count > 0)
                .Select(cl => $" - {cl[0].parent.def.LabelCap}: {cl.Count}");
            jobLog?.AddDetail(
                "ColonyManagerRedux.Energy.Logs.InventoriedBuildingPerType".Translate(
                    string.Join("\n", tradersPerType),
                    string.Join("\n", batteriesPerType)
                )
            );
        }
    }

    private (int current, int max)[] GetCurrentBatteries() =>
        [
            .. _batteries.Select(list =>
                (
                    (int)list.Sum(battery => battery.StoredEnergy),
                    (int)list.Sum(battery => battery.Props.storedEnergyMax)
                )
            ),
        ];

    private (int current, int)[] GetCurrentTrade() =>
        [
            .. _traders.Select(list =>
                ((int)list.Sum(trader => trader.PowerOn ? trader.PowerOutput : 0f), 0)
            ),
        ];

    public override void ExposeData()
    {
        base.ExposeData();

        if (Manager.ScribeSameMapData)
        {
            Scribe_Collections.Look(ref _traderBuildings, "traders", LookMode.Reference);
            Scribe_Collections.Look(ref _batteryBuildings, "batteries", LookMode.Reference);
        }
        Scribe_Deep.Look(ref tradingHistory, "tradingHistory");

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            tradingHistory.UpdateThingDefs(TraderDefs);
            _traderBuildings.RemoveWhere(b => b == null);
            _batteryBuildings.RemoveWhere(b => b == null);
            RefreshCompLists().RunImmediatelyToCompletion();
        }
    }

    public override void PostImport()
    {
        base.PostImport();

        var otherJobs = Manager
            .JobTracker.JobsOfType<ManagerJob_Power>()
            .Where(j => j != this)
            .ToList();
        var remainingJob = PickSurvivor(this, otherJobs, j => j.AnyPoweredStationOnline);

        if (remainingJob != this)
        {
            // We got imported to a map that already has a valid power job, so we need to delete our job
            // (and any other duplicates that may have accumulated).
            ColonyManagerReduxMod.Instance.LogDebug(
                $"ManagerJob_Power.PostImport: Deleting {this} because another power job is already present."
            );
            Manager.JobTracker.Delete(this, false);
        }
        foreach (var extraJob in otherJobs.Where(j => j != remainingJob))
        {
            if (remainingJob == this)
            {
                // We got imported to a map that has power job(s), but none have powered stations online,
                // so we replace them all with our job.
                ColonyManagerReduxMod.Instance.LogDebug(
                    $"ManagerJob_Power.PostImport: Replacing {extraJob} with {this} because it has no powered stations online."
                );
            }
            Manager.JobTracker.Delete(extraJob, false);
        }

        _cachedAnyPoweredStationOnline.Invalidate();
        RefreshBuildingLists().RunImmediatelyToCompletion();
        RefreshCompLists().RunImmediatelyToCompletion();
    }

    /// <summary>
    /// Picks which of <paramref name="current"/> or <paramref name="others"/> should survive a
    /// post-import deduplication: the first online job among <paramref name="others"/>, or
    /// <paramref name="current"/> if none of them are online.
    /// </summary>
    internal static T PickSurvivor<T>(T current, IReadOnlyList<T> others, Func<T, bool> isOnline)
        where T : class => others.FirstOrDefault(isOnline) ?? current;

    /// <summary>
    /// Removes trailing entries from <paramref name="list"/> so its length matches
    /// <paramref name="newCount"/>. No-op if <paramref name="list"/> is already that length or
    /// shorter.
    /// </summary>
    internal static void TrimListTo<T>(List<T> list, int newCount)
    {
        if (newCount < list.Count)
        {
            list.RemoveRange(newCount, list.Count - newCount);
        }
    }
}
