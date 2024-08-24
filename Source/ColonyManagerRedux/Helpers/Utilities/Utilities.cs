// Utilities.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.Collections;
using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;

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
                    new UpdateInterval(GenDate.TicksPerHour, "ColonyManagerRedux.UpdateInterval.Hourly".Translate()),
                    new UpdateInterval(GenDate.TicksPerHour * 2, "ColonyManagerRedux.UpdateInterval.MultipleHourly".Translate(2)),
                    new UpdateInterval(GenDate.TicksPerHour * 4, "ColonyManagerRedux.UpdateInterval.MultipleHourly".Translate(4)),
                    new UpdateInterval(GenDate.TicksPerHour * 8, "ColonyManagerRedux.UpdateInterval.MultipleHourly".Translate(8)),
                    UpdateInterval.Daily,
                    new UpdateInterval(GenDate.TicksPerTwelfth, "ColonyManagerRedux.UpdateInterval.Monthly".Translate()),
                    new UpdateInterval(GenDate.TicksPerYear, "ColonyManagerRedux.UpdateInterval.Yearly".Translate()),
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
        Boxed<int> count = new();
        CountProductsCoroutine(map, filter, count, stockpile, countAllOnMap)
            .RunImmediatelyToCompletion();
        return count.Value;
    }

    public static Coroutine CountProductsCoroutine(
        this Map map,
        ThingFilter filter,
        Boxed<int> count,
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

        if (count == null)
        {
            throw new ArgumentNullException(nameof(count));
        }

        foreach (var (thingDef, i) in filter.AllowedThingDefs.Select((t, i) => (t, i)))
        {
            // if it counts as a resource and we're not limited to a single stockpile, use the ingame counter (e.g. only steel in stockpiles.)
            if (!countAllOnMap &&
                 thingDef.CountAsResource &&
                 stockpile == null)
            {
                // we don't need to bother with quality / hitpoints as these are non-existant/irrelevant for resources.
                count.Value += map.resourceCounter.GetCount(thingDef);
            }
            else
            {
                // otherwise, go look for stuff that matches our filters.
                var thingList = map.listerThings.ThingsOfDef(thingDef);

                // if filtered by stockpile, filter the thinglist accordingly.
                if (stockpile != null)
                {
                    var areaSlotGroup = stockpile.slotGroup;
                    thingList.RemoveWhere(t => t.Position.GetSlotGroup(map) != areaSlotGroup);
                }

                foreach (var (t, j) in thingList.Select((t, j) => (t, j)))
                {
                    if (j > 0 && j % CoroutineBreakAfter == 0)
                    {
                        yield return ResumeImmediately.Singleton;
                    }

                    if (t.IsForbidden(Faction.OfPlayer) ||
                         t.Position.Fogged(map))
                    {
                        continue;
                    }

                    if (!countAllOnMap && !t.IsInAnyStorage())
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

                    count.Value += t.stackCount;
                }
            }

            if (i > 0 && i % CoroutineBreakAfter == 0)
            {
                yield return ResumeImmediately.Singleton;
            }
        }
    }

    public static void DrawReachabilityToggle(ref Vector2 pos, float width, ref bool reachability)
    {
        DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.Threshold.CheckReachability".Translate(),
            "ColonyManagerRedux.Threshold.CheckReachability.Tip".Translate(),
            ref reachability,
            expensive: true);
    }

    public static bool DrawStampButton(Rect stampRect, ManagerJob job)
    {
        if (job == null)
        {
            throw new ArgumentNullException(nameof(job));
        }

        return Widgets.ButtonImage(
            stampRect,
            job.CausedException != null
                ? Resources.StampException
                : job.IsSuspended
                    ? Resources.StampStart
                    : job.IsCompleted
                        ? Resources.StampCompleted
                        : Resources.StampSuspended);
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
                                   GameFont font = GameFont.Small, bool wrap = true, bool leaveRoomForAdditionalIcon = false)
    {
        // set up rects
        var labelRect = rect;
        labelRect.xMax -= size + margin * 2 + ((expensive || leaveRoomForAdditionalIcon) ? size + margin : 0f);
        var iconRect = new Rect(rect.xMax - size - margin, 0f, size, size).CenteredOnYIn(labelRect);

        // draw label
        IlyvionWidgets.Label(
            labelRect,
            label,
            TextAnchor.MiddleLeft,
            font,
            leftMargin: margin,
            wordWrap: wrap);

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
            TooltipHandler.TipRegion(iconRect, "ColonyManagerRedux.Common.Expensive.Tip".Translate());
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
                                   //GameFont font = GameFont.Small,
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
                                   //GameFont font = GameFont.Small,
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

        IlyvionDebugViewSettings.DrawIfUIHelpers(() =>
        {
            Widgets.DrawRectFast(labelRect, ColorLibrary.NeonGreen.ToTransparent(.5f));
            Widgets.DrawRectFast(iconRect, ColorLibrary.PaleBlue.ToTransparent(.5f));
        });

        // draw label
        IlyvionWidgets.Label(
            rect,
            label,
            TextAnchor.MiddleLeft,
            GameFont.Small,
            leftMargin: margin,
            wordWrap: wrap);

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
            TooltipHandler.TipRegion(iconRect, "ColonyManagerRedux.Common.Expensive.Tip".Translate());
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

    private static readonly List<IntVec3> _tmpHomeCells = [];
    public static IntVec3 GetBaseCenter(this Map map)
    {
        if (map == null)
        {
            throw new ArgumentNullException(nameof(map));
        }

        // we need to define a 'base' position to calculate distances.
        // Try to find a managerstation (in all non-debug cases this method will only fire if there
        // is such a station).
        var position = IntVec3.Zero;
        Building managerStation = map.listerBuildings
            .AllBuildingsColonistOfClass<Building_ManagerStation>()
            .FirstOrDefault();
        if (managerStation != null)
        {
            return managerStation.InteractionCell;
        }

        // otherwise, use the average of the home area. Not ideal, but it'll do.
        _tmpHomeCells.AddRange(map.areaManager.Get<Area_Home>().ActiveCells);
        using var _ = new DoOnDispose(_tmpHomeCells.Clear);
        if (_tmpHomeCells.Count > 0)
        {
            for (var i = 0; i < _tmpHomeCells.Count; i++)
            {
                position += _tmpHomeCells[i];
            }

            position.x /= _tmpHomeCells.Count;
            position.y /= _tmpHomeCells.Count;
            position.z /= _tmpHomeCells.Count;
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
            return map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer)?.RandomElement()
                .Position ?? map.Center;
        }
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
                IlyvionWidgets.Label(offsetIcon, label, anchor, font, outlineColour, margin);
            }
        }

        IlyvionWidgets.Label(icon, label, tooltip, anchor, font, textColour, margin);
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

    internal static void Scribe_IntArray(ref CircularBuffer<int> values, string label)
    {
        int capacity = 0;
        string? text = null;
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            capacity = values.Capacity;
            text = values.Join(i => i.ToString(), ":");
        }

        Scribe_Values.Look(ref capacity, $"{label}Capacity", History.EntriesPerInterval);
        Scribe_Values.Look(ref text, label);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            values = new CircularBuffer<int>(
                capacity,
                text?.Split(':')
                    .Select(int.Parse)
                    .ToArray() ?? []);
        }
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

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            for (int i = designations.Count - 1; i >= 0; i--)
            {
                Designation item = designations[i];
                if (!map.designationManager.AllDesignations.Contains(item))
                {
                    designations.RemoveAt(i);
                }
            }
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
}
