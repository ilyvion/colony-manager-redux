// ManagerJob_Foraging.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

public class ManagerJob_Foraging : ManagerJob
{
    private readonly Utilities.CachedValue<int> _cachedCurrentDesignatedCount = new(0);

    public Dictionary<ThingDef, bool> AllowedPlants = [];
    public Area? ForagingArea;
    public bool ForceFullyMature;
    public History History;
    public Utilities.SyncDirection Sync = Utilities.SyncDirection.AllowedToFilter;
    public bool SyncFilterAndAllowed = true;
    public Trigger_Threshold Trigger;

    private List<Designation> _designations = [];

    public ManagerJob_Foraging(Manager manager) : base(manager)
    {
        // populate the trigger field, count all harvested thingdefs from the allowed plant list
        Trigger = new Trigger_Threshold(this);

        // create History tracker
        History = new History(new[] { I18n.HistoryStock, I18n.HistoryDesignated }, [Color.white, Color.grey]);

        // init stuff if we're not loading
        // TODO: please, please refactor this into something less clumsy!
        if (Scribe.mode == LoadSaveMode.Inactive)
        {
            RefreshAllowedPlants();
        }
    }

    public override bool IsCompleted => !Trigger.State;

    public int CurrentDesignatedCount
    {
        get
        {

            // see if we have a cached count
            if (_cachedCurrentDesignatedCount.TryGetValue(out int count))
            {
                return count;
            }

            // fetch count
            foreach (var des in _designations)
            {
                if (!des.target.HasThing)
                {
                    continue;
                }


                if (des.target.Thing is not Plant plant)
                {
                    continue;
                }

                count += plant.YieldNow();
            }

            _cachedCurrentDesignatedCount.Update(count);
            return count;
        }
    }

    public List<Designation> Designations => new(_designations);

    public override bool IsValid => base.IsValid && Trigger != null && History != null;

    public override string Label => "ColonyManagerRedux.Foraging.Foraging".Translate();

    public override ManagerTab Tab => Manager.For(Manager).tabs.Find(tab => tab is ManagerTab_Foraging);

    public override string[] Targets => AllowedPlants
                                       .Keys.Where(key => AllowedPlants[key])
                                       .Select(plant => plant.LabelCap.Resolve()).ToArray();

    public override WorkTypeDef WorkTypeDef => WorkTypeDefOf.Growing;

    public void AddRelevantGameDesignations()
    {
        // get list of game designations not managed by this job that could have been assigned by this job.
        foreach (
            var des in Manager.map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.HarvestPlant)
                              .Except(_designations)
                              .Where(des => IsValidForagingTarget(des.target)))
        {
            AddDesignation(des);
        }
    }

    /// <summary>
    ///     Remove designations in our managed list that are not in the game's designation manager.
    /// </summary>
    public void CleanDeadDesignations()
    {
        var _gameDesignations =
            Manager.map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.HarvestPlant);
        _designations = _designations.Intersect(_gameDesignations).ToList();
    }

    /// <summary>
    ///     Clean up all outstanding designations
    /// </summary>
    public override void CleanUp()
    {
        CleanDeadDesignations();
        foreach (var des in _designations)
        {
            des.Delete();
        }

        _designations.Clear();
    }

    public string DesignationLabel(Designation designation)
    {
        // label, dist, yield.
        var plant = (Plant)designation.target.Thing;
        return "ColonyManagerRedux.Manager.DesignationLabel".Translate(
            plant.LabelCap,
            Distance(plant, Manager.map.GetBaseCenter()).ToString("F0"),
            plant.YieldNow(),
            plant.def.plant.harvestedThingDef.LabelCap);
    }

    public override void DrawListEntry(Rect rect, bool overview = true, bool active = true)
    {
        // (detailButton) | name | (bar | last update)/(stamp) -> handled in Utilities.DrawStatusForListEntry

        // set up rects
        var labelRect = new Rect(
            Margin,
            Margin,
            rect.width - (active ? StatusRectWidth + 4 * Margin : 2 * Margin),
            rect.height - 2 * Margin);
        var statusRect = new Rect(labelRect.xMax + Margin, Margin, StatusRectWidth, rect.height - 2 * Margin);

        // create label string
        var text = Label + "\n";
        var subtext = string.Join(", ", Targets);
        if (subtext.Fits(labelRect))
        {
            text += subtext.Italic();
        }
        else
        {
            text += "multiple".Translate().Resolve().Italic();
        }

        // do the drawing
        GUI.BeginGroup(rect);

        // draw label
        Widgets_Labels.Label(labelRect, text, subtext, TextAnchor.MiddleLeft, margin: Margin);

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
        // scribe base things
        base.ExposeData();

        // settings, references first!
        Scribe_References.Look(ref ForagingArea, "foragingArea");
        Scribe_Deep.Look(ref Trigger, "trigger", Manager);
        Scribe_Collections.Look(ref AllowedPlants, "allowedPlants", LookMode.Def, LookMode.Value);
        Scribe_Values.Look(ref ForceFullyMature, "forceFullyMature");

        if (Manager.LoadSaveMode == Manager.Modes.Normal)
        {
            // scribe history
            Scribe_Deep.Look(ref History, "history");
        }
    }

    public List<ThingDef> GetMaterialsInPlant(ThingDef plantDef)
    {
        var plant = plantDef?.plant;
        if (plant == null)
        {
            throw new ArgumentNullException("no valid plantdef defined");
        }

        return new List<ThingDef>([plant.harvestedThingDef]);
    }

    public void Notify_ThresholdFilterChanged()
    {
        Logger.Debug("Threshold changed.");
        if (!SyncFilterAndAllowed || Sync == Utilities.SyncDirection.AllowedToFilter)
        {
            return;
        }

        foreach (var plant in new List<ThingDef>(AllowedPlants.Keys))
        {
            AllowedPlants[plant] = GetMaterialsInPlant(plant)
               .Any(Trigger.ThresholdFilter.Allows);
        }
    }

    public void RefreshAllowedPlants()
    {
        Logger.Debug("Refreshing allowed plants");

        // all plants that yield something, and it isn't wood.
        var options = Manager.map.Biome.AllWildPlants

                             // cave plants (shrooms)
                             .Concat(DefDatabase<ThingDef>.AllDefsListForReading
                                                           .Where(td => td.plant?.cavePlant ?? false))

                             // ambrosia
                             .Concat(ThingDefOf.Plant_Ambrosia)

                             // and anything on the map that is not in a plant zone/planter
                             .Concat(Manager.map.listerThings.AllThings.OfType<Plant>()
                                             .Where(p => p.Spawned &&
                                                          !(Manager.map.zoneManager.ZoneAt(p.Position) is
                                                              IPlantToGrowSettable) &&
                                                          Manager.map.thingGrid.ThingsAt(p.Position)
                                                                 .FirstOrDefault(
                                                                      t => t is Building_PlantGrower) == null)
                                             .Select(p => p.def)
                                             .Distinct())

                             // that yield something that is not wood
                             .Where(plant => plant.plant.harvestYield > 0 &&
                                              plant.plant.harvestedThingDef != null &&
                                              plant.plant.harvestTag != "Wood")
                             .Distinct();

        foreach (var plant in options)
        {
            if (!AllowedPlants.ContainsKey(plant))
            {
                AllowedPlants.Add(plant, false);
            }
        }

        AllowedPlants = AllowedPlants.OrderBy(plant => plant.Key.LabelCap.RawText)
                                     .ToDictionary(it => it.Key, it => it.Value);
    }

    public void SetPlantAllowed(ThingDef plant, bool allow, bool sync = true)
    {
        if (plant == null)
        {
            throw new ArgumentNullException(nameof(plant));
        }

        AllowedPlants[plant] = allow;

        if (SyncFilterAndAllowed && sync)
        {
            Sync = Utilities.SyncDirection.AllowedToFilter;

            foreach (var material in GetMaterialsInPlant(plant))
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
        History.Update(Trigger.CurrentCount, CurrentDesignatedCount);
    }

    public override bool TryDoJob()
    {
        // keep track of work done
        var workDone = false;

        // clean up designations that were completed.
        CleanDeadDesignations();

        // clean up designations that are (now) in the wrong area.
        CleanAreaDesignations();

        // add designations in the game that could have been handled by this job
        AddRelevantGameDesignations();

        // designate plants until trigger is met.
        var count = Trigger.CurrentCount + CurrentDesignatedCount;
        if (count < Trigger.TargetCount)
        {
            var targets = GetValidForagingTargetsSorted();

            for (var i = 0; i < targets.Count && count < Trigger.TargetCount; i++)
            {
                var des = new Designation(targets[i], DesignationDefOf.HarvestPlant);
                count += targets[i].YieldNow();
                AddDesignation(des);
                workDone = true;
            }
        }

        return workDone;
    }

    private void AddDesignation(Designation des)
    {
        // add to game
        Manager.map.designationManager.AddDesignation(des);

        // add to internal list
        _designations.Add(des);
    }

    private void CleanAreaDesignations()
    {
        foreach (var des in _designations)
        {
            if (!des.target.HasThing)
            {
                des.Delete();
            }

            // if area is not null and does not contain designate location, remove designation.
            else if (!ForagingArea?.ActiveCells.Contains(des.target.Thing.Position) ?? false)
            {
                des.Delete();
            }
        }
    }

    private List<Plant> GetValidForagingTargetsSorted()
    {
        var position = Manager.map.GetBaseCenter();

        return Manager.map.listerThings.AllThings
                      .Where(IsValidForagingTarget)

                      // OrderBy defaults to ascending, switch sign on current yield to get descending
                      .Select(p => (Plant)p)
                      .OrderBy(p => -p.YieldNow() / Distance(p, position))
                      .ToList();
    }

    private bool IsValidForagingTarget(LocalTargetInfo t)
    {
        return t.HasThing
            && IsValidForagingTarget(t.Thing);
    }

    private bool IsValidForagingTarget(Thing t)
    {
        return t is Plant plant && IsValidForagingTarget(plant);
    }

    private bool IsValidForagingTarget(Plant target)
    {
        // should be a plant, and be on the same map as this job
        return target.def.plant != null
            && target.Map == Manager.map

            // non-biome plants won't be on the list, also filters non-yield or wood plants
            && AllowedPlants.ContainsKey(target.def)
            && AllowedPlants[target.def]
            && target.Spawned
            && Manager.map.designationManager.DesignationOn(target) == null

            // cut only mature plants, or non-mature that yield something right now.
            && (!ForceFullyMature && target.YieldNow() > 1
              || target.LifeStage == PlantLifeStage.Mature)

            // limit to area of interest
            && (ForagingArea == null
              || ForagingArea.ActiveCells.Contains(target.Position))

            // reachable
            && IsReachable(target);
    }
}
