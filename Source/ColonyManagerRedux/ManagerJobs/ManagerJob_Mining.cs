// ManagerJob_Mining.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[HotSwappable]
public class ManagerJob_Mining : ManagerJob
{
    private const int RoofSupportGridSpacing = 5;
    private readonly Utilities.CachedValue<int> _chunksCachedValue = new(0);
    private readonly Utilities.CachedValue<int> _designatedCachedValue = new(0);
    public HashSet<ThingDef> AllowedBuildings = [];

    public HashSet<ThingDef> AllowedMinerals = [];
    public bool CheckRoofSupport = true;
    public bool CheckRoofSupportAdvanced;
    public bool CheckRoomDivision = true;
    public bool DeconstructBuildings;
    public History History;
    public Area? MiningArea;
    public Utilities.SyncDirection Sync = Utilities.SyncDirection.AllowedToFilter;

    public bool SyncFilterAndAllowed = true;
    public Trigger_Threshold Trigger;
    private List<Designation> _designations = [];

    private List<ThingDef>? _allDeconstructibleBuildings;
    public List<ThingDef> AllDeconstructibleBuildings
    {
        get
        {
            _allDeconstructibleBuildings ??= Utilities_Mining.GetDeconstructibleBuildings(Manager).ToList();
            return _allDeconstructibleBuildings;
        }
    }

    private List<ThingDef>? _allMinerals;
    public List<ThingDef> AllMinerals
    {
        get
        {
            _allMinerals ??= Utilities_Mining.GetMinerals().ToList();
            return _allMinerals;
        }
    }

    public ManagerJob_Mining(Manager manager) : base(manager)
    {
        // populate the trigger field, set the root category to meats and allow all but human & insect meat.
        Trigger = new Trigger_Threshold(this);

        // start the history tracker;
        History = new History(
            new[] { I18n.HistoryStock, I18n.HistoryChunks, I18n.HistoryDesignated },
            [Color.white, new Color(.7f, .7f, .7f), new Color(.4f, .4f, .4f)]);
    }

    public override bool IsCompleted => !Trigger.State;

    public List<Designation> Designations => new(_designations);


    public override bool IsValid => base.IsValid && History != null && Trigger != null;
    public override string Label => "ColonyManagerRedux.ManagerMining".Translate();
    public override ManagerTab Tab => Manager.tabs.Find(tab => tab is ManagerTab_Mining);

    public override string[] Targets => AllowedMinerals
        .Select(pk => pk.LabelCap.Resolve()).ToArray();

    public override WorkTypeDef WorkTypeDef => WorkTypeDefOf.Mining;

    public static bool IsDesignatedForRemoval(Building building, Map map)
    {
        var designation = map.designationManager.DesignationOn(building);

        return designation != null && (designation.def == DesignationDefOf.Mine ||
            designation.def == DesignationDefOf.Deconstruct);
    }

    // largely copypasta from RoofCollapseUtility.WithinRangeOfRoofHolder
    // TODO: PERFORMANCE; maintain a cellgrid of 'safe' supported areas.
    private static bool WouldCollapseIfSupportDestroyed(IntVec3 position, IntVec3 support, Map map)
    {
        if (!position.InBounds(map) || !position.Roofed(map))
        {
            return false;
        }

        // cell indexes and buildings on map indexed by cellIndex
        var cellIndices = map.cellIndices;
        var innerArray = map.edificeGrid.InnerArray;

        for (var i = 0; i < RoofCollapseUtility.RoofSupportRadialCellsCount; i++)
        {
            Logger.Debug(i.ToString());
            var candidate = position + GenRadial.RadialPattern[i];
            if (candidate != support && candidate.InBounds(map))
            {
                var building = innerArray[cellIndices.CellToIndex(candidate)];
#if DEBUG
                map.debugDrawer.FlashCell(
                    candidate, DebugSolidColorMats.MaterialOf(new Color(0f, 0f, 1f, .1f)), ".", 500);
#endif
                if (building != null && building.def.holdsRoof && !IsDesignatedForRemoval(building, map))
                {
#if DEBUG
                    map.debugDrawer.FlashCell(
                        candidate, DebugSolidColorMats.MaterialOf(new Color(0f, 1f, 0f, .1f)), "!", 500);
                    map.debugDrawer.FlashCell(
                        position, DebugSolidColorMats.MaterialOf(new Color(0f, 1f, 0f, .1f)), "V", 500);
#endif
                    return false;
                }
            }
        }
#if DEBUG
        map.debugDrawer.FlashCell(position, DebugSolidColorMats.MaterialOf(Color.red), "X");
#endif
        return true;
    }

    public void AddDesignation(Designation designation)
    {
        DesignationManager designationManager = Manager.map.designationManager;
        if (designation.def.targetType == TargetType.Thing && !designationManager.HasMapDesignationOn(designation.target.Thing))
        {
            designationManager.AddDesignation(designation);
        }
        else if (designation.def.targetType == TargetType.Cell && !designationManager.HasMapDesignationAt(designation.target.Cell))
        {
            designationManager.AddDesignation(designation);
        }
        _designations.Add(designation);
    }

    public void AddRelevantGameDesignations()
    {
        foreach (var des in Manager.map.designationManager
            .SpawnedDesignationsOfDef(DesignationDefOf.Mine)
            .Except(_designations)
            .Where(des => IsValidMiningTarget(des.target)))
        {
            AddDesignation(des);
        }

        foreach (var des in Manager.map.designationManager
                                    .SpawnedDesignationsOfDef(DesignationDefOf.Deconstruct)
                                    .Except(_designations)
                                    .Where(des => IsValidDeconstructionTarget(des.target)))
        {
            AddDesignation(des);
        }
    }

    public bool Allowed(ThingDef? thingDef)
    {
        if (thingDef == null)
        {
            return false;
        }

        return AllowedMineral(thingDef) || AllowedBuilding(thingDef);
    }

    public bool AllowedBuilding(ThingDef? thingDef)
    {
        if (thingDef == null)
        {
            return false;
        }

        return AllowedBuildings.Contains(thingDef);
    }

    public bool AllowedMineral(ThingDef? thingDef)
    {
        if (thingDef == null)
        {
            return false;
        }

        return AllowedMinerals.Contains(thingDef);
    }

    public override void CleanUp()
    {
        RemoveObsoleteDesignations();
        foreach (var designation in _designations)
        {
            designation.Delete();
        }

        _designations.Clear();
    }

    public bool Counted(ThingDefCountClass thingDefCount)
    {
        return Counted(thingDefCount.thingDef);
    }

    public bool Counted(ThingDef thingDef)
    {
        return Trigger.ThresholdFilter.Allows(thingDef);
    }

    public string DesignationLabel(Designation designation)
    {
        if (designation.def == DesignationDefOf.Deconstruct)
        {
            var building = designation.target.Thing;
            return "ColonyManagerRedux.Manager.DesignationLabel".Translate(
                building.LabelCap,
                Distance(building, Manager.map.GetBaseCenter()).ToString("F0"),
                "?", "?");
        }

        if (designation.def == DesignationDefOf.Mine)
        {
            var mineable = designation.target.Cell.GetFirstMineable(Manager.map);
            return "ColonyManagerRedux.Manager.DesignationLabel".Translate(
                mineable.LabelCap,
                Distance(mineable, Manager.map.GetBaseCenter()).ToString("F0"),
                GetCountInMineral(mineable),
                GetMaterialsInMineral(mineable.def)?.First().LabelCap ?? "?");
        }

        return string.Empty;
    }

    public override void DrawListEntry(Rect rect, bool overview = true, bool active = true)
    {
        // (detailButton) | name | (bar | last update)/(stamp) -> handled in Utilities.DrawStatusForListEntry
        //var shownTargets = overview ? 4 : 3; // there's more space on the overview

        // set up rects
        Rect labelRect = new(Margin, Margin, rect.width -
                                                   (active ? StatusRectWidth + 4 * Margin : 2 * Margin),
                                   rect.height - 2 * Margin),
             statusRect = new(labelRect.xMax + Margin, Margin, StatusRectWidth, rect.height - 2 * Margin);

        // create label string
        var text = Label + "\n";
        var subtext = string.Join(", ", Targets);
        if (subtext.Fits(labelRect))
        {
            text += subtext.Italic();
        }
        else
        {
            text += "multiple".Translate().Italic();
        }

        // do the drawing
        GUI.BeginGroup(rect);

        // draw label
        Widgets_Labels.Label(labelRect, text, subtext.NullOrEmpty() ? "<none>" : subtext, TextAnchor.MiddleLeft, margin: Margin);

        // if the bill has a manager job, give some more info.
        if (active)
        {
            this.DrawStatusForListEntry(statusRect, Trigger);
        }

        GUI.EndGroup();
    }

    public override void DrawOverviewDetails(Rect rect)
    {
        History.DrawPlot(rect, Trigger.TargetCount);
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_References.Look(ref MiningArea, "miningArea");
        Scribe_Deep.Look(ref Trigger, "trigger", this);
        Scribe_Collections.Look(ref AllowedMinerals, "allowedMinerals", LookMode.Def);
        Scribe_Collections.Look(ref AllowedBuildings, "allowedBuildings", LookMode.Def);
        Scribe_Values.Look(ref SyncFilterAndAllowed, "syncFilterAndAllowed", true);
        Scribe_Values.Look(ref DeconstructBuildings, "deconstructBuildings");
        Scribe_Values.Look(ref CheckRoofSupport, "checkRoofSupport", true);
        Scribe_Values.Look(ref CheckRoofSupportAdvanced, "checkRoofSupportAdvanced");
        Scribe_Values.Look(ref CheckRoomDivision, "checkRoomDivision", true);

        // don't store history in import/export mode.
        if (Manager.Mode == Manager.Modes.Normal)
        {
            Scribe_Deep.Look(ref History, "history");
        }

        Utilities.Scribe_Designations(ref _designations, Manager);
    }

    public int GetCountInBuilding(Building? building)
    {
        var def = building?.def;
        if (def == null || building == null)
        {
            return 0;
        }

        var count = def.CostListAdjusted(building.Stuff)
                       .Where(Counted)
                       .Sum(tc => tc.count * def.resourcesFractionWhenDeconstructed);
        return Mathf.RoundToInt(count);
    }

    public int GetCountInChunk(Thing chunk)
    {
        return GetCountInChunk(chunk.def);
    }

    public int GetCountInChunk(ThingDef chunk)
    {
        if (chunk.butcherProducts.NullOrEmpty())
        {
            return 0;
        }

        return chunk.butcherProducts
                    .Where(Counted)
                    .Sum(tc => tc.count);
    }

    public int GetCountInChunks()
    {
        if (_chunksCachedValue.TryGetValue(out int count))
        {
            return count;
        }

        count = Manager.map.listerThings.AllThings
                       .Where(t => t.Faction == Faction.OfPlayer
                                 && !t.IsForbidden(Faction.OfPlayer)
                                 && t.def.IsChunk())
                       .Sum(GetCountInChunk);

        _chunksCachedValue.Update(count);
        return count;
    }

    public int GetCountInDesignations()
    {
        if (_designatedCachedValue.TryGetValue(out int count))
        {
            return count;
        }

        // deconstruction jobs
        count += _designations.Where(d => d.def == DesignationDefOf.Deconstruct)
                              .Sum(d => GetCountInBuilding(d.target.Thing as Building));

        // mining jobs
        var mineralCounts = _designations.Where(d => d.def == DesignationDefOf.Mine && d.target.Cell.IsValid)
                                         .Select(d => Manager
                                            .map.thingGrid.ThingsListAtFast(d.target.Cell)
                                            .FirstOrDefault()?.def)
                                         .Where(d => d != null)
                                         .GroupBy(d => d, d => d, (d, g) => new { def = d, count = g.Count() })
                                         .Where(g => Allowed(g.def));

        foreach (var mineralCount in mineralCounts)
        {
            count += GetCountInMineral(mineralCount.def) * mineralCount.count;
        }

        _designatedCachedValue.Update(count);
        return count;
    }

    public int GetCountInMineral(Mineable rock)
    {
        return GetCountInMineral(rock.def);
    }

    public int GetCountInMineral(ThingDef? rock)
    {
        var resource = rock?.building?.mineableThing;
        if (resource == null || rock == null)
        {
            return 0;
        }

        // stone chunks
        if (resource.IsChunk())
        {
            return (int)(GetCountInChunk(resource) * rock.building.mineableDropChance);
        }

        // metals
        if (Counted(resource))
        {
            return (int)(rock.building.mineableYield * Find.Storyteller.difficulty.mineYieldFactor *
                           rock.building.mineableDropChance);
        }

        return 0;
    }

    public List<Building> GetDeconstructibleBuildingsSorted()
    {
        var position = Manager.map.GetBaseCenter();

        return Manager.map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial).OfType<Building>()
                      .Where(IsValidDeconstructionTarget)
                      .OrderBy(b => -GetCountInBuilding(b) / Distance(b, position))
                      .ToList();
    }

    public List<ThingDef> GetMaterialsInBuilding(ThingDef building)
    {
        if (building == null)
        {
            return [];
        }

        var baseCosts = building.costList.NullOrEmpty()
            ? []
            : building.costList.Select(tc => tc.thingDef);
        var possibleStuffs = DefDatabase<ThingDef>.AllDefsListForReading
                                                  .Where(td => td.IsStuff
                                                             && !td.stuffProps.categories.NullOrEmpty()
                                                             && !building.stuffCategories.NullOrEmpty()
                                                             && td.stuffProps.categories
                                                                  .Intersect(building.stuffCategories).Any());

        return baseCosts.Concat(possibleStuffs).ToList();
    }

    public List<ThingDef> GetMaterialsInChunk(ThingDef chunk)
    {
        var materials = new List<ThingDef>
        {
            chunk
        };

        if (!chunk.butcherProducts.NullOrEmpty())
        {
            materials.AddRange(chunk.butcherProducts.Select(tc => tc.thingDef));
        }

        return materials;
    }

    public List<ThingDef> GetMaterialsInMineral(ThingDef mineral)
    {
        var resource = mineral.building?.mineableThing;
        if (resource == null)
        {
            return [];
        }

        // stone chunks
        if (resource.IsChunk())
        {
            return GetMaterialsInChunk(resource);
        }

        // metals
        var list = new List<ThingDef>
        {
            resource
        };
        return list;
    }

    public List<Mineable> GetMinableMineralsSorted()
    {
        var position = Manager.map.GetBaseCenter();

        return Manager.map.listerThings.AllThings.OfType<Mineable>()
                      .Where(IsValidMiningTarget)
                      .OrderBy(r => -GetCountInMineral(r) / Distance(r, position))
                      .ToList();
    }

    public bool IsARoofSupport_Advanced(Building building)
    {
        if (!CheckRoofSupport || !CheckRoofSupportAdvanced)
        {
            return false;
        }

        // check if any cell in roofing range would collapse if this cell were to be removed
        for (var i = RoofCollapseUtility.RoofSupportRadialCellsCount - 1; i >= 0; i--)
        {
            if (WouldCollapseIfSupportDestroyed(GenRadial.RadialPattern[i] + building.Position, building.Position,
                                                  Manager.map))
            {
                return true;
            }
        }

        return false;
    }

    public bool IsARoofSupport_Basic(Building building)
    {
        if (!CheckRoofSupport || CheckRoofSupportAdvanced)
        {
            return false;
        }

        // simply check location, leaving a grid of pillars
        return IsARoofSupport_Basic(building.Position);
    }

    public bool IsARoofSupport_Basic(IntVec3 cell)
    {
        return cell.x % RoofSupportGridSpacing == 0 && cell.z % RoofSupportGridSpacing == 0;
    }

    public bool IsARoomDivider(Thing target)
    {
        if (!CheckRoomDivision)
        {
            return false;
        }

        var adjacent = GenAdjFast.AdjacentCells8Way(target.Position)
                                 .Where(c => c.InBounds(Manager.map)
                                           && !c.Fogged(Manager.map)
                                           && !c.Impassable(Manager.map))
                                 .ToArray();

        // check if there are more than two rooms in the surrounding cells.
        var rooms = adjacent.Select(c => c.GetRoom(Manager.map))
                            .Where(r => r != null)
                            .Distinct()
                            .ToList();

        if (rooms.Count() >= 2)
        {
            return true;
        }

        // check if any adjacent region is more than x regions from any other region
        for (var i = 0; i < adjacent.Count(); i++)
        {
            for (var j = i + 1; j < adjacent.Count(); j++)
            {
                var path = Manager.map.pathFinder.FindPath(adjacent[i], adjacent[j],
                                                            TraverseParms.For(
                                                                TraverseMode.NoPassClosedDoors, Danger.Some));
                var cost = path.TotalCost;
                path.ReleaseToPool();

                //Logger.Debug($"from {adjacent[i]} to {adjacent[j]}: {cost}");
                if (cost > MaxPathCost)
                {
                    return true;
                }
            }
        }

        return false;
    }


    public bool IsInAllowedArea(Thing target)
    {
        return MiningArea == null || MiningArea.ActiveCells.Contains(target.Position);
    }

    public bool IsRelevantDeconstructionTarget(Building target)
    {
        return target.def.building.IsDeconstructible
            && target.def.resourcesFractionWhenDeconstructed > 0
            && target.def.CostListAdjusted(target.Stuff)
                     .Any(tc => Trigger.ThresholdFilter.Allows(tc.thingDef));
    }

    public bool IsRelevantMiningTarget(Mineable target)
    {
        return GetCountInMineral(target) > 0;
    }

    public bool IsValidDeconstructionTarget(Building target)
    {
        return target != null
            && target.Spawned

            // not ours
            && target.Faction != Faction.OfPlayer

            // not already designated
            && Manager.map.designationManager.DesignationOn(target) == null

            // allowed
            && !target.IsForbidden(Faction.OfPlayer)
            && AllowedBuilding(target.def)

            // drops things we want
            && IsRelevantDeconstructionTarget(target)

            // in allowed area & reachable
            && IsInAllowedArea(target)
            && IsReachable(target)

            // doesn't create safety hazards
            && !IsARoofSupport_Basic(target)
            && !IsARoomDivider(target);
    }

    public bool IsValidDeconstructionTarget(LocalTargetInfo target)
    {
        return target.HasThing
            && target.IsValid
            && target.Thing is Building building
            && IsValidDeconstructionTarget(building);
    }

    public bool IsValidMiningTarget(LocalTargetInfo target)
    {
        return target.HasThing
            && target.IsValid
            && IsValidMiningTarget(target.Thing as Mineable);
    }

    public bool IsValidMiningTarget(Mineable? target)
    {
        // mineable
        return target != null
            && target.def.mineable

            // allowed
            && AllowedMineral(target.def)

            // discovered 
            // NOTE: also in IsReachable, but we expect a lot of fogged tiles, so move this check up a bit.
            && !target.Position.Fogged(Manager.map)

            // not yet designated
            && Manager.map.designationManager.DesignationOn(target) == null

            // matches settings
            && IsInAllowedArea(target)
            && IsRelevantMiningTarget(target)
            && !IsARoomDivider(target)
             &&
               !IsARoofSupport_Basic(
                   target) // note, returns true if advanced checking is enabled - checks will then be done before designating

            // can be reached
            && IsReachable(target);
    }

    public void Notify_ThresholdFilterChanged()
    {
        Logger.Debug("Threshold changed.");
        if (!SyncFilterAndAllowed || Sync == Utilities.SyncDirection.AllowedToFilter)
        {
            return;
        }

        foreach (var building in AllDeconstructibleBuildings)
        {
            if (GetMaterialsInBuilding(building).Any(Trigger.ThresholdFilter.Allows))
            {
                AllowedBuildings.Add(building);
            }
            else
            {
                AllowedBuildings.Remove(building);
            }
        }

        foreach (var mineral in AllMinerals)
        {
            if (GetMaterialsInMineral(mineral).Any(Trigger.ThresholdFilter.Allows))
            {
                AllowedMinerals.Add(mineral);
            }
            else
            {
                AllowedMinerals.Remove(mineral);
            }
        }
    }

    public void RefreshAllBuildingsAndMinerals()
    {
        Logger.Debug("Refreshing all buildings and minerals");

        _allDeconstructibleBuildings = null;
        _allMinerals = null;
    }

    public void SetBuildingAllowed(ThingDef building, bool allow, bool sync = true)
    {
        if (allow)
        {
            AllowedBuildings.Add(building);
        }
        else
        {
            AllowedBuildings.Remove(building);
        }

        if (SyncFilterAndAllowed && sync)
        {
            Sync = Utilities.SyncDirection.AllowedToFilter;

            foreach (var material in GetMaterialsInBuilding(building))
            {
                if (Trigger.ParentFilter.Allows(material))
                {
                    Trigger.ThresholdFilter.SetAllow(material, allow);
                }
            }
        }
    }

    public void SetAllowMineral(ThingDef mineral, bool allow, bool sync = true)
    {
        if (allow)
        {
            AllowedMinerals.Add(mineral);
        }
        else
        {
            AllowedMinerals.Remove(mineral);
        }

        if (SyncFilterAndAllowed && sync)
        {
            Sync = Utilities.SyncDirection.AllowedToFilter;
            foreach (var material in GetMaterialsInMineral(mineral))
            {
                if (Trigger.ParentFilter.Allows(material))
                {
                    Trigger.ThresholdFilter.SetAllow(material, allow);
                }
            }
        }
    }

    public override void Tick()
    {
        History.Update(Trigger.CurrentCount, GetCountInChunks(), GetCountInDesignations());
    }

    public override bool TryDoJob()
    {
        var workDone = false;

        RemoveObsoleteDesignations();
        AddRelevantGameDesignations();

        var count = Trigger.CurrentCount + GetCountInChunks() + GetCountInDesignations();

        if (DeconstructBuildings)
        {
            var buildings = GetDeconstructibleBuildingsSorted();
            for (var i = 0; i < buildings.Count && count < Trigger.TargetCount; i++)
            {
                AddDesignation(buildings[i], DesignationDefOf.Deconstruct);
                count += GetCountInBuilding(buildings[i]);
            }
        }

        var minerals = GetMinableMineralsSorted();
        for (var i = 0; i < minerals.Count && count < Trigger.TargetCount; i++)
        {
            if (!IsARoofSupport_Advanced(minerals[i]))
            {
                AddDesignation(minerals[i], DesignationDefOf.Mine);
                count += GetCountInMineral(minerals[i]);
            }
        }

        return workDone;
    }


    private void AddDesignation(Thing target, DesignationDef designationDef)
    {
        if (designationDef == DesignationDefOf.Deconstruct)
        {
            var building = target as Building;
            if (building?.ClaimableBy(Faction.OfPlayer) ?? false)
            {
                building.SetFaction(Faction.OfPlayer);
            }
        }

        AddDesignation(new Designation(target, designationDef));
    }

    private bool RegionsAreClose(Region start, Region end, int depth = 0)
    {
        if (depth > MaxRegionDistance)
        {
            return false;
        }

        var neighbours = start.Neighbors;
        if (neighbours.Contains(end))
        {
            return true;
        }

        return neighbours.Any(n => RegionsAreClose(n, end, depth + 1));
    }

    private void RemoveObsoleteDesignations()
    {
        // get the intersection of bills in the game and bills in our list.
        var designations = Manager.map.designationManager.AllDesignations
            .Where(d =>
                (d.def == DesignationDefOf.Mine || d.def == DesignationDefOf.Deconstruct) &&
                (!d.target.HasThing || d.target.Thing.Map == Manager.map)); // equates to SpawnedDesignationsOfDef, with two defs.
        _designations = _designations.Intersect(designations).ToList();
    }
}
