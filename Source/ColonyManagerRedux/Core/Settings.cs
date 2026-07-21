// Settings.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024–2026 Alexander Krivács Schrøder

using System.Diagnostics.CodeAnalysis;
using System.Xml;
using ilyvion.Laboratory.Extensions;
using ilyvion.Laboratory.UI;
using Verse.Sound;
using static ColonyManagerRedux.Constants;
using TabRecord = ilyvion.Laboratory.UI.TabRecord;

namespace ColonyManagerRedux;

/// <summary>
/// Stores and manages all mod settings for Colony Manager Redux, including general, threshold, alert, and performance settings.
/// </summary>
[HotSwappable]
[StaticConstructorOnStartup]
public class Settings : ModSettings
{
    private readonly SharedManagerSettings _sharedManagerSettings;
    private readonly PerformanceSettings _performanceSettings;
    private List<ManagerSettings> _managerSettings = [];
    private Tab _currentManagerSettings;

    private bool _doVerboseLogging;

    /// <summary>
    /// Gets whether verbose logging is enabled.
    /// </summary>
    public bool DoVerboseLogging
    {
        get => _doVerboseLogging;
        internal set => _doVerboseLogging = value;
    }

    private int _defaultUpdateIntervalTicks = GenDate.TicksPerDay;

    /// <summary>
    /// Gets the default update interval in ticks.
    /// </summary>
    public int DefaultUpdateIntervalTicks
    {
        get => _defaultUpdateIntervalTicks;
        internal set => _defaultUpdateIntervalTicks = value;
    }

    private int _defaultTargetCount = 500;

    /// <summary>
    /// Gets the default target count for thresholds.
    /// </summary>
    public int DefaultTargetCount
    {
        get => _defaultTargetCount;
        internal set => _defaultTargetCount = value;
    }

    private bool _defaultCountAllOnMap;

    /// <summary>
    /// Gets whether to count all items on the map by default.
    /// </summary>
    public bool DefaultCountAllOnMap
    {
        get => _defaultCountAllOnMap;
        internal set => _defaultCountAllOnMap = value;
    }

    private bool _defaultShouldCheckReachable = true;

    /// <summary>
    /// Gets whether to check reachability by default.
    /// </summary>
    public bool DefaultShouldCheckReachable
    {
        get => _defaultShouldCheckReachable;
        internal set => _defaultShouldCheckReachable = value;
    }

    private bool _defaultUsePathBasedDistance;

    /// <summary>
    /// Gets whether to use path-based distance by default.
    /// </summary>
    public bool DefaultUsePathBasedDistance
    {
        get => _defaultUsePathBasedDistance;
        internal set => _defaultUsePathBasedDistance = value;
    }

    private bool _newJobsAreImmediatelyOutdated = true;

    /// <summary>
    /// Gets whether new jobs are immediately marked as outdated.
    /// </summary>
    public bool NewJobsAreImmediatelyOutdated
    {
        get => _newJobsAreImmediatelyOutdated;
        internal set => _newJobsAreImmediatelyOutdated = value;
    }

    private bool _recordHistoricalData = true;

    /// <summary>
    /// Gets whether to record historical data.
    /// </summary>
    public bool RecordHistoricalData
    {
        get => _recordHistoricalData;
        internal set => _recordHistoricalData = value;
    }

    private bool _newJobsShouldBeResourceLocked = true;

    /// <summary>
    /// Gets whether new jobs should be resource locked by default.
    /// </summary>
    public bool NewJobsShouldBeResourceLocked
    {
        get => _newJobsShouldBeResourceLocked;
        internal set => _newJobsShouldBeResourceLocked = value;
    }

    private bool _autoApplyDefaultTemplateOnFirstStation = true;

    /// <summary>
    /// Gets whether the configured default job template should automatically be applied the
    /// first time a manager station is built on a map.
    /// </summary>
    public bool AutoApplyDefaultTemplateOnFirstStation
    {
        get => _autoApplyDefaultTemplateOnFirstStation;
        internal set => _autoApplyDefaultTemplateOnFirstStation = value;
    }

    private string? _defaultTemplateName;

    /// <summary>
    /// Gets the name of the manager job template to automatically apply to a map the first time
    /// a manager station is built there. <see langword="null"/> or empty means no default
    /// template is configured.
    /// </summary>
    public string? DefaultTemplateName
    {
        get => _defaultTemplateName;
        set => _defaultTemplateName = value;
    }

    private bool _showInfoCardButtonsWherePossible = true;

    /// <summary>
    /// Gets whether to show info card buttons where possible.
    /// </summary>
    public bool ShowInfoCardButtonsWherePossible
    {
        get => _showInfoCardButtonsWherePossible;
        internal set => _showInfoCardButtonsWherePossible = value;
    }

    private int _maxDesignationsPerJob;

    /// <summary>
    /// Gets the maximum number of designations allowed per job.
    /// </summary>
    public int MaxDesignationsPerJob
    {
        get => ScaleUpMaxDesignationsPerJob(_maxDesignationsPerJob);
        internal set => _maxDesignationsPerJob = ScaleDownMaxDesignationsPerJob(value);
    }

    /// <summary>
    /// Pure scaling behind <see cref="MaxDesignationsPerJob"/>'s getter: the raw stored value is
    /// in units of 10 so the settings UI slider can work in steps of 10.
    /// </summary>
    internal static int ScaleUpMaxDesignationsPerJob(int raw) => raw * 10;

    /// <summary>
    /// Pure scaling behind <see cref="MaxDesignationsPerJob"/>'s setter; truncates via integer
    /// division, so values not a multiple of 10 don't round-trip exactly.
    /// </summary>
    internal static int ScaleDownMaxDesignationsPerJob(int value) => value / 10;

    /// <summary>
    /// Determines if more designations can be added to a job.
    /// </summary>
    public bool CanAddMoreDesignations(int currentCount) =>
        CanAddMoreDesignations(MaxDesignationsPerJob, currentCount);

    /// <summary>
    /// Determines if more designations should be removed from a job.
    /// </summary>
    public bool ShouldRemoveMoreDesignations(int currentCount) =>
        ShouldRemoveMoreDesignations(MaxDesignationsPerJob, currentCount);

    /// <summary>
    /// Pure comparison behind <see cref="CanAddMoreDesignations(int)"/>, kept separate so it's
    /// unit-testable without a live <see cref="Settings"/> instance. A limit of 0 means "no limit".
    /// </summary>
    internal static bool CanAddMoreDesignations(int maxDesignationsPerJob, int currentCount) =>
        maxDesignationsPerJob == 0 || maxDesignationsPerJob > currentCount;

    /// <summary>
    /// Pure comparison behind <see cref="ShouldRemoveMoreDesignations(int)"/>, kept separate so
    /// it's unit-testable without a live <see cref="Settings"/> instance. A limit of 0 means "no
    /// limit", so nothing should ever be removed for being over it.
    /// </summary>
    internal static bool ShouldRemoveMoreDesignations(
        int maxDesignationsPerJob,
        int currentCount
    ) => maxDesignationsPerJob != 0 && maxDesignationsPerJob < currentCount;

    private List<int> _customUpdateIntervalTickList = [];

    /// <summary>
    /// Gets the list of custom update intervals in ticks.
    /// </summary>
    public List<int> CustomUpdateIntervalTickList
    {
        get => _customUpdateIntervalTickList;
        internal set => _customUpdateIntervalTickList = value;
    }

    private bool _showNoManagerAlert = true;

    /// <summary>
    /// Gets whether to show the 'No Manager' alert.
    /// </summary>
    public bool ShowNoManagerAlert
    {
        get => _showNoManagerAlert;
        internal set => _showNoManagerAlert = value;
    }

    private bool _showNoTableAlert = true;

    /// <summary>
    /// Gets whether to show the 'No Table' alert.
    /// </summary>
    public bool ShowNoTableAlert
    {
        get => _showNoTableAlert;
        internal set => _showNoTableAlert = value;
    }

    private bool _showJobsNotUpdatingAlert = true;

    /// <summary>
    /// Gets whether to show the 'Jobs Not Updating' alert.
    /// </summary>
    public bool ShowJobsNotUpdatingAlert
    {
        get => _showJobsNotUpdatingAlert;
        internal set => _showJobsNotUpdatingAlert = value;
    }

    private float _daysBeforeShowingAlert = 0.5f;

    /// <summary>
    /// Gets the number of days before showing the alert.
    /// </summary>
    public float DaysBeforeShowingAlert
    {
        get => _daysBeforeShowingAlert;
        internal set => _daysBeforeShowingAlert = value;
    }

    private float _daysBeforeShowingHighAlert = 1f;

    /// <summary>
    /// Gets the number of days before showing the high alert.
    /// </summary>
    public float DaysBeforeShowingHighAlert
    {
        get => _daysBeforeShowingHighAlert;
        internal set => _daysBeforeShowingHighAlert = value;
    }

    private float _daysBeforeShowingCriticalAlert = 2f;

    /// <summary>
    /// Gets the number of days before showing the critical alert.
    /// </summary>
    public float DaysBeforeShowingCriticalAlert
    {
        get => _daysBeforeShowingCriticalAlert;
        internal set => _daysBeforeShowingCriticalAlert = value;
    }

    private bool _showNoTableNeededAlert = true;

    /// <summary>
    /// Gets whether to show the 'No Table Needed' alert.
    /// </summary>
    public bool ShowNoTableNeededAlert
    {
        get => _showNoTableNeededAlert;
        internal set => _showNoTableNeededAlert = value;
    }

    private HashSet<ManagerDef> _disabledManagers = [];

    /// <summary>
    /// Gets the set of disabled manager definitions.
    /// </summary>
    public HashSet<ManagerDef> DisabledManagers => _disabledManagers;

    /// <summary>
    /// Gets the default update interval as an <see cref="UpdateInterval"/>.
    /// </summary>
    public UpdateInterval DefaultUpdateInterval
    {
        get => TicksToInterval(DefaultUpdateIntervalTicks);
        internal set => DefaultUpdateIntervalTicks = value.Ticks;
    }

    private int _operationsPerTick = 10;

    /// <summary>
    /// Gets the number of operations performed per tick.
    /// </summary>
    public int OperationsPerTick
    {
        get => _operationsPerTick;
        internal set => _operationsPerTick = value;
    }

    private int _ticksBetweenOperations;

    /// <summary>
    /// Gets the number of ticks between operations.
    /// </summary>
    public int TicksBetweenOperations
    {
        get => _ticksBetweenOperations;
        internal set => _ticksBetweenOperations = value;
    }

    private bool _showAdvancedPerformanceSettings;

    /// <summary>
    /// Gets whether to show advanced performance settings.
    /// </summary>
    public bool ShowAdvancedPerformanceSettings
    {
        get => _showAdvancedPerformanceSettings;
        internal set => _showAdvancedPerformanceSettings = value;
    }

    private Dictionary<string, int> _coroutineOperationsPerTick = [];
    private Dictionary<string, int> _coroutineTicksBetweenOperations = [];

    private static readonly List<CoroutineSettingsMethodAttribute> _coroutineMethods = [];

    static Settings()
    {
        ColonyManagerReduxMod.Instance.LogDebug("Finding coroutine settings...");
        foreach (var type in CoroutineSettingsTypeAttribute.AllTypesWithAttribute)
        {
            var methods = type.GetMethods(
                BindingFlags.DeclaredOnly
                    | BindingFlags.Public
                    | BindingFlags.NonPublic
                    | BindingFlags.Instance
                    | BindingFlags.Static
            );
            ColonyManagerReduxMod.Instance.LogDebug(
                $"Found coroutine settings type {type.FullName} with "
                    + $"{methods.Count(m => m.HasAttribute<CoroutineSettingsMethodAttribute>())} methods"
            );
            foreach (var method in methods)
            {
                var coroutineMethod = method.GetCustomAttribute<CoroutineSettingsMethodAttribute>();
                if (coroutineMethod == null)
                {
                    continue;
                }

                coroutineMethod.Type = type;
                coroutineMethod.Method = method;
                _coroutineMethods.Add(coroutineMethod);
                ColonyManagerReduxMod.Instance.LogDebug(
                    $"- {method.Name} ({coroutineMethod.FullName})"
                );
            }
        }
        ColonyManagerReduxMod.Instance.LogDebug(
            $"Finished finding coroutine settings; found {_coroutineMethods.Count}."
        );
    }

    private static readonly Dictionary<MethodInfo, string> _fullNameCache = [];

    private static string GetFullName(Delegate del)
    {
        var method = del.Method;
        if (_fullNameCache.TryGetValue(method, out var fullName))
        {
            return fullName;
        }

        fullName = $"{method.DeclaringType.FullName}.{method.Name}";
        _fullNameCache[method] = fullName;
        return fullName;
    }

    /// <summary>
    /// Gets the number of operations per tick for a specific coroutine.
    /// </summary>
    /// <param name="coroutine">The coroutine delegate.</param>
    /// <returns>The number of operations per tick.</returns>
    public int GetOperationsPerTickForCoroutine(Delegate coroutine)
    {
        if (coroutine == null)
        {
            throw new ArgumentNullException(nameof(coroutine));
        }

        var fullName = GetFullName(coroutine);
        var hasOverride = _coroutineOperationsPerTick.TryGetValue(
            fullName,
            out var operationsPerTick
        );

        return ResolveCoroutineSetting(
            ShowAdvancedPerformanceSettings,
            OperationsPerTick,
            hasOverride,
            operationsPerTick,
            minValidOverride: 1
        );
    }

    // The "no override" sentinel differs between the two coroutine settings: 0 operations per
    // tick is invalid (nothing would ever run), but 0 ticks between operations is a valid
    // explicit override (run every tick). `minValidOverride` captures that difference: an
    // override is only used if it's >= minValidOverride.
    internal static int ResolveCoroutineSetting(
        bool showAdvanced,
        int globalValue,
        bool hasOverride,
        int overrideValue,
        int minValidOverride
    ) =>
        showAdvanced && hasOverride && overrideValue >= minValidOverride
            ? overrideValue
            : globalValue;

    /// <summary>
    /// Gets the number of ticks between operations for a specific coroutine.
    /// </summary>
    /// <param name="coroutine">The coroutine delegate.</param>
    /// <returns>The number of ticks between operations.</returns>
    public int GetTicksBetweenOperationsForCoroutine(Delegate coroutine)
    {
        if (coroutine == null)
        {
            throw new ArgumentNullException(nameof(coroutine));
        }

        var fullName = GetFullName(coroutine);
        var hasOverride = _coroutineTicksBetweenOperations.TryGetValue(
            fullName,
            out var ticksBetweenOperations
        );

        return ResolveCoroutineSetting(
            ShowAdvancedPerformanceSettings,
            TicksBetweenOperations,
            hasOverride,
            ticksBetweenOperations,
            minValidOverride: 0
        );
    }

    [AllowNull]
    private List<TabRecord> TabList
    {
        get
        {
            field ??=
            [
                .. Gen.YieldSingle<Tab>(_sharedManagerSettings)
                    .Concat(Gen.YieldSingle<Tab>(_performanceSettings))
                    .Concat(_managerSettings.Where(m => m.Show))
                    .Select(m => new TabRecord(m, () => ref _currentManagerSettings)),
            ];
            return field;
        }
        set;
    }

    private sealed class SharedManagerSettings(Settings settings) : Tab
    {
        public override string Title => "ColonyManagerRedux.SharedSettingsTabLabel".Translate();

        public override void DoTabContents(Rect inRect)
        {
            Widgets_Section.BeginSectionColumn(
                inRect,
                "Shared.Settings",
                out var position,
                out var width
            );

            Widgets_Section.Section(
                ref position,
                width,
                settings.DrawGeneralSettings,
                "ColonyManagerRedux.GeneralSettingsTabLabel".Translate()
            );
            Widgets_Section.Section(
                ref position,
                width,
                settings.DrawThreshold,
                "ColonyManagerRedux.ManagerSettings.DefaultThresholdSettings".Translate()
            );
            Widgets_Section.Section(
                ref position,
                width,
                settings.DrawCustomUpdateIntervals,
                "ColonyManagerRedux.ManagerSettings.CustomUpdateIntervals".Translate()
            );
            Widgets_Section.Section(
                ref position,
                width,
                settings.DrawAlertSettings,
                "ColonyManagerRedux.ManagerSettings.AlertSettings".Translate()
            );
            Widgets_Section.Section(
                ref position,
                width,
                settings.DrawDisableManagers,
                "ColonyManagerRedux.ManagerSettings.DisableManagers".Translate()
            );
            Widgets_Section.Section(
                ref position,
                width,
                settings.DrawTemplateSettings,
                "ColonyManagerRedux.ManagerSettings.TemplateSettings".Translate()
            );

            Widgets_Section.EndSectionColumn("Shared.Settings", position);
        }
    }

    private sealed class PerformanceSettings(Settings settings) : Tab
    {
        public override string Title =>
            "ColonyManagerRedux.PerformanceSettingsTabLabel".Translate();

        public override void DoTabContents(Rect inRect)
        {
            Widgets_Section.BeginSectionColumn(
                inRect,
                "Performance.Settings",
                out var position,
                out var width
            );

            Widgets_Section.Section(
                ref position,
                width,
                settings.DrawPerformanceSettings,
                "ColonyManagerRedux.PerformanceSettingsTabLabel".Translate()
            );

            if (settings._showAdvancedPerformanceSettings)
            {
                var method = 0;
                foreach (
                    var (coroutineMethod, header) in _coroutineMethods
                        .Select(m =>
                            (
                                m,
                                $"ColonyManagerRedux.PerformanceSettings.{XmlConvert.EncodeName(m.FullName)}".Translate()
                            )
                        )
                        .OrderBy(m => m.Item2.RawText)
                )
                {
                    Widgets_Section.Section(
                        ref position,
                        width,
                        (pos, width) => settings.DrawCoroutineSettings(coroutineMethod, pos, width),
                        header,
                        (_coroutineMethods.GetHashCode() + method++).GetHashCode()
                    );
                }
            }

            Widgets_Section.EndSectionColumn("Performance.Settings", position);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Settings"/> class.
    /// </summary>
    public Settings()
    {
        ColonyManagerReduxMod.Instance.LogDebug("Loading manager job defs");
        _managerSettings.AddRange(MakeManagerSettings());

        _currentManagerSettings = _sharedManagerSettings = new(this);
        _performanceSettings = new(this);
    }

    private static IEnumerable<ManagerSettings> MakeManagerSettings()
    {
        foreach (
            var managerDef in DefDatabase<ManagerDef>
                .AllDefs.Where(m => m.managerSettingsClass != null)
                .OrderBy(m => m.order)
        )
        {
            ManagerSettings? managerSettings = null;
            try
            {
                managerSettings = ManagerDefMaker.MakeManagerSettings(managerDef)!;
            }
            catch (Exception err)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    $"Could not create {nameof(ManagerSettings)} instance for "
                        + $"{managerDef.defName} because it threw an exception: \n{err}"
                );
                continue;
            }
            yield return managerSettings;
        }
    }

    internal void DoSettingsWindowContents(Rect rect)
    {
        var rowCount = (int)Math.Ceiling((double)(_managerSettings.Count + 1) / 5);
        rect.yMin += (rowCount * SectionHeaderHeight) + Margin;
        Widgets.DrawMenuSection(rect);
        _ = TabDrawer.DrawTabs(rect, TabList, rowCount, null);

        try
        {
            using var _g = GUIScope.WidgetGroup(rect);
            _currentManagerSettings.DoTabContents(rect.AtZero());
        }
        catch (Exception err)
        {
            ColonyManagerReduxMod.Instance.LogError(
                $"Exception while calling DoTabContents for {_currentManagerSettings.Title}:\n"
                    + err
            );
        }
    }

    private float DrawGeneralSettings(Vector2 pos, float width)
    {
        var start = pos;

        if (Prefs.DevMode)
        {
            Utilities.DrawToggle(
                ref pos,
                width,
                "ColonyManagerRedux.DoVerboseLogging".Translate(),
                "ColonyManagerRedux.DoVerboseLogging.Tip".Translate(),
                ref _doVerboseLogging
            );
        }

        var rect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        pos.y += ListEntryHeight;

        // labels
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(
            rect.TrimLeft(Margin),
            "ColonyManagerRedux.ManagerDefaultUpdateInterval".Translate()
        );
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(rect.TrimRight(Margin), DefaultUpdateInterval.Label);
        Text.Anchor = TextAnchor.UpperLeft;

        // interaction
        Widgets.DrawHighlightIfMouseover(rect);
        if (Widgets.ButtonInvisible(rect))
        {
            var options = new List<FloatMenuOption>();
            foreach (var interval in Utilities.UpdateIntervalOptions)
            {
                options.Add(
                    new FloatMenuOption(interval.Label, () => DefaultUpdateInterval = interval)
                );
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.NewJobsAreImmediatelyOutdated".Translate(),
            "ColonyManagerRedux.NewJobsAreImmediatelyOutdated.Tip".Translate(),
            ref _newJobsAreImmediatelyOutdated
        );

        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.RecordHistoricalData".Translate(),
            "ColonyManagerRedux.RecordHistoricalData.Tip".Translate(),
            ref _recordHistoricalData,
            true
        );

        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.NewJobsShouldBeResourceLocked".Translate(),
            "ColonyManagerRedux.NewJobsShouldBeResourceLocked.Tip".Translate(),
            ref _newJobsShouldBeResourceLocked
        );

        DrawIntSliderConfig(
            _maxDesignationsPerJob,
            v => _maxDesignationsPerJob = v,
            150,
            ref pos,
            width,
            SliderHeight,
            MaxDesignationsPerJob > 0
                ? "ColonyManagerRedux.ManagerSettings.MaxDesignationsPerJob".Translate(
                    MaxDesignationsPerJob
                )
                : "ColonyManagerRedux.ManagerSettings.NoMaxDesignationsPerJob".Translate(),
            "ColonyManagerRedux.ManagerSettings.MaxDesignationsPerJob.Tip".Translate()
        );

        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.ShowInfoCardButtonsWherePossible".Translate(),
            "ColonyManagerRedux.ShowInfoCardButtonsWherePossible.Tip".Translate(),
            ref _showInfoCardButtonsWherePossible
        );

        return pos.y - start.y;
    }

    private float DrawThreshold(Vector2 pos, float width)
    {
        var start = pos;

        DrawIntSliderConfig(
            DefaultTargetCount,
            v => DefaultTargetCount = v,
            DefaultMaxUpperThreshold,
            ref pos,
            width,
            SliderHeight,
            "ColonyManagerRedux.ManagerSettings.TargetCount".Translate(DefaultTargetCount)
        );

        var countAllOnMapToggleRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        pos.y += ListEntryHeight;
        Utilities.DrawToggle(
            countAllOnMapToggleRect,
            "ColonyManagerRedux.Threshold.CountAllOnMap".Translate(),
            "ColonyManagerRedux.Threshold.CountAllOnMap.Tip".Translate(),
            ref _defaultCountAllOnMap,
            true
        );

        Utilities.DrawReachabilityToggle(ref pos, width, ref _defaultShouldCheckReachable);
        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.Threshold.PathBasedDistance".Translate(),
            "ColonyManagerRedux.Threshold.PathBasedDistance.Tip".Translate(),
            ref _defaultUsePathBasedDistance,
            true
        );

        return pos.y - start.y;
    }

    private const int DefaultCustomUpdateIntervalTicks = GenDate.TicksPerDay;
    private int _addCustomUpdateIntervalTicks = DefaultCustomUpdateIntervalTicks;

    private static readonly (string Unit, int TicksPerUnit)[] _customUpdateIntervalUnits =
    [
        ("Hour", GenDate.TicksPerHour),
        ("Day", GenDate.TicksPerDay),
        ("Year", GenDate.TicksPerYear),
    ];

    private float DrawCustomUpdateIntervals(Vector2 pos, float width)
    {
        var start = pos;

        var periodLabel = _addCustomUpdateIntervalTicks.ToStringTicksToPeriodVerboseFull();
        var periodLabelAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        IlyvionWidgets.Label(periodLabelAreaRect, periodLabel, TextAnchor.MiddleLeft);

        var adjustButtonLabels = new string[1 + (2 * _customUpdateIntervalUnits.Length)];
        adjustButtonLabels[0] = "0";
        for (var i = 0; i < _customUpdateIntervalUnits.Length; i++)
        {
            var unit = _customUpdateIntervalUnits[i].Unit;
            adjustButtonLabels[1 + (2 * i)] =
                $"ColonyManagerRedux.ManagerSettings.DecreaseCustomUpdateIntervalBy{unit}".Translate();
            adjustButtonLabels[2 + (2 * i)] =
                $"ColonyManagerRedux.ManagerSettings.IncreaseCustomUpdateIntervalBy{unit}".Translate();
        }
        var addButtonLabel =
            "ColonyManagerRedux.ManagerSettings.AddCustomUpdateIntervals".Translate();

        var totalButtonsWidth = Margin;
        foreach (var label in adjustButtonLabels)
        {
            totalButtonsWidth += RowButtonWidth(label) + Margin;
        }
        totalButtonsWidth += RowButtonWidth(addButtonLabel);

        var rowPos = new Vector2(pos.x + width - totalButtonsWidth, pos.y);
        var labelIndex = 0;
        if (RowButton(adjustButtonLabels[labelIndex++], ref rowPos))
        {
            _addCustomUpdateIntervalTicks = 0;
        }
        foreach (var (_, ticksPerUnit) in _customUpdateIntervalUnits)
        {
            if (RowButton(adjustButtonLabels[labelIndex++], ref rowPos))
            {
                _addCustomUpdateIntervalTicks = AdjustCustomIntervalTicks(
                    _addCustomUpdateIntervalTicks,
                    -ticksPerUnit
                );
            }
            if (RowButton(adjustButtonLabels[labelIndex++], ref rowPos))
            {
                _addCustomUpdateIntervalTicks = AdjustCustomIntervalTicks(
                    _addCustomUpdateIntervalTicks,
                    ticksPerUnit
                );
            }
        }
        rowPos.x += Margin;
        if (RowButton(addButtonLabel, ref rowPos))
        {
            _customUpdateIntervalTickList.Add(_addCustomUpdateIntervalTicks);
            _customUpdateIntervalTickList.Sort();
            _addCustomUpdateIntervalTicks = DefaultCustomUpdateIntervalTicks;
        }
        pos.y += ListEntryHeight + Margin;

        for (var i = 0; i < _customUpdateIntervalTickList.Count; i++)
        {
            var customUpdateIntervalTicks = _customUpdateIntervalTickList[i];
            var rect = new Rect(Margin + pos.x, pos.y, width - (2 * Margin), ListEntryHeight);
            pos.y += ListEntryHeight;
            if (i % 2 == 0)
            {
                Widgets.DrawLightHighlight(rect);
            }
            IlyvionWidgets.Label(
                new Rect(rect.x + 4f, rect.y, rect.width - 4f, rect.height),
                customUpdateIntervalTicks.ToStringTicksToPeriodVerboseFull(),
                TextAnchor.MiddleLeft
            );
            if (
                Widgets.ButtonImage(
                    new Rect(rect.xMax - ListEntryHeight, rect.y, ListEntryHeight, ListEntryHeight),
                    TexButton.Delete,
                    Color.white,
                    GenUI.SubtleMouseoverColor
                )
            )
            {
                _customUpdateIntervalTickList.RemoveAt(i);
                i--;
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            }
        }

        return pos.y - start.y;
    }

    private static float RowButtonWidth(string buttonLabel) =>
        Text.CalcSize(buttonLabel).x + (4 * Margin);

    private static bool RowButton(string buttonLabel, ref Vector2 pos)
    {
        var buttonWidth = RowButtonWidth(buttonLabel);
        Rect buttonRect = new(pos.x, pos.y, buttonWidth, ListEntryHeight);
        pos.x += buttonWidth + Margin;
        return Widgets.ButtonText(buttonRect, buttonLabel);
    }

    private float DrawAlertSettings(Vector2 pos, float width)
    {
        const float MaxAlertDays = 30f;

        var start = pos;

        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.ManagerSettings.AlertSettings.ShowNoManagerAlert".Translate(),
            "ColonyManagerRedux.ManagerSettings.AlertSettings.ShowNoManagerAlert.Tip".Translate(),
            ref _showNoManagerAlert
        );

        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.ManagerSettings.AlertSettings.ShowNoTableAlert".Translate(),
            "ColonyManagerRedux.ManagerSettings.AlertSettings.ShowNoTableAlert.Tip".Translate(),
            ref _showNoTableAlert
        );

        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.ManagerSettings.AlertSettings.ShowNoTableNeededAlert".Translate(),
            "ColonyManagerRedux.ManagerSettings.AlertSettings.ShowNoTableNeededAlert.Tip".Translate(),
            ref _showNoTableNeededAlert
        );

        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.ManagerSettings.AlertSettings.ShowJobsNotUpdatingAlert".Translate(),
            "ColonyManagerRedux.ManagerSettings.AlertSettings.ShowJobsNotUpdatingAlert.Tip".Translate(),
            ref _showJobsNotUpdatingAlert
        );

        if (_showJobsNotUpdatingAlert)
        {
            pos.x += Margin;
            width -= 2 * Margin;

            DrawSliderConfig(
                _daysBeforeShowingAlert,
                v => _daysBeforeShowingAlert = v,
                MaxAlertDays,
                ref pos,
                width,
                SliderHeight,
                "ColonyManagerRedux.ManagerSettings.AlertSettings.DaysBeforeShowingAlert".Translate(
                    _daysBeforeShowingAlert.ToString("F1", CultureInfo.InvariantCulture)
                ),
                minValue: 0.5f,
                roundTo: 0.5f
            );

            (_daysBeforeShowingHighAlert, _) = ClampAlertTiers(
                _daysBeforeShowingAlert,
                _daysBeforeShowingHighAlert,
                _daysBeforeShowingCriticalAlert
            );

            DrawSliderConfig(
                _daysBeforeShowingHighAlert,
                v => _daysBeforeShowingHighAlert = v,
                MaxAlertDays,
                ref pos,
                width,
                SliderHeight,
                "ColonyManagerRedux.ManagerSettings.AlertSettings.DaysBeforeShowingHighAlert".Translate(
                    _daysBeforeShowingHighAlert.ToString("F1", CultureInfo.InvariantCulture)
                ),
                minValue: _daysBeforeShowingAlert,
                roundTo: 0.5f
            );

            (_, _daysBeforeShowingCriticalAlert) = ClampAlertTiers(
                _daysBeforeShowingAlert,
                _daysBeforeShowingHighAlert,
                _daysBeforeShowingCriticalAlert
            );

            DrawSliderConfig(
                _daysBeforeShowingCriticalAlert,
                v => _daysBeforeShowingCriticalAlert = v,
                MaxAlertDays,
                ref pos,
                width,
                SliderHeight,
                "ColonyManagerRedux.ManagerSettings.AlertSettings.DaysBeforeShowingCriticalAlert".Translate(
                    _daysBeforeShowingCriticalAlert.ToString("F1", CultureInfo.InvariantCulture)
                ),
                minValue: _daysBeforeShowingHighAlert,
                roundTo: 0.5f
            );

            pos.x -= Margin;
            width += 2 * Margin;
        }

        return pos.y - start.y;
    }

    /// <summary>
    /// Pure ordering-invariant enforcement behind <see cref="DrawAlertSettings"/>: ratchets
    /// <paramref name="high"/> and <paramref name="critical"/> up so that
    /// <c>alert &lt;= high &lt;= critical</c> always holds, without ever lowering either tier.
    /// </summary>
    internal static (float high, float critical) ClampAlertTiers(
        float alert,
        float high,
        float critical
    )
    {
        if (high < alert)
        {
            high = alert;
        }
        if (critical < high)
        {
            critical = high;
        }
        return (high, critical);
    }

    /// <summary>
    /// Pure clamp behind the custom-update-interval adjust buttons in
    /// <see cref="DrawCustomUpdateIntervals"/>: applies <paramref name="delta"/> to
    /// <paramref name="current"/>, never letting the result go below zero.
    /// </summary>
    internal static int AdjustCustomIntervalTicks(int current, int delta) =>
        Mathf.Max(0, current + delta);

    private float DrawDisableManagers(Vector2 pos, float width)
    {
        var start = pos;

        var text = "ColonyManagerRedux.ManagerSettings.DisableManagers.Tip".Translate();
        float height = -1;
        using (GUIScope.Font(GameFont.Tiny))
        {
            height = Text.CalcHeight(text, width);
        }
        IlyvionWidgets.Label(ref pos, width, height, text, gameFont: GameFont.Tiny);

        foreach (var managerDef in DefDatabase<ManagerDef>.AllDefs.OrderBy(m => m.order))
        {
            Utilities.DrawToggle(
                ref pos,
                width,
                managerDef.LabelCap,
                null,
                !_disabledManagers.Contains(managerDef),
                () => _disabledManagers.Remove(managerDef),
                () => _disabledManagers.Add(managerDef),
                wrap: false
            );
        }

        return pos.y - start.y;
    }

    private float DrawTemplateSettings(Vector2 pos, float width)
    {
        var start = pos;

        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.ManagerSettings.AutoApplyDefaultTemplateOnFirstStation".Translate(),
            "ColonyManagerRedux.ManagerSettings.AutoApplyDefaultTemplateOnFirstStation.Tip".Translate(),
            ref _autoApplyDefaultTemplateOnFirstStation
        );

        var rect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        pos.y += ListEntryHeight;

        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(
            rect.TrimLeft(Margin),
            "ColonyManagerRedux.ManagerSettings.DefaultTemplate".Translate()
        );
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(
            rect.TrimRight(Margin),
            string.IsNullOrEmpty(_defaultTemplateName)
                ? "ColonyManagerRedux.ManagerSettings.DefaultTemplate.None".Translate().Resolve()
                : _defaultTemplateName
        );
        Text.Anchor = TextAnchor.UpperLeft;

        Widgets.DrawHighlightIfMouseover(rect);
        if (Widgets.ButtonInvisible(rect))
        {
            var options = new List<FloatMenuOption>
            {
                new(
                    "ColonyManagerRedux.ManagerSettings.DefaultTemplate.None".Translate(),
                    () => DefaultTemplateName = null
                ),
            };
            foreach (var templateName in ManagerJobTemplates.GetTemplateNames())
            {
                options.Add(
                    new FloatMenuOption(templateName, () => DefaultTemplateName = templateName)
                );
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        return pos.y - start.y;
    }

    private const int MaxOperationsPerTick = 30;
    private const int MaxTicksBetweenOperations = 60;

    private float DrawPerformanceSettings(Vector2 pos, float width)
    {
        var start = pos;

        DrawIntSliderConfig(
            _operationsPerTick,
            v => _operationsPerTick = v,
            MaxOperationsPerTick,
            ref pos,
            width,
            SliderHeight,
            "ColonyManagerRedux.PerformanceSettings.OperationsPerTick".Translate(
                _operationsPerTick.ToString(CultureInfo.InvariantCulture)
            ),
            "ColonyManagerRedux.PerformanceSettings.OperationsPerTick.Tip".Translate(
                _operationsPerTick.ToString(CultureInfo.InvariantCulture)
            ),
            1
        );

        var ticsBetweenOperationsText =
            $"{_ticksBetweenOperations} ({_ticksBetweenOperations.ToStringSecondsFromTicks()})";
        DrawIntSliderConfig(
            _ticksBetweenOperations,
            v => _ticksBetweenOperations = v,
            MaxTicksBetweenOperations,
            ref pos,
            width,
            SliderHeight,
            "ColonyManagerRedux.PerformanceSettings.TicksBetweenOperations".Translate(
                ticsBetweenOperationsText
            ),
            "ColonyManagerRedux.PerformanceSettings.TicksBetweenOperations.Tip".Translate(
                ticsBetweenOperationsText
            )
        );

        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.PerformanceSettings.AdvancedPerformanceSettings".Translate(),
            "ColonyManagerRedux.PerformanceSettings.AdvancedPerformanceSettings.Tip".Translate(),
            ref _showAdvancedPerformanceSettings
        );

        return pos.y - start.y;
    }

    private static void DrawCoroutineSlider(
        int value,
        Action<int> setValue,
        int maxValue,
        ref Vector2 pos,
        float width,
        string labelKey,
        string tipKey,
        string globalValueKey,
        int globalValue,
        Func<int, string>? valueFormatter = null
    )
    {
        var isGlobal = value == globalValue;
        string valueText = labelKey.Translate(
            isGlobal
                ? globalValueKey.Translate()
                : (
                    valueFormatter != null
                        ? valueFormatter(value)
                        : value.ToString(CultureInfo.InvariantCulture)
                )
        );
        string valueTip = tipKey.Translate(
            isGlobal ? globalValueKey.Translate() : value.ToString(CultureInfo.InvariantCulture)
        );
        DrawIntSliderConfig(
            value,
            setValue,
            maxValue,
            ref pos,
            width,
            SliderHeight,
            valueText,
            valueTip,
            globalValue
        );
    }

    private float DrawCoroutineSettings(
        CoroutineSettingsMethodAttribute coroutineSettings,
        Vector2 pos,
        float width
    )
    {
        if (coroutineSettings == null)
        {
            throw new ArgumentNullException(nameof(coroutineSettings));
        }

        var start = pos;

        if (coroutineSettings.HasOperationsPerTickSetting)
        {
            var value = _coroutineOperationsPerTick.TryGetValue(coroutineSettings.FullName);
            DrawCoroutineSlider(
                value,
                v => _coroutineOperationsPerTick[coroutineSettings.FullName] = v,
                MaxOperationsPerTick,
                ref pos,
                width,
                "ColonyManagerRedux.PerformanceSettings.OperationsPerTick",
                "ColonyManagerRedux.PerformanceSettings.OperationsPerTick.Tip",
                "ColonyManagerRedux.PerformanceSettings.UseGlobalValue",
                0
            );
        }

        if (coroutineSettings.HasTicksBetweenOperationsSetting)
        {
            var value = _coroutineTicksBetweenOperations.TryGetValue(
                coroutineSettings.FullName,
                -1
            );
            DrawCoroutineSlider(
                value,
                v => _coroutineTicksBetweenOperations[coroutineSettings.FullName] = v,
                MaxTicksBetweenOperations,
                ref pos,
                width,
                "ColonyManagerRedux.PerformanceSettings.TicksBetweenOperations",
                "ColonyManagerRedux.PerformanceSettings.TicksBetweenOperations.Tip",
                "ColonyManagerRedux.PerformanceSettings.UseGlobalValue",
                -1,
                v => $"{v} ({v.ToStringSecondsFromTicks()})"
            );
        }

        return pos.y - start.y;
    }

    private static void DrawSliderConfig(
        float value,
        Action<float> setValue,
        float maxValue,
        ref Vector2 cur,
        float width,
        float entryHeight,
        string label,
        string? tooltip = null,
        float minValue = 0,
        float roundTo = -1f
    )
    {
        var sliderRect = new Rect(
            Margin + cur.x,
            cur.y + (2 * Margin),
            width - (2 * Margin),
            entryHeight
        );
        cur.y += entryHeight + (3 * Margin);

        var newValue = Widgets.HorizontalSlider(
            sliderRect,
            value,
            minValue,
            maxValue,
            label: " ",
            leftAlignedLabel: label,
            roundTo: roundTo
        );
        if (value != newValue)
        {
            setValue?.Invoke(newValue);
        }

        if (!tooltip.NullOrEmpty())
        {
            TooltipHandler.TipRegion(sliderRect, tooltip);
        }
    }

    /// <summary>
    /// Draws an integer slider configuration UI element.
    /// </summary>
    /// <param name="value">The current value of the slider.</param>
    /// <param name="setValue">The action to set the new value.</param>
    /// <param name="maxValue">The maximum value of the slider.</param>
    /// <param name="cur">The current position vector, passed by reference.</param>
    /// <param name="width">The width of the slider.</param>
    /// <param name="entryHeight">The height of the slider entry.</param>
    /// <param name="label">The label for the slider.</param>
    /// <param name="tooltip">The tooltip for the slider (optional).</param>
    /// <param name="minValue">The minimum value of the slider (default is 0).</param>
    public static void DrawIntSliderConfig(
        int value,
        Action<int> setValue,
        int maxValue,
        ref Vector2 cur,
        float width,
        float entryHeight,
        string label,
        string? tooltip = null,
        int minValue = 0
    ) =>
        DrawSliderConfig(
            value,
            v => setValue((int)v),
            maxValue,
            ref cur,
            width,
            entryHeight,
            label,
            tooltip,
            minValue,
            roundTo: 1f
        );

    private static UpdateInterval TicksToInterval(int ticks)
    {
        var matchedTicks = FindMatchingTicks(
            Utilities.UpdateIntervalOptions.Select(i => i.Ticks),
            ticks
        );
        return matchedTicks == null
            ? UpdateInterval.Daily
            : Utilities.UpdateIntervalOptions.First(i => i.Ticks == matchedTicks.Value);
    }

    /// <summary>
    /// Pure linear search behind <see cref="TicksToInterval"/>, decoupled from live, translated
    /// <see cref="UpdateInterval"/> objects. Returns the first matching tick count, or null if
    /// none of <paramref name="optionTicks"/> equal <paramref name="ticks"/>.
    /// </summary>
    internal static int? FindMatchingTicks(IEnumerable<int> optionTicks, int ticks)
    {
        foreach (var optionTick in optionTicks)
        {
            if (optionTick == ticks)
            {
                return optionTick;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public override void ExposeData()
    {
        Scribe_Values.Look(ref _doVerboseLogging, "doVerboseLogging", false);
        Scribe_Values.Look(
            ref _defaultUpdateIntervalTicks,
            "defaultUpdateInterval",
            GenDate.TicksPerDay
        );
        Scribe_Values.Look(ref _defaultTargetCount, "defaultTargetCount", 500);
        Scribe_Values.Look(ref _defaultShouldCheckReachable, "defaultShouldCheckReachable", true);
        Scribe_Values.Look(ref _defaultUsePathBasedDistance, "defaultUsePathBasedDistance", false);
        Scribe_Values.Look(ref _defaultCountAllOnMap, "defaultCountAllOnMap", false);
        Scribe_Values.Look(
            ref _newJobsAreImmediatelyOutdated,
            "newJobsAreImmediatelyOutdated",
            true
        );
        Scribe_Values.Look(ref _recordHistoricalData, "recordHistoricalData", true);
        Scribe_Values.Look(
            ref _newJobsShouldBeResourceLocked,
            "newJobsShouldBeResourceLocked",
            true
        );
        Scribe_Values.Look(
            ref _showInfoCardButtonsWherePossible,
            "showInfoCardButtonsWherePossible",
            true
        );
        Scribe_Values.Look(ref _maxDesignationsPerJob, "maxDesignationsPerJob");
        Scribe_Values.Look(
            ref _autoApplyDefaultTemplateOnFirstStation,
            "autoApplyDefaultTemplateOnFirstStation",
            true
        );
        Scribe_Values.Look(ref _defaultTemplateName, "defaultTemplateName");
        Scribe_Collections.Look(
            ref _customUpdateIntervalTickList,
            "customUpdateIntervalTickList",
            LookMode.Value
        );
        Scribe_Values.Look(ref _showNoManagerAlert, "showNoManagerAlert", true);
        Scribe_Values.Look(ref _showNoTableAlert, "showNoTableAlert", true);
        Scribe_Values.Look(ref _showJobsNotUpdatingAlert, "showJobsNotUpdatingAlert", true);
        Scribe_Values.Look(ref _daysBeforeShowingAlert, "daysBeforeShowingAlert", 0.5f);
        Scribe_Values.Look(ref _daysBeforeShowingHighAlert, "daysBeforeShowingHighAlert", 1f);
        Scribe_Values.Look(
            ref _daysBeforeShowingCriticalAlert,
            "daysBeforeShowingCriticalAlert",
            2f
        );
        Scribe_Values.Look(ref _showNoTableNeededAlert, "showNoTableNeededAlert", true);

        Scribe_Collections.Look(ref _managerSettings, "jobSettings", LookMode.Deep);
        Scribe_Collections.Look(ref _disabledManagers, "disabledManagers", LookMode.Def);

        Scribe_Values.Look(ref _operationsPerTick, "operationsPerTick", 10);
        Scribe_Values.Look(ref _ticksBetweenOperations, "ticksBetweenOperations", 0);
        Scribe_Values.Look(
            ref _showAdvancedPerformanceSettings,
            "showAdvancedPerformanceSettings",
            false
        );
        Scribe_Collections.Look(
            ref _coroutineOperationsPerTick,
            "coroutineOperationsPerTick",
            LookMode.Value,
            LookMode.Value
        );
        Scribe_Collections.Look(
            ref _coroutineTicksBetweenOperations,
            "coroutineTicksBetweenOperations",
            LookMode.Value,
            LookMode.Value
        );

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            _managerSettings ??= [.. MakeManagerSettings()];
            EnsureManagerSettingsAreCorrect();

            _disabledManagers ??= [];
            _customUpdateIntervalTickList ??= [];

            _coroutineOperationsPerTick ??= [];
            _coroutineTicksBetweenOperations ??= [];
        }
    }

    private void EnsureManagerSettingsAreCorrect()
    {
        var allManagerDefs = DefDatabase<ManagerDef>
            .AllDefs.Where(m => m.managerSettingsClass != null)
            .ToHashSet();

        // remove settings that should no longer be here
        for (var i = _managerSettings.Count - 1; i >= 0; i--)
        {
            var item = _managerSettings[i];
            if (item == null)
            {
                ColonyManagerReduxMod.Instance.LogWarning($"Job settings entry {i} is null");
                _managerSettings.RemoveAt(i);
            }
            else if (item.Def == null)
            {
                ColonyManagerReduxMod.Instance.LogWarning($"Job settings entry {i}'s Def is null");
                _managerSettings.RemoveAt(i);
            }
            else if (!ShouldKeepManagerSettingsEntry(item.Def, allManagerDefs))
            {
                ColonyManagerReduxMod.Instance.LogWarning(
                    $"Job settings exist for {item.Def} but no such ManagerDef was found"
                );
                _managerSettings.RemoveAt(i);
            }
        }

        // add any settings that are missing
        var presentManagerDefs = _managerSettings.Select(s => s.Def).ToHashSet();
        foreach (var missingDef in FindMissingManagerDefs(allManagerDefs, presentManagerDefs))
        {
            ColonyManagerReduxMod.Instance.LogMessage(
                $"Creating new settings instance for {missingDef} since it was missing"
            );
            _managerSettings.Add(ManagerDefMaker.MakeManagerSettings(missingDef)!);
        }

        _managerSettings.SortBy(j => j.Def.order);
    }

    /// <summary>
    /// Pure membership check behind <see cref="EnsureManagerSettingsAreCorrect"/>: an entry is
    /// kept only if its def still exists among the currently registered defs.
    /// </summary>
    internal static bool ShouldKeepManagerSettingsEntry<TDef>(
        TDef def,
        IReadOnlyCollection<TDef> validDefs
    )
        where TDef : notnull => validDefs.Contains(def);

    /// <summary>
    /// Pure set-difference behind <see cref="EnsureManagerSettingsAreCorrect"/>: which known defs
    /// don't yet have a settings entry and need one created for them.
    /// </summary>
    internal static IEnumerable<TDef> FindMissingManagerDefs<TDef>(
        IReadOnlyCollection<TDef> allDefs,
        IReadOnlyCollection<TDef> presentDefs
    )
        where TDef : notnull => allDefs.Where(d => !presentDefs.Contains(d));

    /// <summary>
    /// Gets the manager settings for a specific manager definition.
    /// </summary>
    /// <typeparam name="T">The type of manager settings.</typeparam>
    /// <param name="def">The manager definition.</param>
    /// <returns>The manager settings instance, or null if not found.</returns>
    public T? ManagerSettingsFor<T>(ManagerDef def)
        where T : ManagerSettings => _managerSettings.Find(s => s.Def == def) as T;

    /// <summary>
    /// Resets the tab list before opening the settings window.
    /// </summary>
    internal void PreOpen() => TabList = null;
}

/// <summary>
/// Attribute to mark a type as containing coroutine settings.
/// </summary>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Struct,
    Inherited = false,
    AllowMultiple = false
)]
public sealed class CoroutineSettingsTypeAttribute : Attribute
{
    /// <summary>
    /// Gets all types with the <see cref="CoroutineSettingsTypeAttribute"/> applied.
    /// </summary>
    public static List<Type> AllTypesWithAttribute =>
        field ??= [.. GenTypes.AllTypesWithAttribute<CoroutineSettingsTypeAttribute>()];
}

/// <summary>
/// Attribute to mark a method as a coroutine settings method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class CoroutineSettingsMethodAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the type that declares the coroutine method.
    /// </summary>
    public Type? Type { get; set; }

    /// <summary>
    /// Gets or sets the coroutine method info.
    /// </summary>
    public MethodInfo? Method { get; set; }

    /// <summary>
    /// Gets the full name of the coroutine method.
    /// </summary>
    public string FullName => $"{Type?.FullName}.{Method?.Name}";

    /// <summary>
    /// Gets or sets whether this coroutine method has an operations-per-tick setting.
    /// </summary>
    public bool HasOperationsPerTickSetting { get; set; } = true;

    /// <summary>
    /// Gets or sets whether this coroutine method has a ticks-between-operations setting.
    /// </summary>
    public bool HasTicksBetweenOperationsSetting { get; set; } = true;
}
