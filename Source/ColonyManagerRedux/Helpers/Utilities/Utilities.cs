// Utilities.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.Collections;
using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

/// <summary>
/// Provides utility methods for the Colony Manager Redux mod.
/// </summary>
[HotSwappable]
[CoroutineSettingsType]
public static class Utilities
{
    /// <summary>
    /// Specifies the direction for synchronizing filters and allowed lists.
    /// </summary>
    public enum SyncDirection
    {
        /// <summary>
        /// Synchronize from the filter to the allowed list.
        /// </summary>
        FilterToAllowed,

        /// <summary>
        /// Synchronize from the allowed list to the filter.
        /// </summary>
        AllowedToFilter,
    }

    private static readonly List<UpdateInterval> _defaultUpdateIntervalOptions =
    [
        new UpdateInterval(
            GenDate.TicksPerHour,
            "ColonyManagerRedux.UpdateInterval.Hourly".Translate()
        ),
        new UpdateInterval(
            GenDate.TicksPerHour * 2,
            "ColonyManagerRedux.UpdateInterval.MultipleHourly".Translate(2)
        ),
        new UpdateInterval(
            GenDate.TicksPerHour * 4,
            "ColonyManagerRedux.UpdateInterval.MultipleHourly".Translate(4)
        ),
        new UpdateInterval(
            GenDate.TicksPerHour * 8,
            "ColonyManagerRedux.UpdateInterval.MultipleHourly".Translate(8)
        ),
        UpdateInterval.Daily,
        new UpdateInterval(
            GenDate.TicksPerTwelfth,
            "ColonyManagerRedux.UpdateInterval.Monthly".Translate()
        ),
        new UpdateInterval(
            GenDate.TicksPerYear,
            "ColonyManagerRedux.UpdateInterval.Yearly".Translate()
        ),
    ];

    /// <summary>
    /// Gets a list of available update interval options, including custom intervals if defined in the settings.
    /// </summary>
    public static IEnumerable<UpdateInterval> UpdateIntervalOptions
    {
        get
        {
            if (ColonyManagerReduxMod.Settings.CustomUpdateIntervalTickList.Empty())
            {
                // if there are no custom update intervals, return the default ones.
                return _defaultUpdateIntervalOptions.AsReadOnly();
            }
            else
            {
                // if there are custom update intervals, add them to the list.
                var value = _defaultUpdateIntervalOptions.ToList();
                value.AddRange(
                    ColonyManagerReduxMod.Settings.CustomUpdateIntervalTickList.Select(
                        ticks => new UpdateInterval(
                            ticks,
                            "ColonyManagerRedux.UpdateInterval.Custom".Translate(
                                ticks.ToStringTicksToPeriodVerbose()
                            )
                        )
                    )
                );
                value.SortBy(i => i.Ticks);
                return value;
            }
        }
    }

    /// <summary>
    /// Counts the number of products on the map that match the specified filter, optionally within a specific stockpile or across the entire map.
    /// </summary>
    /// <param name="map">The map to search for products.</param>
    /// <param name="filter">The filter to apply to products.</param>
    /// <param name="stockpile">Optional stockpile to restrict the search to.</param>
    /// <param name="countAllOnMap">If true, counts all matching products on the map; otherwise, only those in storage.</param>
    /// <returns>The total count of products matching the filter.</returns>
    public static int CountProducts(
        this Map map,
        ThingFilter filter,
        Zone_Stockpile? stockpile = null,
        bool countAllOnMap = false
    )
    {
        Boxed<int> count = new();
        CountProductsCoroutine(map, filter, count, stockpile, countAllOnMap)
            .RunImmediatelyToCompletion();
        return count.Value;
    }

    /// <summary>
    /// Coroutine that counts the number of products on the map that match the specified filter, optionally within a specific stockpile or across the entire map.
    /// </summary>
    /// <param name="map">The map to search for products.</param>
    /// <param name="filter">The filter to apply to products.</param>
    /// <param name="count">A boxed integer to store the total count of products matching the filter.</param>
    /// <param name="stockpile">Optional stockpile to restrict the search to.</param>
    /// <param name="countAllOnMap">If true, counts all matching products on the map; otherwise, only those in storage.</param>
    /// <returns>A coroutine that performs the counting operation.</returns>
    [CoroutineSettingsMethod]
    public static Coroutine CountProductsCoroutine(
        this Map map,
        ThingFilter filter,
        Boxed<int> count,
        Zone_Stockpile? stockpile = null,
        bool countAllOnMap = false
    )
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

        var operationsPerTick = ColonyManagerReduxMod.Settings.GetOperationsPerTickForCoroutine(
            CountProductsCoroutine
        );
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                CountProductsCoroutine
            );

        var loopingIndex = 0;
        foreach (var thingDef in filter.AllowedThingDefs)
        {
            // Shouldn't happen, but got a report where it did.
            // So if it does, just skip it.
            if (thingDef == null)
            {
                continue;
            }

            // if it counts as a resource and we're not limited to a single stockpile,
            // use the ingame counter (e.g. only steel in stockpiles.)
            if (!countAllOnMap && thingDef.CountAsResource && stockpile == null)
            {
                // we don't need to bother with quality / hitpoints as these are
                // non-existant/irrelevant for resources.
                count.Value += map.resourceCounter.GetCount(thingDef);
            }
            else
            {
                // otherwise, go look for stuff that matches our filters.
                List<Thing> thingList = [.. map.listerThings.ThingsOfDef(thingDef)];

                // if filtered by stockpile, filter the thinglist accordingly.
                if (stockpile != null)
                {
                    var areaSlotGroup = stockpile.slotGroup;
                    thingList.RemoveWhere(t => t.Position.GetSlotGroup(map) != areaSlotGroup);
                }

                foreach (var t in thingList)
                {
                    if (++loopingIndex > 0 && loopingIndex % operationsPerTick == 0)
                    {
                        yield return new ResumeAfterTicks(ticksBetweenOperations);
                    }

                    if (t.IsForbidden(Faction.OfPlayer) || t.Position.Fogged(map))
                    {
                        continue;
                    }

                    if (!countAllOnMap && !t.IsInAnyStorage())
                    {
                        continue;
                    }

                    if (t.TryGetQuality(out var quality))
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

            if (++loopingIndex > 0 && loopingIndex % operationsPerTick == 0)
            {
                yield return new ResumeAfterTicks(ticksBetweenOperations);
            }
        }
    }

    /// <summary>
    /// Draws a toggle UI element for reachability, allowing the user to enable or disable reachability checks.
    /// </summary>
    /// <param name="pos">The position vector for the toggle UI element (will be updated).</param>
    /// <param name="width">The width of the toggle UI element.</param>
    /// <param name="reachability">A reference to the boolean value indicating whether reachability is enabled.</param>
    public static void DrawReachabilityToggle(
        ref Vector2 pos,
        float width,
        ref bool reachability
    ) =>
        DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.Threshold.CheckReachability".Translate(),
            "ColonyManagerRedux.Threshold.CheckReachability.Tip".Translate(),
            ref reachability,
            expensive: true
        );

    /// <summary>
    /// Draws a stamp button for the specified ManagerJob, displaying an appropriate icon based on the job's state.
    /// </summary>
    /// <param name="stampRect">The rectangle in which to draw the button.</param>
    /// <param name="job">The ManagerJob for which to draw the stamp button.</param>
    /// <returns>True if the button was clicked; otherwise, false.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="job"/> is null.</exception>
    public static bool DrawStampButton(Rect stampRect, ManagerJob job) =>
        job == null
            ? throw new ArgumentNullException(nameof(job))
            : Widgets.ButtonImage(
                stampRect,
                job.CausedException != null ? Resources.StampException
                    : job.IsSuspended ? Resources.StampStart
                    : job.IsCompleted ? Resources.StampCompleted
                    : Resources.StampSuspended
            );

    /// <summary>
    /// Draws a toggle UI element at the specified position, allowing the user to enable or disable a boolean value.
    /// </summary>
    /// <param name="pos">The position vector for the toggle UI element (will be updated).</param>
    /// <param name="width">The width of the toggle UI element.</param>
    /// <param name="label">The label to display next to the toggle.</param>
    /// <param name="tooltip">The tooltip to display when hovering over the toggle.</param>
    /// <param name="checkOn">A reference to the boolean value indicating whether the toggle is enabled.</param>
    /// <param name="expensive">Whether to display an icon indicating the toggle is expensive.</param>
    /// <param name="size">The size of the toggle icon.</param>
    /// <param name="margin">The margin around the toggle.</param>
    /// <param name="font">The font to use for the label.</param>
    /// <param name="wrap">Whether the label should wrap to multiple lines.</param>
    public static void DrawToggle(
        ref Vector2 pos,
        float width,
        string label,
        TipSignal tooltip,
        ref bool checkOn,
        bool expensive = false,
        float size = SmallIconSize,
        float margin = Margin,
        GameFont font = GameFont.Small,
        bool wrap = true
    )
    {
        var labelHeight = Text.CalcHeight(label, width - SmallIconSize - (4 * Margin));
        var height = Mathf.Max(labelHeight, ListEntryHeight);
        var toggleRect = new Rect(pos.x, pos.y, width, height);
        pos.y += height;
        DrawToggle(toggleRect, label, tooltip, ref checkOn, expensive, size, margin, font, wrap);
    }

    /// <summary>
    /// Draws a toggle UI element at the specified rectangle, allowing the user to enable or disable a boolean value.
    /// </summary>
    /// <param name="rect">The rectangle in which to draw the toggle UI element.</param>
    /// <param name="label">The label to display next to the toggle.</param>
    /// <param name="tooltip">The tooltip to display when hovering over the toggle.</param>
    /// <param name="checkOn">A reference to the boolean value indicating whether the toggle is enabled.</param>
    /// <param name="expensive">Whether to display an icon indicating the toggle is expensive.</param>
    /// <param name="size">The size of the toggle icon.</param>
    /// <param name="margin">The margin around the toggle.</param>
    /// <param name="font">The font to use for the label.</param>
    /// <param name="wrap">Whether the label should wrap to multiple lines.</param>
    /// <param name="leaveRoomForAdditionalIcon">Whether to leave room for an additional icon.</param>
    public static void DrawToggle(
        Rect rect,
        string label,
        TipSignal tooltip,
        ref bool checkOn,
        bool expensive = false,
        float size = SmallIconSize,
        float margin = Margin,
        GameFont font = GameFont.Small,
        bool wrap = true,
        bool leaveRoomForAdditionalIcon = false
    )
    {
        // set up rects
        var labelRect = rect;
        labelRect.xMax -=
            size + (margin * 2) + ((expensive || leaveRoomForAdditionalIcon) ? size + margin : 0f);
        var iconRect = new Rect(rect.xMax - size - margin, 0f, size, size).CenteredOnYIn(labelRect);

        // draw label
        IlyvionWidgets.Label(
            labelRect,
            label,
            TextAnchor.MiddleLeft,
            font,
            leftMargin: margin,
            wordWrap: wrap
        );

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
            TooltipHandler.TipRegion(
                iconRect,
                "ColonyManagerRedux.Common.Expensive.Tip".Translate()
            );
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

    /// <summary>
    /// Draws a toggle UI element at the specified position, allowing the user to enable or disable a boolean value,
    /// and invokes the specified actions when toggled on or off.
    /// </summary>
    /// <param name="pos">The position vector for the toggle UI element (will be updated).</param>
    /// <param name="width">The width of the toggle UI element.</param>
    /// <param name="label">The label to display next to the toggle.</param>
    /// <param name="tooltip">The tooltip to display when hovering over the toggle.</param>
    /// <param name="checkOn">Indicates whether the toggle is enabled.</param>
    /// <param name="on">Action to invoke when toggled on.</param>
    /// <param name="off">Action to invoke when toggled off.</param>
    /// <param name="expensive">Whether to display an icon indicating the toggle is expensive.</param>
    /// <param name="size">The size of the toggle icon.</param>
    /// <param name="margin">The margin around the toggle.</param>
    /// <param name="wrap">Whether the label should wrap to multiple lines.</param>
    public static void DrawToggle(
        ref Vector2 pos,
        float width,
        string label,
        TipSignal tooltip,
        bool checkOn,
        Action on,
        Action off,
        bool expensive = false,
        float size = SmallIconSize,
        float margin = Margin,
        bool wrap = true
    )
    {
        var toggleRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        pos.y += ListEntryHeight;
        DrawToggle(toggleRect, label, tooltip, checkOn, on, off, expensive, size, margin, wrap);
    }

    /// <summary>
    /// Draws a toggle UI element at the specified position, allowing the user to enable or disable a boolean value,
    /// and invokes the specified actions when toggled on or off, with explicit on/off states.
    /// </summary>
    /// <param name="pos">The position vector for the toggle UI element (will be updated).</param>
    /// <param name="width">The width of the toggle UI element.</param>
    /// <param name="label">The label to display next to the toggle.</param>
    /// <param name="tooltip">The tooltip to display when hovering over the toggle.</param>
    /// <param name="checkOn">Indicates whether the toggle is enabled.</param>
    /// <param name="checkOff">Indicates whether the toggle is disabled.</param>
    /// <param name="on">Action to invoke when toggled on.</param>
    /// <param name="off">Action to invoke when toggled off.</param>
    /// <param name="expensive">Whether to display an icon indicating the toggle is expensive.</param>
    /// <param name="size">The size of the toggle icon.</param>
    /// <param name="margin">The margin around the toggle.</param>
    /// <param name="wrap">Whether the label should wrap to multiple lines.</param>
    public static void DrawToggle(
        ref Vector2 pos,
        float width,
        string label,
        TipSignal tooltip,
        bool checkOn,
        bool checkOff,
        Action on,
        Action off,
        bool expensive = false,
        float size = SmallIconSize,
        float margin = Margin,
        bool wrap = true
    )
    {
        var toggleRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        pos.y += ListEntryHeight;
        DrawToggle(
            toggleRect,
            label,
            tooltip,
            checkOn,
            checkOff,
            on,
            off,
            expensive,
            size,
            margin,
            wrap
        );
    }

    /// <summary>
    /// Draws a toggle UI element at the specified rectangle, allowing the user to enable or disable a boolean value,
    /// and invokes the specified actions when toggled on or off.
    /// </summary>
    /// <param name="rect">The rectangle in which to draw the toggle UI element.</param>
    /// <param name="label">The label to display next to the toggle.</param>
    /// <param name="tooltip">The tooltip to display when hovering over the toggle.</param>
    /// <param name="checkOn">Indicates whether the toggle is enabled.</param>
    /// <param name="on">Action to invoke when toggled on.</param>
    /// <param name="off">Action to invoke when toggled off.</param>
    /// <param name="expensive">Whether to display an icon indicating the toggle is expensive.</param>
    /// <param name="size">The size of the toggle icon.</param>
    /// <param name="margin">The margin around the toggle.</param>
    /// <param name="wrap">Whether the label should wrap to multiple lines.</param>
    public static void DrawToggle(
        Rect rect,
        string label,
        TipSignal tooltip,
        bool checkOn,
        Action on,
        Action off,
        bool expensive = false,
        float size = SmallIconSize,
        float margin = Margin,
        bool wrap = true
    ) =>
        DrawToggle(rect, label, tooltip, checkOn, !checkOn, on, off, expensive, size, margin, wrap);

    /// <summary>
    /// Draws a toggle UI element at the specified rectangle, allowing the user to enable or disable a boolean value,
    /// and invokes the specified actions when toggled on or off, supporting partial selection state.
    /// </summary>
    /// <param name="rect">The rectangle in which to draw the toggle UI element.</param>
    /// <param name="label">The label to display next to the toggle.</param>
    /// <param name="tooltip">The tooltip to display when hovering over the toggle (nullable).</param>
    /// <param name="allOn">Indicates whether all items are enabled (checked state).</param>
    /// <param name="allOff">Indicates whether all items are disabled (unchecked state).</param>
    /// <param name="on">Action to invoke when toggled on.</param>
    /// <param name="off">Action to invoke when toggled off.</param>
    /// <param name="expensive">Whether to display an icon indicating the toggle is expensive.</param>
    /// <param name="size">The size of the toggle icon.</param>
    /// <param name="margin">The margin around the toggle.</param>
    /// <param name="wrap">Whether the label should wrap to multiple lines.</param>
    public static void DrawToggle(
        Rect rect,
        string label,
        TipSignal? tooltip,
        bool allOn,
        bool allOff,
        Action on,
        Action off,
        bool expensive = false,
        float size = SmallIconSize,
        float margin = Margin,
        bool wrap = true
    )
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
        labelRect.xMax = iconRect.xMin - (Margin / 2f);

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
            wordWrap: wrap
        );

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
            TooltipHandler.TipRegion(
                iconRect,
                "ColonyManagerRedux.Common.Expensive.Tip".Translate()
            );
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

    /// <summary>
    /// Draws a toggle UI element at the specified rectangle, allowing the user to enable or disable a boolean value,
    /// and invokes the specified action when toggled.
    /// </summary>
    /// <param name="rect">The rectangle in which to draw the toggle UI element.</param>
    /// <param name="label">The label to display next to the toggle.</param>
    /// <param name="tooltip">The tooltip to display when hovering over the toggle.</param>
    /// <param name="checkOn">Indicates whether the toggle is enabled.</param>
    /// <param name="toggle">Action to invoke when toggled.</param>
    /// <param name="expensive">Whether to display an icon indicating the toggle is expensive.</param>
    /// <param name="size">The size of the toggle icon.</param>
    /// <param name="margin">The margin around the toggle.</param>
    public static void DrawToggle(
        Rect rect,
        string label,
        TipSignal tooltip,
        bool checkOn,
        Action toggle,
        bool expensive = false,
        float size = SmallIconSize,
        float margin = Margin
    ) => DrawToggle(rect, label, tooltip, checkOn, toggle, toggle, expensive, size, margin);

    private static readonly List<IntVec3> _tmpHomeCells = [];

    /// <summary>
    /// Gets the base center position for the specified map, using the manager station if available,
    /// otherwise the average of the home area or a sensible fallback position.
    /// </summary>
    /// <param name="map">The map to determine the base center for.</param>
    /// <returns>The calculated base center position as an IntVec3.</returns>
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
        Building managerStation = map
            .listerBuildings.AllBuildingsColonistOfClass<Building_ManagerStation>()
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
            return map.mapPawns.SpawnedPawnsInFaction(Faction.OfPlayer)?.RandomElement().Position
                ?? map.Center;
        }
    }

    /// <summary>
    /// Draws a label with an outline by rendering the label multiple times with an offset and outline color, then draws the main label on top.
    /// </summary>
    /// <param name="icon">The rectangle in which to draw the label.</param>
    /// <param name="label">The text to display.</param>
    /// <param name="tooltip">The tooltip to display when hovering over the label (nullable).</param>
    /// <param name="anchor">The text anchor for alignment.</param>
    /// <param name="margin">The margin around the label.</param>
    /// <param name="font">The font to use for the label.</param>
    /// <param name="textColour">The color of the label text.</param>
    /// <param name="outlineColour">The color of the label outline.</param>
    public static void LabelOutline(
        Rect icon,
        string label,
        string? tooltip,
        TextAnchor anchor,
        float margin,
        GameFont font,
        Color textColour,
        Color outlineColour
    )
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

    /// <summary>
    /// Returns the absolute value of the specified integer, safely handling <see cref="int.MinValue"/>.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <returns>The absolute value of <paramref name="value"/>, or <see cref="int.MaxValue"/> if <paramref name="value"/> is <see cref="int.MinValue"/>.</returns>
    public static int SafeAbs(int value) =>
        value >= 0 ? value
        : value == int.MinValue ? int.MaxValue
        : -value;

    /// <summary>
    /// Sums a sequence of integers, saturating at <see cref="int.MaxValue"/> or <see cref="int.MinValue"/> if the sum exceeds the range of <see cref="int"/>.
    /// </summary>
    /// <param name="values">The sequence of integer values to sum.</param>
    /// <returns>The sum of the values, saturated to the range of <see cref="int"/>.</returns>
    public static int SaturatingIntSum(IEnumerable<int> values)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        long sum = 0;
        foreach (var v in values)
        {
            sum += v;
            if (sum > int.MaxValue)
            {
                return int.MaxValue;
            }
            if (sum < int.MinValue)
            {
                return int.MinValue;
            }
        }
        return (int)sum;
    }

    internal static void Scribe_IntArray(ref CircularBuffer<int> values, string label)
    {
        var capacity = 0;
        string? text = null;
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            capacity = values.Capacity;
            text = values.Join(i => i.ToString(CultureInfo.InvariantCulture), ":");
        }

        Scribe_Values.Look(ref capacity, $"{label}Capacity", History.EntriesPerInterval);
        Scribe_Values.Look(ref text, label);
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            values = new CircularBuffer<int>(
                capacity,
                text?.Split(':').Select(int.Parse).ToArray() ?? []
            );
        }
    }

    internal static void Scribe_IntTupleArray(ref CircularBuffer<(int, int)> values, string label)
    {
        var capacity = 0;
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
                        var count = int.Parse(values[0], CultureInfo.InvariantCulture);
                        var target = int.Parse(values[1], CultureInfo.InvariantCulture);
                        return (count, target);
                    })
                    .ToArray() ?? []
            );
        }
    }

    /// <summary>
    /// Serializes and deserializes a list of <see cref="Designation"/> objects for the specified map,
    /// ensuring only valid designations are kept and updating references after loading.
    /// </summary>
    /// <param name="designations">The list of designations to be scribed.</param>
    /// <param name="map">The map associated with the designations.</param>
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
            for (var i = designations.Count - 1; i >= 0; i--)
            {
                var item = designations[i];
                if (!map.designationManager.AllDesignations.Contains(item))
                {
                    designations.RemoveAt(i);
                }
            }
        }

        Scribe_Collections.Look(ref designations, "designations", LookMode.Deep);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            var designationManager = map.designationManager;
            for (var i = 0; i < designations.Count; i++)
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

    /// <summary>
    /// Serializes and deserializes an <see cref="Area"/> reference by its label using the provided <see cref="AreaManager"/>.
    /// </summary>
    /// <param name="area">The area reference to be scribed.</param>
    /// <param name="tmpAreaLabel">A temporary string to hold the area's label during serialization.</param>
    /// <param name="label">The label used for scribing.</param>
    /// <param name="areaManager">The <see cref="AreaManager"/> used to resolve the area by label during deserialization.</param>
    public static void Scribe_AreaByLabel(
        ref Area? area,
        ref string? tmpAreaLabel,
        string label,
        AreaManager areaManager
    )
    {
        if (areaManager == null)
        {
            throw new ArgumentNullException(nameof(areaManager));
        }

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            tmpAreaLabel = area?.Label;
        }

        ColonyManagerReduxMod.Instance.LogDebug(
            $"Scribed '{label}' with area label: '{tmpAreaLabel}' before with {Scribe.mode}"
        );
        Scribe_Values.Look(ref tmpAreaLabel, label);
        ColonyManagerReduxMod.Instance.LogDebug(
            $"Scribed '{label}' with area label: '{tmpAreaLabel}' after with {Scribe.mode}"
        );

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            ColonyManagerReduxMod.Instance.LogDebug(
                $"Attempting to load '{label}' by its area label: '{tmpAreaLabel}' from the areaManager"
            );
            area = areaManager.GetLabeled(tmpAreaLabel);
            tmpAreaLabel = null;
        }
    }

    /// <summary>
    /// Serializes and deserializes a set of <see cref="Area"/> references by their labels using the provided <see cref="AreaManager"/>.
    /// </summary>
    /// <param name="areas">The set of areas to be scribed.</param>
    /// <param name="tmpAreaLabels">A temporary list to hold the area labels during serialization.</param>
    /// <param name="label">The label used for scribing.</param>
    /// <param name="areaManager">The <see cref="AreaManager"/> used to resolve areas by label during deserialization.</param>
    public static void Scribe_AreasByLabel(
        ref HashSet<Area> areas,
        ref List<string>? tmpAreaLabels,
        string label,
        AreaManager areaManager
    )
    {
        if (areaManager == null)
        {
            throw new ArgumentNullException(nameof(areaManager));
        }

        if (Scribe.mode == LoadSaveMode.Saving)
        {
            tmpAreaLabels = areas?.Select(a => a.Label).ToList();
        }
        ColonyManagerReduxMod.Instance.LogDebug(
            $"Scribed '{label}' with area labels: '{string.Join(", ", tmpAreaLabels ?? [])}' before with {Scribe.mode}"
        );
        Scribe_Collections.Look(ref tmpAreaLabels, label, LookMode.Value);
        ColonyManagerReduxMod.Instance.LogDebug(
            $"Scribed '{label}' with area labels: '{string.Join(", ", tmpAreaLabels ?? [])}' after with {Scribe.mode}"
        );

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            ColonyManagerReduxMod.Instance.LogDebug(
                $"Attempting to load '{label}' by its area labels: '{string.Join(", ", tmpAreaLabels ?? [])}' from the areaManager"
            );
            areas = tmpAreaLabels?.Select(areaManager.GetLabeled).ToHashSet() ?? [];
            tmpAreaLabels = null;
        }
    }

    /// <summary>
    /// Determines whether the specified pawn is incapable of performing all tasks in the given work type.
    /// </summary>
    /// <param name="pawn">The pawn to check.</param>
    /// <param name="work">The work type definition to check against.</param>
    /// <returns>True if the pawn is incapable of all work givers in the work type; otherwise, false.</returns>
    // PawnColumnWorker_WorkPriority.IsIncapableOfWholeWorkType, but static
    public static bool IsIncapableOfWholeWorkType(this Pawn pawn, WorkTypeDef work)
    {
        if (pawn == null)
        {
            throw new ArgumentNullException(nameof(pawn));
        }
        if (work == null)
        {
            throw new ArgumentNullException(nameof(work));
        }

        for (var i = 0; i < work.workGiversByPriority.Count; i++)
        {
            var flag = true;
            for (var j = 0; j < work.workGiversByPriority[i].requiredCapacities.Count; j++)
            {
                var capacity = work.workGiversByPriority[i].requiredCapacities[j];
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

    internal static bool IsLikelyAnonymous(Delegate delegat)
    {
        var method = delegat.Method;
        var declaringType = method.DeclaringType;

        // Lambdas and local functions usually have generated names containing things like:
        // "<>c__DisplayClass", "<>c", "<SomeMethodName>b__..."
#if v1_5
        return method.Name.Contains('<')
#else
        return method.Name.Contains('<', StringComparison.Ordinal)
#endif
            || declaringType.GetCustomAttributes(typeof(CompilerGeneratedAttribute), false).Length
                != 0;
    }
}
