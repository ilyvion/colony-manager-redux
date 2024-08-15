// Settings.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.IO;
using ilyvion.Laboratory.Extensions;
using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[HotSwappable]
public class Settings : ModSettings
{
    private bool _jobDefSettingsAreLoaded;
    private List<ManagerSettings> _jobSettings = [];
    private int _currentJobSettingsTab = -1;

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

    public UpdateInterval DefaultUpdateInterval
    {
        get => TicksToInterval(DefaultUpdateIntervalTicks);
        internal set => DefaultUpdateIntervalTicks = value.Ticks;
    }

    public Settings()
    {
        ColonyManagerReduxMod.Instance.LogDebug("Initializing new settings value!");
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            ColonyManagerReduxMod.Instance.LogDebug("Loading manager job defs");
            _jobSettings.AddRange(MakeManagerSettings());
            _jobDefSettingsAreLoaded = true;
            ReloadSettings();
        });
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
        var tabs = new[] {
            new TabRecord("ColonyManagerRedux.SharedSettingsTabLabel".Translate(), () => {
                _currentJobSettingsTab = -1;
            }, _currentJobSettingsTab == -1)
        }.Concat(_jobSettings.Select((s, i) => new TabRecord(s.Label, () =>
            {
                _currentJobSettingsTab = i;
            }, _currentJobSettingsTab == i)))
        .ToList();

        int rowCount = (int)Math.Ceiling((double)tabs.Count / 5);
        rect.yMin += rowCount * SectionHeaderHeight + Margin;
        Widgets.DrawMenuSection(rect);
        TabDrawer.DrawTabs(rect, tabs, rowCount, null);

        if (_currentJobSettingsTab == -1)
        {
            Widgets_Section.BeginSectionColumn(rect, "Settings", out Vector2 position, out float width);

            Widgets_Section.Section(ref position, width, DrawGeneralSettings, "ColonyManagerRedux.GeneralSettingsTabLabel".Translate());
            Widgets_Section.Section(ref position, width, DrawThreshold, "ColonyManagerRedux.ManagerSettings.DefaultThresholdSettings".Translate());

            Widgets_Section.EndSectionColumn("Settings", position);
        }
        else
        {
            GUI.BeginGroup(rect);
            _jobSettings[_currentJobSettingsTab].DoPanelContents(rect.AtZero());
            GUI.EndGroup();
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
        // This normally happens much too early in the loading sequence (before defs are loaded,
        // i.e. before MakeManagerSettings can do its thing), so we're abandoning ship until
        // _jobDefSettingsAreLoaded is true, and then ReloadSettings() does a reload for us.
        if (!_jobDefSettingsAreLoaded)
        {
            return;
        }

        Scribe_Values.Look(ref _defaultUpdateIntervalTicks, "defaultUpdateInterval", GenDate.TicksPerDay);
        Scribe_Values.Look(ref _defaultTargetCount, "defaultTargetCount", 500);
        Scribe_Values.Look(ref _defaultShouldCheckReachable, "defaultShouldCheckReachable", true);
        Scribe_Values.Look(ref _defaultUsePathBasedDistance, "defaultUsePathBasedDistance", false);
        Scribe_Values.Look(ref _defaultCountAllOnMap, "defaultCountAllOnMap", false);
        Scribe_Values.Look(ref _newJobsAreImmediatelyOutdated, "newJobsAreImmediatelyOutdated", true);
        Scribe_Values.Look(ref _recordHistoricalData, "recordHistoricalData", true);

        Scribe_Collections.Look(ref _jobSettings, "jobSettings", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            _jobSettings ??= MakeManagerSettings().ToList();
            EnsureJobSettingsAreCorrect();
        }
    }

    private void EnsureJobSettingsAreCorrect()
    {
        var allManagerDefs = DefDatabase<ManagerDef>.AllDefs
            .Where(m => m.managerSettingsClass != null)
            .ToDictionary(j => j, _ => false);

        // remove settings that should no longer be here
        for (int i = _jobSettings.Count - 1; i >= 0; i--)
        {
            ManagerSettings item = _jobSettings[i];
            if (item == null)
            {
                ColonyManagerReduxMod.Instance.LogWarning($"Job settings entry {i} is null");
                _jobSettings.RemoveAt(i);
            }
            else if (item.Def == null)
            {
                ColonyManagerReduxMod.Instance.LogWarning($"Job settings entry {i}'s Def is null");
                _jobSettings.RemoveAt(i);
            }
            else if (!allManagerDefs.ContainsKey(item.Def))
            {
                ColonyManagerReduxMod.Instance.LogWarning($"Job settings exist for {item.Def} but no such ManagerDef was found");
                _jobSettings.RemoveAt(i);
            }
        }

        // add any settings that are missing
        foreach (var jobSettings in _jobSettings)
        {
            allManagerDefs[jobSettings.Def] = true;
        }
        foreach (var missingDef in allManagerDefs.Where(kv => !kv.Value).Select(kv => kv.Key))
        {
            ColonyManagerReduxMod.Instance.LogMessage($"Creating new settings instance for {missingDef} since it was missing");
            _jobSettings.Add(ManagerDefMaker.MakeManagerSettings(missingDef)!);
        }

        _jobSettings.SortBy(j => j.Def.order);
    }

    private void ReloadSettings()
    {
        string settingsFilename = LoadedModManager.GetSettingsFilename(
            Mod.Content.FolderName,
            Mod.GetType().Name);
        try
        {
            if (File.Exists(settingsFilename))
            {
                Scribe.loader.InitLoading(settingsFilename);
                try
                {
                    if (Scribe.EnterNode("ModSettings"))
                    {
                        try
                        {
                            ExposeData();
                        }
                        finally
                        {
                            Scribe.ExitNode();
                        }
                    }
                }
                finally
                {
                    Scribe.loader.FinalizeLoading();
                }
            }
        }
        catch (Exception e)
        {
            ColonyManagerReduxMod.Instance
                .LogException($"Caught exception while reloading mod settings data for {Mod.Content.FolderName}", e);
        };
    }

    public T? ManagerSettingsFor<T>(ManagerDef def) where T : ManagerSettings
    {
        return _jobSettings.Find(s => s.Def == def) as T;
    }
}
