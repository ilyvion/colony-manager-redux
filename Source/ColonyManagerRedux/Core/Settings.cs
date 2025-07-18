// Settings.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.Extensions;
using ilyvion.Laboratory.UI;
using Verse.Sound;
using static ColonyManagerRedux.Constants;

using TabRecord = ilyvion.Laboratory.UI.TabRecord;

namespace ColonyManagerRedux;

[HotSwappable]
public class Settings : ModSettings
{
    private readonly SharedManagerSettings _sharedManagerSettings;
    private List<ManagerSettings> _managerSettings = [];
    private Tab _currentManagerSettings;

    private int _defaultUpdateIntervalTicks = GenDate.TicksPerDay;
    public int DefaultUpdateIntervalTicks
    {
        get => _defaultUpdateIntervalTicks;
        internal set => _defaultUpdateIntervalTicks = value;
    }

    private int _defaultTargetCount = 500;
    public int DefaultTargetCount
    {
        get => _defaultTargetCount;
        internal set => _defaultTargetCount = value;
    }

    private bool _defaultCountAllOnMap;
    public bool DefaultCountAllOnMap
    {
        get => _defaultCountAllOnMap;
        internal set => _defaultCountAllOnMap = value;
    }

    private bool _defaultShouldCheckReachable = true;
    public bool DefaultShouldCheckReachable
    {
        get => _defaultShouldCheckReachable;
        internal set => _defaultShouldCheckReachable = value;
    }

    private bool _defaultUsePathBasedDistance;
    public bool DefaultUsePathBasedDistance
    {
        get => _defaultUsePathBasedDistance;
        internal set => _defaultUsePathBasedDistance = value;
    }

    private bool _newJobsAreImmediatelyOutdated = true;
    public bool NewJobsAreImmediatelyOutdated
    {
        get => _newJobsAreImmediatelyOutdated;
        internal set => _newJobsAreImmediatelyOutdated = value;
    }

    private bool _recordHistoricalData = true;
    public bool RecordHistoricalData
    {
        get => _recordHistoricalData;
        internal set => _recordHistoricalData = value;
    }

    private int _maxDesignationsPerJob;
    public int MaxDesignationsPerJob
    {
        get => _maxDesignationsPerJob * 10;
        internal set => _maxDesignationsPerJob = value / 10;
    }
    public bool CanAddMoreDesignations(int currentCount)
    {
        return MaxDesignationsPerJob == 0 || MaxDesignationsPerJob > currentCount;
    }
    public bool ShouldRemoveMoreDesignations(int currentCount)
    {
        return MaxDesignationsPerJob != 0 && MaxDesignationsPerJob < currentCount;
    }

    private List<int> _customUpdateIntervalTickList = [];
    public List<int> CustomUpdateIntervalTickList
    {
        get => _customUpdateIntervalTickList;
        internal set => _customUpdateIntervalTickList = value;
    }

    private bool _showNoManagerAlert = true;
    public bool ShowNoManagerAlert
    {
        get => _showNoManagerAlert;
        internal set => _showNoManagerAlert = value;
    }

    private bool _showNoTableAlert = true;
    public bool ShowNoTableAlert
    {
        get => _showNoTableAlert;
        internal set => _showNoTableAlert = value;
    }

    private bool _showJobsNotUpdatingAlert = true;
    public bool ShowJobsNotUpdatingAlert
    {
        get => _showJobsNotUpdatingAlert;
        internal set => _showJobsNotUpdatingAlert = value;
    }

    private float _daysBeforeShowingAlert = 0.5f;
    public float DaysBeforeShowingAlert
    {
        get => _daysBeforeShowingAlert;
        internal set => _daysBeforeShowingAlert = value;
    }

    private float _daysBeforeShowingHighAlert = 1f;
    public float DaysBeforeShowingHighAlert
    {
        get => _daysBeforeShowingHighAlert;
        internal set => _daysBeforeShowingHighAlert = value;
    }

    private float _daysBeforeShowingCriticalAlert = 2f;
    public float DaysBeforeShowingCriticalAlert
    {
        get => _daysBeforeShowingCriticalAlert;
        internal set => _daysBeforeShowingCriticalAlert = value;
    }

    private bool _showNoTableNeededAlert = true;
    public bool ShowNoTableNeededAlert
    {
        get => _showNoTableNeededAlert;
        internal set => _showNoTableNeededAlert = value;
    }

    private HashSet<ManagerDef> _disabledManagers = [];
    public HashSet<ManagerDef> DisabledManagers => _disabledManagers;

    public UpdateInterval DefaultUpdateInterval
    {
        get => TicksToInterval(DefaultUpdateIntervalTicks);
        internal set => DefaultUpdateIntervalTicks = value.Ticks;
    }

    private List<TabRecord>? _tabList;
    private List<TabRecord> TabList
    {
        get
        {
            _tabList ??=
                Gen.YieldSingle<Tab>(_sharedManagerSettings)
                .Concat(_managerSettings.Where(m => m.Show))
                .Select(m => new TabRecord(m, () => ref _currentManagerSettings))
                .ToList();
            return _tabList;
        }
    }

    private sealed class SharedManagerSettings(Settings settings) : Tab
    {
        public override string Title => "ColonyManagerRedux.SharedSettingsTabLabel".Translate();
        public override void DoTabContents(Rect inRect)
        {
            Widgets_Section.BeginSectionColumn(
                inRect, "Settings", out Vector2 position, out float width);

            Widgets_Section.Section(
                ref position,
                width,
                settings.DrawGeneralSettings,
                "ColonyManagerRedux.GeneralSettingsTabLabel".Translate());
            Widgets_Section.Section(
                ref position,
                width,
                settings.DrawThreshold,
                "ColonyManagerRedux.ManagerSettings.DefaultThresholdSettings".Translate());
            Widgets_Section.Section(
                ref position,
                width,
                settings.DrawCustomUpdateIntervals,
                "ColonyManagerRedux.ManagerSettings.CustomUpdateIntervals".Translate());
            Widgets_Section.Section(
                ref position,
                width,
                settings.DrawAlertSettings,
                "ColonyManagerRedux.ManagerSettings.AlertSettings".Translate());
            Widgets_Section.Section(
                ref position,
                width,
                settings.DrawDisableManagers,
                "ColonyManagerRedux.ManagerSettings.DisableManagers".Translate());

            Widgets_Section.EndSectionColumn("Settings", position);
        }
    }

    public Settings()
    {
        ColonyManagerReduxMod.Instance.LogDebug("Loading manager job defs");
        _managerSettings.AddRange(MakeManagerSettings());

        _currentManagerSettings = _sharedManagerSettings = new(this);
    }

    private static IEnumerable<ManagerSettings> MakeManagerSettings()
    {
        foreach (var managerDef in DefDatabase<ManagerDef>.AllDefs
            .Where(m => m.managerSettingsClass != null)
            .OrderBy(m => m.order))
        {
            ManagerSettings? managerSettings = null;
            try
            {
                managerSettings = ManagerDefMaker.MakeManagerSettings(managerDef)!;
            }
            catch (Exception err)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    $"Could not create {nameof(ManagerSettings)} instance for " +
                    $"{managerDef.defName} because it threw an exception: \n{err}");
                continue;
            }
            yield return managerSettings;
        }
    }

    public void DoSettingsWindowContents(Rect rect)
    {
        int rowCount = (int)Math.Ceiling((double)(_managerSettings.Count + 1) / 5);
        rect.yMin += rowCount * SectionHeaderHeight + Margin;
        Widgets.DrawMenuSection(rect);
        TabDrawer.DrawTabs(rect, TabList, rowCount, null);

        try
        {
            using var _g = GUIScope.WidgetGroup(rect);
            _currentManagerSettings.DoTabContents(rect.AtZero());
        }
        catch (Exception err)
        {
            ColonyManagerReduxMod.Instance.LogError(
                $"Exception while calling DoTabContents for {_currentManagerSettings.Title}:\n" +
                err);
        }
    }

    private float DrawGeneralSettings(Vector2 pos, float width)
    {
        var start = pos;

        // target threshold
        var rect = new Rect(
            pos.x,
            pos.y,
            width,
            ListEntryHeight);
        pos.y += ListEntryHeight;

        // labels
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(rect.TrimLeft(Margin), "ColonyManagerRedux.ManagerDefaultUpdateInterval".Translate());
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
                options.Add(new FloatMenuOption(interval.Label, () => DefaultUpdateInterval = interval));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        Utilities.DrawToggle(ref pos, width,
            "ColonyManagerRedux.NewJobsAreImmediatelyOutdated".Translate(),
            "ColonyManagerRedux.NewJobsAreImmediatelyOutdated.Tip".Translate(),
            ref _newJobsAreImmediatelyOutdated);

        Utilities.DrawToggle(ref pos, width,
            "ColonyManagerRedux.RecordHistoricalData".Translate(),
            "ColonyManagerRedux.RecordHistoricalData.Tip".Translate(),
            ref _recordHistoricalData, true);

        DrawIntSliderConfig(
            _maxDesignationsPerJob,
            v => _maxDesignationsPerJob = v,
            150,
            ref pos,
            width,
            ListEntryHeight,
            MaxDesignationsPerJob > 0
                ? "ColonyManagerRedux.ManagerSettings.MaxDesignationsPerJob".Translate(
                    MaxDesignationsPerJob)
                : "ColonyManagerRedux.ManagerSettings.NoMaxDesignationsPerJob".Translate(),
            "ColonyManagerRedux.ManagerSettings.MaxDesignationsPerJob.Tip".Translate());

        return pos.y - start.y;
    }

    public float DrawThreshold(Vector2 pos, float width)
    {
        var start = pos;

        DrawIntSliderConfig(
            DefaultTargetCount,
            v => DefaultTargetCount = v,
            DefaultMaxUpperThreshold,
            ref pos,
            width,
            ListEntryHeight,
            "ColonyManagerRedux.ManagerSettings.TargetCount".Translate(
                DefaultTargetCount));

        var countAllOnMapToggleRect = new Rect(
            pos.x,
            pos.y,
            width,
            ListEntryHeight);
        pos.y += ListEntryHeight;
        Utilities.DrawToggle(
            countAllOnMapToggleRect,
            "ColonyManagerRedux.Threshold.CountAllOnMap".Translate(),
            "ColonyManagerRedux.Threshold.CountAllOnMap.Tip".Translate(),
            ref _defaultCountAllOnMap,
            true);

        Utilities.DrawReachabilityToggle(ref pos, width, ref _defaultShouldCheckReachable);
        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.Threshold.PathBasedDistance".Translate(),
            "ColonyManagerRedux.Threshold.PathBasedDistance.Tip".Translate(),
            ref _defaultUsePathBasedDistance,
            true);

        return pos.y - start.y;
    }

    private const int DefaultCustomUpdateIntervalTicks = GenDate.TicksPerDay;
    private int _addCustomUpdateIntervalTicks = DefaultCustomUpdateIntervalTicks;
    public float DrawCustomUpdateIntervals(Vector2 pos, float width)
    {
        var start = pos;

        string periodLabel = _addCustomUpdateIntervalTicks.ToStringTicksToPeriodVerbose();
        Vector2 periodSize = Text.CalcSize(periodLabel);
        var periodLabelAreaRect = new Rect(pos.x, pos.y, periodSize.x + Margin, ListEntryHeight);
        var periodWidth = periodSize.x + Margin;
        IlyvionWidgets.Label(periodLabelAreaRect, periodLabel, TextAnchor.MiddleLeft);

        var rowPos = pos;
        rowPos.x += periodWidth;
        if (RowButton("0", ref rowPos))
        {
            _addCustomUpdateIntervalTicks = 0;
        }
        if (RowButton("ColonyManagerRedux.ManagerSettings.DecreaseCustomUpdateIntervalByHour".Translate(), ref rowPos))
        {
            _addCustomUpdateIntervalTicks = Mathf.Max(0, _addCustomUpdateIntervalTicks - GenDate.TicksPerHour);
        }
        if (RowButton("ColonyManagerRedux.ManagerSettings.IncreaseCustomUpdateIntervalByHour".Translate(), ref rowPos))
        {
            _addCustomUpdateIntervalTicks += GenDate.TicksPerHour;
        }
        if (RowButton("ColonyManagerRedux.ManagerSettings.DecreaseCustomUpdateIntervalByDay".Translate(), ref rowPos))
        {
            _addCustomUpdateIntervalTicks = Mathf.Max(0, _addCustomUpdateIntervalTicks - GenDate.TicksPerDay);
        }
        if (RowButton("ColonyManagerRedux.ManagerSettings.IncreaseCustomUpdateIntervalByDay".Translate(), ref rowPos))
        {
            _addCustomUpdateIntervalTicks += GenDate.TicksPerDay;
        }
        if (RowButton("ColonyManagerRedux.ManagerSettings.DecreaseCustomUpdateIntervalByYear".Translate(), ref rowPos))
        {
            _addCustomUpdateIntervalTicks = Mathf.Max(0, _addCustomUpdateIntervalTicks - GenDate.TicksPerYear);
        }
        if (RowButton("ColonyManagerRedux.ManagerSettings.IncreaseCustomUpdateIntervalByYear".Translate(), ref rowPos))
        {
            _addCustomUpdateIntervalTicks += GenDate.TicksPerYear;
        }
        rowPos.x += Margin;
        if (RowButton("ColonyManagerRedux.ManagerSettings.AddCustomUpdateIntervals".Translate(), ref rowPos))
        {
            _customUpdateIntervalTickList.Add(_addCustomUpdateIntervalTicks);
            _customUpdateIntervalTickList.Sort();
            _addCustomUpdateIntervalTicks = DefaultCustomUpdateIntervalTicks;
        }
        pos.y += ListEntryHeight + Margin;

        for (int i = 0; i < _customUpdateIntervalTickList.Count; i++)
        {
            int customUpdateIntervalTicks = _customUpdateIntervalTickList[i];
            var rect = new Rect(
                Margin + pos.x,
                pos.y,
                width - 2 * Margin,
                ListEntryHeight);
            pos.y += ListEntryHeight;
            if (i % 2 == 0)
            {
                Widgets.DrawLightHighlight(rect);
            }
            IlyvionWidgets.Label(
                new Rect(rect.x + 4f, rect.y, rect.width - 4f, rect.height),
                customUpdateIntervalTicks.ToStringTicksToPeriodVerbose(),
                TextAnchor.MiddleLeft);
            if (Widgets.ButtonImage(
                new Rect(rect.xMax - ListEntryHeight, rect.y, ListEntryHeight, ListEntryHeight),
                TexButton.Delete,
                Color.white,
                GenUI.SubtleMouseoverColor))
            {
                _customUpdateIntervalTickList.RemoveAt(i);
                SoundDefOf.Tick_Low.PlayOneShotOnCamera();
            }
        }

        return pos.y - start.y;
    }

    private static bool RowButton(string buttonLabel, ref Vector2 pos)
    {
        var buttonWidth = Text.CalcSize(buttonLabel).x + 4 * Margin;
        Rect buttonRect = new(pos.x, pos.y, buttonWidth, ListEntryHeight);
        pos.x += buttonWidth + Margin;
        return Widgets.ButtonText(buttonRect, buttonLabel);
    }

    public float DrawAlertSettings(Vector2 pos, float width)
    {
        const float MaxAlertDays = 30f;

        var start = pos;

        Utilities.DrawToggle(ref pos, width,
            "ColonyManagerRedux.ManagerSettings.AlertSettings.ShowNoManagerAlert".Translate(),
            "ColonyManagerRedux.ManagerSettings.AlertSettings.ShowNoManagerAlert.Tip".Translate(),
            ref _showNoManagerAlert);

        Utilities.DrawToggle(ref pos, width,
            "ColonyManagerRedux.ManagerSettings.AlertSettings.ShowNoTableAlert".Translate(),
            "ColonyManagerRedux.ManagerSettings.AlertSettings.ShowNoTableAlert.Tip".Translate(),
            ref _showNoTableAlert);

        Utilities.DrawToggle(ref pos, width,
            "ColonyManagerRedux.ManagerSettings.AlertSettings.ShowNoTableNeededAlert".Translate(),
            "ColonyManagerRedux.ManagerSettings.AlertSettings.ShowNoTableNeededAlert.Tip".Translate(),
            ref _showNoTableNeededAlert);

        Utilities.DrawToggle(ref pos, width,
            "ColonyManagerRedux.ManagerSettings.AlertSettings.ShowJobsNotUpdatingAlert".Translate(),
            "ColonyManagerRedux.ManagerSettings.AlertSettings.ShowJobsNotUpdatingAlert.Tip".Translate(),
            ref _showJobsNotUpdatingAlert);

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
                ListEntryHeight,
                "ColonyManagerRedux.ManagerSettings.AlertSettings.DaysBeforeShowingAlert".Translate(_daysBeforeShowingAlert.ToString("F1")),
                minValue: 0.5f,
                roundTo: 0.5f);

            if (_daysBeforeShowingHighAlert < _daysBeforeShowingAlert)
            {
                _daysBeforeShowingHighAlert = _daysBeforeShowingAlert;
            }

            DrawSliderConfig(
                _daysBeforeShowingHighAlert,
                v => _daysBeforeShowingHighAlert = v,
                MaxAlertDays,
                ref pos,
                width,
                ListEntryHeight,
                "ColonyManagerRedux.ManagerSettings.AlertSettings.DaysBeforeShowingHighAlert".Translate(_daysBeforeShowingHighAlert.ToString("F1")),
                minValue: _daysBeforeShowingAlert,
                roundTo: 0.5f);

            if (_daysBeforeShowingCriticalAlert < _daysBeforeShowingHighAlert)
            {
                _daysBeforeShowingCriticalAlert = _daysBeforeShowingHighAlert;
            }

            DrawSliderConfig(
                _daysBeforeShowingCriticalAlert,
                v => _daysBeforeShowingCriticalAlert = v,
                MaxAlertDays,
                ref pos,
                width,
                ListEntryHeight,
                "ColonyManagerRedux.ManagerSettings.AlertSettings.DaysBeforeShowingCriticalAlert".Translate(_daysBeforeShowingCriticalAlert.ToString("F1")),
                minValue: _daysBeforeShowingHighAlert,
                roundTo: 0.5f);

            pos.x -= Margin;
            width += 2 * Margin;
        }

        return pos.y - start.y;
    }

    public float DrawDisableManagers(Vector2 pos, float width)
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
                wrap: false);
        }

        return pos.y - start.y;
    }

    public static void DrawSliderConfig(
        float value,
        Action<float> setValue,
        float maxValue,
        ref Vector2 cur,
        float width,
        float entryHeight,
        string label,
        string? tooltip = null,
        float minValue = 0,
        float roundTo = -1f)
    {
        var sliderRect = new Rect(
            Margin + cur.x,
            cur.y,
            width - 2 * Margin,
            entryHeight + SliderHeight);
        cur.y += entryHeight + SliderHeight;

        var newValue = Widgets.HorizontalSlider(
            sliderRect,
            value,
            minValue,
            maxValue,
            label: " ",
            leftAlignedLabel: label,
            roundTo: roundTo);
        if (value != newValue)
        {
            setValue?.Invoke(newValue);
        }

        if (!tooltip.NullOrEmpty())
        {
            TooltipHandler.TipRegion(sliderRect, tooltip);
        }
    }

    public static void DrawIntSliderConfig(
        int value,
        Action<int> setValue,
        int maxValue,
        ref Vector2 cur,
        float width,
        float entryHeight,
        string label,
        string? tooltip = null,
        int minValue = 0)
    {
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
            roundTo: 1f);
    }

    private static UpdateInterval TicksToInterval(int ticks)
    {
        foreach (var interval in Utilities.UpdateIntervalOptions)
        {
            if (interval.Ticks == ticks)
            {
                return interval;
            }
        }

        return UpdateInterval.Daily;
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref _defaultUpdateIntervalTicks, "defaultUpdateInterval", GenDate.TicksPerDay);
        Scribe_Values.Look(ref _defaultTargetCount, "defaultTargetCount", 500);
        Scribe_Values.Look(ref _defaultShouldCheckReachable, "defaultShouldCheckReachable", true);
        Scribe_Values.Look(ref _defaultUsePathBasedDistance, "defaultUsePathBasedDistance", false);
        Scribe_Values.Look(ref _defaultCountAllOnMap, "defaultCountAllOnMap", false);
        Scribe_Values.Look(ref _newJobsAreImmediatelyOutdated, "newJobsAreImmediatelyOutdated", true);
        Scribe_Values.Look(ref _recordHistoricalData, "recordHistoricalData", true);
        Scribe_Values.Look(ref _maxDesignationsPerJob, "maxDesignationsPerJob");
        Scribe_Collections.Look(ref _customUpdateIntervalTickList, "customUpdateIntervalTickList", LookMode.Value);
        Scribe_Values.Look(ref _showNoManagerAlert, "showNoManagerAlert", true);
        Scribe_Values.Look(ref _showNoTableAlert, "showNoTableAlert", true);
        Scribe_Values.Look(ref _showJobsNotUpdatingAlert, "showJobsNotUpdatingAlert", true);
        Scribe_Values.Look(ref _daysBeforeShowingAlert, "daysBeforeShowingAlert", 0.5f);
        Scribe_Values.Look(ref _daysBeforeShowingHighAlert, "daysBeforeShowingHighAlert", 1f);
        Scribe_Values.Look(ref _daysBeforeShowingCriticalAlert, "daysBeforeShowingCriticalAlert", 2f);
        Scribe_Values.Look(ref _showNoTableNeededAlert, "showNoTableNeededAlert", true);

        Scribe_Collections.Look(ref _managerSettings, "jobSettings", LookMode.Deep);
        Scribe_Collections.Look(ref _disabledManagers, "disabledManagers", LookMode.Def);

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            _managerSettings ??= MakeManagerSettings().ToList();
            EnsureManagerSettingsAreCorrect();

            _disabledManagers ??= [];
            _customUpdateIntervalTickList ??= [];
        }
    }

    private void EnsureManagerSettingsAreCorrect()
    {
        var allManagerDefs = DefDatabase<ManagerDef>.AllDefs
            .Where(m => m.managerSettingsClass != null)
            .ToDictionary(j => j, _ => false);

        // remove settings that should no longer be here
        for (int i = _managerSettings.Count - 1; i >= 0; i--)
        {
            ManagerSettings item = _managerSettings[i];
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
            else if (!allManagerDefs.ContainsKey(item.Def))
            {
                ColonyManagerReduxMod.Instance.LogWarning($"Job settings exist for {item.Def} but no such ManagerDef was found");
                _managerSettings.RemoveAt(i);
            }
        }

        // add any settings that are missing
        foreach (var managerSettings in _managerSettings)
        {
            allManagerDefs[managerSettings.Def] = true;
        }
        foreach (var missingDef in allManagerDefs.Where(kv => !kv.Value).Select(kv => kv.Key))
        {
            ColonyManagerReduxMod.Instance.LogMessage($"Creating new settings instance for {missingDef} since it was missing");
            _managerSettings.Add(ManagerDefMaker.MakeManagerSettings(missingDef)!);
        }

        _managerSettings.SortBy(j => j.Def.order);
    }

    public T? ManagerSettingsFor<T>(ManagerDef def) where T : ManagerSettings
    {
        return _managerSettings.Find(s => s.Def == def) as T;
    }

    internal void PreOpen()
    {
        _tabList = null;
    }
}
