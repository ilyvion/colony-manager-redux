﻿// UpdateInterval.cs
// Copyright Karel Kroeze, 2019-2020

using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ColonyManagerRedux
{
    public class UpdateInterval
    {
        private static UpdateInterval? _daily;
        public string label;
        public int ticks;

        public UpdateInterval(int ticks, string label)
        {
            this.ticks = ticks;
            this.label = label;
        }

        public static UpdateInterval Daily
        {
            get
            {
                if (_daily == null) _daily = new UpdateInterval(GenDate.TicksPerDay, "ColonyManagerRedux.ManagerDaily".Translate());

                return _daily;
            }
        }

        public void Draw(Rect canvas, ManagerJob job)
        {
            Text.Anchor = TextAnchor.MiddleCenter;

            // how many hours have passed since the last update?
            var lastUpdate = Find.TickManager.TicksGame - job.lastAction;
            var progress = (float)lastUpdate / GenDate.TicksPerHour;
            var nextUpdate = (float)job.UpdateInterval.ticks / GenDate.TicksPerHour;

            // how far over time are we? Draw redder if further over time.
            var progressColour = progress < nextUpdate
                ? Color.white
                : Color.Lerp(Color.white, Color.red, (progress - nextUpdate) / nextUpdate * 2f);

            if (nextUpdate < 12 && progress < 12)
            {
                var nextUpdateHandle = new ClockHandle(nextUpdate, GenUI.MouseoverColor);
                var progressHandle =
                    new ClockHandle(progress, progressColour);
                Clock.Draw(canvas.ContractedBy(4f), nextUpdateHandle, progressHandle);
            }
            else
            {
                var nextUpdateMarker =
                    new CalendarMarker(nextUpdate / GenDate.HoursPerDay, GenUI.MouseoverColor, false);
                var progressMarker = new CalendarMarker(progress / GenDate.HoursPerDay, progressColour, true);
                Calendar.Draw(canvas.ContractedBy(2f), progressMarker, nextUpdateMarker);
            }

            TooltipHandler.TipRegion(canvas,
                                      "ColonyManagerRedux.ManagerLastUpdateTooltip".Translate(
                                          lastUpdate.TimeString(),
                                          job.UpdateInterval.ticks.TimeString()));

            Widgets.DrawHighlightIfMouseover(canvas);
            if (Widgets.ButtonInvisible(canvas))
            {
                var options = new List<FloatMenuOption>();
                foreach (var interval in Utilities.UpdateIntervalOptions)
                    options.Add(new FloatMenuOption(interval.label, () => job.UpdateInterval = interval));

                Find.WindowStack.Add(new FloatMenu(options));
            }
        }
    }
}
