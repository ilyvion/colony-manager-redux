// ManagerTab_Livestock.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024–2025 Alexander Krivács Schrøder

using ilyvion.Laboratory.Extensions;
using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;
using static ColonyManagerRedux.Managers.ManagerJob_Livestock;
using static ColonyManagerRedux.Utilities;
using TabRecord = ilyvion.Laboratory.UI.TabRecord;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed partial class ManagerTab_Livestock(Manager manager)
    : ManagerTab<ManagerJob_Livestock>(manager)
{
    internal const int EntriesPerRow = 3;
    private readonly List<PawnKindDef> _availablePawnKinds = [];

    private string[] _newCounts = ["", "", "", ""];

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
        if (Selected != null)
        {
            _currentTab = TabList[1].Tab;
        }
        _selectedAvailable = null;
        if (Selected != null)
        {
            _newCounts =
            [
                .. SelectedJob!.TriggerPawnKind.CountTargets.Select(v =>
                    v.ToString(CultureInfo.InvariantCulture)
                ),
            ];
        }
        animalsTameTable?.SetDirty();
        animalsWildTable?.SetDirty();
    }

    protected override void Notify_PawnsChanged()
    {
        if (Selected != null)
        {
            animalsTameTable?.SetDirty();
            animalsWildTable?.SetDirty();
        }
    }

    protected override bool ShouldHaveNewJobButton => false;

    public override (string label, Vector2 labelSize) GetFullLabel(
        ManagerJob job,
        float labelWidth,
        string? subLabel = null,
        bool drawSubLabel = true
    ) => base.GetFullLabel(job, labelWidth, subLabel, false);

    public override string GetMainLabel(ManagerJob job) => ((ManagerJob_Livestock)job).FullLabel;

    public override string GetSubLabel(ManagerJob job) =>
        ((ManagerJob_Livestock)job).TriggerPawnKind.StatusTooltip;

    public static int DrawTrainingSelector(ManagerJob_Livestock job, Rect rect, int rowCount)
    {
        var cellCount = Math.Min(EntriesPerRow, job.Training.Count);
        var cellWidth = (rect.width - (Margin * (cellCount - 1))) / cellCount;
        var keys = TrainingTracker.TrainableDefs;

        var shownJobs = 0;
        if (job.TriggerPawnKind.pawnKind != null)
        {
            GUI.BeginGroup(rect);
            for (var i = 0; i < job.Training.Count; i++)
            {
                var cell = new Rect(
                    shownJobs % cellCount * (cellWidth + Margin),
                    shownJobs / cellCount * ListEntryHeight,
                    cellWidth,
                    rect.height / rowCount
                );
                var report = CanBeTrained(job.TriggerPawnKind.pawnKind, keys[i], out var visible);
                if (visible && report.Accepted)
                {
                    var checkOn = job.Training[keys[i]];
                    DrawToggle(
                        cell,
                        keys[i].LabelCap,
                        keys[i].description,
                        ref checkOn,
                        size: SmallIconSize,
                        wrap: false
                    );
                    job.Training[keys[i]] = checkOn;
                    shownJobs++;
                }
                else if (visible)
                {
                    IlyvionWidgets.Label(
                        cell,
                        keys[i].LabelCap,
                        report.Reason,
                        TextAnchor.MiddleLeft,
                        color: Color.grey,
                        leftMargin: Margin
                    );
                    shownJobs++;
                }
            }

            GUI.EndGroup();
        }

        return shownJobs;
    }

    public static string GetMasterLabel(ManagerJob_Livestock job)
    {
        if (job.TriggerPawnKind.pawnKind == null)
        {
            return job.TriggerPawnKind.ExpectedPawnKindName;
        }

        var report = CanBeTrained(
            job.TriggerPawnKind.pawnKind,
            TrainableDefOf.Obedience,
            out var _
        );
#pragma warning disable IDE0072
        return !report.Accepted
            ? (string)"ColonyManagerRedux.Livestock.MasterUnavailable".Translate()
            : job.Masters switch
            {
                MasterMode.Specific => job.Master?.LabelShort
                    ?? "ColonyManagerRedux.Common.None".Translate(),
                _ => (string)$"ColonyManagerRedux.Livestock.MasterMode.{job.Masters}".Translate(),
            };
#pragma warning restore IDE0072
    }

    public static string GetTrainerLabel(ManagerJob_Livestock job) =>
#pragma warning disable IDE0072
        job.Trainers switch
        {
            MasterMode.Specific => job.Trainer?.LabelShort ?? "BUG: INVALID",
            _ => (string)$"ColonyManagerRedux.Livestock.MasterMode.{job.Trainers}".Translate(),
        };
#pragma warning restore IDE0072

    public override void PreOpen() => Refresh();

    internal const string LivestockOptions = "Livestock.Options";

    protected override bool DoMainContentWhenNothingSelected => true;

    protected override void DoMainContent(Rect rect)
    {
        // background
        Widgets.DrawMenuSection(rect);

        // cop out if nothing is selected.
        if (SelectedJob == null)
        {
            IlyvionWidgets.Label(
                rect,
                "ColonyManagerRedux.Livestock.NoPawnKindSelected".Translate(),
                TextAnchor.MiddleCenter,
                color: Color.gray
            );
            return;
        }

        // rects
        var optionsColumnRect = new Rect(
            rect.xMin,
            rect.yMin,
            rect.width * 4 / 7f,
            rect.height - Margin - ButtonSize.y
        );
        var animalsColumnRect = new Rect(
            optionsColumnRect.xMax,
            rect.yMin,
            (rect.width * 3 / 7f) - 1,
            rect.height - Margin - ButtonSize.y
        );
        var buttonRect = new Rect(
            rect.xMax - ButtonSize.x,
            rect.yMax - ButtonSize.y,
            ButtonSize.x - Margin,
            ButtonSize.y - Margin
        );

        Widgets_Section.BeginSectionColumn(
            optionsColumnRect,
            LivestockOptions,
            out var position,
            out var width
        );

        DrawSection(
            LivestockOptions,
            "TargetCounts",
            ref position,
            width,
            DrawTargetCountsSection,
            "ColonyManagerRedux.Livestock.TargetCountsHeader".Translate()
        );
        DrawSection(
            LivestockOptions,
            "Taming",
            ref position,
            width,
            DrawTamingSection,
            "ColonyManagerRedux.Livestock.TamingHeader".Translate()
        );
        DrawSection(
            LivestockOptions,
            "Culling",
            ref position,
            width,
            DrawCullingSection,
            "ColonyManagerRedux.Livestock.CullingHeader".Translate()
        );
        DrawSection(
            LivestockOptions,
            "CullingExceptions",
            ref position,
            width,
            DrawCullingExceptionsSection,
            "ColonyManagerRedux.Livestock.CullingExceptionsHeader".Translate()
        );
        DrawSection(
            LivestockOptions,
            "Training",
            ref position,
            width,
            DrawTrainingSection,
            "ColonyManagerRedux.Livestock.TrainingHeader".Translate()
        );
        DrawSection(
            LivestockOptions,
            "AreaRestrictions",
            ref position,
            width,
            DrawAreaRestrictionsSection,
            "ColonyManagerRedux.Livestock.AreaRestrictionsHeader".Translate()
        );
        DrawSection(
            LivestockOptions,
            "Follow",
            ref position,
            width,
            DrawFollowSection,
            "ColonyManagerRedux.Livestock.FollowHeader".Translate()
        );

        position.y -= Margin;

        Widgets_Section.EndSectionColumn(LivestockOptions, position);

        DrawAnimalTables(animalsColumnRect);

        // add / remove to the stack
        if (SelectedJob.IsManaged)
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.Common.Delete".Translate()))
            {
                SelectedJob.Delete();
                Selected = null;
                _currentTab = TabList[0].Tab;
                Refresh();
                return; // just skip to the next tick to avoid null reference errors.
            }

            TooltipHandler.TipRegion(
                buttonRect,
                "ColonyManagerRedux.Thresholds.DeleteBillTooltip".Translate()
            );
        }
        else
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.Common.Manage".Translate()))
            {
                SelectedJob.IsManaged = true;
                _currentTab = TabList[1].Tab;
                Manager.JobTracker.Add(SelectedJob);
                Refresh();
            }

            TooltipHandler.TipRegion(
                buttonRect,
                "ColonyManagerRedux.Thresholds.ManageBillTooltip".Translate()
            );
        }
    }

    private void DoCountField(ManagerJob_Livestock job, Rect rect, AgeAndSex ageSex)
    {
        var ageSexIndex = (int)ageSex;

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

    private Tab? _currentTab;
    private List<TabRecord> TabList =>
        field ??= [
            new TabRecord(new AvailableTab(this), () => ref _currentTab!),
            new TabRecord(new CurrentTab(base.DoJobList), () => ref _currentTab!),
        ];

    protected override void DoJobList(Rect rect)
    {
        rect.yMin += 31f;

        _currentTab ??= TabList[0].Tab;

        using (GUIScope.WidgetGroup(rect))
        {
            _currentTab.DoTabContents(rect.AtZero());
        }

        _ = TabDrawer.DrawTabs(rect, TabList);
    }

    private sealed class CurrentTab(Action<Rect> doJobList) : Tab
    {
        public override string Title => "ColonyManagerRedux.Thresholds.Current".Translate();

        public override void DoTabContents(Rect inRect) => doJobList(inRect);
    }

    private sealed class AvailableTab(ManagerTab_Livestock managerTab) : Tab
    {
        public override string Title => "ColonyManagerRedux.Thresholds.Available".Translate();

        public override void DoTabContents(Rect inRect) => managerTab.DrawAvailableJobList(inRect);
    }

    private float DrawAreaRestrictionsSection(ManagerJob_Livestock job, Vector2 pos, float width)
    {
        var start = pos;

        // skip for animals that can't be restricted
        if (job.TriggerPawnKind.pawnKind?.RaceProps.Roamer ?? false)
        {
            var unavailableLabelRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
            unavailableLabelRect.xMin += Margin;
            IlyvionWidgets.Label(
                unavailableLabelRect,
                "ColonyManagerRedux.Livestock.DisabledBecauseRoamingAnimal".Translate(
                    job.TriggerPawnKind.pawnKind.GetLabelPlural().CapitalizeFirst()
                ),
                "ColonyManagerRedux.Livestock.DisabledBecauseRoamingAnimalTip".Translate(
                    job.TriggerPawnKind.pawnKind.GetLabelPlural().CapitalizeFirst()
                ),
                TextAnchor.MiddleLeft,
                color: Color.grey
            );
            return ListEntryHeight;
        }

        // restrict to area
        var restrictAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);

        DrawToggle(
            restrictAreaRect,
            "ColonyManagerRedux.Livestock.RestrictToArea".Translate(),
            "ColonyManagerRedux.Livestock.RestrictToArea.Tip".Translate(),
            ref job.RestrictToArea
        );
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
                        widths[x] - Margin,
                        heights[y]
                    );
                }
            }

            // headers
            IlyvionWidgets.Label(
                areaRects[1, 0],
                Gender.Female.ToString(),
                TextAnchor.LowerCenter,
                GameFont.Tiny
            );
            IlyvionWidgets.Label(
                areaRects[2, 0],
                Gender.Male.ToString(),
                TextAnchor.LowerCenter,
                GameFont.Tiny
            );
            IlyvionWidgets.Label(
                areaRects[0, 1],
                "ColonyManagerRedux.Livestock.Adult".Translate(),
                TextAnchor.MiddleRight,
                GameFont.Tiny
            );

            // do the selectors
            job.RestrictArea[0] = AreaAllowedGUI.DoAllowedAreaSelectors(
                ref areaRects[1, 1],
                job.RestrictArea[0],
                2,
                Manager,
                0
            );
            job.RestrictArea[1] = AreaAllowedGUI.DoAllowedAreaSelectors(
                ref areaRects[2, 1],
                job.RestrictArea[1],
                2,
                Manager,
                0
            );

            // update the juvenile rects
            areaRects[0, 2].y = areaRects[1, 1].yMax + Margin;
            areaRects[1, 2].y = areaRects[1, 1].yMax + Margin;
            areaRects[2, 2].y = areaRects[1, 1].yMax + Margin;

            IlyvionWidgets.Label(
                areaRects[0, 2],
                "ColonyManagerRedux.Livestock.Juvenile".Translate(),
                TextAnchor.MiddleRight,
                GameFont.Tiny
            );

            job.RestrictArea[2] = AreaAllowedGUI.DoAllowedAreaSelectors(
                ref areaRects[1, 2],
                job.RestrictArea[2],
                2,
                Manager,
                0
            );
            job.RestrictArea[3] = AreaAllowedGUI.DoAllowedAreaSelectors(
                ref areaRects[2, 2],
                job.RestrictArea[3],
                2,
                Manager,
                0
            );

            IlyvionDebugViewSettings.DrawIfUIHelpers(() =>
            {
                Widgets.DrawRectFast(areaRects[0, 0], ColorLibrary.Red.ToTransparent(.5f));
                Widgets.DrawRectFast(areaRects[0, 1], ColorLibrary.Green.ToTransparent(.5f));
                Widgets.DrawRectFast(areaRects[0, 2], ColorLibrary.Blue.ToTransparent(.5f));

                Widgets.DrawRectFast(areaRects[1, 0], ColorLibrary.Cyan.ToTransparent(.5f));
                Widgets.DrawRectFast(areaRects[1, 1], ColorLibrary.Yellow.ToTransparent(.5f));
                Widgets.DrawRectFast(areaRects[1, 2], ColorLibrary.Magenta.ToTransparent(.5f));

                Widgets.DrawRectFast(areaRects[2, 0], ColorLibrary.Orange.ToTransparent(.5f));
                Widgets.DrawRectFast(areaRects[2, 1], ColorLibrary.Purple.ToTransparent(.5f));
                Widgets.DrawRectFast(areaRects[2, 2], ColorLibrary.Pink.ToTransparent(.5f));
            });

            Text.Anchor = TextAnchor.UpperLeft; // DoAllowedAreaMode leaves the anchor in an incorrect state.
            pos.y = areaRects[2, 2].yMax;
        }

        var sendToSlaughterAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        pos.y += ListEntryHeight;
        if (job.CullExcess)
        {
            DrawToggle(
                sendToSlaughterAreaRect,
                "ColonyManagerRedux.Livestock.SendToCullingArea".Translate(),
                "ColonyManagerRedux.Livestock.SendToCullingArea.Tip".Translate(),
                ref job.SendToCullingArea
            );

            if (job.SendToCullingArea)
            {
                var slaughterAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
                AreaAllowedGUI.DoAllowedAreaSelectors(
                    ref slaughterAreaRect,
                    ref job.CullingArea,
                    5,
                    Manager
                );
                pos.y += slaughterAreaRect.height;
            }
        }
        else
        {
            sendToSlaughterAreaRect.xMin += Margin;
            IlyvionWidgets.Label(
                sendToSlaughterAreaRect,
                "ColonyManagerRedux.Livestock.SendToCullingArea".Translate(),
                "ColonyManagerRedux.Livestock.DisabledBecauseCullExcessDisabled".Translate(),
                TextAnchor.MiddleLeft,
                color: Color.grey
            );
        }

        if (job.TriggerPawnKind.pawnKind?.Milkable() ?? false)
        {
            var sendToMilkingAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
            pos.y += ListEntryHeight;
            DrawToggle(
                sendToMilkingAreaRect,
                "ColonyManagerRedux.Livestock.SendToMilkingArea".Translate(),
                "ColonyManagerRedux.Livestock.SendToMilkingArea.Tip".Translate(),
                ref job.SendToMilkingArea
            );

            if (job.SendToMilkingArea)
            {
                var milkingAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
                AreaAllowedGUI.DoAllowedAreaSelectors(
                    ref milkingAreaRect,
                    ref job.MilkArea,
                    5,
                    Manager
                );
                pos.y += milkingAreaRect.height;
            }
        }

        if (job.TriggerPawnKind.pawnKind?.Shearable() ?? false)
        {
            var sendToShearingAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
            pos.y += ListEntryHeight;
            DrawToggle(
                sendToShearingAreaRect,
                "ColonyManagerRedux.Livestock.SendToShearingArea".Translate(),
                "ColonyManagerRedux.Livestock.SendToShearingArea.Tip".Translate(),
                ref job.SendToShearingArea
            );

            if (job.SendToShearingArea)
            {
                var shearingAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
                AreaAllowedGUI.DoAllowedAreaSelectors(
                    ref shearingAreaRect,
                    ref job.ShearArea,
                    5,
                    Manager
                );
                pos.y += shearingAreaRect.height;
            }
        }

        var sendToTrainingAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        pos.y += ListEntryHeight;
        if (job.Training.AnyEnabled)
        {
            DrawToggle(
                sendToTrainingAreaRect,
                "ColonyManagerRedux.Livestock.SendToTrainingArea".Translate(),
                "ColonyManagerRedux.Livestock.SendToTrainingArea.Tip".Translate(),
                ref job.SendToTrainingArea
            );

            if (job.SendToTrainingArea)
            {
                var trainingAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
                AreaAllowedGUI.DoAllowedAreaSelectors(
                    ref trainingAreaRect,
                    ref job.TrainingArea,
                    5,
                    Manager
                );
                pos.y += trainingAreaRect.height;
            }

            var sendToTrainedAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
            pos.y += ListEntryHeight;
            DrawToggle(
                sendToTrainedAreaRect,
                "ColonyManagerRedux.Livestock.SendToTrainedArea".Translate(),
                "ColonyManagerRedux.Livestock.SendToTrainedArea.Tip".Translate(),
                ref job.SendToTrainedArea
            );
            if (job.SendToTrainedArea)
            {
                var trainedAreaRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
                AreaAllowedGUI.DoAllowedAreaSelectors(
                    ref trainedAreaRect,
                    ref job.TrainedArea,
                    5,
                    Manager
                );
                pos.y += trainedAreaRect.height;
            }
        }
        else
        {
            sendToTrainingAreaRect.xMin += Margin;
            IlyvionWidgets.Label(
                sendToTrainingAreaRect,
                "ColonyManagerRedux.Livestock.SendToTrainingArea".Translate(),
                "ColonyManagerRedux.Livestock.DisabledBecauseNoTrainingSet".Translate(),
                TextAnchor.MiddleLeft,
                color: Color.grey
            );
            pos.y += ListEntryHeight;
            sendToTrainingAreaRect.y += ListEntryHeight;
            IlyvionWidgets.Label(
                sendToTrainingAreaRect,
                "ColonyManagerRedux.Livestock.SendToTrainedArea".Translate(),
                "ColonyManagerRedux.Livestock.DisabledBecauseNoTrainingSet".Translate(),
                TextAnchor.MiddleLeft,
                color: Color.grey
            );
        }

        return pos.y - start.y;
    }

    private void DrawAvailableJobList(Rect rect)
    {
        Widgets.DrawMenuSection(rect);

        // set sizes
        var viewRect = rect.AtZero();

        viewRect.height = _availablePawnKinds.Count * LargeListEntryHeight;
        if (viewRect.height > rect.height)
        {
            viewRect.width -= GenUI.ScrollBarWidth;
        }

        Widgets.BeginScrollView(rect, ref _scrollPosition, viewRect);
        GUI.BeginGroup(viewRect);

        for (var i = 0; i < _availablePawnKinds.Count; i++)
        {
            var animalDef = _availablePawnKinds[i];

            // set up rect
            var row = new Rect(0f, LargeListEntryHeight * i, viewRect.width, LargeListEntryHeight);

            if (row.yMax < _scrollPosition.y || row.y > _scrollPosition.y + rect.height)
            {
                continue;
            }

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
            if (animalDef.RaceProps.hasMeat && CheckAndReportIfInvalidMeatDef(animalDef))
            {
                var estimatedMeatCount = animalDef.EstimatedMeatCount();
                Widgets.DefIcon(upperIconRect, animalDef.RaceProps.meatDef);
                TooltipHandler.TipRegion(
                    upperIconRect,
                    "ColonyManagerRedux.Livestock.Yields".Translate(
                        animalDef.RaceProps.meatDef.LabelCap,
                        estimatedMeatCount
                    )
                );

                upperIconRect.x -= Margin + SmallIconSize;
            }

            // draw leather yield icon
            if (animalDef.RaceProps.leatherDef != null)
            {
                var estimatedLeatherCount = animalDef.EstimatedLeatherCount();
                Widgets.DefIcon(upperIconRect, animalDef.RaceProps.leatherDef);
                TooltipHandler.TipRegion(
                    upperIconRect,
                    "ColonyManagerRedux.Livestock.Yields".Translate(
                        animalDef.RaceProps.leatherDef.LabelCap,
                        estimatedLeatherCount
                    )
                );

                upperIconRect.x -= Margin + SmallIconSize;
            }

            // draw milk yield icon
            var milkableProperties = animalDef.race.GetCompProperties<CompProperties_Milkable>();
            if (milkableProperties != null)
            {
                Widgets.DefIcon(upperIconRect, milkableProperties.milkDef);
                TooltipHandler.TipRegion(
                    upperIconRect,
                    "ColonyManagerRedux.Livestock.YieldsInterval".Translate(
                        milkableProperties.milkDef.LabelCap,
                        milkableProperties.milkAmount,
                        milkableProperties.milkIntervalDays
                    )
                );

                upperIconRect.x -= Margin + SmallIconSize;
            }

            // draw milk yield icon
            var shearableProperties = animalDef.race.GetCompProperties<CompProperties_Shearable>();
            if (shearableProperties != null)
            {
                Widgets.DefIcon(upperIconRect, shearableProperties.woolDef);
                TooltipHandler.TipRegion(
                    upperIconRect,
                    "ColonyManagerRedux.Livestock.YieldsInterval".Translate(
                        shearableProperties.woolDef.LabelCap,
                        shearableProperties.woolAmount,
                        shearableProperties.shearIntervalDays
                    )
                );

                upperIconRect.x -= Margin + SmallIconSize;
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
            TooltipHandler.TipRegion(
                lowerIconRect,
                "Trainability".Translate() + ": " + animalDef.RaceProps.trainability.LabelCap
            );

            // if it nuzzles, draw nuzzle icon
            if (animalDef.RaceProps.nuzzleMtbHours > 0f)
            {
                lowerIconRect.x -= Margin + SmallIconSize;

                var nuzzleInterval = Mathf
                    .RoundToInt(animalDef.RaceProps.nuzzleMtbHours * 2500f)
                    .ToStringTicksToPeriod();

                GUI.DrawTexture(lowerIconRect, Resources.Nuzzle);
                TooltipHandler.TipRegion(
                    lowerIconRect,
                    "NuzzleInterval".Translate() + ": " + nuzzleInterval
                );
            }

            // if it's aggressive, draw warning icon
            if (animalDef.RaceProps.manhunterOnTameFailChance >= 0.1)
            {
                lowerIconRect.x -= Margin + SmallIconSize;

                var color = GUI.color;
                GUI.color =
                    animalDef.RaceProps.manhunterOnTameFailChance > 0.25
                        ? Color.red
                        : Resources.Orange;

                GUI.DrawTexture(lowerIconRect, Resources.ClawIcon);
                GUI.color = color;
                TooltipHandler.TipRegion(
                    lowerIconRect,
                    I18n.Aggressiveness(animalDef.RaceProps.manhunterOnTameFailChance)
                );
            }

            if (ModsConfig.IdeologyActive)
            {
                var atLeastOneVenerated = false;
                var allVenerated = true;
                foreach (var item in Manager.map.mapPawns.FreeColonistsSpawned)
                {
                    var isVenerated =
                        item.Ideo != null && item.Ideo.IsVeneratedAnimal(animalDef.race);
                    atLeastOneVenerated |= isVenerated;
                    allVenerated &= isVenerated;
                }

                if (atLeastOneVenerated)
                {
                    lowerIconRect.x -= Margin + SmallIconSize;

                    var color = GUI.color;
                    GUI.color = allVenerated ? Color.red : Resources.Orange;

                    GUI.DrawTexture(lowerIconRect, Resources.Venerated);
                    GUI.color = color;

                    TooltipHandler.TipRegion(
                        lowerIconRect,
                        "ColonyManagerRedux.Livestock.VeneratedAnimal.Tip".Translate(
                            allVenerated
                                ? "ColonyManagerRedux.Misc.All".Translate()
                                : "ColonyManagerRedux.Misc.Some".Translate()
                        )
                    );
                }
            }

            var labelRect = row;

            if (ColonyManagerReduxMod.Settings.ShowInfoCardButtonsWherePossible)
            {
                // Info card button
                var infoRect = new Rect(
                    row.xMin + Margin,
                    row.yMin + ((row.height - SmallIconSize) / 2) - 2,
                    SmallIconSize,
                    SmallIconSize
                );
                _ = Widgets.InfoCardButton(infoRect, animalDef.race);

                labelRect.xMin += SmallIconSize;
            }

            // draw label
            var label =
                animalDef.LabelCap
                + "\n<i>"
                + "ColonyManagerRedux.Livestock.TameCount".Translate(
                    animalDef.GetTame(Manager, false).Count()
                )
                + ", "
                + "ColonyManagerRedux.Livestock.WildCount".Translate(
                    animalDef.GetWild(Manager).Count()
                )
                + ".</i>";
            IlyvionWidgets.Label(labelRect, label, TextAnchor.MiddleLeft, leftMargin: Margin * 2);

            // button
            if (Widgets.ButtonInvisible(labelRect))
            {
                _selectedAvailable = animalDef;
                Selected = MakeNewJob(animalDef);
            }
        }

        GUI.EndGroup();
        Widgets.EndScrollView();

        static bool CheckAndReportIfInvalidMeatDef(PawnKindDef def)
        {
            if (def.RaceProps.meatDef != null)
            {
                return true;
            }

            ColonyManagerReduxMod.Instance.LogWarning(
                $"The race of {def} (from {def.modContentPack.Name}) claims to have "
                    + "meat, but its meatDef is null. This race is probably missing "
                    + "having the property `hasMeat` set to `false`."
            );
            return false;
        }
    }

    private float DrawCullingSection(ManagerJob_Livestock job, Vector2 pos, float width)
    {
        var start = pos;

        var cullingStrategies = (LivestockCullingStrategy[])
            Enum.GetValues(typeof(LivestockCullingStrategy));

        var cellWidth = width / EntriesPerRow;

        for (var i = 0; i < cullingStrategies.Length; i++)
        {
            var skipCells = i == 0 ? 0 : 2;
            var cellRect = new Rect(
                pos.x + ((i + skipCells) % EntriesPerRow * (cellWidth + Margin)),
                pos.y + ((i + skipCells) / EntriesPerRow * ListEntryHeight),
                cellWidth,
                ListEntryHeight
            );
            var cullingStrategy = cullingStrategies[i];
            DrawToggle(
                cellRect,
                $"ColonyManagerRedux.Livestock.CullingStrategy.{cullingStrategy}".Translate(),
                $"ColonyManagerRedux.Livestock.CullingStrategy.{cullingStrategy}.Tip".Translate(),
                job.CullingStrategy == cullingStrategy,
                () =>
                {
                    job.CullingStrategy = cullingStrategy;
                    animalsTameTable?.SetDirty();
                },
                () => { }
            );
        }
        pos.y += ListEntryHeight * ((cullingStrategies.Length / EntriesPerRow) + 1);

        return pos.y - start.y;
    }

    private float DrawCullingExceptionsSection(ManagerJob_Livestock job, Vector2 pos, float width)
    {
        var start = pos;

        if (job.CullExcess)
        {
            var cellWidth = (width - (Margin * 2)) / 3f;
            var cullingOptionRect = new Rect(pos.x, pos.y, cellWidth, ListEntryHeight);

            DrawToggle(
                cullingOptionRect,
                "ColonyManagerRedux.Livestock.CullTrained".Translate(),
                "ColonyManagerRedux.Livestock.CullTrained.Tip".Translate(),
                ref job.CullTrained,
                font: GameFont.Tiny,
                wrap: false
            );
            cullingOptionRect.x += cellWidth + Margin;

            DrawToggle(
                cullingOptionRect,
                "ColonyManagerRedux.Livestock.CullPregnant".Translate(),
                "ColonyManagerRedux.Livestock.CullPregnant.Tip".Translate(),
                ref job.CullPregnant,
                font: GameFont.Tiny,
                wrap: false
            );
            cullingOptionRect.x += cellWidth + Margin;

            DrawToggle(
                cullingOptionRect,
                "ColonyManagerRedux.Livestock.CullBonded".Translate(),
                "ColonyManagerRedux.Livestock.CullBonded.Tip".Translate(),
                ref job.CullBonded,
                font: GameFont.Tiny,
                wrap: false
            );

            pos.y += ListEntryHeight;
            cullingOptionRect = new Rect(pos.x, pos.y, cellWidth, ListEntryHeight);

            DrawToggle(
                cullingOptionRect,
                "ColonyManagerRedux.Livestock.AvoidCullingNamed".Translate(),
                "ColonyManagerRedux.Livestock.AvoidCullingNamed.Tip".Translate(),
                ref job.AvoidCullingNamed,
                font: GameFont.Tiny,
                wrap: false
            );

            pos.y += ListEntryHeight;

            DrawToggle(
                ref pos,
                width,
                "ColonyManagerRedux.Livestock.AvoidCullingMilkable".Translate(),
                "ColonyManagerRedux.Livestock.AvoidCullingMilkable.Tip".Translate(),
                ref job.AvoidCullingMilkable
            );

            if (job.AvoidCullingMilkable)
            {
                IlyvionWidgets.Label(
                    new Rect(pos.x + Margin, pos.y, width, ListEntryHeight),
                    "ColonyManagerRedux.Livestock.AvoidCullingMilkableThreshold".Translate(
                        job.AvoidCullingMilkableThreshold.ToString(
                            "0%",
                            CultureInfo.InvariantCulture
                        )
                    )
                );
                pos.y += ListEntryHeight;

                var sliderRect = new Rect(pos.x, pos.y, width, SliderHeight);
                job.AvoidCullingMilkableThreshold = Widgets.HorizontalSlider(
                    sliderRect,
                    job.AvoidCullingMilkableThreshold,
                    0,
                    1
                );
                pos.y += SliderHeight;
            }

            DrawToggle(
                ref pos,
                width,
                "ColonyManagerRedux.Livestock.AvoidCullingShearable".Translate(),
                "ColonyManagerRedux.Livestock.AvoidCullingShearable.Tip".Translate(),
                ref job.AvoidCullingShearable
            );

            if (job.AvoidCullingShearable)
            {
                IlyvionWidgets.Label(
                    new Rect(pos.x + Margin, pos.y, width, ListEntryHeight),
                    "ColonyManagerRedux.Livestock.AvoidCullingShearableThreshold".Translate(
                        job.AvoidCullingShearableThreshold.ToString(
                            "0%",
                            CultureInfo.InvariantCulture
                        )
                    )
                );
                pos.y += ListEntryHeight;

                var sliderRect = new Rect(pos.x, pos.y, width, SliderHeight);
                job.AvoidCullingShearableThreshold = Widgets.HorizontalSlider(
                    sliderRect,
                    job.AvoidCullingShearableThreshold,
                    0,
                    1
                );
                pos.y += SliderHeight;
            }
        }

        return pos.y - start.y;
    }

    public override void DrawLocalListEntry(
        ManagerJob job,
        ref Vector2 position,
        float width,
        DrawLocalListEntryParameters? parameters
    )
    {
        parameters = new()
        {
            ShowOrdering = false,
            StatusHeight = (4 * Trigger_PawnKind.PawnKindProgressBarHeight) + (3 * Margin / 2),
        };

        base.DrawLocalListEntry(job, ref position, width, parameters);
    }

    private float DrawFollowSection(ManagerJob_Livestock job, Vector2 pos, float width)
    {
        var start = pos;
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        var buttonRect = new Rect(
            rowRect.xMax * 3 / 4,
            0f,
            width * 1 / 4,
            ListEntryHeight * 2 / 3
        ).CenteredOnYIn(rowRect);

        // master selection
        var report =
            job.TriggerPawnKind.pawnKind != null
                ? CanBeTrained(job.TriggerPawnKind.pawnKind, TrainableDefOf.Obedience, out var _)
                : (AcceptanceReport)false;
        if (report.Accepted)
        {
            IlyvionWidgets.Label(
                rowRect,
                "ColonyManagerRedux.Livestock.MasterDefault".Translate(),
                "ColonyManagerRedux.Livestock.MasterDefault.Tip".Translate(),
                TextAnchor.MiddleLeft,
                leftMargin: Margin
            );
        }
        else
        {
            IlyvionWidgets.Label(
                rowRect,
                "ColonyManagerRedux.Livestock.MasterDefault".Translate(),
                report.Reason,
                TextAnchor.MiddleLeft,
                leftMargin: Margin,
                color: Color.gray
            );
        }
        if (
            IlyvionWidgets.DisableableButtonText(
                buttonRect,
                GetMasterLabel(job),
                enabled: report.Accepted
            )
        )
        {
            var options = new List<FloatMenuOption>();

            // modes
            foreach (
                var mode in Utilities_Livestock.MasterModeArray.Where(mm =>
                    (mm & MasterMode.All) == mm
                )
            )
            {
                options.Add(
                    new FloatMenuOption(
                        $"ColonyManagerRedux.Livestock.MasterMode.{mode}".Translate(),
                        () => job.Masters = mode
                    )
                );
            }

            // specific pawns
            if (job.TriggerPawnKind.pawnKind != null)
            {
                foreach (
                    var pawn in job.TriggerPawnKind.pawnKind.GetMasterOptions(
                        Manager,
                        MasterMode.All
                    )
                )
                {
                    options.Add(
                        new FloatMenuOption(
                            "ColonyManagerRedux.Livestock.Master".Translate(
                                pawn.LabelShort,
                                pawn.skills.AverageOfRelevantSkillsFor(WorkTypeDefOf.Handling)
                            ),
                            () =>
                            {
                                job.Master = pawn;
                                job.Masters = MasterMode.Specific;
                            }
                        )
                    );
                }
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        // respect bonds?
        rowRect.y += ListEntryHeight;
        if (!report.Accepted)
        {
            IlyvionWidgets.Label(
                rowRect,
                "ColonyManagerRedux.Livestock.RespectBonds".Translate(),
                report.Reason,
                color: Color.grey,
                leftMargin: Margin
            );
        }
        else if (job.Masters is not MasterMode.Manual and not MasterMode.Specific)
        {
            DrawToggle(
                rowRect,
                "ColonyManagerRedux.Livestock.RespectBonds".Translate(),
                "ColonyManagerRedux.Livestock.RespectBonds.Tip".Translate(),
                ref job.RespectBonds
            );
        }
        else
        {
            IlyvionWidgets.Label(
                rowRect,
                "ColonyManagerRedux.Livestock.RespectBonds".Translate(),
                "ColonyManagerRedux.Livestock.RespectBonds.DisabledBecauseMastersNotSet".Translate(),
                color: Color.grey,
                leftMargin: Margin
            );
        }

        // default follow
        rowRect.y += ListEntryHeight;
        if (report.Accepted)
        {
            DrawToggle(
                rowRect,
                "ColonyManagerRedux.Livestock.Follow".Translate(),
                "ColonyManagerRedux.Livestock.Follow.Tip".Translate(),
                ref job.SetFollow
            );
        }
        else
        {
            IlyvionWidgets.Label(
                rowRect,
                "ColonyManagerRedux.Livestock.Follow".Translate(),
                report.Reason,
                color: Color.grey,
                leftMargin: Margin
            );
        }

        if (job.SetFollow && report.Accepted)
        {
            rowRect.y += ListEntryHeight;
            var followRect = rowRect;
            followRect.width /= 2f;
            DrawToggle(
                followRect,
                "ColonyManagerRedux.Livestock.FollowDrafted".Translate(),
                "ColonyManagerRedux.Livestock.FollowDrafted.Tip".Translate(),
                ref job.FollowDrafted,
                font: GameFont.Tiny
            );
            followRect.x += followRect.width;
            DrawToggle(
                followRect,
                "ColonyManagerRedux.Livestock.FollowFieldwork".Translate(),
                "ColonyManagerRedux.Livestock.FollowFieldwork.Tip".Translate(),
                ref job.FollowFieldwork,
                font: GameFont.Tiny
            );
        }

        // follow when training
        rowRect.y += ListEntryHeight;
        if (report.Accepted)
        {
            TooltipHandler.TipRegion(
                rowRect,
                "ColonyManagerRedux.Livestock.FollowTraining.Tip".Translate()
            );
            DrawToggle(
                rowRect,
                "ColonyManagerRedux.Livestock.FollowTraining".Translate(),
                "ColonyManagerRedux.Livestock.FollowTraining.Tip".Translate(),
                ref job.FollowTraining
            );
        }
        else
        {
            IlyvionWidgets.Label(
                rowRect,
                "ColonyManagerRedux.Livestock.FollowTraining".Translate(),
                report.Reason,
                color: Color.grey,
                leftMargin: Margin
            );
        }
        // trainer selection
        if (job.FollowTraining && report.Accepted)
        {
            rowRect.y += ListEntryHeight;
            IlyvionWidgets.Label(
                rowRect,
                "ColonyManagerRedux.Livestock.MasterTraining".Translate(),
                "ColonyManagerRedux.Livestock.MasterTraining.Tip".Translate(),
                TextAnchor.MiddleLeft,
                leftMargin: Margin
            );

            buttonRect = buttonRect.CenteredOnYIn(rowRect);
            if (Widgets.ButtonText(buttonRect, GetTrainerLabel(job)))
            {
                var options = new List<FloatMenuOption>();

                // modes
                foreach (
                    var mode in Utilities_Livestock.MasterModeArray.Where(mm =>
                        (mm & MasterMode.Trainers) == mm
                    )
                )
                {
                    options.Add(
                        new FloatMenuOption(
                            $"ColonyManagerRedux.Livestock.MasterMode.{mode}".Translate(),
                            () => job.Trainers = mode
                        )
                    );
                }

                // specific pawns
                if (job.TriggerPawnKind.pawnKind != null)
                {
                    foreach (
                        var pawn in job.TriggerPawnKind.pawnKind.GetTrainers(
                            Manager,
                            MasterMode.Trainers
                        )
                    )
                    {
                        options.Add(
                            new FloatMenuOption(
                                "ColonyManagerRedux.Livestock.Master".Translate(
                                    pawn.LabelShort,
                                    pawn.skills.AverageOfRelevantSkillsFor(WorkTypeDefOf.Handling)
                                ),
                                () =>
                                {
                                    job.Trainer = pawn;
                                    job.Trainers = MasterMode.Specific;
                                }
                            )
                        );
                    }
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        return rowRect.yMax - start.y;
    }

    private float DrawTamingSection(ManagerJob_Livestock job, Vector2 pos, float width)
    {
        var start = pos;
        DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.Livestock.TameMore".Translate(),
            "ColonyManagerRedux.Livestock.TameMore.Tip".Translate(),
            ref job.TryTameMore
        );

        if (job.TryTameMore)
        {
            AreaAllowedGUI.DoAllowedAreaSelectors(ref pos, width, ref job.TameArea, 5, Manager);
            DrawToggle(
                ref pos,
                width,
                "ColonyManagerRedux.InvertArea".Translate(),
                "ColonyManagerRedux.InvertArea.Tip".Translate(),
                ref job.InvertTameArea
            );

            DrawToggle(
                ref pos,
                width,
                "ColonyManagerRedux.Livestock.TamePastTargets".Translate(),
                "ColonyManagerRedux.Livestock.TamePastTargets.Tip".Translate(),
                ref job.TamePastTargets
            );
            DrawReachabilityToggle(ref pos, width, ref job.ShouldCheckReachable);
            DrawToggle(
                ref pos,
                width,
                "ColonyManagerRedux.Threshold.PathBasedDistance".Translate(),
                "ColonyManagerRedux.Threshold.PathBasedDistance.Tip".Translate(),
                ref job.UsePathBasedDistance,
                true
            );
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
                    heights[y]
                );
            }
        }

        // headers
        IlyvionWidgets.Label(
            countRects[0, 1],
            Gender.Female.ToString(),
            TextAnchor.LowerCenter,
            GameFont.Tiny
        );
        IlyvionWidgets.Label(
            countRects[0, 2],
            Gender.Male.ToString(),
            TextAnchor.LowerCenter,
            GameFont.Tiny
        );
        IlyvionWidgets.Label(
            countRects[1, 0],
            "ColonyManagerRedux.Livestock.Adult".Translate() + " ",
            TextAnchor.MiddleRight,
            GameFont.Tiny
        );
        IlyvionWidgets.Label(
            countRects[2, 0],
            "ColonyManagerRedux.Livestock.Juvenile".Translate() + " ",
            TextAnchor.MiddleRight,
            GameFont.Tiny
        );

        // fields
        DoCountField(job, countRects[1, 1], AgeAndSex.AdultFemale);
        DoCountField(job, countRects[1, 2], AgeAndSex.AdultMale);
        DoCountField(job, countRects[2, 1], AgeAndSex.JuvenileFemale);
        DoCountField(job, countRects[2, 2], AgeAndSex.JuvenileMale);

        return 3 * ListEntryHeight;
    }

    private float DrawTrainingSection(ManagerJob_Livestock job, Vector2 pos, float width)
    {
        var allRowsCount = (int)Math.Ceiling((double)job.Training.Count / EntriesPerRow);
        var trainingRect = new Rect(pos.x, pos.y, width, ListEntryHeight * allRowsCount);
        var visibleJobsRowCount = (int)
            Math.Ceiling(
                (double)DrawTrainingSelector(job, trainingRect, allRowsCount) / EntriesPerRow
            );
        var height = ListEntryHeight * visibleJobsRowCount;

        var unassignTrainingRect = new Rect(pos.x, pos.y + height, width, ListEntryHeight);
        DrawToggle(
            unassignTrainingRect,
            "ColonyManagerRedux.Livestock.UnassignTraining".Translate(),
            "ColonyManagerRedux.Livestock.UnassignTraining.Tip".Translate(),
            ref job.Training.UnassignTraining
        );
        height += ListEntryHeight;

        var trainYoungRect = new Rect(pos.x, pos.y + height, width, ListEntryHeight);
        DrawToggle(
            trainYoungRect,
            "ColonyManagerRedux.Livestock.TrainYoung".Translate(),
            "ColonyManagerRedux.Livestock.TrainYoung.Tip".Translate(),
            ref job.Training.TrainYoung
        );
        height += ListEntryHeight;

        return height;
    }

    private void DrawAnimalTables(Rect animalsColumnRect)
    {
        animalsColumnRect.height /= 2;
        var animalsColumnRect2 = new Rect(animalsColumnRect) { y = animalsColumnRect.height };
        animalsColumnRect2.yMax -= Margin;
        animalsColumnRect.yMin += Margin;
        var headerRect = new Rect(
            animalsColumnRect.x,
            animalsColumnRect.y,
            animalsColumnRect.width,
            SectionHeaderHeight
        ).RoundToInt();
        IlyvionWidgets.Label(
            headerRect,
            "ColonyManagerRedux.Livestock.AnimalsHeader"
                .Translate(
                    "ColonyManagerRedux.Livestock.Tame".Translate(),
                    SelectedJob!.TriggerPawnKind.pawnKind?.GetLabelPlural()
                        ?? SelectedJob!.TriggerPawnKind.ExpectedPawnKindName
                )
                .CapitalizeFirst(),
            TextAnchor.LowerLeft,
            GameFont.Tiny,
            leftMargin: 3 * Margin
        );
        animalsColumnRect.yMin += SectionHeaderHeight;
        animalsColumnRect.yMax -= Margin;
        GUI.DrawTexture(animalsColumnRect, Resources.SlightlyDarkBackground);
        GUI.BeginGroup(animalsColumnRect);
        DrawTamedAnimalTable(animalsColumnRect.AtZero());
        GUI.EndGroup();

        headerRect = new Rect(
            animalsColumnRect2.x,
            animalsColumnRect2.y,
            animalsColumnRect2.width,
            SectionHeaderHeight
        ).RoundToInt();
        IlyvionWidgets.Label(
            headerRect,
            "ColonyManagerRedux.Livestock.AnimalsHeader"
                .Translate(
                    "ColonyManagerRedux.Livestock.Wild".Translate(),
                    SelectedJob.TriggerPawnKind.pawnKind?.GetLabelPlural()
                        ?? SelectedJob!.TriggerPawnKind.ExpectedPawnKindName
                )
                .CapitalizeFirst(),
            TextAnchor.LowerLeft,
            GameFont.Tiny,
            leftMargin: 3 * Margin
        );
        animalsColumnRect2.yMin += SectionHeaderHeight;
        GUI.DrawTexture(animalsColumnRect2, Resources.SlightlyDarkBackground);
        GUI.BeginGroup(animalsColumnRect2);
        DrawWildAnimalTable(animalsColumnRect2.AtZero());
        GUI.EndGroup();
    }

    private void DrawTamedAnimalTable(Rect rect) =>
        DrawAnimalTable(
            rect,
            ref animalsTameTable,
            "ColonyManagerRedux.Livestock.Tame".Translate(),
            p => p.GetTame(Manager)
        );

    private void DrawWildAnimalTable(Rect rect) =>
        DrawAnimalTable(
            rect,
            ref animalsWildTable,
            "ColonyManagerRedux.Livestock.Wild".Translate(),
            p => p.GetWild(Manager)
        );

    private void DrawAnimalTable(
        Rect rect,
        ref PawnTable? pawnTable,
        string type,
        Func<PawnKindDef, IEnumerable<Pawn>?> animalGetter
    )
    {
        if (pawnTable == null)
        {
            pawnTable = CreateAnimalsTable(
                () =>
                {
                    var pawnKind = SelectedJob!.TriggerPawnKind.pawnKind;
                    return pawnKind != null ? animalGetter(pawnKind) ?? [] : [];
                },
                () => SelectedJob
            );
            pawnTable.SetFixedSize(new(rect.width, rect.height));
        }

        pawnTable.PawnTableOnGUI(Vector2.zero);
        if (pawnTable.PawnsListForReading.Count == 0)
        {
            var pawnKind = SelectedJob!.TriggerPawnKind.pawnKind;
            IlyvionWidgets.Label(
                rect,
                "ColonyManagerRedux.Livestock.NoAnimals".Translate(
                    type,
                    pawnKind?.GetLabelPlural() ?? SelectedJob!.TriggerPawnKind.ExpectedPawnKindName
                ),
                TextAnchor.MiddleCenter,
                color: Color.grey
            );
        }
    }

    protected override void Refresh()
    {
        // currently managed
        var currentJobs = Manager.JobTracker.JobsOfType<ManagerJob_Livestock>().ToList();

        // concatenate lists of animals on biome and animals in colony.
        _availablePawnKinds.Clear();
        _availablePawnKinds.AddRange(
            Manager
                .map.Biome.AllWildAnimals.Concat(
                    Manager
                        .map.mapPawns.AllPawns.Where(p => p.RaceProps.Animal)
                        .Select(p => p.kindDef)
                )
                // get distinct pawnkinds from the merges
                .Distinct()
                // remove already managed pawnkinds
                .Where(pk => !currentJobs.Select(job => job.TriggerPawnKind.pawnKind).Contains(pk))
                // order by label
                .OrderBy(def => def.LabelCap.RawText)
        );
    }
}
