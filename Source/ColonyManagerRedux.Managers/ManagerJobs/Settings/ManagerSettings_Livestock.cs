// ManagerSettings_Livestock.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;
using static ColonyManagerRedux.Managers.ManagerJob_Livestock;

using TabRecord = Verse.TabRecord;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class PawnKindSettings : IExposable
{
    private PawnKindDef? _def;
    internal ManagerSettings_Livestock settings;

    public int[] DefaultCountTargets = Utilities_Livestock.AgeSexArray
                .Select(_ => 5)
                .ToArray();

    public bool DefaultTryTameMore;
    public bool DefaultTamePastTargets;

    public bool DefaultCullExcess = true;
    public bool DefaultCullTrained;
    public bool DefaultCullPregnant;
    public bool DefaultCullBonded;
    public LivestockCullingStrategy DefaultCullingStrategy;

    public bool DefaultUnassignTraining;
    public bool DefaultTrainYoung;

    public MasterMode DefaultMasterMode = MasterMode.Manual;
    public bool DefaultRespectBonds = true;
    public bool DefaultSetFollow = true;
    public bool DefaultFollowDrafted = true;
    public bool DefaultFollowFieldwork = true;
    public bool DefaultFollowTraining;
    public MasterMode DefaultTrainerMode = MasterMode.Manual;

    public HashSet<TrainableDef> EnabledTrainingTargets = [];

    private string[] _newCounts =
        Utilities_Livestock.AgeSexArray.Select(_ => "5").ToArray();

#pragma warning disable CS8618 // Set by ManagerSettings_Livestock/scribe
    public PawnKindSettings() { }
#pragma warning restore CS8618

    public PawnKindSettings(PawnKindDef pawnKindDef, PawnKindSettings copyFrom) : this()
    {
        _def = pawnKindDef;
        Array.Copy(copyFrom.DefaultCountTargets, DefaultCountTargets, DefaultCountTargets.Length);
        DefaultTryTameMore = copyFrom.DefaultTryTameMore;
        DefaultTamePastTargets = copyFrom.DefaultTamePastTargets;
        DefaultCullExcess = copyFrom.DefaultCullExcess;
        DefaultCullTrained = copyFrom.DefaultCullTrained;
        DefaultCullPregnant = copyFrom.DefaultCullPregnant;
        DefaultCullBonded = copyFrom.DefaultCullBonded;
        DefaultUnassignTraining = copyFrom.DefaultUnassignTraining;
        DefaultTrainYoung = copyFrom.DefaultTrainYoung;
        DefaultMasterMode = copyFrom.DefaultMasterMode;
        DefaultRespectBonds = copyFrom.DefaultRespectBonds;
        DefaultSetFollow = copyFrom.DefaultSetFollow;
        DefaultFollowDrafted = copyFrom.DefaultFollowDrafted;
        DefaultFollowFieldwork = copyFrom.DefaultFollowFieldwork;
        DefaultFollowTraining = copyFrom.DefaultFollowTraining;
        DefaultTrainerMode = copyFrom.DefaultTrainerMode;

        EnabledTrainingTargets = new(copyFrom.EnabledTrainingTargets);
    }

    public void DoSettingPanelContents(Rect panelRect)
    {

        Widgets_Section.BeginSectionColumn(panelRect, "Livestock.Settings", out Vector2 position, out float width);
        Widgets_Section.Section(ref position, width, DrawTargetCounts, "ColonyManagerRedux.Livestock.ManagerSettings.DefaultTargetCountsHeader".Translate());
        Widgets_Section.Section(ref position, width, DrawTamingSection, "ColonyManagerRedux.Livestock.ManagerSettings.DefaultTamingHeader".Translate());
        Widgets_Section.Section(ref position, width, DrawCullingSection, "ColonyManagerRedux.Livestock.ManagerSettings.DefaultCullingHeader".Translate());
        Widgets_Section.Section(ref position, width, DrawTrainingSection, "ColonyManagerRedux.Livestock.ManagerSettings.DefaultTrainingHeader".Translate());
        Widgets_Section.Section(ref position, width, DrawFollowSection, "ColonyManagerRedux.Livestock.ManagerSettings.DefaultFollowHeader".Translate());
        if (_def != null)
        {
            Widgets_Section.Section(ref position, width, DrawDeleteSection);
        }
        Widgets_Section.EndSectionColumn("Livestock.Settings", position);
    }

    private float DrawDeleteSection(Vector2 pos, float width)
    {
        var buttonRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.Livestock.ManagerSettings.DeleteOverrideFor".Translate(_def!.GetLabelPlural())))
        {
            settings.RemoveOverride(_def);
        }

        return ListEntryHeight;
    }

    private float DrawTargetCounts(Vector2 pos, float width)
    {
        // counts table
        const int cols = 3;
        const int rows = 3;

        var fifth = width / 5;
        float[] widths = [fifth, fifth * 2, fifth * 2];
        float[] heights = [ListEntryHeight * 2 / 3, ListEntryHeight, ListEntryHeight];

        // set up a 3x3 table of rects
#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
        var countRects = new Rect[rows, cols];
#pragma warning restore CA1814 // Prefer jagged arrays over multidimensional
        for (var x = 0; x < cols; x++)
        {
            for (var y = 0; y < rows; y++)
            {
                // kindof overkill for a 3x3 table, but ok.
                countRects[y, x] = new Rect(
                    pos.x + widths.Take(x).Sum(),
                    pos.y + heights.Take(y).Sum(),
                    widths[x],
                    heights[y]);
            }
        }

        // headers
        IlyvionWidgets.Label(
            countRects[0, 1], Gender.Female.ToString(), TextAnchor.LowerCenter, GameFont.Tiny);
        IlyvionWidgets.Label(
            countRects[0, 2], Gender.Male.ToString(), TextAnchor.LowerCenter, GameFont.Tiny);
        IlyvionWidgets.Label(
            countRects[1, 0],
            "ColonyManagerRedux.Livestock.Adult".Translate(),
            TextAnchor.MiddleRight,
            GameFont.Tiny);
        IlyvionWidgets.Label(
            countRects[2, 0],
            "ColonyManagerRedux.Livestock.Juvenile".Translate(),
            TextAnchor.MiddleRight,
            GameFont.Tiny);

        // fields
        DoCountField(countRects[1, 1], AgeAndSex.AdultFemale);
        DoCountField(countRects[1, 2], AgeAndSex.AdultMale);
        DoCountField(countRects[2, 1], AgeAndSex.JuvenileFemale);
        DoCountField(countRects[2, 2], AgeAndSex.JuvenileMale);

        return 3 * ListEntryHeight;
    }

    private void DoCountField(Rect rect, AgeAndSex ageSex)
    {
        int ageSexIndex = (int)ageSex;

        if (int.TryParse(_newCounts[ageSexIndex], out var value))
        {
            DefaultCountTargets[ageSexIndex] = value;
        }
        else
        {
            GUI.color = Color.red;
        }

        _newCounts[ageSexIndex] = Widgets.TextField(rect.ContractedBy(1f), _newCounts[ageSexIndex]);
        GUI.color = Color.white;
    }

    private float DrawTamingSection(Vector2 pos, float width)
    {
        var start = pos;
        Utilities.DrawToggle(ref pos, width,
            "ColonyManagerRedux.Livestock.TameMore".Translate(),
            "ColonyManagerRedux.Livestock.TameMore.Tip".Translate(),
            ref DefaultTryTameMore);
        Utilities.DrawToggle(ref pos, width,
            "ColonyManagerRedux.Livestock.TamePastTargets".Translate(),
            "ColonyManagerRedux.Livestock.TamePastTargets.Tip".Translate(),
            ref DefaultTamePastTargets);

        return pos.y - start.y;
    }

    private float DrawCullingSection(Vector2 pos, float width)
    {
        var start = pos;

        var cullingStrategies =
            (LivestockCullingStrategy[])Enum.GetValues(typeof(LivestockCullingStrategy));

        var cellWidth = width / (cullingStrategies.Length + 1);
        var cellRect = new Rect(pos.x, pos.y, cellWidth, ListEntryHeight);

        Utilities.DrawToggle(cellRect,
            "ColonyManagerRedux.Livestock.CullingStrategy.None".Translate(),
            "ColonyManagerRedux.Livestock.CullingStrategy.None.Tip".Translate(),
            !DefaultCullExcess,
            () => DefaultCullExcess = false,
            () => { });
        cellRect.x += cellWidth;

        foreach (var cullingStrategy in cullingStrategies)
        {
            Utilities.DrawToggle(
                cellRect,
                $"ColonyManagerRedux.Livestock.CullingStrategy.{cullingStrategy}".Translate(),
                $"ColonyManagerRedux.Livestock.CullingStrategy.{cullingStrategy}.Tip"
                    .Translate(),
                DefaultCullExcess && DefaultCullingStrategy == cullingStrategy,
                () => { DefaultCullExcess = true; DefaultCullingStrategy = cullingStrategy; },
                () => { });
            cellRect.x += cellWidth;
        }
        pos.y += ListEntryHeight;

        cellWidth = (width - Margin * 2) / 3f;
        var cullingOptionRect = new Rect(pos.x, pos.y, cellWidth, ListEntryHeight);

        Utilities.DrawToggle(cullingOptionRect,
            "ColonyManagerRedux.Livestock.CullTrained".Translate(),
            "ColonyManagerRedux.Livestock.CullTrained.Tip".Translate(),
            ref DefaultCullTrained, font: GameFont.Tiny, wrap: false);
        cullingOptionRect.x += cellWidth + Margin;

        Utilities.DrawToggle(cullingOptionRect,
            "ColonyManagerRedux.Livestock.CullPregnant".Translate(),
            "ColonyManagerRedux.Livestock.CullPregnant.Tip".Translate(),
            ref DefaultCullPregnant, font: GameFont.Tiny, wrap: false);
        cullingOptionRect.x += cellWidth + Margin;

        Utilities.DrawToggle(cullingOptionRect,
            "ColonyManagerRedux.Livestock.CullBonded".Translate(),
            "ColonyManagerRedux.Livestock.CullBonded.Tip".Translate(),
            ref DefaultCullBonded, font: GameFont.Tiny, wrap: false);

        pos.y += ListEntryHeight;

        return pos.y - start.y;
    }

    private float DrawTrainingSection(Vector2 pos, float width)
    {
        var allTrainingTargets = DefDatabase<TrainableDef>.AllDefsListForReading;
        int rowCount = (int)Math.Ceiling((double)allTrainingTargets.Count / ManagerTab_Livestock.TrainingJobsPerRow);
        var trainingRect = new Rect(pos.x, pos.y, width, ListEntryHeight * rowCount);
        DrawTrainingSelector(trainingRect, rowCount);
        var height = ListEntryHeight * rowCount;

        var unassignTrainingRect = new Rect(pos.x, pos.y + height, width, ListEntryHeight);
        Utilities.DrawToggle(unassignTrainingRect,
            "ColonyManagerRedux.Livestock.UnassignTraining".Translate(),
            "ColonyManagerRedux.Livestock.UnassignTraining.Tip".Translate(),
            ref DefaultUnassignTraining);
        height += ListEntryHeight;

        var trainYoungRect = new Rect(pos.x, pos.y + height, width, ListEntryHeight);
        Utilities.DrawToggle(trainYoungRect,
            "ColonyManagerRedux.Livestock.TrainYoung".Translate(),
            "ColonyManagerRedux.Livestock.TrainYoung.Tip".Translate(),
            ref DefaultTrainYoung);
        height += ListEntryHeight;

        return height;
    }

    public void DrawTrainingSelector(Rect rect, int rowCount)
    {
        var allTrainingTargets = DefDatabase<TrainableDef>.AllDefsListForReading;
        var cellCount = Math.Min(ManagerTab_Livestock.TrainingJobsPerRow, allTrainingTargets.Count);
        var cellWidth = (rect.width - Margin * (cellCount - 1)) / cellCount;

        GUI.BeginGroup(rect);
        for (var i = 0; i < allTrainingTargets.Count; i++)
        {
            var cell = new Rect((i % cellCount) * (cellWidth + Margin), (i / cellCount) * ListEntryHeight, cellWidth, rect.height / rowCount);
            bool visible = true;
            var report = _def != null
                ? ManagerJob_Livestock.CanBeTrained(_def, allTrainingTargets[i], out visible)
                : AcceptanceReport.WasAccepted;

            if (visible && report.Accepted)
            {
                Utilities.DrawToggle(cell, allTrainingTargets[i].LabelCap, allTrainingTargets[i].description,
                    EnabledTrainingTargets.Contains(allTrainingTargets[i]),
                    () => EnabledTrainingTargets.Add(allTrainingTargets[i]),
                    () => EnabledTrainingTargets.Remove(allTrainingTargets[i]));
            }
            else
            {
                EnabledTrainingTargets.Remove(allTrainingTargets[i]);
                if (visible)
                {
                    IlyvionWidgets.Label(
                        cell,
                        allTrainingTargets[i].LabelCap,
                        report.Reason, TextAnchor.MiddleLeft,
                        color: Color.grey,
                        leftMargin: Margin);
                }
            }
        }
        GUI.EndGroup();
    }

    private float DrawFollowSection(Vector2 pos, float width)
    {
        var start = pos;
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        var buttonRect = new Rect(
                rowRect.xMax * 3 / 4,
                0f,
                width * 1 / 4,
                ListEntryHeight * 2 / 3)
           .CenteredOnYIn(rowRect);

        // master selection
        var report = _def != null
            ? ManagerJob_Livestock.CanBeTrained(_def, TrainableDefOf.Obedience, out bool _)
            : AcceptanceReport.WasAccepted;

        if (report.Accepted)
        {
            IlyvionWidgets.Label(
                rowRect,
                "ColonyManagerRedux.Livestock.MasterDefault".Translate(),
                "ColonyManagerRedux.Livestock.MasterDefault.Tip".Translate(),
                TextAnchor.MiddleLeft, leftMargin: Margin);
        }
        else
        {
            IlyvionWidgets.Label(
                rowRect,
                "ColonyManagerRedux.Livestock.MasterDefault".Translate(),
                report.Reason,
                TextAnchor.MiddleLeft, leftMargin: Margin, color: Color.gray);
        }

        TaggedString label = report.Accepted
            ? $"ColonyManagerRedux.Livestock.MasterMode.{DefaultMasterMode}".Translate()
            : "ColonyManagerRedux.Livestock.MasterUnavailable".Translate();
        if (IlyvionWidgets.DisableableButtonText(
            buttonRect,
            label,
            enabled: report.Accepted))
        {
            var options = new List<FloatMenuOption>();

            // modes
            foreach (var mode in Utilities_Livestock.MasterModeArray.Where(mm => (mm & MasterMode.All) == mm))
            {
                options.Add(new FloatMenuOption($"ColonyManagerRedux.Livestock.MasterMode.{mode}".Translate(),
                    () => DefaultMasterMode = mode));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        // respect bonds?
        rowRect.y += ListEntryHeight;
        if (!report.Accepted)
        {
            DefaultRespectBonds = false;
            IlyvionWidgets.Label(rowRect,
                "ColonyManagerRedux.Livestock.RespectBonds".Translate(),
                report.Reason,
                color: Color.grey, leftMargin: Margin);
        }
        else
        {
            Utilities.DrawToggle(rowRect,
                "ColonyManagerRedux.Livestock.RespectBonds".Translate(),
                "ColonyManagerRedux.Livestock.RespectBonds.Tip".Translate(),
                ref DefaultRespectBonds);
        }

        // default follow
        rowRect.y += ListEntryHeight;
        if (report.Accepted)
        {
            Utilities.DrawToggle(rowRect,
                "ColonyManagerRedux.Livestock.Follow".Translate(),
                "ColonyManagerRedux.Livestock.Follow.Tip".Translate(),
                ref DefaultSetFollow);
        }
        else
        {
            DefaultSetFollow = false;
            IlyvionWidgets.Label(rowRect,
                "ColonyManagerRedux.Livestock.Follow".Translate(),
                report.Reason,
                color: Color.grey, leftMargin: Margin);
        }

        if (report.Accepted)
        {
            rowRect.y += ListEntryHeight;
            var followRect = rowRect;
            followRect.width /= 2f;
            Utilities.DrawToggle(followRect,
                "ColonyManagerRedux.Livestock.FollowDrafted".Translate(),
                "ColonyManagerRedux.Livestock.FollowDrafted.Tip".Translate(),
                ref DefaultFollowDrafted,
                font: GameFont.Tiny);
            followRect.x += followRect.width;
            Utilities.DrawToggle(followRect,
                "ColonyManagerRedux.Livestock.FollowFieldwork".Translate(),
                "ColonyManagerRedux.Livestock.FollowFieldwork.Tip".Translate(),
                ref DefaultFollowFieldwork,
                font: GameFont.Tiny);
        }
        else
        {
            DefaultFollowDrafted = false;
            DefaultFollowFieldwork = false;
        }

        // follow when training
        rowRect.y += ListEntryHeight;
        if (report.Accepted)
        {
            TooltipHandler.TipRegion(rowRect, "ColonyManagerRedux.Livestock.FollowTraining.Tip".Translate());
            Utilities.DrawToggle(rowRect,
                "ColonyManagerRedux.Livestock.FollowTraining".Translate(),
                "ColonyManagerRedux.Livestock.FollowTraining.Tip".Translate(),
                ref DefaultFollowTraining);
        }
        else
        {
            DefaultFollowTraining = false;
            IlyvionWidgets.Label(rowRect,
                "ColonyManagerRedux.Livestock.FollowTraining".Translate(),
                report.Reason,
                color: Color.grey, leftMargin: Margin);
        }

        // trainer selection
        rowRect.y += ListEntryHeight;
        if (report.Accepted)
        {
            IlyvionWidgets.Label(rowRect, "ColonyManagerRedux.Livestock.MasterTraining".Translate(),
                "ColonyManagerRedux.Livestock.MasterTraining.Tip".Translate(),
                TextAnchor.MiddleLeft, leftMargin: Margin);
        }
        else
        {
            IlyvionWidgets.Label(rowRect, "ColonyManagerRedux.Livestock.MasterTraining".Translate(),
                report.Reason,
                TextAnchor.MiddleLeft, color: Color.gray, leftMargin: Margin);
        }

        label = report.Accepted
            ? $"ColonyManagerRedux.Livestock.MasterMode.{DefaultTrainerMode}".Translate()
            : "ColonyManagerRedux.Livestock.MasterUnavailable".Translate();
        buttonRect = buttonRect.CenteredOnYIn(rowRect);
        if (IlyvionWidgets.DisableableButtonText(buttonRect, label, enabled: report.Accepted))
        {
            var options = new List<FloatMenuOption>();

            // modes
            foreach (var mode in Utilities_Livestock.MasterModeArray.Where(mm => (mm & MasterMode.Trainers) == mm))
            {
                options.Add(new FloatMenuOption($"ColonyManagerRedux.Livestock.MasterMode.{mode}".Translate(),
                    () => DefaultTrainerMode = mode));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        return rowRect.yMax - start.y;
    }

    public void ExposeData()
    {
        Scribe_Defs.Look(ref _def, "def");

        foreach (var ageAndSex in Utilities_Livestock.AgeSexArray)
        {
            Scribe_Values.Look(
                ref DefaultCountTargets[(int)ageAndSex],
                $"{ageAndSex.ToString().UncapitalizeFirst()}DefaultTargetCount", 5);
        }

        Scribe_Values.Look(ref DefaultTryTameMore, "defaultTryTameMore", false);
        Scribe_Values.Look(ref DefaultTamePastTargets, "defaultTamePastTargets", false);

        Scribe_Values.Look(ref DefaultCullExcess, "defaultButcherExcess", true);
        Scribe_Values.Look(ref DefaultCullTrained, "defaultButcherTrained", false);
        Scribe_Values.Look(ref DefaultCullPregnant, "defaultButcherPregnant", false);
        Scribe_Values.Look(ref DefaultCullBonded, "defaultButcherBonded", false);
        Scribe_Values.Look(
            ref DefaultCullingStrategy, "cullingStrategy", LivestockCullingStrategy.Butcher);

        Scribe_Collections.Look(ref EnabledTrainingTargets, "enabledTrainingTargets", LookMode.Def);
        Scribe_Values.Look(ref DefaultUnassignTraining, "defaultUnassignTraining", false);
        Scribe_Values.Look(ref DefaultTrainYoung, "defaultTrainYoung", false);

        Scribe_Values.Look(ref DefaultMasterMode, "defaultMasterMode", MasterMode.Manual);
        Scribe_Values.Look(ref DefaultRespectBonds, "defaultRespectBonds", true);
        Scribe_Values.Look(ref DefaultSetFollow, "defaultSetFollow", true);
        Scribe_Values.Look(ref DefaultFollowDrafted, "defaultFollowDrafted", true);
        Scribe_Values.Look(ref DefaultFollowFieldwork, "defaultFollowFieldwork", true);
        Scribe_Values.Look(ref DefaultFollowTraining, "defaultFollowTraining", false);
        Scribe_Values.Look(ref DefaultTrainerMode, "defaultTrainerMode", MasterMode.Manual);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            _newCounts = DefaultCountTargets.Select(v => v.ToString()).ToArray();

            EnabledTrainingTargets ??= [];
        }
    }
}

[HotSwappable]
internal sealed class ManagerSettings_Livestock : ManagerSettings
{
    private PawnKindSettings defaults = new();
    private Dictionary<PawnKindDef, PawnKindSettings> overrides = [];

    private List<PawnKindDef>? pawnKindDefs;
    private List<PawnKindDef> PawnKindDefs
    {
        get
        {
            pawnKindDefs ??= DefDatabase<PawnKindDef>.AllDefs
                .Where(p => p.RaceProps.Animal)
                .OrderBy(p => p.GetLabelPlural())
                .ToList();
            return pawnKindDefs;
        }
    }

    private int _currentLivestockSettingsTab = -1;
    private PawnKindSettings? currentOverrideTab;
    private List<TabRecord> _tmpTabRecords = [];
    public override void DoTabContents(Rect rect)
    {
        _tmpTabRecords.Add(
            new TabRecord("ColonyManagerRedux.Livestock.ManagerSettings.Default".Translate(), () =>
            {
                _currentLivestockSettingsTab = -1;
                currentOverrideTab = null;
            }, _currentLivestockSettingsTab == -1));
        _tmpTabRecords.AddRange(
            overrides.Select((s) => new TabRecord(s.Key.GetLabelPlural().CapitalizeFirst(), () =>
            {
                _currentLivestockSettingsTab = 0;
                currentOverrideTab = s.Value;
            }, _currentLivestockSettingsTab == 0 && currentOverrideTab == s.Value)));
        _tmpTabRecords.Add(
            new TabRecordWithTip("+", "ColonyManagerRedux.Livestock.ManagerSettings.AddOverride".Translate(), () =>
            {
                var options = new List<FloatMenuOption>();
                foreach (var pawnKindDef in PawnKindDefs)
                {
                    if (overrides.ContainsKey(pawnKindDef))
                    {
                        continue;
                    }

                    options.Add(new FloatMenuOption(pawnKindDef.GetLabelPlural().CapitalizeFirst(), () =>
                    {
                        PawnKindSettings @override = new(pawnKindDef, defaults)
                        {
                            settings = this
                        };
                        overrides.Add(pawnKindDef, @override);
                        _currentLivestockSettingsTab = 0;
                        currentOverrideTab = @override;
                    }));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }, false));
        using var _ = new DoOnDispose(_tmpTabRecords.Clear);

        int rowCount = (int)Math.Ceiling((double)_tmpTabRecords.Count / 5);
        rect.yMin += rowCount * SectionHeaderHeight + rowCount * Margin;
        rect = rect.ContractedBy(Margin);
        Widgets.DrawMenuSection(rect);
        TabDrawer.DrawTabs(rect, _tmpTabRecords, rowCount, null);

        var panelRect = new Rect(
            rect.xMin,
            rect.yMin,
            rect.width,
            rect.height - Margin);

        switch (_currentLivestockSettingsTab)
        {
            case -1:
                defaults.DoSettingPanelContents(panelRect);
                break;

            case 0:
                currentOverrideTab!.DoSettingPanelContents(panelRect);
                break;

            default:
                throw new Exception("Invalid _currentLivestockSettingsTab");
        }
    }
    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Deep.Look(ref defaults, "default");
        Scribe_Collections.Look(ref overrides, "overrides", LookMode.Def, LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            defaults.settings = this;
            foreach (var item in overrides.Values)
            {
                item.settings = this;
            }
        }
    }

    public PawnKindSettings GetSettingsFor(PawnKindDef pawnKind)
    {
        if (overrides.TryGetValue(pawnKind, out var settings))
        {
            return settings;
        }
        return defaults;
    }

    internal void RemoveOverride(PawnKindDef pawnKind)
    {
        _currentLivestockSettingsTab = -1;
        currentOverrideTab = null;
        overrides.Remove(pawnKind);
    }
}
