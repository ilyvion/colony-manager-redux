﻿// ManagerJob_Mining.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Buffers;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerJob_Mining
    : ManagerJob<ManagerSettings_Mining>, INotifyStoneChunkMined
{
    public sealed class History : HistoryWorker<ManagerJob_Mining>
    {
        public override Coroutine GetCountForHistoryChapterCoroutine(
            ManagerJob_Mining managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> count)
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryStock)
            {
                yield return managerJob.TriggerThreshold.GetCurrentCountCoroutine(count)
                    .ResumeWhenOtherCoroutineIsCompleted();
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryDesignated)
            {
                count.Value = managerJob.GetCountInDesignations(cached: false);
            }
            else if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryChunks)
            {
                count.Value = managerJob.GetCountInChunks(cached: false);
            }
            else
            {
                throw new ArgumentException($"Unexpected chapterDef value {chapterDef.defName}");
            }
        }

        public override Coroutine GetTargetForHistoryChapterCoroutine(
            ManagerJob_Mining managerJob,
            int tick,
            ManagerJobHistoryChapterDef chapterDef,
            Boxed<int> target)
        {
            if (chapterDef == ManagerJobHistoryChapterDefOf.CM_HistoryStock)
            {
                target.Value = managerJob.TriggerThreshold.TargetCount;
            }
            else
            {
                target.Value = 0;
            }
            yield break;
        }
    }

    private const int RoofSupportGridSpacing = 5;
    private readonly CachedValue<int> _chunksCachedValue = new(0);
    private readonly CachedValue<ChunkProcessingKind> _chunkProductKindCachedValue
        = new(ChunkProcessingKind.Neither);
    private readonly CachedValue<int> _designatedCachedValue = new(0);
    public HashSet<ThingDef> AllowedBuildings = [];

    public HashSet<ThingDef> AllowedMinerals = [];
    public bool MineThickRoofs = true;
    public bool CheckRoofSupport = true;
    public bool CheckRoofSupportAdvanced;
    public bool CheckRoomDivision = true;
    public bool HaulMapChunks = true;
    public bool HaulMinedChunks = true;
    public bool DeconstructBuildings;
    public bool DeconstructAncientDangerWhenFogged;
    public Area? MiningArea;
    public Utilities.SyncDirection Sync = Utilities.SyncDirection.AllowedToFilter;

    public bool SyncFilterAndAllowed = true;
    private List<Designation> _designations = [];

    private List<ThingDef>? _allDeconstructibleBuildings;
    public List<ThingDef> AllDeconstructibleBuildings
    {
        get
        {
            _allDeconstructibleBuildings ??=
                Utilities_Mining.GetDeconstructibleBuildings(Manager).ToList();
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

    public Trigger_Threshold TriggerThreshold => (Trigger_Threshold)Trigger!;



    public ManagerJob_Mining(Manager manager) : base(manager)
    {
        // populate the trigger field
        Trigger = new Trigger_Threshold(this);
        ConfigureThresholdTriggerParentFilter();
        TriggerThreshold.SettingsChanged = Notify_ThresholdFilterChanged;
    }

    public override void PostMake()
    {
        var miningSettings = ManagerSettings;
        if (miningSettings != null)
        {
            SyncFilterAndAllowed = miningSettings.DefaultSyncFilterAndAllowed;
            HaulMapChunks = miningSettings.DefaultHaulMapChunks;
            HaulMinedChunks = miningSettings.DefaultHaulMinedChunks;
            DeconstructBuildings = miningSettings.DefaultDeconstructBuildings;
            DeconstructAncientDangerWhenFogged =
                miningSettings.DefaultDeconstructAncientDangerWhenFogged;
            CheckRoofSupport = miningSettings.DefaultCheckRoofSupport;
            CheckRoofSupportAdvanced = miningSettings.DefaultCheckRoofSupportAdvanced;
            CheckRoomDivision = miningSettings.DefaultCheckRoomDivision;
            MineThickRoofs = miningSettings.DefaultMineThickRoofs;
        }
    }

    public override void PostImport()
    {
        base.PostImport();

        AllowedMinerals.RemoveWhere(m => !AllMinerals.Contains(m));
        AllowedBuildings.RemoveWhere(b => !AllDeconstructibleBuildings.Contains(b));
    }

    public List<Designation> Designations => new(_designations);


    public override bool IsValid => base.IsValid && TriggerThreshold != null;

    public override IEnumerable<string> Targets => AllowedMinerals
        .Select(pk => pk.LabelCap.Resolve());

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

    private void AddDesignation(Thing target, DesignationDef designationDef)
    {
        AddDesignation(new Designation(target, designationDef));
    }

    private void AddDesignation(Designation designation)
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

    public Coroutine AddRelevantGameDesignations(ManagerLog? jobLog = null)
    {
        int addedMineCount = 0;
        int addedDeconstructCount = 0;
        int addedHaulCount = 0;

        foreach (var des in Manager.map.designationManager
            .SpawnedDesignationsOfDef(DesignationDefOf.Mine)
            .Except(_designations)
            .Where(des => IsValidMiningTarget(des.target)))
        {
            addedMineCount++;
            AddDesignation(des);
        }
        yield return ResumeImmediately.Singleton;

        foreach (var des in Manager.map.designationManager
            .SpawnedDesignationsOfDef(DesignationDefOf.Deconstruct)
            .Except(_designations)
            .Where(des => IsValidDeconstructionTarget(des.target)))
        {
            addedDeconstructCount++;
            AddDesignation(des);
        }
        yield return ResumeImmediately.Singleton;

        foreach (var des in Manager.map.designationManager
            .SpawnedDesignationsOfDef(DesignationDefOf.Haul)
            .Except(_designations)
            .Where(des => des.target.HasThing &&
                des.target.Thing.def.GetChunkProducts().Any(Counted)))
        {
            addedHaulCount++;
            AddDesignation(des);
        }

        if (addedMineCount > 0 || addedDeconstructCount > 0 || addedHaulCount > 0)
        {
            jobLog?.AddDetail("ColonyManagerRedux.Mining.Logs.AddRelevantGameDesignations"
                .Translate(addedMineCount, addedDeconstructCount, addedHaulCount));
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

    public override void CleanUp(ManagerLog? jobLog)
    {
        CleanDeadDesignations(_designations, null, jobLog);

        var originalCount = _designations.Count;

        // cancel outstanding designation
        foreach (var designation in _designations)
        {
            designation.Delete();
        }

        // clear the list completely
        _designations.Clear();

        var newCount = _designations.Count;
        if (originalCount != newCount)
        {
            jobLog?.AddDetail("ColonyManagerRedux.Logs.CleanJobCompletedDesignations"
                .Translate(originalCount - newCount, originalCount, newCount));
        }
    }

    public bool Counted(ThingDefCountClass thingDefCount)
    {
        return Counted(thingDefCount.thingDef);
    }

    public bool Counted(ThingDef thingDef)
    {
        return TriggerThreshold.ThresholdFilter.Allows(thingDef);
    }

    public string DesignationLabel(Designation designation)
    {
        if (designation.def == DesignationDefOf.Deconstruct)
        {
            var building = designation.target.Thing;
            return "ColonyManagerRedux.Job.DesignationLabel".Translate(
                building.LabelCap,
                Distance(building, Manager.map.GetBaseCenter()).ToString("F0"),
                "?", "?");
        }

        if (designation.def == DesignationDefOf.Mine)
        {
            var mineable = designation.target.Cell.GetFirstMineable(Manager.map);
            return "ColonyManagerRedux.Job.DesignationLabel".Translate(
                mineable.LabelCap,
                Distance(mineable, Manager.map.GetBaseCenter()).ToString("F0"),
                GetCountInMineral(mineable),
                GetMaterialsInMineral(mineable.def)?.First().LabelCap ?? "?");
        }

        if (designation.def == DesignationDefOf.Haul && designation.target.HasThing)
        {
            var thing = designation.target.Thing;
            return "ColonyManagerRedux.Job.DesignationLabel".Translate(
                thing.LabelCap,
                Distance(thing, Manager.map.GetBaseCenter()).ToString("F0"),
                GetCountInChunk(thing),
                thing.def.GetChunkProducts().First().thingDef.LabelCap);
        }

        return string.Empty;
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Collections.Look(ref AllowedMinerals, "allowedMinerals", LookMode.Def);
        Scribe_Collections.Look(ref AllowedBuildings, "allowedBuildings", LookMode.Def);
        Scribe_Values.Look(ref SyncFilterAndAllowed, "syncFilterAndAllowed", true);
        Scribe_Values.Look(ref HaulMapChunks, "haulMapChunks", true);
        Scribe_Values.Look(ref HaulMinedChunks, "haulMinedChunks", true);
        Scribe_Values.Look(ref DeconstructBuildings, "deconstructBuildings", false);
        Scribe_Values.Look(
            ref DeconstructAncientDangerWhenFogged,
            "deconstructAncientDangerWhenFogged",
            false);
        Scribe_Values.Look(ref CheckRoofSupport, "checkRoofSupport", true);
        Scribe_Values.Look(ref CheckRoofSupportAdvanced, "checkRoofSupportAdvanced");
        Scribe_Values.Look(ref CheckRoomDivision, "checkRoomDivision", true);
        Scribe_Values.Look(ref MineThickRoofs, "mineThickRoofs", true);

        // don't store history in import/export mode.
        if (Manager.ScribeGameSpecificData)
        {
            Scribe_References.Look(ref MiningArea, "miningArea");

            Utilities.Scribe_Designations(ref _designations, Manager);
        }

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            ConfigureThresholdTriggerParentFilter();
            TriggerThreshold.SettingsChanged = Notify_ThresholdFilterChanged;
        }
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
        if (chunk.butcherProducts.NullOrEmpty() && chunk.smeltProducts.NullOrEmpty())
        {
            return 0;
        }

        return chunk.GetChunkProducts()
            .Where(Counted)
            .Sum(tc => tc.count);
    }

    public ChunkProcessingKind GetChunkProductKind()
    {
        if (_chunkProductKindCachedValue.TryGetValue(out var chunkProductKind))
        {
            return chunkProductKind;
        }

        chunkProductKind = ChunkProcessingKind.Neither;
        foreach (var chunk in DefDatabase<ThingDef>.AllDefs.Where(t => t.IsChunk() &&
            ((t.butcherProducts?.Any(Counted) ?? false) ||
                (t.smeltProducts?.Any(Counted) ?? false))))
        {
            if (chunk.butcherProducts != null)
            {
                chunkProductKind |= ChunkProcessingKind.Stonecutting;
            }
            if (chunk.smeltProducts != null)
            {
                chunkProductKind |= ChunkProcessingKind.Smelting;
            }

            if (chunkProductKind == ChunkProcessingKind.Both)
            {
                break;
            }
        }

        _chunkProductKindCachedValue.Update(chunkProductKind);
        return chunkProductKind;
    }

    public int GetCountInChunks(bool cached = true)
    {
        if (cached && _chunksCachedValue.TryGetValue(out int count))
        {
            return count;
        }

        count = Manager.map.listerThings.AllThings
            .Where(t => t.def.IsChunk()
                && t.IsInAnyStorage()
                && !t.IsForbidden(Faction.OfPlayer))
            .Sum(GetCountInChunk);

        _chunksCachedValue.Update(count);
        return count;
    }

    public int GetCountInDesignations(bool cached = true)
    {
        if (cached && _designatedCachedValue.TryGetValue(out int cachedCount))
        {
            return cachedCount;
        }

        if (!cached)
        {
            CleanDeadDesignations(_designations, null, null);
            AddRelevantGameDesignations().RunImmediatelyToCompletion();
        }

        // deconstruction jobs
        var count = _designations
            .Where(d => d.def == DesignationDefOf.Deconstruct)
            .Sum(d => GetCountInBuilding(d.target.Thing as Building));

        // mining jobs
        var mineralCounts = _designations
            .Where(d => d.def == DesignationDefOf.Mine && d.target.Cell.IsValid)
            .Select(d => Manager.map.thingGrid.ThingsListAtFast(d.target.Cell)
                .FirstOrDefault()?.def)
            .Where(d => d != null)
            .GroupBy(d => d, d => d, (d, g) => new { def = d, count = g.Count() })
            .Where(g => Allowed(g.def));

        foreach (var mineralCount in mineralCounts)
        {
            count += GetCountInMineral(mineralCount.def) * mineralCount.count;
        }

        // hauling jobs
        count += _designations
            .Where(d => d.def == DesignationDefOf.Haul && d.target.HasThing)
            .Sum(d => GetCountInChunk(d.target.Thing));

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

    public static IEnumerable<ThingDef> GetMaterialsInBuilding(ThingDef building)
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
                && td.stuffProps.categories.Intersect(building.stuffCategories).Any());

        return baseCosts.Concat(possibleStuffs);
    }

    public static IEnumerable<ThingDef> GetMaterialsInChunk(ThingDef chunk)
    {
        return chunk.GetChunkProducts().Select(tc => tc.thingDef);
    }

    private readonly CachedValues<ThingDef, List<ThingDef>> _materialsInMineralCache = new();
    public List<ThingDef> GetMaterialsInMineral(ThingDef mineral)
    {
        if (!_materialsInMineralCache.TryGetValue(mineral, out var materials))
        {
            _materialsInMineralCache.Add(mineral, () => UpdateMaterialsInMineral(mineral));
            materials = _materialsInMineralCache[mineral];
        }
        return materials!;

        static List<ThingDef> UpdateMaterialsInMineral(ThingDef mineral)
        {
            var resource = mineral.building?.mineableThing;
            if (resource == null)
            {
                return [];
            }

            // stone chunks
            if (resource.IsChunk())
            {
                return GetMaterialsInChunk(resource).ToList();
            }

            // metals
            List<ThingDef> list = [resource];
            return list;
        }
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

    public static bool IsARoofSupport_Basic(IntVec3 cell)
    {
        return cell.x % RoofSupportGridSpacing == 0 && cell.z % RoofSupportGridSpacing == 0;
    }

    private const float MaxPathCost = 500f;
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
            .Distinct();

        if (rooms.Count() >= 2)
        {
            return true;
        }

        // check if any adjacent region is more than x regions from any other region
        for (var i = 0; i < adjacent.Length; i++)
        {
            for (var j = i + 1; j < adjacent.Length; j++)
            {
                var path = Manager.map.pathFinder.FindPath(adjacent[i], adjacent[j],
                    TraverseParms.For(TraverseMode.NoPassClosedDoors, Danger.Some));
                var cost = path.TotalCost;
                path.ReleaseToPool();

                if (cost > MaxPathCost)
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool IsAllowedToMineRoofAt(Thing target)
    {
        if (MineThickRoofs)
        {
            return true;
        }

        return !target.Map.roofGrid.RoofAt(target.Position)?.isThickRoof ?? true;
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
                .Any(tc => TriggerThreshold.ThresholdFilter.Allows(tc.thingDef));
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
            // note, returns true if advanced checking is enabled - checks will then be done before designating
            && !IsARoofSupport_Basic(target)
            && IsAllowedToMineRoofAt(target)

            // can be reached
            && IsReachable(target);
    }

    public void Notify_ThresholdFilterChanged()
    {
        ColonyManagerReduxMod.Instance.LogDebug("Threshold changed.");

        _chunksCachedValue.Invalidate();
        _chunkProductKindCachedValue.Invalidate();

        if (!SyncFilterAndAllowed || Sync == Utilities.SyncDirection.AllowedToFilter)
        {
            return;
        }

        foreach (var building in AllDeconstructibleBuildings)
        {
            if (GetMaterialsInBuilding(building).Any(TriggerThreshold.ThresholdFilter.Allows))
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
            if (GetMaterialsInMineral(mineral).Any(TriggerThreshold.ThresholdFilter.Allows))
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
        ColonyManagerReduxMod.Instance.LogDebug("Refreshing all buildings and minerals");

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
                var setAllow =
                    AllowedBuildings.Any(b => GetMaterialsInBuilding(b).Contains(material)) ||
                    AllowedMinerals.Any(m => GetMaterialsInMineral(m).Contains(material));
                TriggerThreshold.ThresholdFilter.SetAllow(material, setAllow);
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
                var setAllow =
                    AllowedBuildings.Any(b => GetMaterialsInBuilding(b).Contains(material)) ||
                    AllowedMinerals.Any(m => GetMaterialsInMineral(m).Contains(material));
                TriggerThreshold.ThresholdFilter.SetAllow(material, setAllow);
            }
        }
    }

    public override Coroutine TryDoJobCoroutine(ManagerLog jobLog, Boxed<bool> workDone)
    {
        if (!TriggerThreshold.State)
        {
            if (JobState != ManagerJobState.Completed)
            {
                JobState = ManagerJobState.Completed;
                jobLog.AddDetail("ColonyManagerRedux.Logs.JobCompleted".Translate());

                CleanUp(jobLog);
            }
            yield break;
        }
        else
        {
            JobState = ManagerJobState.Active;
        }

        // clean up designations that were completed.
        CleanDeadDesignations(_designations, null, jobLog);
        yield return ResumeImmediately.Singleton;

        // add designations in the game that could have been handled by this job
        yield return AddRelevantGameDesignations(jobLog).ResumeWhenOtherCoroutineIsCompleted();

        // designate work until trigger is met.
        var count = TriggerThreshold.GetCurrentCount()
            + GetCountInChunks()
            + GetCountInDesignations();

        if (count >= TriggerThreshold.TargetCount)
        {
            yield break;
        }

        yield return ResumeImmediately.Singleton;

        // Prioritize chunks; it's the lowest hanging "fruit" in terms of effort
        if (HaulMapChunks)
        {
            Map map = Manager.map;
            List<Thing> sortedChunks = [];
            yield return GetTargetsSorted(
                sortedChunks,
                t => t.def.IsChunk()
                    && !t.IsInAnyStorage()
                    && !t.IsForbidden(Faction.OfPlayer)
                    && !map.reservationManager.IsReserved(t)
                    && GetCountInChunk(t) > 0,
                (c, d) => GetCountInChunk(c) / d)
                .ResumeWhenOtherCoroutineIsCompleted();

            foreach (var (chunk, i) in sortedChunks.Select((c, i) => (c, i)))
            {
                if (count >= TriggerThreshold.TargetCount)
                {
                    break;
                }

                int chunkCount = GetCountInChunk(chunk);
                AddDesignation(chunk, DesignationDefOf.Haul);
                count += chunkCount;

                jobLog.AddDetail("ColonyManagerRedux.Logs.AddDesignation"
                    .Translate(
                        DesignationDefOf.Haul.ActionText(),
                        "ColonyManagerRedux.Mining.Logs.Chunk".Translate(),
                        chunk.Label,
                        chunkCount,
                        count,
                        TriggerThreshold.TargetCount),
                    chunk);

                workDone.Value = true;

                if (i > 0 && i % Constants.CoroutineBreakAfter == 0)
                {
                    yield return ResumeImmediately.Singleton;
                }
            }
        }

        if (DeconstructBuildings)
        {
            List<Building> sortedBuildings = [];
            yield return GetTargetsSorted(
                sortedBuildings,
                IsValidDeconstructionTarget,
                (b, d) => GetCountInBuilding(b) / d)
                .ResumeWhenOtherCoroutineIsCompleted();

            var ancientDangerRects = Manager.AncientDangerRects;
            List<LocalTargetInfo> skippedAncientDangerTargets = [];

            foreach (var (building, i) in sortedBuildings.Select((c, i) => (c, i)))
            {
                if (count >= TriggerThreshold.TargetCount)
                {
                    break;
                }

                int buildingCount = GetCountInBuilding(building);

                if (!DeconstructAncientDangerWhenFogged)
                {
                    for (int j = ancientDangerRects.Count - 1; j >= 0; j--)
                    {
                        CellRect ancientDangerRect = ancientDangerRects[j];
                        if (!ancientDangerRect.CenterCell.Fogged(Manager))
                        {
                            ancientDangerRects.RemoveAt(j);
                            continue;
                        }

                        if (ancientDangerRect.Contains(building.Position))
                        {
                            skippedAncientDangerTargets.Add(building);
                            break;
                        }
                    }
                }
                AddDesignation(building, DesignationDefOf.Deconstruct);
                count += buildingCount;

                jobLog.AddDetail("ColonyManagerRedux.Logs.AddDesignation"
                    .Translate(
                        DesignationDefOf.Deconstruct.ActionText(),
                        "ColonyManagerRedux.Mining.Logs.Building".Translate(),
                        building.Label,
                        buildingCount,
                        count,
                        TriggerThreshold.TargetCount),
                    building);

                workDone.Value = true;

                if (i > 0 && i % Constants.CoroutineBreakAfter == 0)
                {
                    yield return ResumeImmediately.Singleton;
                }
            }
            if (skippedAncientDangerTargets.Count > 0)
            {
                jobLog.AddDetail("ColonyManagerRedux.Mining.Logs.SkippedAncientDangerBuildings"
                    .Translate(
                        skippedAncientDangerTargets.Count),
                    skippedAncientDangerTargets);
            }
        }

        List<Mineable> sortedMineable = [];
        yield return GetTargetsSorted(
            sortedMineable,
            IsValidMiningTarget,
            (m, d) => GetCountInMineral(m) / d)
            .ResumeWhenOtherCoroutineIsCompleted();

        foreach (var (mineable, i) in sortedMineable.Select((c, i) => (c, i)))
        {
            if (count >= TriggerThreshold.TargetCount)
            {
                break;
            }

            int mineableCount = GetCountInMineral(mineable);

            if (!IsARoofSupport_Advanced(mineable))
            {
                workDone.Value = true;
                AddDesignation(mineable, DesignationDefOf.Mine);
                count += mineableCount;

                jobLog.AddDetail("ColonyManagerRedux.Logs.AddDesignation"
                    .Translate(
                        DesignationDefOf.Mine.ActionText(),
                        "ColonyManagerRedux.Mining.Logs.Rock".Translate(),
                        mineable.Label,
                        mineableCount,
                        count,
                        TriggerThreshold.TargetCount),
                    mineable);

                if (i > 0 && i % Constants.CoroutineBreakAfter == 0)
                {
                    yield return ResumeImmediately.Singleton;
                }
            }
        }
    }

    private const int MaxRegionDistance = 4;
    private static bool RegionsAreClose(Region start, Region end, int depth = 0)
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

    protected override IEnumerable<Designation> GetIntersectionDesignations(DesignationDef? designationDef)
    {
        return Manager.map.designationManager.AllDesignations
            .Where(d =>
                (d.def == DesignationDefOf.Mine ||
                    d.def == DesignationDefOf.Deconstruct ||
                    d.def == DesignationDefOf.Haul) &&
                (!d.target.HasThing || d.target.Thing.Map == Manager.map));
    }

    private void ConfigureThresholdTriggerParentFilter()
    {
        foreach (var mineral in AllMinerals)
        {
            TriggerThreshold.ParentFilter.SetAllow(mineral.building.mineableThing, true);
        }
        foreach (var material in AllDeconstructibleBuildings
            .SelectMany(GetMaterialsInBuilding)
            .Distinct())
        {
            TriggerThreshold.ParentFilter.SetAllow(material, true);
        }
        TriggerThreshold.ParentFilter.SetAllow(ThingCategoryDefOf.Chunks, false);
    }

    public void Notify_StoneChunkMined(Pawn _, Thing thing)
    {
        if (!HaulMinedChunks)
        {
            return;
        }

        if (thing.def.designateHaulable && thing.def.GetChunkProducts().Any(Counted) &&
            _designations.Any(d => d.target.Cell == thing.Position))
        {
            AddDesignation(thing, DesignationDefOf.Haul);
        }
    }

    protected override void Notify_AreaRemoved(Area area)
    {
        if (MiningArea == area)
        {
            MiningArea = null;
        }
    }
}

[Flags]
internal enum ChunkProcessingKind
{
    Neither = 0x0,
    Stonecutting = 0x1,
    Smelting = 0x2,
    Both = Stonecutting | Smelting
}
