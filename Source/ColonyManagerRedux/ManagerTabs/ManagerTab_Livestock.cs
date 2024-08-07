// ManagerTab_Livestock.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.Extensions;
using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;
using static ColonyManagerRedux.Utilities;
using static ColonyManagerRedux.Widgets_Labels;

namespace ColonyManagerRedux;

[HotSwappable]
internal sealed partial class ManagerTab_Livestock(Manager manager) : ManagerTab<ManagerJob_Livestock>(manager)
{
    internal const int TrainingJobsPerRow = 3;

    private List<PawnKindDef> _availablePawnKinds = [];

    private string[] _newCounts = ["", "", "", ""];

    private bool _onCurrentTab;
    private Vector2 _scrollPosition = Vector2.zero;
    private PawnKindDef? _selectedAvailable;

    protected override bool CreateNewSelectedJobOnMake => false;

    public override void PostOpen()
    {
        animalsTameTable?.SetDirty();
        animalsWildTable?.SetDirty();
    }

    protected override void PostSelect()
    {
        _onCurrentTab = Selected != null;
        _selectedAvailable = null;
        if (Selected != null)
        {
            _newCounts =
                SelectedJob!.TriggerPawnKind.CountTargets.Select(v => v.ToString()).ToArray();
        }
        animalsTameTable?.SetDirty();
        animalsWildTable?.SetDirty();
    }

    internal override void Notify_PawnsChanged()
    {
        if (Selected != null)
        {
            animalsTameTable?.SetDirty();
            animalsWildTable?.SetDirty();
        }
    }

    protected override bool ShouldHaveNewJobButton => false;

    public override (string label, Vector2 labelSize) GetFullLabel(ManagerJob job, ListEntryDrawMode mode, float labelWidth, string? subLabel = null, bool drawSubLabel = true)
    {
        return base.GetFullLabel(job, mode, labelWidth, subLabel, false);
    }

    public override string GetMainLabel(ManagerJob job, ListEntryDrawMode mode)
    {
        return ((ManagerJob_Livestock)job).FullLabel;
    }

    public override string GetSubLabel(ManagerJob job, ListEntryDrawMode mode)
    {
        return ((ManagerJob_Livestock)job).TriggerPawnKind.StatusTooltip;
    }

    public static void DrawTrainingSelector(ManagerJob_Livestock job, Rect rect, int rowCount)
    {
        var cellCount = Math.Min(TrainingJobsPerRow, job.Training.Count);
        var cellWidth = (rect.width - Margin * (cellCount - 1)) / cellCount;
        var keys = ManagerJob_Livestock.TrainingTracker.TrainableDefs;

        GUI.BeginGroup(rect);
        for (var i = 0; i < job.Training.Count; i++)
        {
            var cell = new Rect(i % cellCount * (cellWidth + Margin), i / cellCount * ListEntryHeight, cellWidth, rect.height / rowCount);
            var report = ManagerJob_Livestock.CanBeTrained(job.TriggerPawnKind.pawnKind, keys[i], out bool visible);
            if (visible && report.Accepted)
            {
                var checkOn = job.Training[keys[i]];
                DrawToggle(cell, keys[i].LabelCap, keys[i].description, ref checkOn, size: SmallIconSize,
                            wrap: false);
                job.Training[keys[i]] = checkOn;
            }
            else if (visible)
            {
                Label(cell, keys[i].LabelCap, report.Reason, TextAnchor.MiddleLeft, color: Color.grey, margin: Margin);
            }
        }

        GUI.EndGroup();
    }

    public static string GetMasterLabel(ManagerJob_Livestock job)
    {
        var report = ManagerJob_Livestock.CanBeTrained(job.TriggerPawnKind.pawnKind, TrainableDefOf.Obedience, out bool _);
        if (!report.Accepted)
        {
            return "ColonyManagerRedux.Livestock.MasterUnavailable".Translate();
        }
        return job.Masters switch
        {
            MasterMode.Specific => job.Master?.LabelShort ?? "ColonyManagerRedux.Common.None".Translate(),
            _ => (string)$"ColonyManagerRedux.Livestock.MasterMode.{job.Masters}".Translate(),
        };
    }

    public static string GetTrainerLabel(ManagerJob_Livestock job)
    {
        return job.Trainers switch
        {
            MasterMode.Specific => job.Trainer?.LabelShort ?? "BUG: INVALID",
            _ => (string)$"ColonyManagerRedux.Livestock.MasterMode.{job.Trainers}".Translate(),
        };
    }

    public override void PreOpen()
    {
        Refresh();
    }

    protected override void DoMainContent(Rect rect)
    {
        // background
        Widgets.DrawMenuSection(rect);

        // cop out if nothing is selected.
        if (SelectedJob == null)
        {
            Label(rect, "ColonyManagerRedux.Livestock.SelectPawnKind".Translate(), TextAnchor.MiddleCenter);
            return;
        }

        // rects
        var optionsColumnRect = new Rect(
            rect.xMin,
            rect.yMin,
            rect.width * 4 / 7f,
            rect.height - Margin - ButtonSize.y);
        var animalsColumnRect = new Rect(
            optionsColumnRect.xMax,
            rect.yMin,
            rect.width * 3 / 7f - 1,
            rect.height - Margin - ButtonSize.y);
        var buttonRect = new Rect(
            rect.xMax - ButtonSize.x,
            rect.yMax - ButtonSize.y,
            ButtonSize.x - Margin,
            ButtonSize.y - Margin);

        Widgets_Section.BeginSectionColumn(optionsColumnRect, "Livestock.Options", out Vector2 position, out float width);

        Widgets_Section.Section(SelectedJob, ref position, width, DrawTargetCountsSection,
            "ColonyManagerRedux.Livestock.TargetCountsHeader".Translate());
        Widgets_Section.Section(SelectedJob, ref position, width, DrawTamingSection,
            "ColonyManagerRedux.Livestock.TamingHeader".Translate());
        Widgets_Section.Section(SelectedJob, ref position, width, DrawButcherSection,
            "ColonyManagerRedux.Livestock.ButcherHeader".Translate());
        Widgets_Section.Section(SelectedJob, ref position, width, DrawTrainingSection,
            "ColonyManagerRedux.Livestock.TrainingHeader".Translate());
        Widgets_Section.Section(SelectedJob, ref position, width, DrawAreaRestrictionsSection,
            "ColonyManagerRedux.Livestock.AreaRestrictionsHeader".Translate());
        Widgets_Section.Section(SelectedJob, ref position, width, DrawFollowSection,
            "ColonyManagerRedux.Livestock.FollowHeader".Translate());

        position.y -= Margin;

        Widgets_Section.EndSectionColumn("Livestock.Options", position);

        DrawAnimalTables(animalsColumnRect);

        // add / remove to the stack
        if (SelectedJob.IsManaged)
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.Common.Delete".Translate()))
            {
                SelectedJob.Delete();
                Selected = null;
                _onCurrentTab = false;
                Refresh();
                return; // just skip to the next tick to avoid null reference errors.
            }

            TooltipHandler.TipRegion(buttonRect, "ColonyManagerRedux.Thresholds.DeleteBillTooltip".Translate());
        }
        else
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.Common.Manage".Translate()))
            {
                SelectedJob.IsManaged = true;
                _onCurrentTab = true;
                manager.JobTracker.Add(SelectedJob);
                Refresh();
            }

            TooltipHandler.TipRegion(buttonRect, "ColonyManagerRedux.Thresholds.ManageBillTooltip".Translate());
        }
    }

    private void DoCountField(ManagerJob_Livestock job, Rect rect, AgeAndSex ageSex)
    {
        int ageSexIndex = (int)ageSex;

        if (int.TryParse(_newCounts[ageSexIndex], out var value))
        {
            job.TriggerPawnKind.CountTargets[ageSexIndex] = value;
        }
        else
        {
            GUI.color = Color.red;
        }

        _newCounts[ageSexIndex] = Widgets.TextField(rect.ContractedBy(1f), _newCounts[ageSexIndex]);
        GUI.color = Color.white;
    }

    protected override void DoJobList(Rect rect)
    {
        rect.yMin += 31f;

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
            base.DoJobList(outRect);
        }
        else
        {
            DrawAvailableJobList(outRect, viewRect);
        }
    }

    private float DrawAreaRestrictionsSection(ManagerJob_Livestock job, Vector2 pos, float width)
    {
        var start = pos;

        // skip for animals that can't be restricted
        if (job.TriggerPawnKind.pawnKind.RaceProps.Roamer)
        {
            var unavailableLabelRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
            unavailableLabelRect.xMin += Margin;
            Label(unavailableLabelRect,
                "ColonyManagerRedux.Livestock.DisabledBecauseRoamingAnimal".Translate(job.TriggerPawnKind.pawnKind.GetLabelPlural()),
                "ColonyManagerRedux.Livestock.DisabledBecauseRoamingAnimalTip".Translate(job.TriggerPawnKind.pawnKind.GetLabelPlural()),
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

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
            var areaRects = new Rect[cols, cols];
#pragma warning restore CA1814 // Prefer jagged arrays over multidimensional
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
                "ColonyManagerRedux.Livestock.DisabledBecauseSlaughterExcessDisabled".Translate(),
                TextAnchor.MiddleLeft,
                color: Color.grey);
        }

        if (job.TriggerPawnKind.pawnKind.Milkable())
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

        if (job.TriggerPawnKind.pawnKind.Shearable())
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
                "ColonyManagerRedux.Livestock.DisabledBecauseNoTrainingSet".Translate(),
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

            var upperIconRect = new Rect(row)
            {
                x = viewRect.width - Margin - SmallIconSize,
                width = SmallIconSize,
                height = SmallIconSize,
            };
            upperIconRect.y += Margin;

            // draw meat yield icon
            var estimatedMeatCount = animalDef.EstimatedMeatCount();
            Widgets.DefIcon(upperIconRect, animalDef.RaceProps.meatDef);
            TooltipHandler.TipRegion(upperIconRect,
                "ColonyManagerRedux.Livestock.Yields".Translate(animalDef.RaceProps.meatDef.LabelCap,
                    estimatedMeatCount));

            // draw leather yield icon
            if (animalDef.RaceProps.leatherDef != null)
            {
                upperIconRect.x -= Margin + SmallIconSize;
                var estimatedLeatherCount = animalDef.EstimatedLeatherCount();
                Widgets.DefIcon(upperIconRect, animalDef.RaceProps.leatherDef);
                TooltipHandler.TipRegion(upperIconRect,
                    "ColonyManagerRedux.Livestock.Yields".Translate(animalDef.RaceProps.leatherDef.LabelCap,
                        estimatedLeatherCount));
            }

            // draw milk yield icon
            var milkableProperties = animalDef.race.GetCompProperties<CompProperties_Milkable>();
            if (milkableProperties != null)
            {
                upperIconRect.x -= Margin + SmallIconSize;
                Widgets.DefIcon(upperIconRect, milkableProperties.milkDef);
                TooltipHandler.TipRegion(upperIconRect,
                    "ColonyManagerRedux.Livestock.YieldsInterval".Translate(milkableProperties.milkDef.LabelCap,
                        milkableProperties.milkAmount, milkableProperties.milkIntervalDays));
            }

            // draw milk yield icon
            var shearableProperties = animalDef.race.GetCompProperties<CompProperties_Shearable>();
            if (shearableProperties != null)
            {
                upperIconRect.x -= Margin + SmallIconSize;
                Widgets.DefIcon(upperIconRect, shearableProperties.woolDef);
                TooltipHandler.TipRegion(upperIconRect,
                    "ColonyManagerRedux.Livestock.YieldsInterval".Translate(shearableProperties.woolDef.LabelCap,
                        shearableProperties.woolAmount, shearableProperties.shearIntervalDays));
            }

            var lowerIconRect = new Rect(row)
            {
                x = viewRect.width - Margin - SmallIconSize,
                width = SmallIconSize,
                height = SmallIconSize,
            };
            lowerIconRect.y += row.height - SmallIconSize - Margin;

            // draw trainability icon
            if (animalDef.RaceProps.trainability == TrainabilityDefOf.Advanced)
            {
                GUI.DrawTexture(lowerIconRect, Resources.TrainableAdvancedIcon);
            }
            else if (animalDef.RaceProps.trainability == TrainabilityDefOf.Intermediate)
            {
                GUI.DrawTexture(lowerIconRect, Resources.TrainableIntermediateIcon);
            }
            else if (animalDef.RaceProps.trainability == TrainabilityDefOf.None)
            {
                GUI.DrawTexture(lowerIconRect, Resources.TrainableNoneIcon);
            }
            else
            {
                GUI.DrawTexture(lowerIconRect, Resources.UnkownIcon);
            }
            TooltipHandler.TipRegion(lowerIconRect,
                "Trainability".Translate() + ": " + animalDef.RaceProps.trainability.LabelCap);

            // if aggressive, draw warning icon
            lowerIconRect.x -= Margin + SmallIconSize;
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

                GUI.DrawTexture(lowerIconRect, Resources.ClawIcon);
                GUI.color = color;
                TooltipHandler.TipRegion(lowerIconRect, I18n.Aggressiveness(animalDef.RaceProps.manhunterOnTameFailChance));
            }

            // draw label
            var label = animalDef.LabelCap + "\n<i>" +
                "ColonyManagerRedux.Livestock.TameCount".Translate(
                    animalDef.GetTame(manager, false).Count()) + ", " +
                "ColonyManagerRedux.Livestock.WildCount".Translate(
                    animalDef.GetWild(manager).Count()) + ".</i>";
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

    public override void DrawListEntry(ManagerJob job, ref Vector2 position, float width, ListEntryDrawMode mode, bool active = true, bool showOrdering = true, float statusHeight = 50)
    {
        base.DrawListEntry(
            job,
            ref position,
            width,
            mode,
            active,
            false, 4 * Trigger_PawnKind.PawnKindProgressBarHeight + 3 * Margin / 2);
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
        var report = ManagerJob_Livestock.CanBeTrained(job.TriggerPawnKind.pawnKind, TrainableDefOf.Obedience, out bool _);
        if (report.Accepted)
        {
            Label(rowRect,
                "ColonyManagerRedux.Livestock.MasterDefault".Translate(),
                "ColonyManagerRedux.Livestock.MasterDefault.Tip".Translate(),
                TextAnchor.MiddleLeft, margin: Margin);
        }
        else
        {
            Label(rowRect,
                "ColonyManagerRedux.Livestock.MasterDefault".Translate(),
                report.Reason,
                TextAnchor.MiddleLeft, margin: Margin, color: Color.gray);
        }
        if (IlyvionWidgets.DisableableButtonText(buttonRect, GetMasterLabel(job), enabled: report.Accepted))
        {
            var options = new List<FloatMenuOption>();

            // modes
            foreach (var mode in Utilities_Livestock.MasterModeArray.Where(mm => (mm & MasterMode.All) == mm))
            {
                options.Add(new FloatMenuOption($"ColonyManagerRedux.Livestock.MasterMode.{mode}".Translate(),
                    () => job.Masters = mode));
            }

            // specific pawns
            foreach (var pawn in job.TriggerPawnKind.pawnKind.GetMasterOptions(manager, MasterMode.All))
            {
                options.Add(new FloatMenuOption(
                    "ColonyManagerRedux.Livestock.Master".Translate(pawn.LabelShort,
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
                "ColonyManagerRedux.Livestock.RespectBonds".Translate(),
                report.Reason,
                color: Color.grey, margin: Margin);
        }
        else if (job.Masters != MasterMode.Manual && job.Masters != MasterMode.Specific)
        {
            DrawToggle(rowRect,
                "ColonyManagerRedux.Livestock.RespectBonds".Translate(),
                "ColonyManagerRedux.Livestock.RespectBonds.Tip".Translate(),
                ref job.RespectBonds);
        }
        else
        {
            Label(rowRect,
                "ColonyManagerRedux.Livestock.RespectBonds".Translate(),
                "ColonyManagerRedux.Livestock.RespectBonds.DisabledBecauseMastersNotSet".Translate(),
                color: Color.grey, margin: Margin);
        }

        // default follow
        rowRect.y += ListEntryHeight;
        if (report.Accepted)
        {
            DrawToggle(rowRect,
                "ColonyManagerRedux.Livestock.Follow".Translate(),
                "ColonyManagerRedux.Livestock.Follow.Tip".Translate(),
                ref job.SetFollow);
        }
        else
        {
            Label(rowRect,
                "ColonyManagerRedux.Livestock.Follow".Translate(),
                report.Reason,
                color: Color.grey, margin: Margin);
        }

        if (job.SetFollow && report.Accepted)
        {
            rowRect.y += ListEntryHeight;
            var followRect = rowRect;
            followRect.width /= 2f;
            DrawToggle(followRect,
                "ColonyManagerRedux.Livestock.FollowDrafted".Translate(),
                "ColonyManagerRedux.Livestock.FollowDrafted.Tip".Translate(),
                ref job.FollowDrafted,
                font: GameFont.Tiny);
            followRect.x += followRect.width;
            DrawToggle(followRect,
                "ColonyManagerRedux.Livestock.FollowFieldwork".Translate(),
                "ColonyManagerRedux.Livestock.FollowFieldwork.Tip".Translate(),
                ref job.FollowFieldwork,
                font: GameFont.Tiny);
        }

        // follow when training
        rowRect.y += ListEntryHeight;
        if (report.Accepted)
        {
            TooltipHandler.TipRegion(rowRect, "ColonyManagerRedux.Livestock.FollowTraining.Tip".Translate());
            DrawToggle(rowRect,
                "ColonyManagerRedux.Livestock.FollowTraining".Translate(),
                "ColonyManagerRedux.Livestock.FollowTraining.Tip".Translate(),
                ref job.FollowTraining);
        }
        else
        {
            Label(rowRect,
                "ColonyManagerRedux.Livestock.FollowTraining".Translate(),
                report.Reason,
                color: Color.grey, margin: Margin);
        }
        // trainer selection
        if (job.FollowTraining && report.Accepted)
        {
            rowRect.y += ListEntryHeight;
            Label(rowRect, "ColonyManagerRedux.Livestock.MasterTraining".Translate(),
                "ColonyManagerRedux.Livestock.MasterTraining.Tip".Translate(),
                TextAnchor.MiddleLeft, margin: Margin);

            buttonRect = buttonRect.CenteredOnYIn(rowRect);
            if (Widgets.ButtonText(buttonRect, GetTrainerLabel(job)))
            {
                var options = new List<FloatMenuOption>();

                // modes
                foreach (var mode in Utilities_Livestock.MasterModeArray.Where(mm => (mm & MasterMode.Trainers) == mm))
                {
                    options.Add(new FloatMenuOption($"ColonyManagerRedux.Livestock.MasterMode.{mode}".Translate(),
                        () => job.Trainers = mode));
                }

                // specific pawns
                foreach (var pawn in job.TriggerPawnKind.pawnKind.GetTrainers(manager, MasterMode.Trainers))
                {
                    options.Add(new FloatMenuOption(
                        "ColonyManagerRedux.Livestock.Master".Translate(pawn.LabelShort,
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

    private float DrawTamingSection(ManagerJob_Livestock job, Vector2 pos, float width)
    {
        var start = pos;
        DrawToggle(ref pos, width,
            "ColonyManagerRedux.Livestock.TameMore".Translate(),
            "ColonyManagerRedux.Livestock.TameMore.Tip".Translate(),
            ref job.TryTameMore);

        if (job.TryTameMore)
        {
            DrawToggle(ref pos, width,
                "ColonyManagerRedux.Livestock.TamePastTargets".Translate(),
                "ColonyManagerRedux.Livestock.TamePastTargets.Tip".Translate(),
                ref job.TamePastTargets);

            AreaAllowedGUI.DoAllowedAreaSelectors(ref pos, width, ref job.TameArea, manager);
            DrawReachabilityToggle(ref pos, width, ref job.ShouldCheckReachable);
            DrawToggle(ref pos, width,
                "ColonyManagerRedux.Threshold.PathBasedDistance".Translate(),
                "ColonyManagerRedux.Threshold.PathBasedDistance.Tip".Translate(),
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
#pragma warning disable CA1814
        var countRects = new Rect[rows, cols];
#pragma warning restore CA1814
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

    private void DrawAnimalTables(Rect animalsColumnRect)
    {
        animalsColumnRect.height /= 2;
        var animalsColumnRect2 = new Rect(animalsColumnRect)
        {
            y = animalsColumnRect.height
        };
        animalsColumnRect2.yMax -= Margin;
        animalsColumnRect.yMin += Margin;
        var headerRect = new Rect(animalsColumnRect.x, animalsColumnRect.y,
            animalsColumnRect.width, SectionHeaderHeight).RoundToInt();
        Widgets_Labels.Label(headerRect, "ColonyManagerRedux.Livestock.AnimalsHeader"
            .Translate(
                "ColonyManagerRedux.Livestock.Tame".Translate(),
                SelectedJob!.TriggerPawnKind.pawnKind.GetLabelPlural())
            .CapitalizeFirst(), TextAnchor.LowerLeft, GameFont.Tiny, margin: 3 * Margin);
        animalsColumnRect.yMin += SectionHeaderHeight;
        animalsColumnRect.yMax -= Margin;
        GUI.DrawTexture(animalsColumnRect, Resources.SlightlyDarkBackground);
        GUI.BeginGroup(animalsColumnRect);
        DrawTamedAnimalTable(animalsColumnRect.AtZero());
        GUI.EndGroup();

        headerRect = new Rect(animalsColumnRect2.x, animalsColumnRect2.y,
            animalsColumnRect2.width, SectionHeaderHeight).RoundToInt();
        Widgets_Labels.Label(headerRect, "ColonyManagerRedux.Livestock.AnimalsHeader"
            .Translate(
                "ColonyManagerRedux.Livestock.Wild".Translate(),
                SelectedJob.TriggerPawnKind.pawnKind.GetLabelPlural())
            .CapitalizeFirst(), TextAnchor.LowerLeft, GameFont.Tiny, margin: 3 * Margin);
        animalsColumnRect2.yMin += SectionHeaderHeight;
        GUI.DrawTexture(animalsColumnRect2, Resources.SlightlyDarkBackground);
        GUI.BeginGroup(animalsColumnRect2);
        DrawWildAnimalTable(animalsColumnRect2.AtZero());
        GUI.EndGroup();
    }

    private void DrawTamedAnimalTable(Rect rect)
    {
        DrawAnimalTable(rect, ref animalsTameTable, "ColonyManagerRedux.Livestock.Tame".Translate(), p => p.GetTame(manager));
    }

    private void DrawWildAnimalTable(Rect rect)
    {
        DrawAnimalTable(rect, ref animalsWildTable, "ColonyManagerRedux.Livestock.Wild".Translate(), p => p.GetWild(manager));
    }

    private void DrawAnimalTable(Rect rect, ref PawnTable? pawnTable, string type, Func<PawnKindDef, IEnumerable<Pawn>?> animalGetter)
    {
        if (pawnTable == null)
        {
            pawnTable = CreateAnimalsTable(() =>
            {
                var pawnKind = SelectedJob!.TriggerPawnKind.pawnKind;
                return animalGetter(pawnKind) ?? [];
            });
            pawnTable.SetFixedSize(new(rect.width, rect.height));
        }

        pawnTable.PawnTableOnGUI(Vector2.zero);
        if (pawnTable.PawnsListForReading.Count == 0)
        {
            var pawnKind = SelectedJob!.TriggerPawnKind.pawnKind;
            Label(rect,
                "ColonyManagerRedux.Livestock.NoAnimals".Translate(type, pawnKind.GetLabelPlural()),
                TextAnchor.MiddleCenter, color: Color.grey);
        }
    }

    protected override void Refresh()
    {
        // currently managed
        var currentJobs = manager.JobTracker.JobsOfType<ManagerJob_Livestock>().ToList();

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
            .Where(pk => !currentJobs.Select(job => job.TriggerPawnKind.pawnKind).Contains(pk))

            // order by label
            .OrderBy(def => def.LabelCap.RawText)
            .ToList();
    }
}
