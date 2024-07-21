// ManagerTab_Livestock.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using Verse.Noise;
using static ColonyManagerRedux.Constants;
using static ColonyManagerRedux.Utilities;
using static ColonyManagerRedux.Widgets_Labels;

namespace ColonyManagerRedux;

[HotSwappable]
public class ManagerTab_Livestock(Manager manager) : ManagerTab(manager)
{
    internal const int TrainingJobsPerRow = 3;

    private List<PawnKindDef> _availablePawnKinds = [];

    private string[] _newCounts = ["", "", "", ""];

    private bool _onCurrentTab;
    private Vector2 _scrollPosition = Vector2.zero;
    private PawnKindDef? _selectedAvailable;

    public override string Label => "ColonyManagerRedux.Livestock.Livestock".Translate();

    protected override bool CreateNewSelectedJobOnMake => false;

    public ManagerJob_Livestock? SelectedCurrentLivestockJob
    {
        get => (ManagerJob_Livestock?)Selected;
    }

    protected override void PostSelect()
    {
        _onCurrentTab = Selected != null;
        _selectedAvailable = null;
        if (Selected != null)
        {
            _newCounts =
                SelectedCurrentLivestockJob!.Trigger.CountTargets.Select(v => v.ToString()).ToArray();
        }
    }



    public override void DoWindowContents(Rect canvas)
    {
        var leftRow = new Rect(0f, 31f, DefaultLeftRowSize, canvas.height - 31f);
        var contentCanvas = new Rect(leftRow.xMax + Margin, 0f,
            canvas.width - leftRow.width - Margin, canvas.height);

        DoLeftRow(leftRow);
        DoContent(contentCanvas);
    }

    public override void DrawListEntry(ManagerJob job, Rect rect, ListEntryDrawMode mode, bool active = true)
    {
        // (detailButton) | name | (bar | last update)/(stamp) -> handled in Utilities.DrawStatusForListEntry

        var livestockJob = (ManagerJob_Livestock)job;

        // set up rects
        Rect labelRect = new(
            Margin, Margin, rect.width - (active ? StatusRectWidth + 4 * Margin : 2 * Margin),
            rect.height - 2 * Margin),
        statusRect = new(labelRect.xMax + Margin, Margin, StatusRectWidth,
            rect.height - 2 * Margin);

        // do the drawing
        GUI.BeginGroup(rect);

        // draw label
        Widgets.Label(labelRect, livestockJob.FullLabel);
        TooltipHandler.TipRegion(labelRect, () => livestockJob.Trigger.StatusTooltip, GetHashCode());

        // if the bill has a manager job, give some more info.
        if (active)
        {
            livestockJob.DrawStatusForListEntry(statusRect, livestockJob.Trigger, mode == ListEntryDrawMode.Export);
        }

        GUI.EndGroup();
    }

    public void DrawTrainingSelector(ManagerJob_Livestock job, Rect rect, int rowCount)
    {
        var cellCount = Math.Min(TrainingJobsPerRow, job.Training.Count);
        var cellWidth = (rect.width - Margin * (cellCount - 1)) / cellCount;
        var keys = job.Training.Defs;

        GUI.BeginGroup(rect);
        for (var i = 0; i < job.Training.Count; i++)
        {
            var cell = new Rect(i % cellCount * (cellWidth + Margin), i / cellCount * ListEntryHeight, cellWidth, rect.height / rowCount);
            var report = job.CanBeTrained(job.Trigger.pawnKind, keys[i], out bool visible);
            if (visible && report.Accepted)
            {
                var checkOn = job.Training[keys[i]];
                DrawToggle(cell, keys[i].LabelCap, keys[i].description, ref checkOn, size: SmallIconSize,
                            font: GameFont.Tiny, wrap: false);
                job.Training[keys[i]] = checkOn;
            }
            else if (visible)
            {
                Label(cell, keys[i].LabelCap, report.Reason, TextAnchor.MiddleCenter, GameFont.Tiny, Color.grey);
            }
        }

        GUI.EndGroup();
    }

    public string GetMasterLabel(ManagerJob_Livestock job)
    {
        var report = job.CanBeTrained(job.Trigger.pawnKind, TrainableDefOf.Obedience, out bool _);
        if (!report.Accepted)
        {
            return "ColonyManagerRedux.ManagerLivestock.MasterUnavailable".Translate();
        }
        return job.Masters switch
        {
            MasterMode.Specific => job.Master?.LabelShort ?? "ColonyManagerRedux.ManagerNone".Translate(),
            _ => (string)$"ColonyManagerRedux.ManagerLivestock.MasterMode.{job.Masters}".Translate(),
        };
    }

    public string GetTrainerLabel(ManagerJob_Livestock job)
    {
        return job.Trainers switch
        {
            MasterMode.Specific => job.Trainer?.LabelShort ?? "BUG: INVALID",
            _ => (string)$"ColonyManagerRedux.ManagerLivestock.MasterMode.{job.Trainers}".Translate(),
        };
    }

    public override void PreOpen()
    {
        Refresh();
    }

    private void DoContent(Rect rect)
    {
        // background
        Widgets.DrawMenuSection(rect);

        // cop out if nothing is selected.
        if (SelectedCurrentLivestockJob == null)
        {
            Label(rect, "ColonyManagerRedux.ManagerLivestock.SelectPawnKind".Translate(), TextAnchor.MiddleCenter);
            return;
        }

        // rects
        var optionsColumnRect = new Rect(
            rect.xMin,
            rect.yMin,
            rect.width * 3 / 5f,
            rect.height - Margin - ButtonSize.y);
        var animalsColumnRect = new Rect(
            optionsColumnRect.xMax,
            rect.yMin,
            rect.width * 2 / 5f,
            rect.height - Margin - ButtonSize.y);
        var buttonRect = new Rect(
            rect.xMax - ButtonSize.x,
            rect.yMax - ButtonSize.y,
            ButtonSize.x - Margin,
            ButtonSize.y - Margin);

        Widgets_Section.BeginSectionColumn(optionsColumnRect, "Livestock.Options", out Vector2 position, out float width);

        Widgets_Section.Section(SelectedCurrentLivestockJob, ref position, width, DrawTargetCountsSection,
            "ColonyManagerRedux.ManagerLivestock.TargetCountsHeader".Translate());
        Widgets_Section.Section(SelectedCurrentLivestockJob, ref position, width, DrawTamingSection,
            "ColonyManagerRedux.ManagerLivestock.TamingHeader".Translate());
        Widgets_Section.Section(SelectedCurrentLivestockJob, ref position, width, DrawButcherSection,
            "ColonyManagerRedux.ManagerLivestock.ButcherHeader".Translate());
        Widgets_Section.Section(SelectedCurrentLivestockJob, ref position, width, DrawTrainingSection,
            "ColonyManagerRedux.ManagerLivestock.TrainingHeader".Translate());
        Widgets_Section.Section(SelectedCurrentLivestockJob, ref position, width, DrawAreaRestrictionsSection,
            "ColonyManagerRedux.ManagerLivestock.AreaRestrictionsHeader".Translate());
        Widgets_Section.Section(SelectedCurrentLivestockJob, ref position, width, DrawFollowSection,
            "ColonyManagerRedux.ManagerLivestock.FollowHeader".Translate());

        Widgets_Section.EndSectionColumn("Livestock.Options", position);

        // Start animals list
        // get our pawnkind
        Widgets_Section.BeginSectionColumn(animalsColumnRect, "Livestock.Animals", out position, out width);

        Widgets_Section.Section(SelectedCurrentLivestockJob, ref position, width, DrawTamedAnimalSection,
            "ColonyManagerRedux.ManagerLivestock.AnimalsHeader"
            .Translate(
                "ColonyManagerRedux.Livestock.Tame".Translate(),
                SelectedCurrentLivestockJob.Trigger.pawnKind.GetLabelPlural())
            .CapitalizeFirst());
        Widgets_Section.Section(SelectedCurrentLivestockJob, ref position, width, DrawWildAnimalSection,
            "ColonyManagerRedux.ManagerLivestock.AnimalsHeader"
            .Translate(
                "ColonyManagerRedux.Livestock.Wild".Translate(),
                SelectedCurrentLivestockJob.Trigger.pawnKind.GetLabelPlural())
            .CapitalizeFirst());

        Widgets_Section.EndSectionColumn("Livestock.Animals", position);

        // add / remove to the stack
        if (SelectedCurrentLivestockJob.IsManaged)
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.ManagerDelete".Translate()))
            {
                SelectedCurrentLivestockJob.Delete();
                Selected = null;
                _onCurrentTab = false;
                Refresh();
                return; // just skip to the next tick to avoid null reference errors.
            }

            TooltipHandler.TipRegion(buttonRect, "ColonyManagerRedux.Thresholds.DeleteBillTooltip".Translate());
        }
        else
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.ManagerManage".Translate()))
            {
                SelectedCurrentLivestockJob.IsManaged = true;
                _onCurrentTab = true;
                manager.JobStack.Add(SelectedCurrentLivestockJob);
                Refresh();
            }

            TooltipHandler.TipRegion(buttonRect, "ColonyManagerRedux.Thresholds.ManageBillTooltip".Translate());
        }
    }

    private void DoCountField(ManagerJob_Livestock job, Rect rect, AgeAndSex ageSex)
    {
        int ageSexIndex = (int)ageSex;

        if (!_newCounts[ageSexIndex].IsInt())
        {
            GUI.color = Color.red;
        }
        else
        {
            job.Trigger.CountTargets[ageSexIndex] = int.Parse(_newCounts[ageSexIndex]);
        }

        _newCounts[ageSexIndex] = Widgets.TextField(rect.ContractedBy(1f), _newCounts[ageSexIndex]);
        GUI.color = Color.white;
    }

    private void DoLeftRow(Rect rect)
    {
        // background (minus top line so we can draw tabs.)
        Widgets.DrawMenuSection(rect);

        // tabs
        var tabs = new List<TabRecord>();
        var availableTabRecord = new TabRecord("ColonyManagerRedux.Thresholds.Available".Translate(), delegate
        {
            _onCurrentTab = false;
            Refresh();
        }, !_onCurrentTab);
        tabs.Add(availableTabRecord);
        var currentTabRecord = new TabRecord("ColonyManagerRedux.Thresholds.Current".Translate(), delegate
        {
            _onCurrentTab = true;
            Refresh();
        }, _onCurrentTab);
        tabs.Add(currentTabRecord);

        TabDrawer.DrawTabs(rect, tabs);

        var outRect = rect;
        var viewRect = outRect.AtZero();

        if (_onCurrentTab)
        {
            DrawCurrentJobList(outRect, viewRect);
        }
        else
        {
            DrawAvailableJobList(outRect, viewRect);
        }
    }

    private void DrawAnimalListheader(ref Vector2 pos, Vector2 size, PawnKindDef pawnKind)
    {
        var start = pos;

        // use a third of available screenspace for labels
        pos.x += size.x / 3f;

        // gender, lifestage, current meat (and if applicable, milking + shearing)
        var cols = 3;

        // extra columns?
        var milk = pawnKind.Milkable();
        var wool = pawnKind.Shearable();
        if (milk)
        {
            cols++;
        }

        if (wool)
        {
            cols++;
        }

        var colwidth = size.x * 2 / 3 / cols;

        // gender header
        var genderRect = new Rect(pos.x, pos.y, colwidth, size.y);
        var genderMale =
            new Rect(0f, 0f, SmallIconSize, SmallIconSize).CenteredIn(genderRect, -SmallIconSize / 2);
        var genderFemale =
            new Rect(0f, 0f, SmallIconSize, SmallIconSize).CenteredIn(genderRect, SmallIconSize / 2);
        GUI.DrawTexture(genderMale, Resources.MaleIcon);
        GUI.DrawTexture(genderFemale, Resources.FemaleIcon);
        TooltipHandler.TipRegion(genderRect, "ColonyManagerRedux.Livestock.GenderHeader".Translate());
        pos.x += colwidth;

        // lifestage header
        var ageRect = new Rect(pos.x, pos.y, colwidth, size.y);
        var ageRectC = new Rect(0f, 0f, SmallIconSize, SmallIconSize).CenteredIn(ageRect, SmallIconSize / 2);
        var ageRectB = new Rect(0f, 0f, SmallIconSize, SmallIconSize).CenteredIn(ageRect);
        var ageRectA = new Rect(0f, 0f, SmallIconSize, SmallIconSize).CenteredIn(ageRect, -SmallIconSize / 2);
        GUI.DrawTexture(ageRectC, Resources.LifeStages(2));
        GUI.DrawTexture(ageRectB, Resources.LifeStages(1));
        GUI.DrawTexture(ageRectA, Resources.LifeStages(0));
        TooltipHandler.TipRegion(ageRect, "ColonyManagerRedux.Livestock.AgeHeader".Translate());
        pos.x += colwidth;

        // meat header
        var meatRect = new Rect(pos.x, pos.y, colwidth, size.y);
        var meatIconRect =
            new Rect(0f, 0f, SmallIconSize, SmallIconSize).CenteredIn(meatRect);
        GUI.DrawTexture(meatIconRect, Resources.MeatIcon);
        TooltipHandler.TipRegion(meatRect, "ColonyManagerRedux.Livestock.MeatHeader".Translate());
        pos.x += colwidth;

        // milk header
        if (milk)
        {
            var milkRect = new Rect(pos.x, pos.y, colwidth, size.y);
            var milkIconRect =
                new Rect(0f, 0f, SmallIconSize, SmallIconSize).CenteredIn(milkRect);
            GUI.DrawTexture(milkIconRect, Resources.MilkIcon);
            TooltipHandler.TipRegion(milkRect, "ColonyManagerRedux.Livestock.MilkHeader".Translate());
            pos.x += colwidth;
        }

        // wool header
        if (wool)
        {
            var woolRect = new Rect(pos.x, pos.y, colwidth, size.y);
            var woolIconRect =
                new Rect(0f, 0f, MediumIconSize, MediumIconSize).CenteredIn(woolRect);
            GUI.DrawTexture(woolIconRect, Resources.WoolIcon);
            TooltipHandler.TipRegion(woolRect, "ColonyManagerRedux.Livestock.WoolHeader".Translate());
            pos.x += colwidth;
        }

        // start next row
        pos.x = start.x;
        pos.y += size.y;
    }

    private void DrawAnimalRow(ref Vector2 pos, Vector2 size, Pawn p)
    {
        var start = pos;

        // highlights and interactivity.
        var row = new Rect(pos.x, pos.y, size.x, size.y);
        Widgets.DrawHighlightIfMouseover(row);
        if (Widgets.ButtonInvisible(row))
        {
            // move camera and select
            Find.MainTabsRoot.EscapeCurrentTab();
            CameraJumper.TryJump(p.PositionHeld, p.Map);
            Find.Selector.ClearSelection();
            if (p.Spawned)
            {
                Find.Selector.Select(p);
            }
        }

        // use a third of available screenspace for labels
        var nameRect = new Rect(pos.x, pos.y, size.x / 3f, size.y);
        Label(nameRect, p.LabelCap, TextAnchor.MiddleCenter, GameFont.Tiny);
        pos.x += size.x / 3f;

        // gender, lifestage, current meat (and if applicable, milking + shearing)
        var cols = 3;

        // extra columns?
        if (p.kindDef.Milkable())
        {
            cols++;
        }

        if (p.kindDef.Shearable())
        {
            cols++;
        }

        var colwidth = size.x * 2 / 3 / cols;

        // gender column
        var genderRect = new Rect(pos.x, pos.y, colwidth, size.y);
        var genderIconRect =
            new Rect(0f, 0f, SmallIconSize, SmallIconSize).CenteredIn(genderRect);
        switch (p.gender)
        {
            case Gender.Female:
                GUI.DrawTexture(genderIconRect, Resources.FemaleIcon);
                break;

            case Gender.Male:
                GUI.DrawTexture(genderIconRect, Resources.MaleIcon);
                break;

            case Gender.None:
                GUI.DrawTexture(genderIconRect, Resources.UnkownIcon);
                break;
        }

        TooltipHandler.TipRegion(genderRect, p.gender.GetLabel());
        pos.x += colwidth;

        // lifestage column
        var ageRect = new Rect(pos.x, pos.y, colwidth, size.y);
        var ageIconRect = new Rect(0f, 0f, SmallIconSize, SmallIconSize).CenteredIn(ageRect);
        GUI.DrawTexture(ageIconRect, Resources.LifeStages(p.ageTracker.CurLifeStageIndex));
        TooltipHandler.TipRegion(ageRect, p.ageTracker.AgeTooltipString);
        pos.x += colwidth;

        // meat column
        var meatRect = new Rect(pos.x, pos.y, colwidth, size.y);
        // NOTE: When splitting tabs into separate mods; estimated meat count is defined in the Hunting helper.
        Label(meatRect, p.EstimatedMeatCount().ToString(),
            "ColonyManagerRedux.Livestock.Yields".Translate(p.RaceProps.meatDef.LabelCap, p.EstimatedMeatCount()),
            TextAnchor.MiddleCenter, GameFont.Tiny);
        pos.x += colwidth;

        // milk column
        if (p.Milkable())
        {
            var milkRect = new Rect(pos.x, pos.y, colwidth, size.y);
            var comp = p.TryGetComp<CompMilkable>();
            Label(milkRect, comp.Fullness.ToString("0%"),
                "ColonyManagerRedux.Livestock.Yields".Translate(comp.Props.milkDef.LabelCap, comp.Props.milkAmount),
                TextAnchor.MiddleCenter, GameFont.Tiny);
        }

        if (p.kindDef.Milkable())
        {
            pos.x += colwidth;
        }

        // wool column
        if (p.Shearable())
        {
            var woolRect = new Rect(pos.x, pos.y, colwidth, size.y);
            var comp = p.TryGetComp<CompShearable>();
            Label(woolRect, comp.Fullness.ToString("0%"),
                "ColonyManagerRedux.Livestock.Yields".Translate(comp.Props.woolDef.LabelCap, comp.Props.woolAmount),
                TextAnchor.MiddleCenter, GameFont.Tiny);
        }

        if (p.kindDef.Milkable())
        {
            pos.x += colwidth;
        }

        // do the carriage return on ref pos
        pos.x = start.x;
        pos.y += size.y;
    }

    private float DrawAnimalSection(ref Vector2 pos, float width, string type, PawnKindDef pawnKind,
                                     IEnumerable<Pawn> animals)
    {
        if (animals == null)
        {
            return 0;
        }

        var start = pos;
        DrawAnimalListheader(ref pos, new Vector2(width, ListEntryHeight / 3 * 2), pawnKind);

        if (!animals.Any())
        {
            Label(new Rect(pos.x, pos.y, width, ListEntryHeight),
                "ColonyManagerRedux.Livestock.NoAnimals".Translate(type, pawnKind.GetLabelPlural()),
                TextAnchor.MiddleCenter, color: Color.grey);
            pos.y += ListEntryHeight;
        }

        foreach (var animal in animals)
        {
            DrawAnimalRow(ref pos, new Vector2(width, ListEntryHeight), animal);
        }

        return pos.y - start.y;
    }

    private float DrawAreaRestrictionsSection(ManagerJob_Livestock job, Vector2 pos, float width)
    {
        var start = pos;

        // skip for animals that can't be restricted
        if (job.Trigger.pawnKind.RaceProps.Roamer)
        {
            var unavailableLabelRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
            unavailableLabelRect.xMin += Margin;
            Label(unavailableLabelRect,
                "ColonyManagerRedux.ManagerLivestock.DisabledBecauseRoamingAnimal".Translate(job.Trigger.pawnKind.GetLabelPlural()),
                "ColonyManagerRedux.ManagerLivestock.DisabledBecauseRoamingAnimalTip".Translate(job.Trigger.pawnKind.GetLabelPlural()),
                TextAnchor.MiddleLeft,
                color: Color.grey);
            return ListEntryHeight;
        }


        // restrict to area
        var restrictAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);

        DrawToggle(restrictAreaRect,
            "ColonyManagerRedux.Livestock.RestrictToArea".Translate(),
            "ColonyManagerRedux.Livestock.RestrictToArea.Tip".Translate(),
            ref job.RestrictToArea);
        pos.y += ListEntryHeight;

        if (job.RestrictToArea)
        {
            // area selectors table
            // set up a 3x3 table of rects
            var cols = 3;
            var fifth = width / 5;
            float[] widths = [fifth, fifth * 2, fifth * 2];
            float[] heights = [ListEntryHeight * 2 / 3, ListEntryHeight, ListEntryHeight];

            var areaRects = new Rect[cols, cols];
            for (var x = 0; x < cols; x++)
            {
                for (var y = 0; y < cols; y++)
                {
                    areaRects[x, y] = new Rect(
                        widths.Take(x).Sum(),
                        pos.y + heights.Take(y).Sum(),
                        widths[x],
                        heights[y]);
                }
            }

            // headers
            Label(areaRects[1, 0], Gender.Female.ToString(), TextAnchor.LowerCenter, GameFont.Tiny);
            Label(areaRects[2, 0], Gender.Male.ToString(), TextAnchor.LowerCenter, GameFont.Tiny);
            Label(areaRects[0, 1], "ColonyManagerRedux.Livestock.Adult".Translate(), TextAnchor.MiddleRight, GameFont.Tiny);
            Label(areaRects[0, 2], "ColonyManagerRedux.Livestock.Juvenile".Translate(), TextAnchor.MiddleRight, GameFont.Tiny);

            // do the selectors
            job.RestrictArea[0] = AreaAllowedGUI.DoAllowedAreaSelectors(
                areaRects[1, 1],
                job.RestrictArea[0], manager, Margin);
            job.RestrictArea[1] = AreaAllowedGUI.DoAllowedAreaSelectors(
                areaRects[2, 1],
                job.RestrictArea[1], manager, Margin);
            job.RestrictArea[2] = AreaAllowedGUI.DoAllowedAreaSelectors(
                areaRects[1, 2],
                job.RestrictArea[2], manager, Margin);
            job.RestrictArea[3] = AreaAllowedGUI.DoAllowedAreaSelectors(
                areaRects[2, 2],
                job.RestrictArea[3], manager, Margin);

            Text.Anchor = TextAnchor.UpperLeft; // DoAllowedAreaMode leaves the anchor in an incorrect state.
            pos.y += 3 * ListEntryHeight;
        }

        var sendToSlaughterAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        pos.y += ListEntryHeight;
        if (job.ButcherExcess)
        {
            DrawToggle(sendToSlaughterAreaRect,
                "ColonyManagerRedux.Livestock.SendToSlaughterArea".Translate(),
                "ColonyManagerRedux.Livestock.SendToSlaughterArea.Tip".Translate(),
                ref job.SendToSlaughterArea);

            if (job.SendToSlaughterArea)
            {
                var slaughterAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
                AreaAllowedGUI.DoAllowedAreaSelectors(slaughterAreaRect, ref job.SlaughterArea,
                    manager);
                pos.y += ListEntryHeight;
            }
        }
        else
        {
            sendToSlaughterAreaRect.xMin += Margin;
            Label(sendToSlaughterAreaRect,
                "ColonyManagerRedux.Livestock.SendToSlaughterArea".Translate(),
                "ColonyManagerRedux.ManagerLivestock.DisabledBecauseSlaughterExcessDisabled".Translate(),
                TextAnchor.MiddleLeft,
                color: Color.grey);
        }

        if (job.Trigger.pawnKind.Milkable())
        {
            var sendToMilkingAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
            pos.y += ListEntryHeight;
            DrawToggle(sendToMilkingAreaRect,
                "ColonyManagerRedux.Livestock.SendToMilkingArea".Translate(),
                "ColonyManagerRedux.Livestock.SendToMilkingArea.Tip".Translate(),
                ref job.SendToMilkingArea);

            if (job.SendToMilkingArea)
            {
                var milkingAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
                AreaAllowedGUI.DoAllowedAreaSelectors(milkingAreaRect, ref job.MilkArea,
                    manager);
                pos.y += ListEntryHeight;
            }
        }

        if (job.Trigger.pawnKind.Shearable())
        {
            var sendToShearingAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
            pos.y += ListEntryHeight;
            DrawToggle(sendToShearingAreaRect,
                "ColonyManagerRedux.Livestock.SendToShearingArea".Translate(),
                "ColonyManagerRedux.Livestock.SendToShearingArea.Tip".Translate(),
                ref job.SendToShearingArea);

            if (job.SendToShearingArea)
            {
                var shearingAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
                AreaAllowedGUI.DoAllowedAreaSelectors(shearingAreaRect, ref job.ShearArea,
                    manager);
                pos.y += ListEntryHeight;
            }
        }

        var sendToTrainingAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        pos.y += ListEntryHeight;
        if (job.Training.Any)
        {
            DrawToggle(sendToTrainingAreaRect,
                "ColonyManagerRedux.Livestock.SendToTrainingArea".Translate(),
                "ColonyManagerRedux.Livestock.SendToTrainingArea.Tip".Translate(),
                ref job.SendToTrainingArea);

            if (job.SendToTrainingArea)
            {
                var trainingAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
                AreaAllowedGUI.DoAllowedAreaSelectors(trainingAreaRect, ref job.TrainingArea,
                    manager);
                pos.y += ListEntryHeight;
            }
        }
        else
        {
            sendToTrainingAreaRect.xMin += Margin;
            Label(sendToTrainingAreaRect,
                "ColonyManagerRedux.Livestock.SendToTrainingArea".Translate(),
                "ColonyManagerRedux.ManagerLivestock.DisabledBecauseNoTrainingSet".Translate(),
                TextAnchor.MiddleLeft,
                color: Color.grey);
        }

        return pos.y - start.y;
    }

    private void DrawAvailableJobList(Rect outRect, Rect viewRect)
    {
        // set sizes
        viewRect.height = _availablePawnKinds.Count * LargeListEntryHeight;
        if (viewRect.height > outRect.height)
        {
            viewRect.width -= ScrollbarWidth;
        }

        Widgets.BeginScrollView(outRect, ref _scrollPosition, viewRect);
        GUI.BeginGroup(viewRect);

        for (var i = 0; i < _availablePawnKinds.Count; i++)
        {
            PawnKindDef animalDef = _availablePawnKinds[i];

            // set up rect
            var row = new Rect(0f, LargeListEntryHeight * i, viewRect.width, LargeListEntryHeight);

            // highlights
            Widgets.DrawHighlightIfMouseover(row);
            if (i % 2 == 0)
            {
                Widgets.DrawAltRect(row);
            }

            if (animalDef == _selectedAvailable)
            {
                Widgets.DrawHighlightSelected(row);
            }

            // draw trainability icon
            var iconRect = new Rect(row)
            {
                width = SmallIconSize,
                height = SmallIconSize
            };
            iconRect.y += LargeListEntryHeight / 2 - SmallIconSize / 2;
            iconRect.x = viewRect.width - Margin - SmallIconSize;
            if (animalDef.RaceProps.trainability == TrainabilityDefOf.Advanced)
            {
                GUI.DrawTexture(iconRect, Resources.TrainableAdvancedIcon);
            }
            else if (animalDef.RaceProps.trainability == TrainabilityDefOf.Intermediate)
            {
                GUI.DrawTexture(iconRect, Resources.TrainableIntermediateIcon);
            }
            else if (animalDef.RaceProps.trainability == TrainabilityDefOf.None)
            {
                GUI.DrawTexture(iconRect, Resources.TrainableNoneIcon);
            }
            else
            {
                GUI.DrawTexture(iconRect, Resources.UnkownIcon);
            }
            TooltipHandler.TipRegion(iconRect,
                "Trainability".Translate() + ": " + animalDef.RaceProps.trainability.LabelCap);

            // if aggressive, draw warning icon
            iconRect.x -= Margin + SmallIconSize;
            if (animalDef.RaceProps.manhunterOnTameFailChance >= 0.1)
            {
                var color = GUI.color;
                if (animalDef.RaceProps.manhunterOnTameFailChance > 0.25)
                {
                    GUI.color = Color.red;
                }
                else
                {
                    GUI.color = Resources.Orange;
                }

                GUI.DrawTexture(iconRect, Resources.ClawIcon);
                GUI.color = color;
                TooltipHandler.TipRegion(iconRect, I18n.Aggressiveness(animalDef.RaceProps.manhunterOnTameFailChance));
            }

            // draw label
            var label = animalDef.LabelCap + "\n<i>" +
                "ColonyManagerRedux.Livestock.TameWildCount".Translate(
                    animalDef.GetTame(manager).Count(),
                    animalDef.GetWild(manager).Count()) + "</i>";
            Label(row, label, TextAnchor.MiddleLeft, margin: Margin * 2);

            // button
            if (Widgets.ButtonInvisible(row))
            {
                _selectedAvailable = animalDef;
                Selected = MakeNewJob(animalDef);
            }
        }

        GUI.EndGroup();
        Widgets.EndScrollView();
    }

    private float DrawButcherSection(ManagerJob_Livestock job, Vector2 pos, float width)
    {
        var start = pos;

        // butchery stuff
        var butcherExcessRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        DrawToggle(butcherExcessRect,
            "ColonyManagerRedux.Livestock.ButcherExcess".Translate(),
            "ColonyManagerRedux.Livestock.ButcherExcess.Tip".Translate(),
            ref job.ButcherExcess);
        pos.y += ListEntryHeight;

        if (job.ButcherExcess)
        {
            var cellWidth = (width - Margin * 2) / 3f;
            var butcherOptionRect = new Rect(pos.x, pos.y, cellWidth, ListEntryHeight);

            DrawToggle(butcherOptionRect,
                "ColonyManagerRedux.Livestock.ButcherTrained".Translate(),
                "ColonyManagerRedux.Livestock.ButcherTrained.Tip".Translate(),
                ref job.ButcherTrained, font: GameFont.Tiny, wrap: false);
            butcherOptionRect.x += cellWidth + Margin;

            DrawToggle(butcherOptionRect,
                "ColonyManagerRedux.Livestock.ButcherPregnant".Translate(),
                "ColonyManagerRedux.Livestock.ButcherPregnant.Tip".Translate(),
                ref job.ButcherPregnant, font: GameFont.Tiny, wrap: false);
            butcherOptionRect.x += cellWidth + Margin;

            DrawToggle(butcherOptionRect,
                "ColonyManagerRedux.Livestock.ButcherBonded".Translate(),
                "ColonyManagerRedux.Livestock.ButcherBonded.Tip".Translate(),
                ref job.ButcherBonded, font: GameFont.Tiny, wrap: false);

            pos.y += ListEntryHeight;
        }

        return pos.y - start.y;
    }

    private void DrawCurrentJobList(Rect outRect, Rect viewRect)
    {
        var currentJobs = manager.JobStack.JobsOfType<ManagerJob_Livestock>().ToList();

        // set sizes
        viewRect.height = currentJobs.Count * LargeListEntryHeight;
        if (viewRect.height > outRect.height)
        {
            viewRect.width -= ScrollbarWidth;
        }

        Widgets.BeginScrollView(outRect, ref _scrollPosition, viewRect);
        GUI.BeginGroup(viewRect);

        for (var i = 0; i < currentJobs.Count; i++)
        {
            // set up rect
            var row = new Rect(0f, LargeListEntryHeight * i, viewRect.width, LargeListEntryHeight);

            // highlights
            Widgets.DrawHighlightIfMouseover(row);
            if (i % 2 == 0)
            {
                Widgets.DrawAltRect(row);
            }

            if (currentJobs[i] == SelectedCurrentLivestockJob)
            {
                Widgets.DrawHighlightSelected(row);
            }

            // draw label
            DrawListEntry(currentJobs[i], row, ListEntryDrawMode.Local);

            // button
            if (Widgets.ButtonInvisible(row))
            {
                Selected = currentJobs[i];
            }
        }

        GUI.EndGroup();
        Widgets.EndScrollView();
    }

    private float DrawFollowSection(ManagerJob_Livestock job, Vector2 pos, float width)
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
        var report = job.CanBeTrained(job.Trigger.pawnKind, TrainableDefOf.Obedience, out bool _);
        if (report.Accepted)
        {
            Label(rowRect,
                "ColonyManagerRedux.ManagerLivestock.MasterDefault".Translate(),
                "ColonyManagerRedux.ManagerLivestock.MasterDefault.Tip".Translate(),
                TextAnchor.MiddleLeft, margin: Margin);
        }
        else
        {
            Label(rowRect,
                "ColonyManagerRedux.ManagerLivestock.MasterDefault".Translate(),
                report.Reason,
                TextAnchor.MiddleLeft, margin: Margin, color: Color.gray);
        }
        if (Widgets_Buttons.DisableableButtonText(buttonRect, GetMasterLabel(job), enabled: report.Accepted))
        {
            var options = new List<FloatMenuOption>();

            // modes
            foreach (var mode in Utilities_Livestock.MasterModeArray.Where(mm => (mm & MasterMode.All) == mm))
            {
                options.Add(new FloatMenuOption($"ColonyManagerRedux.ManagerLivestock.MasterMode.{mode}".Translate(),
                    () => job.Masters = mode));
            }

            // specific pawns
            foreach (var pawn in job.Trigger.pawnKind.GetMasterOptions(manager, MasterMode.All))
            {
                options.Add(new FloatMenuOption(
                    "ColonyManagerRedux.ManagerLivestock.Master".Translate(pawn.LabelShort,
                        pawn.skills.AverageOfRelevantSkillsFor(
                            WorkTypeDefOf.Handling)),
                    () =>
                    {
                        job.Master = pawn;
                        job.Masters = MasterMode.Specific;
                    }));
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        // respect bonds?
        rowRect.y += ListEntryHeight;
        if (!report.Accepted)
        {
            Label(rowRect,
                "ColonyManagerRedux.ManagerLivestock.RespectBonds".Translate(),
                report.Reason,
                color: Color.grey, margin: Margin);
        }
        else if (job.Masters != MasterMode.Manual && job.Masters != MasterMode.Specific)
        {
            DrawToggle(rowRect,
                "ColonyManagerRedux.ManagerLivestock.RespectBonds".Translate(),
                "ColonyManagerRedux.ManagerLivestock.RespectBonds.Tip".Translate(),
                ref job.RespectBonds);
        }
        else
        {
            Label(rowRect,
                "ColonyManagerRedux.ManagerLivestock.RespectBonds".Translate(),
                "ColonyManagerRedux.ManagerLivestock.RespectBonds.DisabledBecauseMastersNotSet".Translate(),
                color: Color.grey, margin: Margin);
        }

        // default follow
        rowRect.y += ListEntryHeight;
        if (report.Accepted)
        {
            DrawToggle(rowRect,
                "ColonyManagerRedux.ManagerLivestock.Follow".Translate(),
                "ColonyManagerRedux.ManagerLivestock.Follow.Tip".Translate(),
                ref job.SetFollow);
        }
        else
        {
            Label(rowRect,
                "ColonyManagerRedux.ManagerLivestock.Follow".Translate(),
                report.Reason,
                color: Color.grey, margin: Margin);
        }

        if (job.SetFollow && report.Accepted)
        {
            rowRect.y += ListEntryHeight;
            var followRect = rowRect;
            followRect.width /= 2f;
            DrawToggle(followRect,
                "ColonyManagerRedux.ManagerLivestock.FollowDrafted".Translate(),
                "ColonyManagerRedux.ManagerLivestock.FollowDrafted.Tip".Translate(),
                ref job.FollowDrafted,
                font: GameFont.Tiny);
            followRect.x += followRect.width;
            DrawToggle(followRect,
                "ColonyManagerRedux.ManagerLivestock.FollowFieldwork".Translate(),
                "ColonyManagerRedux.ManagerLivestock.FollowFieldwork.Tip".Translate(),
                ref job.FollowFieldwork,
                font: GameFont.Tiny);
        }

        // follow when training
        rowRect.y += ListEntryHeight;
        if (report.Accepted)
        {
            TooltipHandler.TipRegion(rowRect, "ColonyManagerRedux.ManagerLivestock.FollowTraining.Tip".Translate());
            DrawToggle(rowRect,
                "ColonyManagerRedux.ManagerLivestock.FollowTraining".Translate(),
                "ColonyManagerRedux.ManagerLivestock.FollowTraining.Tip".Translate(),
                ref job.FollowTraining);
        }
        else
        {
            Label(rowRect,
                "ColonyManagerRedux.ManagerLivestock.FollowTraining".Translate(),
                report.Reason,
                color: Color.grey, margin: Margin);
        }
        // trainer selection
        if (job.FollowTraining && report.Accepted)
        {
            rowRect.y += ListEntryHeight;
            Label(rowRect, "ColonyManagerRedux.ManagerLivestock.MasterTraining".Translate(),
                "ColonyManagerRedux.ManagerLivestock.MasterTraining.Tip".Translate(),
                TextAnchor.MiddleLeft, margin: Margin);

            buttonRect = buttonRect.CenteredOnYIn(rowRect);
            if (Widgets.ButtonText(buttonRect, GetTrainerLabel(job)))
            {
                var options = new List<FloatMenuOption>();

                // modes
                foreach (var mode in Utilities_Livestock.MasterModeArray.Where(mm => (mm & MasterMode.Trainers) == mm))
                {
                    options.Add(new FloatMenuOption($"ColonyManagerRedux.ManagerLivestock.MasterMode.{mode}".Translate(),
                        () => job.Trainers = mode));
                }

                // specific pawns
                foreach (var pawn in job.Trigger.pawnKind.GetTrainers(manager, MasterMode.Trainers))
                {
                    options.Add(new FloatMenuOption(
                        "ColonyManagerRedux.ManagerLivestock.Master".Translate(pawn.LabelShort,
                            pawn.skills.AverageOfRelevantSkillsFor(
                                WorkTypeDefOf.Handling)),
                        () =>
                        {
                            job.Trainer = pawn;
                            job.Trainers = MasterMode.Specific;
                        }));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        return rowRect.yMax - start.y;
    }

    private float DrawTamedAnimalSection(ManagerJob_Livestock job, Vector2 pos, float width)
    {
        var pawnKind = job.Trigger.pawnKind;
        var animals = pawnKind.GetTame(manager) ?? [];
        return DrawAnimalSection(ref pos, width, "ColonyManagerRedux.Livestock.Tame".Translate(), pawnKind, animals);
    }

    private float DrawTamingSection(ManagerJob_Livestock job, Vector2 pos, float width)
    {
        var start = pos;
        DrawToggle(ref pos, width,
            "ColonyManagerRedux.Livestock.TameMore".Translate(),
            "ColonyManagerRedux.Livestock.TameMore.Tip".Translate(),
            ref job.TryTameMore);

        // area to tame from (if taming more);
        if (job.TryTameMore)
        {
            AreaAllowedGUI.DoAllowedAreaSelectors(ref pos, width, ref job.TameArea, manager);
            DrawReachabilityToggle(ref pos, width, ref job.ShouldCheckReachable);
            DrawToggle(ref pos, width,
                "ColonyManagerRedux.ManagerPathBasedDistance".Translate(),
                "ColonyManagerRedux.ManagerPathBasedDistance.Tip".Translate(),
                ref job.UsePathBasedDistance, true);
        }

        return pos.y - start.y;
    }

    private float DrawTargetCountsSection(ManagerJob_Livestock job, Vector2 pos, float width)
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
        Label(countRects[0, 1], Gender.Female.ToString(), TextAnchor.LowerCenter, GameFont.Tiny);
        Label(countRects[0, 2], Gender.Male.ToString(), TextAnchor.LowerCenter, GameFont.Tiny);
        Label(countRects[1, 0], "ColonyManagerRedux.Livestock.Adult".Translate(), TextAnchor.MiddleRight, GameFont.Tiny);
        Label(countRects[2, 0], "ColonyManagerRedux.Livestock.Juvenile".Translate(), TextAnchor.MiddleRight, GameFont.Tiny);

        // fields
        DoCountField(job, countRects[1, 1], AgeAndSex.AdultFemale);
        DoCountField(job, countRects[1, 2], AgeAndSex.AdultMale);
        DoCountField(job, countRects[2, 1], AgeAndSex.JuvenileFemale);
        DoCountField(job, countRects[2, 2], AgeAndSex.JuvenileMale);

        return 3 * ListEntryHeight;
    }

    private float DrawTrainingSection(ManagerJob_Livestock job, Vector2 pos, float width)
    {
        int rowCount = (int)Math.Ceiling((double)job.Training.Count / TrainingJobsPerRow);
        var trainingRect = new Rect(pos.x, pos.y, width, ListEntryHeight * rowCount);
        DrawTrainingSelector(job, trainingRect, rowCount);
        var height = ListEntryHeight * rowCount;

        var unassignTrainingRect = new Rect(pos.x, pos.y + height, width, ListEntryHeight);
        DrawToggle(unassignTrainingRect,
            "ColonyManagerRedux.Livestock.UnassignTraining".Translate(),
            "ColonyManagerRedux.Livestock.UnassignTraining.Tip".Translate(),
            ref job.Training.UnassignTraining);
        height += ListEntryHeight;

        if (job.Training.Any)
        {
            var trainYoungRect = new Rect(pos.x, pos.y + height, width, ListEntryHeight);
            DrawToggle(trainYoungRect,
                "ColonyManagerRedux.Livestock.TrainYoung".Translate(),
                "ColonyManagerRedux.Livestock.TrainYoung.Tip".Translate(),
                ref job.Training.TrainYoung);
            height += ListEntryHeight;
        }

        return height;
    }

    private float DrawWildAnimalSection(ManagerJob_Livestock job, Vector2 pos, float width)
    {
        var pawnKind = job.Trigger.pawnKind;
        var animals = pawnKind.GetWild(manager) ?? [];
        return DrawAnimalSection(ref pos, width, "ColonyManagerRedux.Livestock.Wild".Translate(), pawnKind, animals);
    }

    private void Refresh()
    {
        // currently managed
        var currentJobs = manager.JobStack.JobsOfType<ManagerJob_Livestock>().ToList();

        // concatenate lists of animals on biome and animals in colony.
        _availablePawnKinds = manager.map.Biome.AllWildAnimals.ToList();
        _availablePawnKinds.AddRange(
            manager.map.mapPawns.AllPawns
                .Where(p => p.RaceProps.Animal)
                .Select(p => p.kindDef));
        _availablePawnKinds = _availablePawnKinds

            // get distinct pawnkinds from the merges
            .Distinct()

            // remove already managed pawnkinds
            .Where(pk => !currentJobs.Select(job => job.Trigger.pawnKind).Contains(pk))

            // order by label
            .OrderBy(def => def.LabelCap.RawText)
            .ToList();
    }
}
