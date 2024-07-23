// ManagerTab_Mining.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Text;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[HotSwappable]
internal sealed class ManagerTab_Mining(Manager manager) : ManagerTab(manager)
{
    public static HashSet<ThingDef> _metals = new(DefDatabase<ThingDef>.AllDefsListForReading
        .Where(td => td.IsStuff && td.stuffProps.categories.Contains(StuffCategoryDefOf.Metallic)));

    private float _jobListHeight;
    private Vector2 _jobListScrollPosition = Vector2.zero;

    public ManagerJob_Mining SelectedMiningJob
    {
        get => (ManagerJob_Mining)Selected!;
    }

    public override string Label => "ColonyManagerRedux.ManagerMining".Translate();

    public static string GetMineralTooltip(ThingDef mineral)
    {
        var sb = new StringBuilder();
        sb.Append(mineral.description);

        var resource = mineral.building?.mineableThing;
        if (resource != null && mineral.building != null)
        {
            sb.Append("\n\n");
            var yield = string.Empty;
            // stone chunks
            if (resource.IsChunk())
            {
                yield = $"\n{resource.label}" +
                        $"\n - {I18n.ChanceToDrop(mineral.building.mineableDropChance)}" +
                        $"\n - {resource.butcherProducts.Select(tc => tc.Label).ToCommaList()}";
            }
            // other
            else
            {
                yield = $"{resource.label} x{mineral.building.mineableYield * Find.Storyteller.difficulty.mineYieldFactor}" +
                        $"\n - {I18n.ChanceToDrop(mineral.building.mineableDropChance)}";
            }

            sb.Append(I18n.YieldOne(yield));
        }

        return sb.ToString();
    }

    public override void DoWindowContents(Rect canvas)
    {
        var jobListRect = new Rect(
            0,
            0,
            DefaultLeftRowSize,
            canvas.height);
        var jobDetailsRect = new Rect(
            jobListRect.xMax + Margin,
            0,
            canvas.width - jobListRect.width - Margin,
            canvas.height);

        DoJobList(jobListRect);
        if (Selected != null)
        {
            DoJobDetails(jobDetailsRect);
        }
    }

    public override string GetSubLabel(ManagerJob job)
    {
        var subLabel = base.GetSubLabel(job);
        ManagerJob_Mining miningJob = (ManagerJob_Mining)job;
        if (miningJob.DeconstructBuildings)
        {
            subLabel += "\n\n" + string.Join(", ", miningJob.AllowedBuildings
                .Select(pk => pk.LabelCap.Resolve()));
        }
        return subLabel;
    }

    public float DrawAllowedBuildings(Vector2 pos, float width)
    {
        var start = pos;

        var allowedBuildings = SelectedMiningJob.AllowedBuildings;
        var allBuildings = SelectedMiningJob.AllDeconstructibleBuildings;

        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        foreach (var building in allBuildings)
        {
            Utilities.DrawToggle(rowRect, building.LabelCap, building.description, allowedBuildings.Contains(building),
                () => SelectedMiningJob.SetBuildingAllowed(building, !allowedBuildings.Contains(building)));
            rowRect.y += ListEntryHeight;
        }

        return rowRect.yMin - start.y;
    }


    public float DrawAllowedBuildingsShortcuts(Vector2 pos, float width)
    {
        var start = pos;

        // list of keys in allowed animals list (all animals in biome + visible animals on map)
        var allowedBuildings = SelectedMiningJob.AllowedBuildings;
        var allBuildings = SelectedMiningJob.AllDeconstructibleBuildings;

        // toggle all
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        DrawShortcutToggle(allBuildings, allowedBuildings, (b, v) => SelectedMiningJob.SetBuildingAllowed(b, v), rowRect, "ManagerAll", null);

        return rowRect.yMax - start.y;
    }

    public float DrawAllowedMinerals(Vector2 pos, float width)
    {
        var start = pos;
        // list of keys in allowed animals list (all animals in biome + visible animals on map)
        var allowedMinerals = SelectedMiningJob.AllowedMinerals;
        var allMinerals = SelectedMiningJob.AllMinerals;

        // toggle for each animal
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        foreach (var mineral in allMinerals)
        {
            // draw the toggle
            Utilities.DrawToggle(rowRect, mineral.LabelCap,
                new TipSignal(() => GetMineralTooltip(mineral), mineral.GetHashCode()),
                allowedMinerals.Contains(mineral),
                () => SelectedMiningJob.SetAllowMineral(mineral, !allowedMinerals.Contains(mineral)));
            rowRect.y += ListEntryHeight;
        }

        return rowRect.yMin - start.y;
    }

    public float DrawAllowedMineralsShortcuts(Vector2 pos, float width)
    {
        var start = pos;

        // list of keys in allowed animals list (all animals in biome + visible animals on map)
        var allowedMinerals = SelectedMiningJob.AllowedMinerals;
        var allMinerals = SelectedMiningJob.AllMinerals;

        var rowRect = new Rect(
            pos.x,
            pos.y,
            width,
            ListEntryHeight);

        // toggle all
        DrawShortcutToggle(allMinerals, allowedMinerals, (m, v) => SelectedMiningJob.SetAllowMineral(m, v), rowRect, "ManagerAll", null);

        // toggle stone
        rowRect.y += ListEntryHeight;
        var stone = allMinerals.Where(m => !m.building.isResourceRock).ToList();
        DrawShortcutToggle(stone, allowedMinerals, (m, v) => SelectedMiningJob.SetAllowMineral(m, v), rowRect, "ManagerMining.Stone", "ManagerMining.Stone.Tip");

        // toggle metal
        rowRect.y += ListEntryHeight;
        var metal = allMinerals
            .Where(m => m.building.isResourceRock && IsMetal(m.building.mineableThing))
            .ToList();
        DrawShortcutToggle(metal, allowedMinerals, (m, v) => SelectedMiningJob.SetAllowMineral(m, v), rowRect, "ManagerMining.Metal", "ManagerMining.Metal.Tip");

        // toggle precious
        rowRect.y += ListEntryHeight;
        var precious = allMinerals
            .Where(m => m.building.isResourceRock && (m.building.mineableThing?.smallVolume ?? false))
            .ToList();
        DrawShortcutToggle(precious, allowedMinerals, (m, v) => SelectedMiningJob.SetAllowMineral(m, v), rowRect, "ManagerMining.Precious", "ManagerMining.Precious.Tip");

        return rowRect.yMax - start.y;
    }

    public float DrawDeconstructBuildings(Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(rowRect,
                              "ColonyManagerRedux.ManagerMining.DeconstructBuildings".Translate(),
                              "ColonyManagerRedux.ManagerMining.DeconstructBuildings.Tip".Translate(),
                              ref SelectedMiningJob.DeconstructBuildings);
        return ListEntryHeight;
    }

    public float DrawMiningArea(Vector2 pos, float width)
    {
        var start = pos;
        AreaAllowedGUI.DoAllowedAreaSelectors(ref pos, width, ref SelectedMiningJob.MiningArea, manager);
        return pos.y - start.y;
    }

    public float DrawRoofRoomChecks(Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.ManagerMining.CheckRoofSupport".Translate(),
            "ColonyManagerRedux.ManagerMining.CheckRoofSupport.Tip".Translate(),
            ref SelectedMiningJob.CheckRoofSupport);

        rowRect.y += ListEntryHeight;
        if (SelectedMiningJob.CheckRoofSupport)
        {
            Utilities.DrawToggle(rowRect,
                "ColonyManagerRedux.ManagerMining.CheckRoofSupportAdvanced".Translate(),
                "ColonyManagerRedux.ManagerMining.CheckRoofSupportAdvanced.Tip".Translate(),
                ref SelectedMiningJob.CheckRoofSupportAdvanced, true);
        }
        else
        {
            Widgets_Labels.Label(rowRect,
                "ColonyManagerRedux.ManagerMining.CheckRoofSupportAdvanced".Translate(),
                "ColonyManagerRedux.ManagerMining.CheckRoofSupportAdvanced.Disabled.Tip".Translate(),
                TextAnchor.MiddleLeft, margin: Margin,
                color: Color.grey);
        }

        rowRect.y += ListEntryHeight;
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.ManagerMining.CheckRoomDivision".Translate(),
            "ColonyManagerRedux.ManagerMining.CheckRoomDivision.Tip".Translate(),
            ref SelectedMiningJob.CheckRoomDivision, true);

        return rowRect.yMax - pos.y;
    }

    public float DrawThresholdSettings(Vector2 pos, float width)
    {
        var start = pos;

        var currentCount = SelectedMiningJob.TriggerThreshold.CurrentCount;
        var chunkCount = SelectedMiningJob.GetCountInChunks();
        var designatedCount = SelectedMiningJob.GetCountInDesignations();
        var targetCount = SelectedMiningJob.TriggerThreshold.TargetCount;

        SelectedMiningJob.TriggerThreshold.DrawTriggerConfig(ref pos, width, ListEntryHeight,
                                             "ColonyManagerRedux.ManagerMining.TargetCount".Translate(
                                                 currentCount, chunkCount, designatedCount, targetCount),
                                             "ColonyManagerRedux.ManagerMining.TargetCount.Tip".Translate(
                                                 currentCount, chunkCount, designatedCount, targetCount),
                                             SelectedMiningJob.Designations,
                                             delegate { SelectedMiningJob.Sync = Utilities.SyncDirection.FilterToAllowed; },
                                             SelectedMiningJob.DesignationLabel);

        Utilities.DrawToggle(ref pos, width,
                              "ColonyManagerRedux.ManagerMining.SyncFilterAndAllowed".Translate(),
                              "ColonyManagerRedux.ManagerMining.SyncFilterAndAllowed.Tip".Translate(),
                              ref SelectedMiningJob.SyncFilterAndAllowed);
        Utilities.DrawReachabilityToggle(ref pos, width, ref SelectedMiningJob.ShouldCheckReachable);
        Utilities.DrawToggle(ref pos, width,
                              "ColonyManagerRedux.ManagerPathBasedDistance".Translate(),
                              "ColonyManagerRedux.ManagerPathBasedDistance.Tip".Translate(),
                              ref SelectedMiningJob.UsePathBasedDistance,
                              true);

        return pos.y - start.y;
    }

    public static bool IsMetal(ThingDef def)
    {
        return def != null && _metals.Contains(def);
    }

    public override void PreOpen()
    {
        Refresh();
    }

    public void Refresh()
    {
        // update pawnkind options
        foreach (var job in manager.JobTracker.JobsOfType<ManagerJob_Mining>())
        {
            job.RefreshAllBuildingsAndMinerals();
        }

        SelectedMiningJob?.RefreshAllBuildingsAndMinerals();
    }

    private void DoJobDetails(Rect rect)
    {
        Widgets.DrawMenuSection(rect);

        // rects
        var optionsColumnRect = new Rect(
            rect.xMin,
            rect.yMin,
            rect.width * 3 / 5f,
            rect.height - Margin - ButtonSize.y);
        var mineralsColumnRect = new Rect(
            optionsColumnRect.xMax,
            rect.yMin,
            rect.width * 2 / 5f,
            rect.height - Margin - ButtonSize.y);
        var buttonRect = new Rect(
            rect.xMax - ButtonSize.x,
            rect.yMax - ButtonSize.y,
            ButtonSize.x - Margin,
            ButtonSize.y - Margin);
        var debugButtonRect = new Rect(buttonRect);
        debugButtonRect.x -= ButtonSize.x + Margin;


        // options
        Widgets_Section.BeginSectionColumn(optionsColumnRect, "Mining.Options", out Vector2 position, out float width);
        Widgets_Section.Section(ref position, width, DrawThresholdSettings, "ColonyManagerRedux.ManagerThreshold".Translate());
        Widgets_Section.Section(ref position, width, DrawDeconstructBuildings);
        Widgets_Section.Section(ref position, width, DrawMiningArea, "ColonyManagerRedux.ManagerMining.MiningArea".Translate());
        Widgets_Section.Section(ref position, width, DrawRoofRoomChecks, "ColonyManagerRedux.ManagerMining.HealthAndSafety".Translate());
        Widgets_Section.EndSectionColumn("Mining.Options", position);

        // minerals
        Widgets_Section.BeginSectionColumn(mineralsColumnRect, "Mining.Minerals", out position, out width);
        var refreshRect = new Rect(
            position.x + width - SmallIconSize - 2 * Margin,
            position.y + Margin,
            SmallIconSize,
            SmallIconSize);
        if (Widgets.ButtonImage(refreshRect, Resources.Refresh, Color.grey))
        {
            SelectedMiningJob.RefreshAllBuildingsAndMinerals();
        }

        Widgets_Section.Section(ref position, width, DrawAllowedMineralsShortcuts,
                                 "ColonyManagerRedux.ManagerMining.AllowedMinerals".Translate());
        Widgets_Section.Section(ref position, width, DrawAllowedMinerals);
        Widgets_Section.Section(ref position, width, DrawAllowedBuildingsShortcuts,
                                 "ColonyManagerRedux.ManagerMining.AllowedBuildings".Translate());
        Widgets_Section.Section(ref position, width, DrawAllowedBuildings);
        Widgets_Section.EndSectionColumn("Mining.Minerals", position);

        if (Prefs.DevMode && Widgets.ButtonText(debugButtonRect, "DEV: Debug Options"))
        {
            Find.WindowStack.Add(new Dialog_MiningDebugOptions(SelectedMiningJob));
        }

        if (!SelectedMiningJob.IsManaged)
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.ManagerManage".Translate()))
            {
                // activate job, add it to the stack
                SelectedMiningJob.IsManaged = true;
                manager.JobTracker.Add(SelectedMiningJob);

                // refresh source list
                Refresh();
            }
        }
        else
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.ManagerDelete".Translate()))
            {
                // inactivate job, remove from the stack.
                manager.JobTracker.Delete(SelectedMiningJob);

                // remove content from UI
                Selected = MakeNewJob();

                // refresh source list
                Refresh();
            }
        }
    }

    private void DoJobList(Rect rect)
    {
        Widgets.DrawMenuSection(rect);

        // content
        var height = _jobListHeight;
        var scrollView = new Rect(0f, 0f, rect.width, height);
        if (height > rect.height)
        {
            scrollView.width -= ScrollbarWidth;
        }

        Widgets.BeginScrollView(rect, ref _jobListScrollPosition, scrollView);
        var scrollContent = scrollView;

        GUI.BeginGroup(scrollContent);
        var cur = Vector2.zero;
        var i = 0;

        foreach (var job in manager.JobTracker.JobsOfType<ManagerJob_Mining>())
        {
            var row = new Rect(0f, cur.y, scrollContent.width, LargeListEntryHeight);
            Widgets.DrawHighlightIfMouseover(row);
            if (SelectedMiningJob == job)
            {
                Widgets.DrawHighlightSelected(row);
            }

            if (i++ % 2 == 1)
            {
                Widgets.DrawAltRect(row);
            }

            var jobRect = row;

            if (DrawOrderButtons(new Rect(row.xMax - 50f, row.yMin, 50f, 50f), manager, job))
            {
                Refresh();
            }

            jobRect.width -= 50f;

            DrawListEntry(job, jobRect, ListEntryDrawMode.Local);
            if (Widgets.ButtonInvisible(jobRect))
            {
                Selected = job;
            }

            cur.y += LargeListEntryHeight;
        }

        // row for new job.
        var newRect = new Rect(0f, cur.y, scrollContent.width, LargeListEntryHeight);
        Widgets.DrawHighlightIfMouseover(newRect);

        if (i++ % 2 == 1)
        {
            Widgets.DrawAltRect(newRect);
        }

        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(newRect, "<" + "ColonyManagerRedux.ManagerMining.NewJob".Translate() + ">");
        Text.Anchor = TextAnchor.UpperLeft;

        if (Widgets.ButtonInvisible(newRect))
        {
            Selected = MakeNewJob();
        }

        TooltipHandler.TipRegion(newRect, "ColonyManagerRedux.ManagerMining.NewJob.Tip".Translate());

        cur.y += LargeListEntryHeight;

        _jobListHeight = cur.y;
        GUI.EndGroup();
        Widgets.EndScrollView();
    }
}
