// ManagerLog.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Diagnostics.CodeAnalysis;

namespace ColonyManagerRedux;

/// <summary>
/// Represents a log entry for a manager job, including details, date, and job association.
/// </summary>
[HotSwappable]
public class ManagerLog : IExposable
{
    private ManagerJob? _originatingJob;
    private ManagerDef _originatingDef;
    private string? _label;

    internal bool _workDone;

    /// <summary>
    /// Gets whether any work was done for this log entry.
    /// </summary>
    public bool WorkDone => _workDone;
    private int _mapTile;
    private int _logTick;

    /// <summary>
    /// Gets the date and time of the log entry as a formatted string.
    /// </summary>
    public string LogDate =>
        GenDate.DateFullStringWithHourAt(
            GenDate.TickGameToAbs(_logTick),
            Find.WorldGrid.LongLatOf(_mapTile)
        );

    private List<LogDetails> _details = [];

    /// <summary>
    /// Gets the details associated with this log entry.
    /// </summary>
    public IEnumerable<LogDetails> Details
    {
        get
        {
            if (!_workDone && _details.Count == 0)
            {
                yield return new LogDetails("ColonyManagerRedux.Logs.NoWorkDone".Translate());
            }
            foreach (var detail in _details)
            {
                yield return detail;
            }
        }
    }

    /// <summary>
    /// Gets the icon associated with the originating manager definition.
    /// </summary>
    public Texture2D Icon => _originatingDef.icon;

    /// <summary>
    /// Gets or sets the label for this log entry.
    /// </summary>
    public string LogLabel
    {
        get => _label ?? JobLabel;
        set => _label = value;
    }

    /// <summary>
    /// Gets the job label (uncapitalized) for this log entry.
    /// </summary>
    public string JobLabel => _originatingJob?.Label.UncapitalizeFirst() ?? _originatingDef.label;

    /// <summary>
    /// Gets the capitalized job label for this log entry.
    /// </summary>
    public string JobLabelCap =>
        _label.CapitalizeFirst()
        ?? _originatingJob?.Label.CapitalizeFirst()
        ?? _originatingDef.LabelCap;

    /// <summary>
    /// Gets whether this log entry is associated with a valid job.
    /// </summary>
    public bool HasJob =>
        _originatingJob != null && _originatingJob.Manager.JobTracker.HasJob(_originatingJob);

    /// <summary>
    /// Gets the manager tab associated with the originating job, if any.
    /// </summary>
    public ManagerTab? Tab => _originatingJob?.Tab;

    /// <summary>
    /// Navigates to the manager tab for the originating job, if available.
    /// </summary>
    public void GoToJobTab()
    {
        if (_originatingJob != null)
        {
            MainTabWindow_Manager.GoTo(_originatingJob.Tab, _originatingJob);
        }
    }

    /// <summary>
    /// Determines whether this log entry is for the specified job.
    /// </summary>
    /// <param name="job">The job to check.</param>
    /// <returns>True if this log is for the given job; otherwise, false.</returns>
    public bool IsForJob(ManagerJob job) => _originatingJob == job;

#pragma warning disable CS8618 // Only for scribing
    /// <summary>
    /// Default constructor for scribing only.
    /// </summary>
    public ManagerLog()
#pragma warning restore CS8618
    { }

    /// <summary>
    /// Creates a new log entry for the specified originating job.
    /// </summary>
    /// <param name="originatingJob">The originating manager job.</param>
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

    /// <inheritdoc/>
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

    /// <summary>
    /// Adds a detail entry to the log with the specified text and targets.
    /// </summary>
    /// <param name="detailText">The detail text.</param>
    /// <param name="targets">The associated targets.</param>
    public void AddDetail(string detailText, IEnumerable<LocalTargetInfo> targets) =>
        _details.Add(new LogDetails(detailText, targets));

    /// <summary>
    /// Adds a detail entry to the log with the specified text and targets.
    /// </summary>
    /// <param name="detailText">The detail text.</param>
    /// <param name="targets">The associated targets.</param>
    public void AddDetail(string detailText, params LocalTargetInfo[] targets) =>
        _details.Add(new LogDetails(detailText, targets));
}

/// <summary>
/// Represents a detail entry in a manager log, including text and associated targets.
/// </summary>
public sealed class LogDetails : IExposable
{
    private int jumpToTargetCycleIndex = -1;

    /// <summary>
    /// Gets the next target index for cycling through targets.
    /// </summary>
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

#pragma warning disable IDE0032 // Use auto property
    private string _text;
#pragma warning restore IDE0032 // Use auto property
    /// <summary>
    /// Gets or sets the detail text for this log detail entry.
    /// </summary>
    public string Text
    {
        get => _text;
        [MemberNotNull([nameof(_text)])]
        set => _text = value;
    }

    private List<LocalTargetInfo> _targets;

    /// <summary>
    /// Gets the list of associated targets for this log detail entry.
    /// </summary>
    public List<LocalTargetInfo> Targets => _targets;

    /// <summary>
    /// Creates a new log detail entry with the specified text and targets.
    /// </summary>
    /// <param name="detailText">The detail text.</param>
    /// <param name="targets">The associated targets.</param>
    public LogDetails(string detailText, IEnumerable<LocalTargetInfo> targets)
    {
        Text = detailText;
        _targets = [.. targets.Where(t => t.IsValid && t != LocalTargetInfo.Invalid)];
    }

    /// <summary>
    /// Creates a new log detail entry with the specified text and targets.
    /// </summary>
    /// <param name="detailText">The detail text.</param>
    /// <param name="targets">The associated targets.</param>
    public LogDetails(string detailText, params LocalTargetInfo[] targets)
        : this(detailText, (IEnumerable<LocalTargetInfo>)targets) { }

#pragma warning disable CS8618 // For scribing only
    /// <summary>
    /// Default constructor for scribing only.
    /// </summary>
    public LogDetails()
#pragma warning restore CS8618
    { }

    /// <inheritdoc/>
    public void ExposeData()
    {
        Scribe_Values.Look(ref _text!, "text");
        Scribe_Collections.Look(ref _targets, "targets", LookMode.LocalTargetInfo);
    }
}
