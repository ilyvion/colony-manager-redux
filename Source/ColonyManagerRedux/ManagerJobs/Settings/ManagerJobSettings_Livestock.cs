// ManagerJobSettings_Livestock.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[HotSwappable]
public class ManagerJobSettings_Livestock : ManagerJobSettings
{
    public int[] DefaultCountTargets = Utilities_Livestock.AgeSexArray
                .Select(_ => 5)
                .ToArray();

    public bool DefaultTryTameMore = false;

    public bool DefaultButcherExcess = true;
    public bool DefaultButcherTrained = false;
    public bool DefaultButcherPregnant = false;
    public bool DefaultButcherBonded = false;

    public bool DefaultUnassignTraining = false;
    public bool DefaultTrainYoung = false;

    public MasterMode DefaultMasterMode = MasterMode.Manual;
    public bool DefaultRespectBonds = true;
    public bool DefaultSetFollow = true;
    public bool DefaultFollowDrafted = true;
    public bool DefaultFollowFieldwork = true;
    public bool DefaultFollowTraining = false;
    public MasterMode DefaultTrainerMode = MasterMode.Manual;

    private string[] _newCounts =
        Utilities_Livestock.AgeSexArray.Select(_ => "5").ToArray();

    public override string Label => "ColonyManagerRedux.Livestock.Livestock".Translate();

    public HashSet<TrainableDef> EnabledTrainingTargets = [];

    public override void DoPanelContents(Rect rect)
    {
        var panelRect = new Rect(
            rect.xMin,
            rect.yMin,
            rect.width,
            rect.height - Margin);

        Widgets_Section.BeginSectionColumn(panelRect, "Livestock.Settings", out Vector2 position, out float width);
        Widgets_Section.Section(ref position, width, DrawTargetCounts, "ColonyManagerRedux.LivestockJobSettings.DefaultTargetCountsHeader".Translate());
        Widgets_Section.Section(ref position, width, DrawTamingSection, "ColonyManagerRedux.LivestockJobSettings.DefaultTamingHeader".Translate());
        Widgets_Section.Section(ref position, width, DrawButcherSection, "ColonyManagerRedux.LivestockJobSettings.DefaultButcherHeader".Translate());
        Widgets_Section.Section(ref position, width, DrawTrainingSection, "ColonyManagerRedux.LivestockJobSettings.DefaultTrainingHeader".Translate());
        Widgets_Section.Section(ref position, width, DrawFollowSection, "ColonyManagerRedux.LivestockJobSettings.DefaultFollowHeader".Translate());
        Widgets_Section.EndSectionColumn("Livestock.Settings", position);
    }

    private float DrawTargetCounts(Vector2 pos, float width)
    {
        // counts table
        var cols = 3;
        var rows = 3;
        var fifth = width / 5;
        float[] widths = [fifth, fifth * 2, fifth * 2];
        float[] heights = [ListEntryHeight * 2 / 3, ListEntryHeight, ListEntryHeight];

        // set up a 3x3 table of rects
        var countRects = new Rect[rows, cols];
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
        Widgets_Labels.Label(countRects[0, 1], Gender.Female.ToString(), TextAnchor.LowerCenter, GameFont.Tiny);
        Widgets_Labels.Label(countRects[0, 2], Gender.Male.ToString(), TextAnchor.LowerCenter, GameFont.Tiny);
        Widgets_Labels.Label(countRects[1, 0], "ColonyManagerRedux.Livestock.Adult".Translate(), TextAnchor.MiddleRight, GameFont.Tiny);
        Widgets_Labels.Label(countRects[2, 0], "ColonyManagerRedux.Livestock.Juvenile".Translate(), TextAnchor.MiddleRight, GameFont.Tiny);

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

        if (!_newCounts[ageSexIndex].IsInt())
        {
            GUI.color = Color.red;
        }
        else
        {
            DefaultCountTargets[ageSexIndex] = int.Parse(_newCounts[ageSexIndex]);
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

        return pos.y - start.y;
    }

    private float DrawButcherSection(Vector2 pos, float width)
    {
        var start = pos;

        // butchery stuff
        var butcherExcessRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(butcherExcessRect,
            "ColonyManagerRedux.Livestock.ButcherExcess".Translate(),
            "ColonyManagerRedux.Livestock.ButcherExcess.Tip".Translate(),
            ref DefaultButcherExcess);
        pos.y += ListEntryHeight;

        var cellWidth = (width - Margin * 2) / 3f;
        var butcherOptionRect = new Rect(pos.x, pos.y, cellWidth, ListEntryHeight);

        Utilities.DrawToggle(butcherOptionRect,
            "ColonyManagerRedux.Livestock.ButcherTrained".Translate(),
            "ColonyManagerRedux.Livestock.ButcherTrained.Tip".Translate(),
            ref DefaultButcherTrained, font: GameFont.Tiny, wrap: false);
        butcherOptionRect.x += cellWidth + Margin;

        Utilities.DrawToggle(butcherOptionRect,
            "ColonyManagerRedux.Livestock.ButcherPregnant".Translate(),
            "ColonyManagerRedux.Livestock.ButcherPregnant.Tip".Translate(),
            ref DefaultButcherPregnant, font: GameFont.Tiny, wrap: false);
        butcherOptionRect.x += cellWidth + Margin;

        Utilities.DrawToggle(butcherOptionRect,
            "ColonyManagerRedux.Livestock.ButcherBonded".Translate(),
            "ColonyManagerRedux.Livestock.ButcherBonded.Tip".Translate(),
            ref DefaultButcherBonded, font: GameFont.Tiny, wrap: false);

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

            Utilities.DrawToggle(cell, allTrainingTargets[i].LabelCap, allTrainingTargets[i].description,
                EnabledTrainingTargets.Contains(allTrainingTargets[i]),
                () => EnabledTrainingTargets.Add(allTrainingTargets[i]),
                () => EnabledTrainingTargets.Remove(allTrainingTargets[i]));
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
        Widgets_Labels.Label(
            rowRect,
            "ColonyManagerRedux.ManagerLivestock.MasterDefault".Translate(),
            "ColonyManagerRedux.ManagerLivestock.MasterDefault.Tip".Translate(),
            TextAnchor.MiddleLeft, margin: Margin);
        if (Widgets.ButtonText(buttonRect, $"ColonyManagerRedux.ManagerLivestock.MasterMode.{DefaultMasterMode}".Translate()))
        {
            var options = new List<FloatMenuOption>();

            // modes
            foreach (var mode in Utilities_Livestock.MasterModeArray.Where(mm => (mm & MasterMode.All) == mm))
            {
                options.Add(new FloatMenuOption($"ColonyManagerRedux.ManagerLivestock.MasterMode.{mode}".Translate(),
                    () => DefaultMasterMode = mode));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        // respect bonds?
        rowRect.y += ListEntryHeight;
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.ManagerLivestock.RespectBonds".Translate(),
            "ColonyManagerRedux.ManagerLivestock.RespectBonds.Tip".Translate(),
            ref DefaultRespectBonds);

        // default follow
        rowRect.y += ListEntryHeight;
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.ManagerLivestock.Follow".Translate(),
            "ColonyManagerRedux.ManagerLivestock.Follow.Tip".Translate(),
            ref DefaultSetFollow);


        rowRect.y += ListEntryHeight;
        var followRect = rowRect;
        followRect.width /= 2f;
        Utilities.DrawToggle(followRect,
            "ColonyManagerRedux.ManagerLivestock.FollowDrafted".Translate(),
            "ColonyManagerRedux.ManagerLivestock.FollowDrafted.Tip".Translate(),
            ref DefaultFollowDrafted,
            font: GameFont.Tiny);
        followRect.x += followRect.width;
        Utilities.DrawToggle(followRect,
            "ColonyManagerRedux.ManagerLivestock.FollowFieldwork".Translate(),
            "ColonyManagerRedux.ManagerLivestock.FollowFieldwork.Tip".Translate(),
            ref DefaultFollowFieldwork,
            font: GameFont.Tiny);

        // follow when training
        rowRect.y += ListEntryHeight;
        TooltipHandler.TipRegion(rowRect, "ColonyManagerRedux.ManagerLivestock.FollowTraining.Tip".Translate());
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.ManagerLivestock.FollowTraining".Translate(),
            "ColonyManagerRedux.ManagerLivestock.FollowTraining.Tip".Translate(),
            ref DefaultFollowTraining);

        // trainer selection
        rowRect.y += ListEntryHeight;
        Widgets_Labels.Label(rowRect, "ColonyManagerRedux.ManagerLivestock.MasterTraining".Translate(),
            "ColonyManagerRedux.ManagerLivestock.MasterTraining.Tip".Translate(),
            TextAnchor.MiddleLeft, margin: Margin);

        buttonRect = buttonRect.CenteredOnYIn(rowRect);
        if (Widgets.ButtonText(buttonRect, $"ColonyManagerRedux.ManagerLivestock.MasterMode.{DefaultTrainerMode}".Translate()))
        {
            var options = new List<FloatMenuOption>();

            // modes
            foreach (var mode in Utilities_Livestock.MasterModeArray.Where(mm => (mm & MasterMode.Trainers) == mm))
            {
                options.Add(new FloatMenuOption($"ColonyManagerRedux.ManagerLivestock.MasterMode.{mode}".Translate(),
                    () => DefaultTrainerMode = mode));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        return rowRect.yMax - start.y;
    }

    public override void ExposeData()
    {
        base.ExposeData();

        foreach (var ageAndSex in Utilities_Livestock.AgeSexArray)
        {
            Scribe_Values.Look(
                ref DefaultCountTargets[(int)ageAndSex],
                $"{ageAndSex.ToString().UncapitalizeFirst()}DefaultTargetCount", 5);
        }

        Scribe_Values.Look(ref DefaultTryTameMore, "defaultTryTameMore", false);

        Scribe_Values.Look(ref DefaultButcherExcess, "defaultButcherExcess", true);
        Scribe_Values.Look(ref DefaultButcherTrained, "defaultButcherTrained", false);
        Scribe_Values.Look(ref DefaultButcherPregnant, "defaultButcherPregnant", false);
        Scribe_Values.Look(ref DefaultButcherBonded, "defaultButcherBonded", false);

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
