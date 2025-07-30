// ManagerJob_Mining.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Runtime.CompilerServices;
using ilyvion.Laboratory.Extensions;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
[CoroutineSettingsType]
internal sealed class ManagerJob_Power : ManagerJob
{
    [HotSwappable]
    [CoroutineSettingsType]
    public sealed class HistoryWorker : HistoryWorker<ManagerJob_Power>
    {
        public override bool UpdatesMax => true;

        private readonly ConditionalWeakTable<ManagerJob_Power, CachedValue<(int current, int)[]>> cachedTrades = new();
        private CachedValue<(int current, int)[]> GetCachedTradeForJob(ManagerJob_Power managerJob)
        {
            return cachedTrades.GetValue(managerJob, _ => new([]));
        }

        public override Coroutine GetCountForHistoryChapterCoroutine(
            ManagerJob_Power managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> count)
        {
            var cachedTrade = GetCachedTradeForJob(managerJob);
            var trade = cachedTrade.Value;

            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryProduction)
            {
                count.Value = trade.Where(i => i.current > 0).Sum(i => i.current);
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryConsumption)
            {
                count.Value = trade.Where(i => i.current < 0).Sum(i => Utilities.SafeAbs(i.current));
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryBatteries)
            {
                count.Value = managerJob.GetCurrentBatteries().Sum(b => b.current);
            }
            else
            {
                throw new ArgumentException($"Unexpected chapterDef value {chapterDef.defName}");
            }
            yield break;
        }

        public override Coroutine GetTargetForHistoryChapterCoroutine(
            ManagerJob_Power managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> target)
        {
            target.Value = 0;
            yield break;
        }

        public override Coroutine GetMaxForHistoryChapterCoroutine(
            ManagerJob_Power managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> max)
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryBatteries)
            {
                max.Value = (int)managerJob._batteries.Sum(list =>
                    list.Sum(battery => battery.Props.storedEnergyMax));
            }
            else
            {
                max.Value = 0;
            }
            yield break;
        }

        [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
        public override Coroutine HistoryUpdateCoroutine(ManagerJob_Power managerJob, int tick)
        {
            var ticksBetweenOperations = ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                (Func<ManagerJob_Power, int, Coroutine>)HistoryUpdateCoroutine);

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
                managerJob._traders.Select(list => list.Count).ToArray(),
                managerJob._traders.Select(list => 0).ToArray());

            managerJob.tradingHistory.Update(tick, trade);
        }
    }

    private static List<ThingDef>? _batteryDefs;
    public static List<ThingDef> BatteryDefs
    {
        get
        {
            _batteryDefs ??= GetBatteryDefs().ToList();
            return _batteryDefs;
        }
    }

    private static List<ThingDef>? _traderDefs;
    public static List<ThingDef> TraderDefs
    {
        get
        {
            _traderDefs ??= GetTraderDefs().ToList();
            return _traderDefs;
        }
    }

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
                var producerCount = _traders
                    .Sum(list => list.Count(i => i.PowerOutput > 0));
                var consumerCount = _traders
                    .Sum(list => list.Count(i => i.PowerOutput < 0));
                trade = [producerCount, consumerCount];
                _ = cachedTradeCounts.Update(trade);
            }
            return trade;
        }
    }
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
                cachedBatteryCount.Update(batteryCount);
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

            value = Manager.map.listerBuildings
                .AllBuildingsColonistOfClass<Building_ManagerStation>()
                .Select(t => t.TryGetComp<CompPowerTrader>())
                .Concat(Manager.map.listerBuildings
                    .AllBuildingsColonistOfClass<Building_AIManager>()
                    .Select(t => t.TryGetComp<CompPowerTrader>()))
                .Any(c => c != null && c.PowerOn);
            _cachedAnyPoweredStationOnline.Update(value);
            return value;
        }
    }

    public override bool IsTransferable => Manager.ScribeSameGameData;

    public ManagerJob_Power(Manager manager) : base(manager)
    {
        if (Scribe.mode == LoadSaveMode.Inactive)
        {
            tradingHistory = new History(TraderDefs
                .Select(def => new ThingDefCount(
                    def,
                    manager.map.listerBuildings.AllBuildingsColonistOfDef(def).Count))
                .ToArray())
            {
                DrawOptions = false,
                DrawInlineLegend = false,
                YAxisSuffix = "W",
                DrawTargetLine = false,
            };
        }
        else
        {
            tradingHistory = null!;
        }
    }

    public override string IsCompletedTooltip => "ColonyManagerRedux.Energy.RecordHistoricalDataDisabled".Translate().CapitalizeFirst();

    public override IEnumerable<string> Targets => [];

    public override WorkTypeDef? WorkTypeDef => ManagerWorkTypeDefOf.Managing;

    public override void CleanUp(ManagerLog? jobLog)
    {
        // The power job is never removed/cleaned up
        throw new NotImplementedException();
    }

    [CoroutineSettingsMethod(HasOperationsPerTickSetting = false)]
    public override Coroutine TryDoJobCoroutine(ManagerLog jobLog, Boxed<bool> workDone)
    {
        if (!AnyPoweredStationOnline)
        {
            yield break;
        }

        if (!ColonyManagerReduxMod.Settings.RecordHistoricalData)
        {
            if (JobState != ManagerJobState.Completed)
            {
                JobState = ManagerJobState.Completed;
            }
            yield break;
        }
        else
        {
            JobState = ManagerJobState.Active;
        }

        var ticksBetweenOperations = ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(TryDoJobCoroutine);

        yield return RefreshBuildingLists(jobLog).ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);
        yield return RefreshCompLists(jobLog).ResumeWhenOtherCoroutineIsCompleted();
        yield return new ResumeAfterTicks(ticksBetweenOperations);
        workDone.Value = true;
    }

    private static IEnumerable<ThingDef> GetTraderDefs()
    {
        return from td in DefDatabase<ThingDef>.AllDefsListForReading
               where td.HasCompOrChildCompOf(typeof(CompPowerTrader))
               select td;
    }

    private static IEnumerable<ThingDef> GetBatteryDefs()
    {
        return from td in DefDatabase<ThingDef>.AllDefsListForReading
               where td.HasCompOrChildCompOf(typeof(CompPowerBattery))
               select td;
    }

    private bool _isRefreshingBuildingLists = false;
    [CoroutineSettingsMethod]
    private Coroutine RefreshBuildingLists(ManagerLog? jobLog = null)
    {
        if (_isRefreshingBuildingLists)
        {
            yield return new ResumeWhenTrue(() => !_isRefreshingBuildingLists);
            yield break;
        }

        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(RefreshBuildingLists);
        var ticksBetweenOperations = ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(RefreshBuildingLists);

        _isRefreshingBuildingLists = true;
        using var _ = new DoOnDispose(() => _isRefreshingBuildingLists = false);

        int buildingsBefore = _traderBuildings.Count;
        int batteriesBefore = _batteryBuildings.Count;

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

        int buildingsAfter = _traderBuildings.Count;
        int batteriesAfter = _batteryBuildings.Count;

        if (buildingsBefore != buildingsAfter || batteriesBefore != batteriesAfter)
        {
            jobLog?.AddDetail("ColonyManagerRedux.Energy.Logs.InventoriedBuildings"
                .Translate(buildingsBefore, batteriesBefore, buildingsAfter, batteriesAfter));
        }
    }

    private bool _isRefreshingCompLists = false;
    private readonly List<(IEnumerable<CompPowerTrader> traders, int i)> _refreshCompListTraders = [];
    [CoroutineSettingsMethod]
    private Coroutine RefreshCompLists(ManagerLog? jobLog = null)
    {
        if (_isRefreshingCompLists)
        {
            yield return new ResumeWhenTrue(() => !_isRefreshingCompLists);
            yield break;
        }

        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(RefreshCompLists);
        var ticksBetweenOperations = ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(RefreshCompLists);

        _isRefreshingCompLists = true;
        using var _ = new DoOnDispose(() => _isRefreshingCompLists = false);
        using var _2 = new DoOnDispose(_refreshCompListTraders.Clear);

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

        if (TraderDefs.Count < _traders.Count)
        {
            _traders.RemoveRange(TraderDefs.Count - 1, _traders.Count - TraderDefs.Count);
        }

        _refreshCompListTraders.Clear();
        _refreshCompListTraders.AddRange(TraderDefs
            .Select((def, i) => (_traderBuildings
                .Where(b => b.def == def)
                .Select(b => b.GetComp<CompPowerTrader>()), i)));
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

        if (BatteryDefs.Count < _batteries.Count)
        {
            _batteries.RemoveRange(BatteryDefs.Count - 1, _batteries.Count - BatteryDefs.Count);
        }
        foreach (var (batteries, i) in BatteryDefs
            .Select((def, i) => (_batteryBuildings
                .Where(b => b.def == def)
                .Select(b => b.GetComp<CompPowerBattery>()), i)))
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
            jobLog?.AddDetail("ColonyManagerRedux.Energy.Logs.InventoriedBuildingPerType"
                .Translate(string.Join("\n", tradersPerType), string.Join("\n", batteriesPerType)));
        }
    }

    private (int current, int max)[] GetCurrentBatteries()
    {
        return _batteries
            .Select(list => (
                (int)list.Sum(battery => battery.StoredEnergy),
                (int)list.Sum(battery => battery.Props.storedEnergyMax)))
            .ToArray();
    }

    private (int current, int)[] GetCurrentTrade()
    {
        return _traders
            .Select(list => ((int)list.Sum(trader => trader.PowerOn ? trader.PowerOutput : 0f), 0))
            .ToArray();
    }

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

        ManagerJob_Power remainingJob;
        if (Manager.JobTracker.JobsOfType<ManagerJob_Power>().Count() > 1)
        {
            var otherJob = Manager.JobTracker.JobsOfType<ManagerJob_Power>().SingleOrDefault(j => j != this);
            if (otherJob.AnyPoweredStationOnline)
            {
                // We got imported to a map that already has a valid power job, so we need to delete our job.
                ColonyManagerReduxMod.Instance.LogDebug(
                    $"ManagerJob_Power.PostImport: Deleting {this} because another power job is already present.");
                Manager.JobTracker.Delete(this, false);
                remainingJob = otherJob;
            }
            else
            {
                // We got imported to a map that has a power job, but it has no powered stations online, so we replace that job with our job.
                ColonyManagerReduxMod.Instance.LogDebug(
                    $"ManagerJob_Power.PostImport: Replacing {otherJob} with {this} because it has no powered stations online.");
                Manager.JobTracker.Delete(otherJob, false);
                remainingJob = this;
            }
        }
        else
        {
            remainingJob = this;
        }

        _cachedAnyPoweredStationOnline.Invalidate();
        RefreshBuildingLists().RunImmediatelyToCompletion();
        RefreshCompLists().RunImmediatelyToCompletion();
    }
}
