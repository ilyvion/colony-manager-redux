// ManagerTab_Mining.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Text;
using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed partial class ManagerTab_Mining(Manager manager)
    : ManagerTab<ManagerJob_Mining>(manager)
{
    private const string MiningOptions = "Mining.Options";
    public static HashSet<ThingDef> _metals =
    [
        .. DefDatabase<ThingDef>.AllDefsListForReading.Where(td =>
            td.IsStuff && td.stuffProps.categories.Contains(StuffCategoryDefOf.Metallic)
        ),
    ];

    public ManagerJob_Mining SelectedMiningJob => SelectedJob!;

    protected override bool CreateNewSelectedJobOnMake => false;

    public static string GetMineralTooltip(ThingDef mineral)
    {
        var sb = new StringBuilder();
        _ = sb.Append(mineral.description);

        var resource = mineral.building?.mineableThing;
        if (resource != null && mineral.building != null)
        {
            _ = sb.Append("\n\n");
            var yield = string.Empty;
            // stone chunks
            yield = resource.IsChunk()
                ? $"\n{resource.label}"
                    + $"\n - {"ColonyManagerRedux.Info.ChanceToDrop".Translate(mineral.building.mineableDropChance.ToStringPercent())}"
                    + $"\n - {resource.butcherProducts.Select(tc => tc.Label).ToCommaList()}"
                // other
                : $"{resource.label} x{mineral.building.mineableYield * Find.Storyteller.difficulty.mineYieldFactor}"
                    + $"\n - {"ColonyManagerRedux.Info.ChanceToDrop".Translate(mineral.building.mineableDropChance.ToStringPercent())}";

            _ = sb.Append(I18n.YieldOne(yield));
        }

        return sb.ToString();
    }

    public override string GetSubLabel(ManagerJob job)
    {
        var miningJob = (ManagerJob_Mining)job;
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
            subLabel += string.Join(
                ", ",
                miningJob.AllowedBuildings.Select(pk => pk.LabelCap.Resolve())
            );
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
            Utilities.DrawToggle(
                rowRect,
                building.LabelCap,
                building.description,
                allowedBuildings.Contains(building),
                () =>
                    SelectedMiningJob.SetBuildingAllowed(
                        building,
                        !allowedBuildings.Contains(building)
                    )
            );
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
        DrawShortcutToggle(
            allBuildings,
            allowedBuildings,
            (b, v) => SelectedMiningJob.SetBuildingAllowed(b, v),
            rowRect,
            "ColonyManagerRedux.Shortcuts.All",
            null
        );

        return rowRect.yMax - start.y;
    }

    public float DrawAllowedMinerals(Vector2 pos, float width)
    {
        var start = pos;
        // list of keys in allowed animals list (all animals in biome + visible animals on map)
        var allowedMinerals = SelectedMiningJob.AllowedMinerals;

        // toggle for each animal
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        foreach (var mineral in Utilities_Mining.AllMinerals)
        {
            // draw the toggle
            Utilities.DrawToggle(
                rowRect,
                mineral.LabelCap,
                new TipSignal(() => GetMineralTooltip(mineral), mineral.GetHashCode()),
                allowedMinerals.Contains(mineral),
                () => SelectedMiningJob.SetAllowMineral(mineral, !allowedMinerals.Contains(mineral))
            );
            rowRect.y += ListEntryHeight;
        }

        return rowRect.yMin - start.y;
    }

    private readonly List<ThingDef> _tmpThings = [];

    public float DrawAllowedMineralsShortcuts(Vector2 pos, float width)
    {
        using var _ = new DoOnDispose(_tmpThings.Clear);

        var start = pos;

        // list of keys in allowed animals list (all animals in biome + visible animals on map)
        var allowedMinerals = SelectedMiningJob.AllowedMinerals;
        var allMinerals = Utilities_Mining.AllMinerals;

        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);

        // toggle all
        DrawShortcutToggle(
            allMinerals,
            allowedMinerals,
            (m, v) => SelectedMiningJob.SetAllowMineral(m, v),
            rowRect,
            "ColonyManagerRedux.Shortcuts.All",
            null
        );

        // toggle stone
        rowRect.y += ListEntryHeight;
        _tmpThings.Clear();
        _tmpThings.AddRange(allMinerals.Where(m => !m.building.isResourceRock));
        DrawShortcutToggle(
            _tmpThings,
            allowedMinerals,
            (m, v) => SelectedMiningJob.SetAllowMineral(m, v),
            rowRect,
            "ColonyManagerRedux.Mining.Stone",
            "ColonyManagerRedux.Mining.Stone.Tip"
        );

        // toggle metal
        rowRect.y += ListEntryHeight;
        _tmpThings.Clear();
        _tmpThings.AddRange(
            allMinerals.Where(m => m.building.isResourceRock && IsMetal(m.building.mineableThing))
        );
        DrawShortcutToggle(
            _tmpThings,
            allowedMinerals,
            (m, v) => SelectedMiningJob.SetAllowMineral(m, v),
            rowRect,
            "ColonyManagerRedux.Mining.Metal",
            "ColonyManagerRedux.Mining.Metal.Tip"
        );

        // toggle precious
        rowRect.y += ListEntryHeight;
        _tmpThings.Clear();
        _tmpThings.AddRange(
            allMinerals.Where(m =>
                m.building.isResourceRock && (m.building.mineableThing?.smallVolume ?? false)
            )
        );
        DrawShortcutToggle(
            _tmpThings,
            allowedMinerals,
            (m, v) => SelectedMiningJob.SetAllowMineral(m, v),
            rowRect,
            "ColonyManagerRedux.Mining.Precious",
            "ColonyManagerRedux.Mining.Precious.Tip"
        );

        return rowRect.yMax - start.y;
    }

    public float DrawMining(ManagerJob_Mining job, Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);

        Utilities.DrawToggle(
            rowRect,
            "ColonyManagerRedux.Mining.AllowMining".Translate(),
            "ColonyManagerRedux.Mining.AllowMining.Tip".Translate(),
            ref job.AllowMining
        );

        rowRect.y += ListEntryHeight;
        if (job.AllowMining)
        {
            Utilities.DrawToggle(
                rowRect,
                "ColonyManagerRedux.Mining.TakeOwnershipOfMiningJobs".Translate(),
                "ColonyManagerRedux.Mining.TakeOwnershipOfMiningJobs.Tip".Translate(),
                ref job.TakeOwnershipOfMiningJobs
            );
        }
        else
        {
            IlyvionWidgets.Label(
                rowRect,
                "ColonyManagerRedux.Mining.TakeOwnershipOfMiningJobs".Translate(),
                "ColonyManagerRedux.Mining.TakeOwnershipOfMiningJobs.Disabled.Tip".Translate(),
                TextAnchor.MiddleLeft,
                color: Color.grey,
                leftMargin: Margin
            );
        }

        rowRect.y += ListEntryHeight;
        Utilities.DrawToggle(
            rowRect,
            "ColonyManagerRedux.Mining.ControlDeepDrills".Translate(),
            "ColonyManagerRedux.Mining.ControlDeepDrills.Tip".Translate(),
            ref job.ControlDeepDrills
        );

        return rowRect.yMax - pos.y;
    }

    public float DrawChunks(ManagerJob_Mining job, Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(
            rowRect,
            "ColonyManagerRedux.Mining.HaulMapChunks".Translate(),
            "ColonyManagerRedux.Mining.HaulMapChunks.Tip".Translate(),
            ref job.HaulMapChunks
        );

        rowRect.y += ListEntryHeight;
        Utilities.DrawToggle(
            rowRect,
            "ColonyManagerRedux.Mining.HaulMinedChunks".Translate(),
            "ColonyManagerRedux.Mining.HaulMinedChunks.Tip".Translate(),
            ref job.HaulMinedChunks
        );

        return rowRect.yMax - pos.y;
    }

    public float DrawDeconstructBuildings(ManagerJob_Mining job, Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(
            rowRect,
            "ColonyManagerRedux.Mining.DeconstructBuildings".Translate(),
            "ColonyManagerRedux.Mining.DeconstructBuildings.Tip".Translate(),
            ref job.DeconstructBuildings
        );

        if (job.DeconstructBuildings)
        {
            var hasAncientDangerRect = Manager.AncientDangerRects.Count > 0;
            var label = "ColonyManagerRedux.Mining.DeconstructAncientDangerWhenFogged".Translate();
            var labelHeight = Text.CalcHeight(label, width - Margin);
            rowRect.y += ListEntryHeight;
            rowRect.height = labelHeight;
            Utilities.DrawToggle(
                rowRect,
                label,
                "ColonyManagerRedux.Mining.DeconstructAncientDangerWhenFogged.Tip".Translate(),
                ref job.DeconstructAncientDangerWhenFogged,
                leaveRoomForAdditionalIcon: !hasAncientDangerRect
            );

            if (!hasAncientDangerRect)
            {
                var iconRect = new Rect(
                    rowRect.xMax - SmallIconSize - Margin,
                    0f,
                    SmallIconSize,
                    SmallIconSize
                ).CenteredOnYIn(rowRect);
                iconRect.x -= SmallIconSize + Margin;
                TooltipHandler.TipRegion(
                    iconRect,
                    "ColonyManagerRedux.Mining.CannotFindAncientDanger".Translate()
                );
                GUI.color = job.DeconstructAncientDangerWhenFogged ? Resources.Orange : Color.grey;
                GUI.DrawTexture(iconRect, Resources.Warning);
                GUI.color = Color.white;
            }
        }

        return rowRect.yMax - pos.y;
    }

    public float DrawMiningArea(ManagerJob_Mining job, Vector2 pos, float width)
    {
        var start = pos;
        AreaAllowedGUI.DoAllowedAreaSelectors(ref pos, width, ref job.MiningArea, 5, Manager);
        return pos.y - start.y;
    }

    public float DrawRoofRoomChecks(ManagerJob_Mining job, Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(
            rowRect,
            "ColonyManagerRedux.Mining.MineThickRoofs".Translate(),
            "ColonyManagerRedux.Mining.MineThickRoofs.Tip".Translate(),
            ref job.MineThickRoofs
        );

        rowRect.y += ListEntryHeight;
        Utilities.DrawToggle(
            rowRect,
            "ColonyManagerRedux.Mining.CheckRoofSupport".Translate(),
            "ColonyManagerRedux.Mining.CheckRoofSupport.Tip".Translate(),
            ref job.CheckRoofSupport
        );

        rowRect.y += ListEntryHeight;
        if (job.CheckRoofSupport)
        {
            Utilities.DrawToggle(
                rowRect,
                "ColonyManagerRedux.Mining.CheckRoofSupportAdvanced".Translate(),
                "ColonyManagerRedux.Mining.CheckRoofSupportAdvanced.Tip".Translate(),
                ref job.CheckRoofSupportAdvanced,
                true
            );
        }
        else
        {
            IlyvionWidgets.Label(
                rowRect,
                "ColonyManagerRedux.Mining.CheckRoofSupportAdvanced".Translate(),
                "ColonyManagerRedux.Mining.CheckRoofSupportAdvanced.Disabled.Tip".Translate(),
                TextAnchor.MiddleLeft,
                leftMargin: Margin,
                color: Color.grey
            );
        }

        rowRect.y += ListEntryHeight;
        Utilities.DrawToggle(
            rowRect,
            "ColonyManagerRedux.Mining.CheckRoomDivision".Translate(),
            "ColonyManagerRedux.Mining.CheckRoomDivision.Tip".Translate(),
            ref job.CheckRoomDivision,
            true
        );

        return rowRect.yMax - pos.y;
    }

    public float DrawThresholdSettings(ManagerJob_Mining job, Vector2 pos, float width)
    {
        var start = pos;

        var currentCount = job.TriggerThreshold.GetCurrentCount();
        _ = job.ChunksCachedValue.DoUpdateIfNeeded();
        var chunkCount = job.ChunksCachedValue.Value;
        _ = job.DesignatedCachedValue.DoUpdateIfNeeded();
        var designatedCount = job.DesignatedCachedValue.Value;
        var targetLabel = job.TriggerThreshold.TargetLabel;
        var chunkProductKind = job.GetChunkProductKind();

        job.TriggerThreshold.DrawTriggerConfig(
            ref pos,
            width,
            ListEntryHeight,
            "ColonyManagerRedux.Mining.TargetCount".Translate(
                currentCount,
                chunkCount,
                designatedCount,
                targetLabel
            ),
            "ColonyManagerRedux.Mining.TargetCount.Tip".Translate(
                currentCount,
                chunkCount,
                designatedCount,
                targetLabel,
                $"ColonyManagerRedux.Mining.TargetCount.Tip.{chunkProductKind}".Translate()
            ),
            job.Designations,
            delegate
            {
                job.Sync = Utilities.SyncDirection.FilterToAllowed;
            },
            job.DesignationLabel
        );

        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.SyncFilterAndAllowed".Translate(),
            "ColonyManagerRedux.Mining.SyncFilterAndAllowed.Tip".Translate(),
            ref job.SyncFilterAndAllowed
        );
        Utilities.DrawReachabilityToggle(ref pos, width, ref job.ShouldCheckReachable);
        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.Threshold.PathBasedDistance".Translate(),
            "ColonyManagerRedux.Threshold.PathBasedDistance.Tip".Translate(),
            ref job.UsePathBasedDistance,
            true
        );

        return pos.y - start.y;
    }

    public static bool IsMetal(ThingDef def) => def != null && _metals.Contains(def);

    public override void PreOpen()
    {
        Refresh();
        if (SelectedJob == null)
        {
            Selected = MakeNewJob();
        }
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
            rect.height - Margin - ButtonSize.y
        );
        var mineralsColumnRect = new Rect(
            optionsColumnRect.xMax,
            rect.yMin,
            rect.width * 2 / 5f,
            rect.height - Margin - ButtonSize.y
        );
        Rect? buildingColumnRect = null;
        if (SelectedMiningJob.DeconstructBuildings)
        {
            mineralsColumnRect.height /= 2;
            mineralsColumnRect.height -= Margin / 2;

            buildingColumnRect = new Rect(
                mineralsColumnRect.x,
                mineralsColumnRect.yMax + Margin,
                mineralsColumnRect.width,
                mineralsColumnRect.height
            );
        }
        var buttonRect = new Rect(
            rect.xMax - ButtonSize.x,
            rect.yMax - ButtonSize.y,
            ButtonSize.x - Margin,
            ButtonSize.y - Margin
        );
        var debugButtonRect = new Rect(buttonRect);
        debugButtonRect.x -= ButtonSize.x + Margin;

        // options
        Widgets_Section.BeginSectionColumn(
            optionsColumnRect,
            MiningOptions,
            out var position,
            out var width
        );
        DrawSection(
            MiningOptions,
            "Threshold",
            ref position,
            width,
            DrawThresholdSettings,
            "ColonyManagerRedux.Threshold".Translate()
        );
        DrawSection(
            MiningOptions,
            "Mining",
            ref position,
            width,
            DrawMining,
            "ColonyManagerRedux.Mining.Mining".Translate()
        );
        DrawSection(
            MiningOptions,
            "Chunks",
            ref position,
            width,
            DrawChunks,
            "ColonyManagerRedux.Mining.Chunks".Translate()
        );
        DrawSection(
            MiningOptions,
            "DeconstructBuildings",
            ref position,
            width,
            DrawDeconstructBuildings
        );
        DrawSection(
            MiningOptions,
            "MiningArea",
            ref position,
            width,
            DrawMiningArea,
            "ColonyManagerRedux.Mining.MiningArea".Translate()
        );
        DrawSection(
            MiningOptions,
            "HealthAndSafety",
            ref position,
            width,
            DrawRoofRoomChecks,
            "ColonyManagerRedux.Mining.HealthAndSafety".Translate()
        );
        Widgets_Section.EndSectionColumn(MiningOptions, position);

        // minerals
        Widgets_Section.BeginSectionColumn(
            mineralsColumnRect,
            "Mining.Minerals",
            out position,
            out width
        );
        var refreshRect = new Rect(
            position.x + width - SmallIconSize - (2 * Margin),
            position.y + Margin,
            SmallIconSize,
            SmallIconSize
        );
        if (Widgets.ButtonImage(refreshRect, Resources.Refresh, Color.grey))
        {
            SelectedMiningJob.RefreshAllBuildingsAndMinerals();
        }

        Widgets_Section.Section(
            ref position,
            width,
            DrawAllowedMineralsShortcuts,
            "ColonyManagerRedux.Mining.AllowedMinerals".Translate()
        );
        Widgets_Section.Section(ref position, width, DrawAllowedMinerals);
        Widgets_Section.EndSectionColumn("Mining.Minerals", position);

        if (buildingColumnRect.HasValue)
        {
            // buildings
            Widgets_Section.BeginSectionColumn(
                buildingColumnRect.Value,
                "Mining.Buildings",
                out position,
                out width
            );

            Widgets_Section.Section(
                ref position,
                width,
                DrawAllowedBuildingsShortcuts,
                "ColonyManagerRedux.Mining.AllowedBuildings".Translate()
            );
            Widgets_Section.Section(ref position, width, DrawAllowedBuildings);

            Widgets_Section.EndSectionColumn("Mining.Buildings", position);
        }

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
