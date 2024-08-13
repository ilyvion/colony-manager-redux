// ManagerTab_Mining.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Text;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed partial class ManagerTab_Mining(Manager manager) : ManagerTab<ManagerJob_Mining>(manager)
{
    public static HashSet<ThingDef> _metals = new(DefDatabase<ThingDef>.AllDefsListForReading
        .Where(td => td.IsStuff && td.stuffProps.categories.Contains(StuffCategoryDefOf.Metallic)));

    public ManagerJob_Mining SelectedMiningJob => SelectedJob!;

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
                        $"\n - {"ColonyManagerRedux.Info.ChanceToDrop".Translate(mineral.building.mineableDropChance.ToStringPercent())}" +
                        $"\n - {resource.butcherProducts.Select(tc => tc.Label).ToCommaList()}";
            }
            // other
            else
            {
                yield = $"{resource.label} x{mineral.building.mineableYield * Find.Storyteller.difficulty.mineYieldFactor}" +
                        $"\n - {"ColonyManagerRedux.Info.ChanceToDrop".Translate(mineral.building.mineableDropChance.ToStringPercent())}";
            }

            sb.Append(I18n.YieldOne(yield));
        }

        return sb.ToString();
    }

    public override string GetSubLabel(ManagerJob job)
    {
        ManagerJob_Mining miningJob = (ManagerJob_Mining)job;
        var subLabel = base.GetSubLabel(job);
        if (miningJob.DeconstructBuildings && miningJob.AllowedBuildings.Count > 0)
        {
            if (subLabel == "ColonyManagerRedux.Common.None".Translate())
            {
                subLabel = "";
            }
            else if (!string.IsNullOrEmpty(subLabel))
            {
                subLabel += " | ";
            }
            subLabel += string.Join(", ", miningJob.AllowedBuildings
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
        DrawShortcutToggle(allBuildings, allowedBuildings, (b, v) => SelectedMiningJob.SetBuildingAllowed(b, v), rowRect, "ColonyManagerRedux.Shortcuts.All", null);

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
        DrawShortcutToggle(allMinerals, allowedMinerals, (m, v) => SelectedMiningJob.SetAllowMineral(m, v), rowRect, "ColonyManagerRedux.Shortcuts.All", null);

        // toggle stone
        rowRect.y += ListEntryHeight;
        var stone = allMinerals.Where(m => !m.building.isResourceRock).ToList();
        DrawShortcutToggle(stone, allowedMinerals, (m, v) => SelectedMiningJob.SetAllowMineral(m, v), rowRect, "ColonyManagerRedux.Mining.Stone", "ColonyManagerRedux.Mining.Stone.Tip");

        // toggle metal
        rowRect.y += ListEntryHeight;
        var metal = allMinerals
            .Where(m => m.building.isResourceRock && IsMetal(m.building.mineableThing))
            .ToList();
        DrawShortcutToggle(metal, allowedMinerals, (m, v) => SelectedMiningJob.SetAllowMineral(m, v), rowRect, "ColonyManagerRedux.Mining.Metal", "ColonyManagerRedux.Mining.Metal.Tip");

        // toggle precious
        rowRect.y += ListEntryHeight;
        var precious = allMinerals
            .Where(m => m.building.isResourceRock && (m.building.mineableThing?.smallVolume ?? false))
            .ToList();
        DrawShortcutToggle(precious, allowedMinerals, (m, v) => SelectedMiningJob.SetAllowMineral(m, v), rowRect, "ColonyManagerRedux.Mining.Precious", "ColonyManagerRedux.Mining.Precious.Tip");

        return rowRect.yMax - start.y;
    }

    public float DrawChunks(Vector2 pos, float width)
    {
        var start = pos;

        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.Mining.HaulMapChunks".Translate(),
            "ColonyManagerRedux.Mining.HaulMapChunks.Tip".Translate(),
            ref SelectedMiningJob.HaulMapChunks);

        rowRect.y += ListEntryHeight;
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.Mining.HaulMinedChunks".Translate(),
            "ColonyManagerRedux.Mining.HaulMinedChunks.Tip".Translate(),
            ref SelectedMiningJob.HaulMinedChunks);

        return rowRect.yMax - pos.y;
    }

    public float DrawDeconstructBuildings(Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.Mining.DeconstructBuildings".Translate(),
            "ColonyManagerRedux.Mining.DeconstructBuildings.Tip".Translate(),
            ref SelectedMiningJob.DeconstructBuildings);

        if (SelectedMiningJob.DeconstructBuildings)
        {
            var hasAncientDangerRect = Manager.AncientDangerRect.HasValue;
            var label = "ColonyManagerRedux.Mining.DeconstructAncientDangerWhenFogged".Translate();
            var labelHeight = Text.CalcHeight(label, width);
            rowRect.y += ListEntryHeight;
            rowRect.height = labelHeight;
            Utilities.DrawToggle(rowRect,
                "ColonyManagerRedux.Mining.DeconstructAncientDangerWhenFogged".Translate(),
                "ColonyManagerRedux.Mining.DeconstructAncientDangerWhenFogged.Tip".Translate(),
                ref SelectedMiningJob.DeconstructAncientDangerWhenFogged,
                leaveRoomForAdditionalIcon: !hasAncientDangerRect);

            if (!hasAncientDangerRect)
            {
                var iconRect = new Rect(
                    rowRect.xMax - SmallIconSize - Margin,
                    0f,
                    SmallIconSize,
                    SmallIconSize).CenteredOnYIn(rowRect);
                iconRect.x -= SmallIconSize + Margin;
                TooltipHandler.TipRegion(
                    iconRect,
                    "ColonyManagerRedux.Mining.CannotFindAncientDanger".Translate());
                GUI.color = SelectedMiningJob.DeconstructAncientDangerWhenFogged
                    ? Resources.Orange
                    : Color.grey;
                GUI.DrawTexture(iconRect, Resources.Warning);
                GUI.color = Color.white;
            }
        }

        return rowRect.yMax - pos.y;
    }

    public float DrawMiningArea(Vector2 pos, float width)
    {
        var start = pos;
        AreaAllowedGUI.DoAllowedAreaSelectors(ref pos, width, ref SelectedMiningJob.MiningArea, Manager);
        return pos.y - start.y;
    }

    public float DrawRoofRoomChecks(Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.Mining.MineThickRoofs".Translate(),
            "ColonyManagerRedux.Mining.MineThickRoofs.Tip".Translate(),
            ref SelectedMiningJob.MineThickRoofs);

        rowRect.y += ListEntryHeight;
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.Mining.CheckRoofSupport".Translate(),
            "ColonyManagerRedux.Mining.CheckRoofSupport.Tip".Translate(),
            ref SelectedMiningJob.CheckRoofSupport);

        rowRect.y += ListEntryHeight;
        if (SelectedMiningJob.CheckRoofSupport)
        {
            Utilities.DrawToggle(rowRect,
                "ColonyManagerRedux.Mining.CheckRoofSupportAdvanced".Translate(),
                "ColonyManagerRedux.Mining.CheckRoofSupportAdvanced.Tip".Translate(),
                ref SelectedMiningJob.CheckRoofSupportAdvanced, true);
        }
        else
        {
            Widgets_Labels.Label(rowRect,
                "ColonyManagerRedux.Mining.CheckRoofSupportAdvanced".Translate(),
                "ColonyManagerRedux.Mining.CheckRoofSupportAdvanced.Disabled.Tip".Translate(),
                TextAnchor.MiddleLeft, margin: Margin,
                color: Color.grey);
        }

        rowRect.y += ListEntryHeight;
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.Mining.CheckRoomDivision".Translate(),
            "ColonyManagerRedux.Mining.CheckRoomDivision.Tip".Translate(),
            ref SelectedMiningJob.CheckRoomDivision, true);

        return rowRect.yMax - pos.y;
    }

    public float DrawThresholdSettings(Vector2 pos, float width)
    {
        var start = pos;

        var currentCount = SelectedMiningJob.TriggerThreshold.GetCurrentCount();
        var chunkCount = SelectedMiningJob.GetCountInChunks();
        var designatedCount = SelectedMiningJob.GetCountInDesignations();
        var targetCount = SelectedMiningJob.TriggerThreshold.TargetCount;

        SelectedMiningJob.TriggerThreshold.DrawTriggerConfig(ref pos, width, ListEntryHeight,
            "ColonyManagerRedux.Mining.TargetCount".Translate(
                currentCount, chunkCount, designatedCount, targetCount),
            "ColonyManagerRedux.Mining.TargetCount.Tip".Translate(
                currentCount, chunkCount, designatedCount, targetCount),
            SelectedMiningJob.Designations,
            delegate { SelectedMiningJob.Sync = Utilities.SyncDirection.FilterToAllowed; },
            SelectedMiningJob.DesignationLabel);

        Utilities.DrawToggle(ref pos, width,
            "ColonyManagerRedux.Mining.SyncFilterAndAllowed".Translate(),
            "ColonyManagerRedux.Mining.SyncFilterAndAllowed.Tip".Translate(),
            ref SelectedMiningJob.SyncFilterAndAllowed);
        Utilities.DrawReachabilityToggle(ref pos, width, ref SelectedMiningJob.ShouldCheckReachable);
        Utilities.DrawToggle(ref pos, width,
            "ColonyManagerRedux.Threshold.PathBasedDistance".Translate(),
            "ColonyManagerRedux.Threshold.PathBasedDistance.Tip".Translate(),
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

    protected override void Refresh()
    {
        // update pawnkind options
        foreach (var job in Manager.JobTracker.JobsOfType<ManagerJob_Mining>())
        {
            job.RefreshAllBuildingsAndMinerals();
        }

        SelectedMiningJob?.RefreshAllBuildingsAndMinerals();
    }

    protected override void DoMainContent(Rect rect)
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
        Widgets_Section.Section(ref position, width, DrawThresholdSettings, "ColonyManagerRedux.Threshold".Translate());
        Widgets_Section.Section(ref position, width, DrawChunks, "ColonyManagerRedux.Mining.Chunks".Translate());
        Widgets_Section.Section(ref position, width, DrawDeconstructBuildings);
        Widgets_Section.Section(ref position, width, DrawMiningArea, "ColonyManagerRedux.Mining.MiningArea".Translate());
        Widgets_Section.Section(ref position, width, DrawRoofRoomChecks, "ColonyManagerRedux.Mining.HealthAndSafety".Translate());
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
            "ColonyManagerRedux.Mining.AllowedMinerals".Translate());
        Widgets_Section.Section(ref position, width, DrawAllowedMinerals);
        if (SelectedMiningJob.DeconstructBuildings)
        {
            Widgets_Section.Section(ref position, width, DrawAllowedBuildingsShortcuts,
                "ColonyManagerRedux.Mining.AllowedBuildings".Translate());
            Widgets_Section.Section(ref position, width, DrawAllowedBuildings);
        }
        Widgets_Section.EndSectionColumn("Mining.Minerals", position);

        if (Prefs.DevMode && Widgets.ButtonText(debugButtonRect, "DEV: Debug Options"))
        {
            Find.WindowStack.Add(new Dialog_MiningDebugOptions(SelectedMiningJob));
        }

        if (!SelectedMiningJob.IsManaged)
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.Common.Manage".Translate()))
            {
                // activate job, add it to the stack
                SelectedMiningJob.IsManaged = true;
                Manager.JobTracker.Add(SelectedMiningJob);

                // refresh source list
                Refresh();
            }
        }
        else
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.Common.Delete".Translate()))
            {
                // inactivate job, remove from the stack.
                Manager.JobTracker.Delete(SelectedMiningJob);

                // remove content from UI
                Selected = MakeNewJob();

                // refresh source list
                Refresh();
            }
        }
    }
}
