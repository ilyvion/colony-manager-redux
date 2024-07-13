// ManagerTab_Mining.cs
// Copyright Karel Kroeze, 2018-2020

using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

public class ManagerTab_Mining(Manager manager) : ManagerTab(manager)
{
    public static HashSet<ThingDef> _metals = new(DefDatabase<ThingDef>.AllDefsListForReading
                                                                                          .Where(td => td.IsStuff
                                                                                                     && td
                                                                                                       .stuffProps
                                                                                                       .categories
                                                                                                       .Contains(
                                                                                                            StuffCategoryDefOf
                                                                                                               .Metallic)));

    public List<ManagerJob_Mining> Jobs = [];

    private float _jobListHeight;
    private Vector2 _jobListScrollPosition = Vector2.zero;
    private ManagerJob_Mining _selected = new(manager);

    public override string Label => "ColonyManagerRedux.ManagerMining".Translate();

    public override ManagerJob? Selected
    {
        get => _selected;
        set => _selected = (ManagerJob_Mining)value!;
    }

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

    public float DrawAllowedBuildings(Vector2 pos, float width)
    {
        var start = pos;

        var allowedBuildings = _selected.AllowedBuildings;
        var buildings = new List<ThingDef>(allowedBuildings.Keys);

        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        foreach (var building in buildings)
        {
            Utilities.DrawToggle(rowRect, building.LabelCap, building.description, allowedBuildings[building],
                                  () => _selected.SetAllowBuilding(building, !allowedBuildings[building]));
            rowRect.y += ListEntryHeight;
        }

        return rowRect.yMin - start.y;
    }


    public float DrawAllowedBuildingsShortcuts(Vector2 pos, float width)
    {
        var start = pos;

        // list of keys in allowed animals list (all animals in biome + visible animals on map)
        var allowedBuildings = _selected.AllowedBuildings;
        var buildings = new List<ThingDef>(allowedBuildings.Keys);

        // toggle all
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(rowRect,
                              "ColonyManagerRedux.ManagerAll".Translate().Italic(),
                              string.Empty,
                              allowedBuildings.Values.All(v => v),
                              allowedBuildings.Values.All(v => !v),
                              () => buildings.ForEach(b => _selected.SetAllowBuilding(b, true)),
                              () => buildings.ForEach(b => _selected.SetAllowBuilding(b, false)));

        return rowRect.yMax - start.y;
    }

    public float DrawAllowedMinerals(Vector2 pos, float width)
    {
        var start = pos;
        // list of keys in allowed animals list (all animals in biome + visible animals on map)
        var allowedMinerals = _selected.AllowedMinerals;
        var minerals = new List<ThingDef>(allowedMinerals.Keys);

        // toggle for each animal
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        foreach (var mineral in minerals)
        {
            // draw the toggle
            Utilities.DrawToggle(rowRect, mineral.LabelCap,
                                  new TipSignal(() => GetMineralTooltip(mineral), mineral.GetHashCode()),
                                  _selected.AllowedMinerals[mineral],
                                  () => _selected.SetAllowMineral(mineral, !_selected.AllowedMinerals[mineral]));
            rowRect.y += ListEntryHeight;
        }

        return rowRect.yMin - start.y;
    }

    public float DrawAllowedMineralsShortcuts(Vector2 pos, float width)
    {
        var start = pos;

        // list of keys in allowed animals list (all animals in biome + visible animals on map)
        var allowedMinerals = _selected.AllowedMinerals;
        var minerals = new List<ThingDef>(allowedMinerals.Keys);

        // toggle all
        Utilities.DrawToggle(ref pos, width,
                              "ColonyManagerRedux.ManagerAll".Translate().Italic(),
                              string.Empty,
                              _selected.AllowedMinerals.Values.All(v => v),
                              _selected.AllowedMinerals.Values.All(v => !v),
                              () => minerals.ForEach(p => _selected.SetAllowMineral(p, true)),
                              () => minerals.ForEach(p => _selected.SetAllowMineral(p, false)));

        // toggle stone
        var stone = minerals.Where(m => !m.building.isResourceRock).ToList();
        Utilities.DrawToggle(ref pos, width,
                              "ColonyManagerRedux.ManagerMining.Stone".Translate().Italic(),
                              "ColonyManagerRedux.ManagerMining.Stone.Tip".Translate(),
                              stone.All(p => allowedMinerals[p]),
                              stone.All(p => !allowedMinerals[p]),
                              () => stone.ForEach(p => _selected.SetAllowMineral(p, true)),
                              () => stone.ForEach(p => _selected.SetAllowMineral(p, false)));

        // toggle metal
        var metal = minerals.Where(m => m.building.isResourceRock && IsMetal(m.building.mineableThing))
                            .ToList();
        Utilities.DrawToggle(ref pos, width,
                              "ColonyManagerRedux.ManagerMining.Metal".Translate().Italic(),
                              "ColonyManagerRedux.ManagerMining.Metal.Tip".Translate(),
                              metal.All(p => allowedMinerals[p]),
                              metal.All(p => !allowedMinerals[p]),
                              () => metal.ForEach(p => _selected.SetAllowMineral(p, true)),
                              () => metal.ForEach(p => _selected.SetAllowMineral(p, false)));

        // toggle precious
        var precious = minerals
                      .Where(m => m.building.isResourceRock && (m.building.mineableThing?.smallVolume ?? false))
                      .ToList();
        Utilities.DrawToggle(ref pos, width,
                              "ColonyManagerRedux.ManagerMining.Precious".Translate().Italic(),
                              "ColonyManagerRedux.ManagerMining.Precious.Tip".Translate(),
                              precious.All(p => allowedMinerals[p]),
                              precious.All(p => !allowedMinerals[p]),
                              () => precious.ForEach(p => _selected.SetAllowMineral(p, true)),
                              () => precious.ForEach(p => _selected.SetAllowMineral(p, false)));

        return pos.y - start.y;
    }

    public float DrawDeconstructBuildings(Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(rowRect,
                              "ColonyManagerRedux.ManagerMining.DeconstructBuildings".Translate(),
                              "ColonyManagerRedux.ManagerMining.DeconstructBuildings.Tip".Translate(),
                              ref _selected.DeconstructBuildings);
        return ListEntryHeight;
    }

    public float DrawMiningArea(Vector2 pos, float width)
    {
        var start = pos;
        AreaAllowedGUI.DoAllowedAreaSelectors(ref pos, width, ref _selected.MiningArea, manager);
        return pos.y - start.y;
    }

    public float DrawRoofRoomChecks(Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(rowRect, "ColonyManagerRedux.ManagerMining.CheckRoofSupport".Translate(),
                              "ColonyManagerRedux.ManagerMining.CheckRoofSupport.Tip".Translate(), ref _selected.CheckRoofSupport);

        rowRect.y += ListEntryHeight;
        if (_selected.CheckRoofSupport)
        {
            Utilities.DrawToggle(rowRect, "ColonyManagerRedux.ManagerMining.CheckRoofSupportAdvanced".Translate(),
                                  "ColonyManagerRedux.ManagerMining.CheckRoofSupportAdvanced.Tip".Translate(),
                                  ref _selected.CheckRoofSupportAdvanced, true);
        }
        else
        {
            Widgets_Labels.Label(rowRect, "ColonyManagerRedux.ManagerMining.CheckRoofSupportAdvanced".Translate(),
                                  "ColonyManagerRedux.ManagerMining.CheckRoofSupportAdvanced.Disabled.Tip".Translate(),
                                  TextAnchor.MiddleLeft, margin: Margin,
                                  color: Color.grey);
        }

        rowRect.y += ListEntryHeight;
        Utilities.DrawToggle(rowRect, "ColonyManagerRedux.ManagerMining.CheckRoomDivision".Translate(),
                              "ColonyManagerRedux.ManagerMining.CheckRoomDivision.Tip".Translate(), ref _selected.CheckRoomDivision,
                              true);

        return rowRect.yMax - pos.y;
    }

    public float DrawThresholdSettings(Vector2 pos, float width)
    {
        var start = pos;

        var currentCount = _selected.Trigger.CurrentCount;
        var chunkCount = _selected.GetCountInChunks();
        var designatedCount = _selected.GetCountInDesignations();
        var targetCount = _selected.Trigger.TargetCount;

        _selected.Trigger.DrawTriggerConfig(ref pos, width, ListEntryHeight,
                                             "ColonyManagerRedux.ManagerMining.TargetCount".Translate(
                                                 currentCount, chunkCount, designatedCount, targetCount),
                                             "ColonyManagerRedux.ManagerMining.TargetCount.Tip".Translate(
                                                 currentCount, chunkCount, designatedCount, targetCount),
                                             _selected.Designations,
                                             delegate { _selected.Sync = Utilities.SyncDirection.FilterToAllowed; },
                                             _selected.DesignationLabel);

        Utilities.DrawToggle(ref pos, width,
                              "ColonyManagerRedux.ManagerMining.SyncFilterAndAllowed".Translate(),
                              "ColonyManagerRedux.ManagerMining.SyncFilterAndAllowed.Tip".Translate(),
                              ref _selected.SyncFilterAndAllowed);
        Utilities.DrawReachabilityToggle(ref pos, width, ref _selected.CheckReachable);
        Utilities.DrawToggle(ref pos, width,
                              "ColonyManagerRedux.ManagerPathBasedDistance".Translate(),
                              "ColonyManagerRedux.ManagerPathBasedDistance.Tip".Translate(),
                              ref _selected.PathBasedDistance,
                              true);

        return pos.y - start.y;
    }

    public bool IsMetal(ThingDef def)
    {
        return def != null && _metals.Contains(def);
    }

    public override void PreOpen()
    {
        Refresh();
    }

    public void Refresh()
    {
        // upate our list of jobs
        Jobs = Manager.For(manager).JobStack.FullStack<ManagerJob_Mining>();

        // update pawnkind options
        foreach (var job in Jobs)
        {
            job.RefreshAllowedMinerals();
        }

        _selected?.RefreshAllowedMinerals();
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
            _selected.RefreshAllowedMinerals();
        }

        Widgets_Section.Section(ref position, width, DrawAllowedMineralsShortcuts,
                                 "ColonyManagerRedux.ManagerMining.AllowedMinerals".Translate());
        Widgets_Section.Section(ref position, width, DrawAllowedMinerals);
        Widgets_Section.Section(ref position, width, DrawAllowedBuildingsShortcuts,
                                 "ColonyManagerRedux.ManagerMining.AllowedBuildings".Translate());
        Widgets_Section.Section(ref position, width, DrawAllowedBuildings);
        Widgets_Section.EndSectionColumn("Mining.Minerals", position);

        // do the button
        if (Event.current.control && Widgets.ButtonInvisible(buttonRect))
        {
            Find.WindowStack.Add(new Dialog_MiningDebugOptions(_selected));
        }

        if (!_selected.Managed)
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.ManagerManage".Translate()))
            {
                // activate job, add it to the stack
                _selected.Managed = true;
                Manager.For(manager).JobStack.Add(_selected);

                // refresh source list
                Refresh();
            }
        }
        else
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.ManagerDelete".Translate()))
            {
                // inactivate job, remove from the stack.
                Manager.For(manager).JobStack.Delete(_selected);

                // remove content from UI
                _selected = new ManagerJob_Mining(manager);

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

        foreach (var job in Jobs)
        {
            var row = new Rect(0f, cur.y, scrollContent.width, LargeListEntryHeight);
            Widgets.DrawHighlightIfMouseover(row);
            if (_selected == job)
            {
                Widgets.DrawHighlightSelected(row);
            }

            if (i++ % 2 == 1)
            {
                Widgets.DrawAltRect(row);
            }

            var jobRect = row;

            if (ManagerTab_Overview.DrawOrderButtons(new Rect(row.xMax - 50f, row.yMin, 50f, 50f), manager,
                                                       job))
            {
                Refresh();
            }

            jobRect.width -= 50f;

            job.DrawListEntry(jobRect, false);
            if (Widgets.ButtonInvisible(jobRect))
            {
                _selected = job;
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
            Selected = new ManagerJob_Mining(manager);
        }

        TooltipHandler.TipRegion(newRect, "ColonyManagerRedux.ManagerMining.NewJob.Tip".Translate());

        cur.y += LargeListEntryHeight;

        _jobListHeight = cur.y;
        GUI.EndGroup();
        Widgets.EndScrollView();
    }
}
