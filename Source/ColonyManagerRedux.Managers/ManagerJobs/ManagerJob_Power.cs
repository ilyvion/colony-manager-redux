// ManagerJob_Mining.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.Extensions;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerJob_Power : ManagerJob
{
    [HotSwappable]
    public sealed class HistoryWorker : HistoryWorker<ManagerJob_Power>
    {
        public override bool UpdatesMax => true;

        private readonly CachedValue<(int current, int)[]> cachedTrade = new([]);
        public override Coroutine GetCountForHistoryChapterCoroutine(
            ManagerJob_Power managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> count)
        {
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

        public override void HistoryUpdateTick(ManagerJob_Power managerJob, int tick)
        {
            if (!cachedTrade.TryGetValue(out var trade))
            {
                trade = managerJob.GetCurrentTrade();
                cachedTrade.Update(trade);
            }

            if (History.IsUpdateTick)
            {
                managerJob.tradingHistory.UpdateThingCountAndMax(
                    managerJob._traders.Select(list => list.Count).ToArray(),
                    managerJob._traders.Select(list => 0).ToArray());

                managerJob.tradingHistory.Update(tick, trade);
            }
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
    private List<List<CompPowerBattery>> _batteries = [];
    private List<List<CompPowerTrader>> _traders = [];

    private readonly CachedValue<int[]> cachedTradeCounts = new([]);
    private int[] CachedTradeCounts
    {
        get
        {
            if (!cachedTradeCounts.TryGetValue(out var trade))
            {
                var producerCount = _traders
                    .Select(list => list.Where(i => i.PowerOutput > 0).Count())
                .Sum();
                var consumerCount = _traders
                    .Select(list => list.Where(i => i.PowerOutput < 0).Count())
                .Sum();
                trade = [producerCount, consumerCount];
                cachedTradeCounts.Update(trade);
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

    private CachedValue<bool> _cachedAnyPoweredStationOnline = new(false);
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

    public override bool IsTransferable => false;

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

        yield return RefreshBuildingLists(jobLog).ResumeWhenOtherCoroutineIsCompleted();
        yield return RefreshCompLists(jobLog).ResumeWhenOtherCoroutineIsCompleted();
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

    private Coroutine RefreshBuildingLists(ManagerLog jobLog)
    {
        int buildingsBefore = _traderBuildings.Count;
        int batteriesBefore = _batteryBuildings.Count;

        _traderBuildings.Clear();
        _batteryBuildings.Clear();

        foreach (var (def, i) in TraderDefs.Select((d, i) => (d, i)))
        {
            _traderBuildings.AddRange(Manager.map.listerBuildings.AllBuildingsColonistOfDef(def));
            if (i > 0 && i % Constants.CoroutineBreakAfter == 0)
            {
                yield return ResumeImmediately.Singleton;
            }
        }

        foreach (var (def, i) in BatteryDefs.Select((d, i) => (d, i)))
        {
            _batteryBuildings.AddRange(Manager.map.listerBuildings.AllBuildingsColonistOfDef(def));
            if (i > 0 && i % Constants.CoroutineBreakAfter == 0)
            {
                yield return ResumeImmediately.Singleton;
            }
        }

        int buildingsAfter = _traderBuildings.Count;
        int batteriesAfter = _batteryBuildings.Count;

        if (buildingsBefore != buildingsAfter || batteriesBefore != batteriesAfter)
        {
            jobLog.AddDetail("ColonyManagerRedux.Energy.Logs.InventoriedBuildings"
                .Translate(buildingsBefore, batteriesBefore, buildingsAfter, batteriesAfter));
        }
    }

    private Coroutine RefreshCompLists(ManagerLog? jobLog = null)
    {
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
        foreach (var (traders, i) in TraderDefs
            .Select((def, i) => (_traderBuildings
                .Where(b => b.def == def)
                .Select(b => b.GetComp<CompPowerTrader>()), i)))
        {
            if (i == _traders.Count)
            {
                _traders.Add([]);
            }
            foreach (var comp in traders)
            {
                _traders[i].Add(comp);
                if (++compCounter > 0 && compCounter % Constants.CoroutineBreakAfter == 0)
                {
                    yield return ResumeImmediately.Singleton;
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
                if (++compCounter > 0 && compCounter % Constants.CoroutineBreakAfter == 0)
                {
                    yield return ResumeImmediately.Singleton;
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

        Scribe_Collections.Look(ref _traderBuildings, "traders", LookMode.Reference);
        Scribe_Collections.Look(ref _batteryBuildings, "batteries", LookMode.Reference);
        Scribe_Deep.Look(ref tradingHistory, "tradingHistory");

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            tradingHistory.UpdateThingDefs(TraderDefs);
            _traderBuildings.RemoveWhere(b => b == null);
            _batteryBuildings.RemoveWhere(b => b == null);
            RefreshCompLists().RunImmediatelyToCompletion();
        }
    }
}
