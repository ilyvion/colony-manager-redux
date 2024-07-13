﻿// Utilities.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;
using static FluffyManager.Constants;
using static FluffyManager.Widgets_Labels;

namespace FluffyManager
{
    public static class Utilities
    {
        public enum SyncDirection
        {
            FilterToAllowed,
            AllowedToFilter
        }

        public static Dictionary<MapStockpileFilter, FilterCountCache> CountCache =
            new Dictionary<MapStockpileFilter, FilterCountCache>();

        public static WorkTypeDef WorkTypeDefOf_Managing = DefDatabase<WorkTypeDef>.GetNamed("Managing");

        private static List<UpdateInterval> _updateIntervalOptions;

        static Utilities()
        {
        }

        public static List<UpdateInterval> UpdateIntervalOptions
        {
            get
            {
                if (_updateIntervalOptions.NullOrEmpty())
                {
                    _updateIntervalOptions = new List<UpdateInterval>();
                    _updateIntervalOptions.Add(new UpdateInterval(GenDate.TicksPerHour, "FM.Hourly".Translate()));
                    _updateIntervalOptions.Add(
                        new UpdateInterval(GenDate.TicksPerHour * 2, "FM.MultiHourly".Translate(2)));
                    _updateIntervalOptions.Add(
                        new UpdateInterval(GenDate.TicksPerHour * 4, "FM.MultiHourly".Translate(4)));
                    _updateIntervalOptions.Add(
                        new UpdateInterval(GenDate.TicksPerHour * 8, "FM.MultiHourly".Translate(8)));
                    _updateIntervalOptions.Add(UpdateInterval.Daily);
                    _updateIntervalOptions.Add(
                        new UpdateInterval(GenDate.TicksPerTwelfth, "FM.Monthly".Translate()));
                    _updateIntervalOptions.Add(new UpdateInterval(GenDate.TicksPerYear, "FM.Yearly".Translate()));
                }

                return _updateIntervalOptions;
            }
        }

        public static int CountProducts([NotNull] this Map map, [NotNull] ThingFilter filter, [CanBeNull] Zone_Stockpile stockpile = null,
                                         bool countAllOnMap = false)
        {
            if (map == null)
                throw new NullReferenceException(nameof(map));

            if (filter == null)
                throw new NullReferenceException(nameof(filter));

            var key = new MapStockpileFilter(map, filter, stockpile, countAllOnMap);
            if (TryGetCached(key, out var count)) return count;

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
                            continue;

                        QualityCategory quality;
                        if (t.TryGetQuality(out quality))
                            if (!filter.AllowedQualityLevels.Includes(quality))
                                continue;

                        if (filter.AllowedHitPointsPercents.IncludesEpsilon(t.HitPoints)) continue;


#if DEBUG_COUNTS
                            Log.Message(t.LabelCap + ": " + CountProducts(t));
#endif

                        count += t.stackCount;
                    }
                }

                // update cache if exists.
                if (CountCache.ContainsKey(key))
                {
                    CountCache[key].Cache = count;
                    CountCache[key].TimeSet = Find.TickManager.TicksGame;
                }
                else
                {
                    CountCache.Add(key, new FilterCountCache(count));
                }
            }

            return count;
        }

        public static void DrawReachabilityToggle(ref Vector2 pos, float width, ref bool reachability)
        {
            DrawToggle(ref pos, width, "FM.CheckReachability".Translate(), "FM.CheckReachability.Tip".Translate(),
                        ref reachability, true);
        }

        public static void DrawStatusForListEntry<T>(this T job, Rect rect, Trigger trigger) where T : ManagerJob
        {
            // set up rects
            var stampRect = new Rect(
                rect.xMax - ManagerJob.SuspendStampWidth - Margin,
                rect.yMin,
                ManagerJob.SuspendStampWidth,
                ManagerJob.SuspendStampWidth).CenteredOnYIn(rect);
            var lastUpdateRect = new Rect(
                stampRect.xMin - Margin - ManagerJob.LastUpdateRectWidth,
                rect.yMin,
                ManagerJob.LastUpdateRectWidth,
                rect.height);
            var progressRect = new Rect(
                lastUpdateRect.xMin - Margin - ManagerJob.ProgressRectWidth,
                rect.yMin,
                ManagerJob.ProgressRectWidth,
                rect.height);

            // draw stamp
            if (Widgets.ButtonImage(
                stampRect,
                job.Completed ? Resources.StampCompleted :
                job.Suspended ? Resources.StampStart : Resources.StampSuspended)) job.Suspended = !job.Suspended;
            if (job.Suspended)
            {
                TooltipHandler.TipRegion(stampRect, "FM.UnsuspendJobTooltip".Translate());
                return;
            }

            if (job.Completed)
            {
                TooltipHandler.TipRegion(stampRect, "FM.JobCompletedTooltip".Translate());
                return;
            }

            TooltipHandler.TipRegion(stampRect, "FM.SuspendJobTooltip".Translate());

            // should never happen?
            if (trigger == null)
                return;

            // draw progress bar
            trigger.DrawProgressBar(progressRect, true);
            TooltipHandler.TipRegion(progressRect, () => trigger.StatusTooltip, trigger.GetHashCode());

            // draw update interval
            var timeSinceLastUpdate = Find.TickManager.TicksGame - job.lastAction;
            job.UpdateInterval?.Draw(lastUpdateRect, job);
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
                GUI.DrawTexture(iconRect, Widgets.CheckboxOnTex);
            else
                GUI.DrawTexture(iconRect, Widgets.CheckboxOffTex);

            // draw expensive icon
            if (expensive)
            {
                iconRect.x -= size + margin;
                TooltipHandler.TipRegion(iconRect, "FM.Expensive.Tip".Translate());
                GUI.color = checkOn ? Resources.Orange : Color.grey;
                GUI.DrawTexture(iconRect, Resources.Stopwatch);
                GUI.color = Color.white;
            }

            // interactivity
            Widgets.DrawHighlightIfMouseover(rect);
            if (Widgets.ButtonInvisible(rect)) checkOn = !checkOn;
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


        public static void DrawToggle(Rect rect, string label, TipSignal? tooltip, bool checkOn, bool checkOff, Action on,
                                       Action off, bool expensive = false, float size = SmallIconSize,
                                       float margin = Margin, bool wrap = true)
        {
            // set up rects
            var labelRect = rect;
            var iconRect = new Rect(rect.xMax - size - margin, 0f, size, size);
            labelRect.xMax = iconRect.xMin - Margin / 2f;

            // finetune rects
            iconRect = iconRect.CenteredOnYIn(labelRect);

            // draw label
            Label(rect, label, TextAnchor.MiddleLeft, GameFont.Small, margin: margin, wrap: wrap);

            // tooltip
            if (tooltip.HasValue) TooltipHandler.TipRegion(rect, tooltip.Value);

            // draw check
            if (checkOn)
                GUI.DrawTexture(iconRect, Widgets.CheckboxOnTex);
            else if (checkOff)
                GUI.DrawTexture(iconRect, Widgets.CheckboxOffTex);
            else
                GUI.DrawTexture(iconRect, Widgets.CheckboxPartialTex);

            // draw expensive icon
            if (expensive)
            {
                iconRect.x -= size + margin;
                TooltipHandler.TipRegion(iconRect, "FM.Expensive.Tip".Translate());
                GUI.color = checkOn ? Resources.Orange : Color.grey;
                GUI.DrawTexture(iconRect, Resources.Stopwatch);
                GUI.color = Color.white;
            }

            // interactivity
            Widgets.DrawHighlightIfMouseover(rect);
            if (Widgets.ButtonInvisible(rect))
            {
                if (!checkOn)
                    on();
                else
                    off();
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
            // we need to define a 'base' position to calculate distances.
            // Try to find a managerstation (in all non-debug cases this method will only fire if there is such a station).
            var position = IntVec3.Zero;
            Building managerStation = map.listerBuildings.AllBuildingsColonistOfClass<Building_ManagerStation>()
                                         .FirstOrDefault();
            if (managerStation != null) return managerStation.InteractionCell;

            // otherwise, use the average of the home area. Not ideal, but it'll do.
            var homeCells =
                map.areaManager.Get<Area_Home>().ActiveCells.ToList();
            for (var i = 0; i < homeCells.Count; i++) position += homeCells[i];

            position.x /= homeCells.Count;
            position.y /= homeCells.Count;
            position.z /= homeCells.Count;
            var standableCell = position;

            // find the closest traversable cell to the center
            for (var i = 0; !standableCell.Walkable(map); i++)
                standableCell = position + GenRadial.RadialPattern[i];
            return standableCell;
        }

        public static object GetPrivatePropertyValue(this object src, string propName,
                                                      BindingFlags flags =
                                                          BindingFlags.Instance | BindingFlags.NonPublic)
        {
            return src.GetType().GetProperty(propName, flags).GetValue(src, null);
        }

        public static bool HasCompOrChildCompOf(this ThingDef def, Type compType)
        {
            for (var index = 0; index < def.comps.Count; ++index)
                if (compType.IsAssignableFrom(def.comps[index].compClass))
                    return true;

            return false;
        }

        public static bool IsInt(this string text)
        {
            return int.TryParse(text, out var num);
        }

        public static void LabelOutline(Rect icon, string label, string tooltip, TextAnchor anchor, float margin,
                                         GameFont font, Color textColour, Color outlineColour)
        {
            // horribly inefficient way of getting an outline to show - draw 4 background coloured labels with a 1px offset, then draw the foreground on top.
            int[] offsets = { -1, 0, 1 };

            foreach (var xOffset in offsets)
                foreach (var yOffset in offsets)
                {
                    var offsetIcon = icon;
                    offsetIcon.x += xOffset;
                    offsetIcon.y += yOffset;
                    Label(offsetIcon, label, anchor, font, outlineColour, margin);
                }

            Label(icon, label, tooltip, anchor, font, textColour, margin);
        }

        public static int SafeAbs(int value)
        {
            if (value >= 0) return value;
            if (value == int.MinValue) return int.MaxValue;
            return -value;
        }

        public static void Scribe_IntArray(ref List<int> values, string label)
        {
            string text = null;
            if (Scribe.mode == LoadSaveMode.Saving)
                text = string.Join(":", values.ConvertAll(i => i.ToString()).ToArray());
            Scribe_Values.Look(ref text, label);
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                values = text.Split(":".ToCharArray()).ToList().ConvertAll(int.Parse);
        }

        public static string TimeString(this int ticks)
        {
            int days = ticks / GenDate.TicksPerDay,
                hours = ticks % GenDate.TicksPerDay / GenDate.TicksPerHour;

            var s = string.Empty;

            if (days > 0) s += days + "LetterDay".Translate() + " ";
            s += hours + "LetterHour".Translate();

            return s;
        }

        public static bool TryGetPrivateField(Type type, object instance, string fieldName, out object value,
                                               BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance)
        {
            var field = type.GetField(fieldName, flags);
            value = field?.GetValue(instance);
            return value != null;
        }

        public static bool TrySetPrivateField(Type type, object instance, string fieldName, object value,
                                               BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance)
        {
            // get field info
            var field = type.GetField(fieldName, flags);

            // failed?
            if (field == null) return false;

            // try setting it.
            field.SetValue(instance, value);

            // test by fetching the field again. (this is highly, stupidly inefficient, but ok).
            object test;
            if (!TryGetPrivateField(type, instance, fieldName, out test, flags)) return false;

            return test == value;
        }

        private static bool TryGetCached(MapStockpileFilter mapStockpileFilter, out int count)
        {
            if (CountCache.ContainsKey(mapStockpileFilter))
            {
                var filterCountCache = CountCache[mapStockpileFilter];
                if (Find.TickManager.TicksGame - filterCountCache.TimeSet < 250 && // less than 250 ticks ago
                     Find.TickManager.TicksGame > filterCountCache.TimeSet)
                // cache is not from future (switching games without restarting could cause this).
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

        public struct MapStockpileFilter
        {
            private ThingFilter filter;
            private Zone_Stockpile stockpile;
            private Map map;
            private bool countAllOnMap;

            public MapStockpileFilter(Map map, ThingFilter filter, Zone_Stockpile stockpile,
                                       bool countAllOnMap = false)
            {
                this.map = map;
                this.filter = filter;
                this.stockpile = stockpile;
                this.countAllOnMap = countAllOnMap;
            }
        }

        public class CachedValues<T, V>
        {
            private readonly Dictionary<T, CachedValue<V>> _cache;
            private readonly int updateInterval;

            public CachedValues(int updateInterval = 250)
            {
                _cache = new Dictionary<T, CachedValue<V>>();
            }

            public V this[T index]
            {
                get
                {
                    TryGetValue(index, out var value);
                    return value;
                }
                set => Update(index, value);
            }

            public void Add(T key, Func<V> updater)
            {
                // Log.Message( $"Adding cached value for: {key}", true );

                var value = updater();
                var cached = new CachedValue<V>(value, updateInterval, updater);
                _cache.Add(key, cached);
            }

            public bool TryGetValue(T key, out V value)
            {
                if (_cache.TryGetValue(key, out var cachedValue)) return cachedValue.TryGetValue(out value);

                value = default;
                return false;
            }

            public void Update(T key, V value)
            {
                if (_cache.TryGetValue(key, out var cachedValue))
                    cachedValue.Update(value);
                else
                    _cache.Add(key, new CachedValue<V>(value, updateInterval));
            }
        }

        public class CachedValue<T>
        {
            private readonly T _default;
            private readonly int _updateInterval;
            private readonly Func<T> _updater;
            private T _cached;
            private int _timeSet;

            public CachedValue(T @default = default, int updateInterval = 250, Func<T> updater = null)
            {
                _updateInterval = updateInterval;
                _cached = _default = @default;
                _updater = updater;
                _timeSet = Find.TickManager.TicksGame;
            }

            public T Value
            {
                get
                {
                    if (TryGetValue(out var value))
                        return value;
                    throw new InvalidOperationException(
                        "get_Value() on a CachedValue that is out of date, and has no updater.");
                }
            }

            public bool TryGetValue(out T value)
            {
                if (Find.TickManager.TicksGame - _timeSet <= _updateInterval)
                {
                    value = _cached;
                    return true;
                }

                if (_updater != null)
                {
                    Update();
                    value = _cached;
                    return true;
                }

                value = _default;
                return false;
            }

            public void Update(T value)
            {
                _cached = value;
                _timeSet = Find.TickManager.TicksGame;
            }

            public void Update()
            {
                // Log.Message( $"Running Update()", true );

                if (_updater == null)
                    Log.Error("Calling Update() without updater");
                else
                    Update(_updater());
            }
        }

        // count cache for multiple products
        public class FilterCountCache
        {
            public int Cache;
            public int TimeSet;

            public FilterCountCache(int count)
            {
                Cache = count;
                TimeSet = Find.TickManager.TicksGame;
            }
        }
    }
}