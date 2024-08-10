// ManagerLog.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HotSwappable]
public class ManagerLog : IExposable
{
    internal sealed class LogDetails : IExposable
    {
        private int jumpToTargetCycleIndex = -1;
        public int NextTargetIndex
        {
            get
            {
                jumpToTargetCycleIndex++;
                if (jumpToTargetCycleIndex >= Targets.Count)
                {
                    jumpToTargetCycleIndex = 0;
                }
                return jumpToTargetCycleIndex;
            }
        }

        public string Text;
        public List<LocalTargetInfo> Targets;

        public LogDetails(string detailText, IEnumerable<LocalTargetInfo> targets)
        {
            Text = detailText;
            Targets = targets.Where(t => t.IsValid && t != LocalTargetInfo.Invalid).ToList();
        }

        public LogDetails(string detailText, params LocalTargetInfo[] targets)
            : this(detailText, (IEnumerable<LocalTargetInfo>)targets)
        {
        }

#pragma warning disable CS8618 // For scribing only
        public LogDetails()
#pragma warning restore CS8618
        {

        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref Text!, "text");
            Scribe_Collections.Look(ref Targets, "targets", LookMode.LocalTargetInfo);
        }
    }

    private ManagerJob? _originatingJob;
    private ManagerDef _originatingDef;
    private string? _label;

    internal bool _workDone;
    private int _mapTile;
    private int _logTick;
    public string LogDate => GenDate.DateFullStringWithHourAt(GenDate.TickGameToAbs(_logTick), Find.WorldGrid.LongLatOf(_mapTile));

    private List<LogDetails> _details = [];
    internal IEnumerable<LogDetails> Details
    {
        get
        {
            if (!_workDone)
            {
                yield return new LogDetails("ColonyManagerRedux.Logs.NoWorkDone".Translate());
            }
            foreach (var detail in _details)
            {
                yield return detail;
            }
        }
    }

    public Texture2D Icon => _originatingDef.icon;
    public string LogLabel
    {
        get
        {
            return _label ?? JobLabel;
        }
        set
        {
            _label = value;
        }
    }

    public string JobLabel => _originatingJob?.Label.UncapitalizeFirst() ?? _originatingDef.label;
    public string JobLabelCap => _label.CapitalizeFirst() ?? _originatingJob?.Label.CapitalizeFirst() ?? _originatingDef.LabelCap;

    public bool HasJob => _originatingJob != null && _originatingJob.Manager.JobTracker.HasJob(_originatingJob);
    public ManagerTab? Tab => _originatingJob?.Tab;

    public void GoToJobTab()
    {
        if (_originatingJob != null)
        {
            MainTabWindow_Manager.GoTo(_originatingJob.Tab, _originatingJob);
        }
    }

    public bool IsForJob(ManagerJob job) => _originatingJob == job;

#pragma warning disable CS8618 // Only for scribing
    public ManagerLog()
#pragma warning restore CS8618
    {
    }

    public ManagerLog(ManagerJob originatingJob)
    {
        if (originatingJob == null)
        {
            throw new ArgumentNullException(nameof(originatingJob));
        }

        _originatingDef = originatingJob.Def;
        _originatingJob = originatingJob;

        _mapTile = originatingJob.Manager.map.Tile;
        _logTick = Find.TickManager.TicksGame;
    }

    public void ExposeData()
    {
        Scribe_Defs.Look(ref _originatingDef, "originatingDef");
        Scribe_References.Look(ref _originatingJob, "originatingJob");
        Scribe_Values.Look(ref _label, "label");
        Scribe_Values.Look(ref _workDone, "workDone");
        Scribe_Values.Look(ref _mapTile, "mapTile");
        Scribe_Values.Look(ref _logTick, "logTick");
        Scribe_Collections.Look(ref _details, "details", LookMode.Deep);
    }

    internal void AddDetail(string detailText, IEnumerable<LocalTargetInfo> targets)
    {
        _details.Add(new LogDetails(detailText, targets));
    }

    internal void AddDetail(string detailText, params LocalTargetInfo[] targets)
    {
        _details.Add(new LogDetails(detailText, targets));
    }
}
