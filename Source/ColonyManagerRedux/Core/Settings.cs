// Settings.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.Extensions;
using ilyvion.Laboratory.UI;
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
        return DefDatabase<ManagerDef>.AllDefs
            .Where(m => m.managerSettingsClass != null)
            .OrderBy(m => m.order)
            .Select(m => ManagerDefMaker.MakeManagerSettings(m)!);
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

        return pos.y - start.y;
    }

    public float DrawThreshold(Vector2 pos, float width)
    {
        var start = pos;

        DrawTriggerConfig(ref pos, width, ListEntryHeight,
            "ColonyManagerRedux.ManagerSettings.TargetCount".Translate(
                DefaultTargetCount));

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

    public void DrawTriggerConfig(ref Vector2 cur, float width, float entryHeight, string label,
        string? tooltip = null)
    {
        // target threshold
        var thresholdLabelRect = new Rect(
            cur.x,
            cur.y,
            width,
            entryHeight);
        cur.y += entryHeight;

        var thresholdRect = new Rect(
            cur.x,
            cur.y,
            width,
            SliderHeight);
        cur.y += SliderHeight;

        var useResourceListerToggleRect = new Rect(
            cur.x,
            cur.y,
            width,
            entryHeight);
        cur.y += entryHeight;

        IlyvionWidgets.Label(thresholdLabelRect, label!, tooltip);

        Utilities.DrawToggle(useResourceListerToggleRect, "ColonyManagerRedux.Threshold.CountAllOnMap".Translate(),
                              "ColonyManagerRedux.Threshold.CountAllOnMap.Tip".Translate(), ref _defaultCountAllOnMap, true);
        DefaultTargetCount = (int)GUI.HorizontalSlider(thresholdRect, DefaultTargetCount, 0, DefaultMaxUpperThreshold);
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

        Scribe_Collections.Look(ref _managerSettings, "jobSettings", LookMode.Deep);
        Scribe_Collections.Look(ref _disabledManagers, "disabledManagers", LookMode.Def);

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            _managerSettings ??= MakeManagerSettings().ToList();
            EnsureManagerSettingsAreCorrect();

            _disabledManagers ??= [];
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
