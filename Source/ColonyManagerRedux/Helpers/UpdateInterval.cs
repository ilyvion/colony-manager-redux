// UpdateInterval.cs
// Copyright Karel Kroeze, 2019-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HotSwappable]
public class UpdateInterval(int ticks, string label)
{
    private static UpdateInterval? _daily;
    public string label = label;
    public int ticks = ticks;

    public static UpdateInterval Daily
    {
        get
        {
            _daily ??= new UpdateInterval(GenDate.TicksPerDay, "ColonyManagerRedux.UpdateInterval.Daily".Translate());

            return _daily;
        }
    }

    internal static void Draw(Rect canvas, ManagerJob job, bool exporting)
    {
        string lastUpdateTooltip;
        var nextUpdate = (float)job.UpdateInterval.ticks / GenDate.TicksPerHour;
        if (exporting)
        {
            if (nextUpdate < 12)
            {
                var nextUpdateHandle = new ClockHandle(nextUpdate, GenUI.MouseoverColor);
                var progressHandle = new ClockHandle(0f, Color.white);
                Clock.Draw(canvas.ContractedBy(4f), nextUpdateHandle, progressHandle);
            }
            else
            {
                var nextUpdateMarker =
                    new CalendarMarker(nextUpdate / GenDate.HoursPerDay, GenUI.MouseoverColor, false);
                var progressMarker = new CalendarMarker(0f, Color.white, true);
                Calendar.Draw(canvas.ContractedBy(2f), progressMarker, nextUpdateMarker);
            }

            lastUpdateTooltip = "";
        }
        else if (job.HasBeenUpdated)
        {
            // how many hours have passed since the last update?
            var lastUpdate = job.TimeSinceLastUpdate;
            var progress = (float)lastUpdate / GenDate.TicksPerHour;

            // how far over time are we? Draw redder if further over time.
            var progressColour = progress < nextUpdate
                ? Color.white
                : Color.Lerp(Color.white, Color.red, (progress - nextUpdate) / nextUpdate * 2f);

            if (nextUpdate < 12 && progress < 12)
            {
                var nextUpdateHandle = new ClockHandle(nextUpdate, GenUI.MouseoverColor);
                var progressHandle = new ClockHandle(progress, progressColour);
                Clock.Draw(canvas.ContractedBy(4f), nextUpdateHandle, progressHandle);
            }
            else
            {
                var nextUpdateMarker =
                    new CalendarMarker(nextUpdate / GenDate.HoursPerDay, GenUI.MouseoverColor, false);
                var progressMarker = new CalendarMarker(progress / GenDate.HoursPerDay, progressColour, true);
                Calendar.Draw(canvas.ContractedBy(2f), progressMarker, nextUpdateMarker);
            }

            lastUpdateTooltip = "ColonyManagerRedux.Job.LastUpdatedTooltip".Translate(
                lastUpdate.TimeString()) + " ";
        }
        else
        {
            GUI.color = GenUI.MouseoverColor;
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(canvas, "---");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.LowerLeft;

            lastUpdateTooltip = "ColonyManagerRedux.Job.NeverUpdatedTooltip".Translate() + " ";
        }

        lastUpdateTooltip += "ColonyManagerRedux.Job.ScheduledToBeUpdatedTooltip".Translate(
            job.UpdateInterval.ticks.TimeString());

        if (!exporting)
        {
            lastUpdateTooltip += "\n\n" + "ColonyManagerRedux.Job.ClickToChangeUpdateIntervalTooltip".Translate();
        }
        TooltipHandler.TipRegion(canvas, lastUpdateTooltip);

        if (!exporting)
        {
            Widgets.DrawHighlightIfMouseover(canvas);
            if (Widgets.ButtonInvisible(canvas))
            {
                var options = new List<FloatMenuOption>();
                if (!job.IsSuspended && !job.IsCompleted)
                {
                    options.Add(new FloatMenuOption("ColonyManagerRedux.Job.ForceUpdate".Translate(), job.Untouch));
                }
                foreach (var interval in Utilities.UpdateIntervalOptions)
                {
                    options.Add(new FloatMenuOption("ColonyManagerRedux.Job.Update".Translate(interval.label.UncapitalizeFirst()), () => job.UpdateInterval = interval));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }
        }
    }
}
