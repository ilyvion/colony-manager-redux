// Utilities.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Collections.ObjectModel;
using System.Reflection;
using CircularBuffer;
using static ColonyManagerRedux.Constants;
using static ColonyManagerRedux.Widgets_Labels;

namespace ColonyManagerRedux;

[HotSwappable]
public static class Utilities
{
    public enum SyncDirection
    {
        FilterToAllowed,
        AllowedToFilter
    }

    private static List<UpdateInterval>? _updateIntervalOptions;

    public static List<UpdateInterval> UpdateIntervalOptions
    {
        get
        {
            if (_updateIntervalOptions.NullOrEmpty())
            {
                _updateIntervalOptions =
                [
                    new UpdateInterval(GenDate.TicksPerHour, "ColonyManagerRedux.ManagerHourly".Translate()),
                    new UpdateInterval(GenDate.TicksPerHour * 2, "ColonyManagerRedux.ManagerMultiHourly".Translate(2)),
                    new UpdateInterval(GenDate.TicksPerHour * 4, "ColonyManagerRedux.ManagerMultiHourly".Translate(4)),
                    new UpdateInterval(GenDate.TicksPerHour * 8, "ColonyManagerRedux.ManagerMultiHourly".Translate(8)),
                    UpdateInterval.Daily,
                    new UpdateInterval(GenDate.TicksPerTwelfth, "ColonyManagerRedux.ManagerMonthly".Translate()),
                    new UpdateInterval(GenDate.TicksPerYear, "ColonyManagerRedux.ManagerYearly".Translate()),
                ];
            }

            return _updateIntervalOptions!;
        }
    }

    public static int CountProducts(
        this Map map,
        ThingFilter filter,
        Zone_Stockpile? stockpile = null,
        bool countAllOnMap = false)
    {
        if (map == null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        if (filter == null)
        {
            throw new ArgumentNullException(nameof(filter));
        }

        var key = new MapStockpileFilter(filter, stockpile, countAllOnMap);
        if (TryGetCached(map, key, out var count))
        {
            return count;
        }

        foreach (var td in filter.AllowedThingDefs)
        {
            // if it counts as a resource and we're not limited to a single stockpile, use the ingame counter (e.g. only steel in stockpiles.)
            if (!countAllOnMap &&
                 td.CountAsResource &&
                 stockpile == null)
            {
                // we don't need to bother with quality / hitpoints as these are non-existant/irrelevant for resources.
                count += map.resourceCounter.GetCount(td);
            }
            else
            {
                // otherwise, go look for stuff that matches our filters.
                var thingList = map.listerThings.ThingsOfDef(td);

                // if filtered by stockpile, filter the thinglist accordingly.
                if (stockpile != null)
                {
                    var areaSlotGroup = stockpile.slotGroup;
                    thingList = thingList.Where(t => t.Position.GetSlotGroup(map) == areaSlotGroup).ToList();
                }

                foreach (var t in thingList)
                {
                    if (t.IsForbidden(Faction.OfPlayer) ||
                         t.Position.Fogged(map))
                    {
                        continue;
                    }

                    if (t.TryGetQuality(out QualityCategory quality))
                    {
                        if (!filter.AllowedQualityLevels.Includes(quality))
                        {
                            continue;
                        }
                    }

                    if (filter.AllowedHitPointsPercents.IncludesEpsilon(t.HitPoints))
                    {
                        continue;
                    }


#if DEBUG_COUNTS
                    Log.Message(t.LabelCap + ": " + t.stackCount);
#endif

                    count += t.stackCount;
                }
            }

            // update cache if exists.
            var countCache = Manager.For(map).CountCache;
            if (countCache.TryGetValue(key, out FilterCountCache? value))
            {
                value.Cache = count;
                value.TimeSet = Find.TickManager.TicksGame;
            }
            else
            {
                countCache.Add(key, new FilterCountCache(count));
            }
        }

        return count;
    }

    public static void DrawReachabilityToggle(ref Vector2 pos, float width, ref bool reachability)
    {
        DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.ManagerCheckReachability".Translate(),
            "ColonyManagerRedux.ManagerCheckReachability.Tip".Translate(),
            ref reachability,
            expensive: true);
    }

    public static void DrawStatusForListEntry(
        this ManagerJob job,
        Rect rect,
        Trigger trigger,
        bool exporting)
    {
        if (job == null)
        {
            throw new ArgumentNullException(nameof(job));
        }
        if (trigger == null)
        {
            throw new ArgumentNullException(nameof(trigger));
        }

        // set up rects
        var stampRect = new Rect(
            rect.xMax - ManagerTab.StampWidth,
            rect.yMin,
            ManagerTab.StampWidth,
            ManagerTab.StampWidth).CenteredOnYIn(rect);
        var lastUpdateRect = new Rect(
            stampRect.xMin - Margin - ManagerTab.LastUpdateRectWidth,
            rect.yMin,
            ManagerTab.LastUpdateRectWidth,
            rect.height);
        var progressRect = new Rect(
            lastUpdateRect.xMin - Margin - ManagerTab.ProgressRectWidth,
            rect.yMin,
            ManagerTab.ProgressRectWidth,
            rect.height);

        // draw stamp
        if (!exporting)
        {
            if (Widgets.ButtonImage(
                stampRect,
                job.IsSuspended ? Resources.StampStart :
                job.IsCompleted ? Resources.StampCompleted : Resources.StampSuspended))
            {
                job.IsSuspended = !job.IsSuspended;
            }

            if (job.IsSuspended)
            {
                TooltipHandler.TipRegion(stampRect,
                    "ColonyManagerRedux.Overview.JobHasBeenSuspendedTooltip".Translate() + "\n\n" +
                    "ColonyManagerRedux.Overview.ClickToChangeJob".Translate(
                        "ColonyManagerRedux.Overview.Unsuspend".Translate()));
            }
            else if (job.IsCompleted)
            {
                TooltipHandler.TipRegion(stampRect,
                    "ColonyManagerRedux.Overview.JobHasbeenCompletedTooltip".Translate() + "\n\n" +
                    "ColonyManagerRedux.Overview.ClickToChangeJob".Translate(
                        "ColonyManagerRedux.Overview.Suspend".Translate()));
            }
            else
            {
                TooltipHandler.TipRegion(stampRect,
                    "ColonyManagerRedux.Overview.ClickToChangeJob".Translate(
                        "ColonyManagerRedux.Overview.Suspend".Translate()));
            }

            // draw progress bar
            trigger.DrawProgressBars(progressRect, !job.IsSuspended && !job.IsCompleted);
        }

        if (!job.IsSuspended && !job.IsCompleted)
        {
            // draw update interval
            var timeSinceLastUpdate = Find.TickManager.TicksGame - job.LastActionTick;
            UpdateInterval.Draw(lastUpdateRect, job, exporting);
        }
    }

    public static void DrawToggle(ref Vector2 pos, float width, string label, TipSignal tooltip, ref bool checkOn,
                                   bool expensive = false, float size = SmallIconSize, float margin = Margin,
                                   GameFont font = GameFont.Small,
                                   bool wrap = true)
    {
        var toggleRect = new Rect(
            pos.x,
            pos.y,
            width,
            ListEntryHeight);
        pos.y += ListEntryHeight;
        DrawToggle(toggleRect, label, tooltip, ref checkOn, expensive, size, margin, font, wrap);
    }

    public static void DrawToggle(Rect rect, string label, TipSignal tooltip, ref bool checkOn,
                                   bool expensive = false, float size = SmallIconSize, float margin = Margin,
                                   GameFont font = GameFont.Small, bool wrap = true)
    {
        // set up rects
        var labelRect = rect;
        labelRect.xMax -= size + margin * 2;
        var iconRect = new Rect(rect.xMax - size - margin, 0f, size, size).CenteredOnYIn(labelRect);

        // draw label
        Label(labelRect, label, TextAnchor.MiddleLeft, font, margin: margin, wrap: wrap);

        // tooltip
        TooltipHandler.TipRegion(rect, tooltip);

        // draw check
        if (checkOn)
        {
            GUI.DrawTexture(iconRect, Widgets.CheckboxOnTex);
        }
        else
        {
            GUI.DrawTexture(iconRect, Widgets.CheckboxOffTex);
        }

        // draw expensive icon
        if (expensive)
        {
            iconRect.x -= size + margin;
            TooltipHandler.TipRegion(iconRect, "ColonyManagerRedux.ManagerExpensive.Tip".Translate());
            GUI.color = checkOn ? Resources.Orange : Color.grey;
            GUI.DrawTexture(iconRect, Resources.Stopwatch);
            GUI.color = Color.white;
        }

        // interactivity
        Widgets.DrawHighlightIfMouseover(rect);
        if (Widgets.ButtonInvisible(rect))
        {
            checkOn = !checkOn;
        }
    }

    public static void DrawToggle(ref Vector2 pos, float width, string label, TipSignal tooltip, bool checkOn,
                                   Action on, Action off,
                                   bool expensive = false, float size = SmallIconSize, float margin = Margin,
                                   GameFont font = GameFont.Small,
                                   bool wrap = true)
    {
        var toggleRect = new Rect(
            pos.x,
            pos.y,
            width,
            ListEntryHeight);
        pos.y += ListEntryHeight;
        DrawToggle(toggleRect, label, tooltip, checkOn, on, off, expensive, size, margin, wrap);
    }

    public static void DrawToggle(ref Vector2 pos, float width, string label, TipSignal tooltip, bool checkOn,
                                   bool checkOff, Action on, Action off,
                                   bool expensive = false, float size = SmallIconSize, float margin = Margin,
                                   GameFont font = GameFont.Small,
                                   bool wrap = true)
    {
        var toggleRect = new Rect(
            pos.x,
            pos.y,
            width,
            ListEntryHeight);
        pos.y += ListEntryHeight;
        DrawToggle(toggleRect, label, tooltip, checkOn, checkOff, on, off, expensive, size, margin, wrap);
    }

    public static void DrawToggle(Rect rect, string label, TipSignal tooltip, bool checkOn, Action on, Action off,
                                   bool expensive = false, float size = SmallIconSize, float margin = Margin,
                                   bool wrap = true)
    {
        DrawToggle(rect, label, tooltip, checkOn, !checkOn, on, off, expensive, size, margin, wrap);
    }


    public static void DrawToggle(Rect rect, string label, TipSignal? tooltip, bool allOn, bool allOff, Action on,
                                   Action off, bool expensive = false, float size = SmallIconSize,
                                   float margin = Margin, bool wrap = true)
    {
        if (on == null)
        {
            throw new ArgumentNullException(nameof(on));
        }
        if (off == null)
        {
            throw new ArgumentNullException(nameof(off));
        }

        // set up rects
        var labelRect = rect;
        var iconRect = new Rect(rect.xMax - size - margin, 0f, size, size);
        labelRect.xMax = iconRect.xMin - Margin / 2f;

        // finetune rects
        iconRect = iconRect.CenteredOnYIn(labelRect);

        // draw label
        Label(rect, label, TextAnchor.MiddleLeft, GameFont.Small, margin: margin, wrap: wrap);

        // tooltip
        if (tooltip.HasValue)
        {
            TooltipHandler.TipRegion(rect, tooltip.Value);
        }

        // draw check
        if (allOn)
        {
            GUI.DrawTexture(iconRect, Widgets.CheckboxOnTex);
        }
        else if (allOff)
        {
            GUI.DrawTexture(iconRect, Widgets.CheckboxOffTex);
        }
        else
        {
            GUI.DrawTexture(iconRect, Widgets.CheckboxPartialTex);
        }

        // draw expensive icon
        if (expensive)
        {
            iconRect.x -= size + margin;
            TooltipHandler.TipRegion(iconRect, "ColonyManagerRedux.ManagerExpensive.Tip".Translate());
            GUI.color = allOn ? Resources.Orange : Color.grey;
            GUI.DrawTexture(iconRect, Resources.Stopwatch);
            GUI.color = Color.white;
        }

        // interactivity
        Widgets.DrawHighlightIfMouseover(rect);
        if (Widgets.ButtonInvisible(rect))
        {
            if (!allOn)
            {
                on();
            }
            else
            {
                off();
            }
        }
    }

    public static void DrawToggle(Rect rect, string label, TipSignal tooltip, bool checkOn, Action toggle,
                                   bool expensive = false,
                                   float size = SmallIconSize, float margin = Margin)
    {
        DrawToggle(rect, label, tooltip, checkOn, toggle, toggle, expensive, size);
    }

    public static IntVec3 GetBaseCenter(this Map map)
    {
        if (map == null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        // we need to define a 'base' position to calculate distances.
        // Try to find a managerstation (in all non-debug cases this method will only fire if there is such a station).
        var position = IntVec3.Zero;
        Building managerStation = map.listerBuildings.AllBuildingsColonistOfClass<Building_ManagerStation>()
                                     .FirstOrDefault();
        if (managerStation != null)
        {
            return managerStation.InteractionCell;
        }

        // otherwise, use the average of the home area. Not ideal, but it'll do.
        var homeCells =
            map.areaManager.Get<Area_Home>().ActiveCells.ToList();
        if (homeCells.Count > 0)
        {
            for (var i = 0; i < homeCells.Count; i++)
            {
                position += homeCells[i];
            }

            position.x /= homeCells.Count;
            position.y /= homeCells.Count;
            position.z /= homeCells.Count;
            var standableCell = position;

            // find the closest traversable cell to the center
            for (var i = 0; !standableCell.Walkable(map); i++)
            {
                standableCell = position + GenRadial.RadialPattern[i];
            }

            return standableCell;
        }
        else
        {
            // Just return the position of a pawn (or, if nobody is alive, the map center)
            return map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer)?.RandomElement().Position ?? map.Center;
        }
    }

    public static bool HasCompOrChildCompOf(this ThingDef def, Type compType)
    {
        if (def == null)
        {
            throw new ArgumentNullException(nameof(def));
        }
        if (compType == null)
        {
            throw new ArgumentNullException(nameof(compType));
        }

        for (var index = 0; index < def.comps.Count; ++index)
        {
            if (compType.IsAssignableFrom(def.comps[index].compClass))
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsInt(this string text)
    {
        return int.TryParse(text, out var _);
    }

    public static void LabelOutline(Rect icon, string label, string? tooltip, TextAnchor anchor, float margin,
                                     GameFont font, Color textColour, Color outlineColour)
    {
        // horribly inefficient way of getting an outline to show - draw 4 background coloured labels with a 1px offset, then draw the foreground on top.
        int[] offsets = [-1, 0, 1];

        foreach (var xOffset in offsets)
        {
            foreach (var yOffset in offsets)
            {
                var offsetIcon = icon;
                offsetIcon.x += xOffset;
                offsetIcon.y += yOffset;
                Label(offsetIcon, label, anchor, font, outlineColour, margin);
            }
        }

        Label(icon, label, tooltip, anchor, font, textColour, margin);
    }

    public static int SafeAbs(int value)
    {
        if (value >= 0)
        {
            return value;
        }

        if (value == int.MinValue)
        {
            return int.MaxValue;
        }

        return -value;
    }

    internal static void Scribe_IntTupleArray(ref CircularBuffer<(int, int)> values, string label)
    {
        int capacity = 0;
        string? text = null;
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            capacity = values.Capacity;
            text = values.Join(i => $"{i.Item1},{i.Item2}", ":");
        }

        Scribe_Values.Look(ref capacity, $"{label}Capacity", History.EntriesPerInterval);
        Scribe_Values.Look(ref text, label);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            values = new CircularBuffer<(int, int)>(
                capacity,
                text?.Split(':')
                    .Select(v =>
                    {
                        var values = v.Split(',');
                        var count = int.Parse(values[0]);
                        var target = int.Parse(values[1]);
                        return (count, target);
                    })
                    .ToArray() ?? []);
        }
    }

    public static void Scribe_Designations(ref List<Designation> designations, Map map)
    {
        if (designations == null)
        {
            throw new ArgumentNullException(nameof(designations));
        }
        if (map == null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        Scribe_Collections.Look(ref designations, "designations", LookMode.Deep);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            DesignationManager designationManager = map.designationManager;
            for (int i = 0; i < designations.Count; i++)
            {
                var thing = designations[i].target.Thing;
                if (thing == null)
                {
                    continue;
                }
                designations[i] = designationManager.DesignationOn(thing) ?? designations[i];
            }
        }
    }

    public static string TimeString(this int ticks)
    {
        int days = ticks / GenDate.TicksPerDay,
            hours = ticks % GenDate.TicksPerDay / GenDate.TicksPerHour;

        var s = string.Empty;

        if (days > 0)
        {
            s += days + "LetterDay".Translate() + " ";
        }

        s += hours + "LetterHour".Translate();

        return s;
    }

    // PawnColumnWorker_WorkPriority.IsIncapableOfWholeWorkType, but static
    public static bool IsIncapableOfWholeWorkType(Pawn pawn, WorkTypeDef work)
    {
        if (pawn == null)
        {
            throw new ArgumentNullException(nameof(pawn));
        }
        if (work == null)
        {
            throw new ArgumentNullException(nameof(work));
        }

        for (int i = 0; i < work.workGiversByPriority.Count; i++)
        {
            bool flag = true;
            for (int j = 0; j < work.workGiversByPriority[i].requiredCapacities.Count; j++)
            {
                PawnCapacityDef capacity = work.workGiversByPriority[i].requiredCapacities[j];
                if (!pawn.health.capacities.CapableOf(capacity))
                {
                    flag = false;
                    break;
                }
            }
            if (flag)
            {
                return false;
            }
        }
        return true;
    }

    private static bool TryGetCached(Map map, MapStockpileFilter mapStockpileFilter, out int count)
    {
        var countCache = Manager.For(map).CountCache;
        if (countCache.TryGetValue(mapStockpileFilter, out FilterCountCache filterCountCache))
        {
            if (Find.TickManager.TicksGame - filterCountCache.TimeSet < 250)
            {
                count = filterCountCache.Cache;
                return true;
            }

        }
#if DEBUG_COUNTS
        Log.Message("not cached");
#endif
        count = 0;
        return false;
    }

    internal sealed record MapStockpileFilter(
        ThingFilter Filter,
        Zone_Stockpile? Stockpile,
        bool CountAllOnMap = false);
}
