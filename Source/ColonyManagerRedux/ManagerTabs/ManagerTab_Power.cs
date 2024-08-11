// ManagerTab_Power.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[HotSwappable]
internal sealed class ManagerTab_Power(Manager manager) : ManagerTab<ManagerJob_Power>(manager)
{
    private Vector2 _consumptionScrollPos = Vector2.zero;

    private Vector2 _overallScrollPos = Vector2.zero;

    private Vector2 _productionScrollPos = Vector2.zero;

    private readonly DetailedLegendRenderer _detailedLegendRendererTrading = new()
    {
        DrawInfoInBar = true,
        DrawMaxMarkers = true,
    };
    private readonly DetailedLegendRenderer _detailedLegendRendererOverall = new()
    {
        DrawIcons = false,
        DrawInfoInBar = true,
        DrawMaxMarkers = true,
        MaxPerChapter = true,
    };

    protected override IEnumerable<ManagerJob> ManagerJobs
    {
        get
        {
            var job = Manager.JobTracker.JobsOfType<ManagerJob_Power>().SingleOrDefault();
            if (job == null)
            {
                job = (ManagerJob_Power)ManagerDefMaker.MakeManagerJob(Def, Manager)!;
                Manager.JobTracker.Add(job);
                Selected = job;
            }
            yield return job;
        }
    }

    public override string DisabledReason
    {
        get
        {
            if (!ResearchedFinished)
            {
                return "ColonyManagerRedux.Energy.NotResearched".Translate();
            }

            if (!SelectedJob!.AnyPoweredStationOnline)
            {
                return "ColonyManagerRedux.Energy.NoPoweredStation".Translate();
            }

            if (!ColonyManagerReduxMod.Settings.RecordHistoricalData)
            {
                return "ColonyManagerRedux.Energy.RecordHistoricalDataDisabled".Translate();
            }

            return "Not sure. It should be enabled? Send a bug report.";
        }
    }

    public override bool Enabled => ResearchedFinished && SelectedJob!.AnyPoweredStationOnline && ColonyManagerReduxMod.Settings.RecordHistoricalData;

    protected override bool CreateNewSelectedJobOnMake => false;

    public static bool ResearchedFinished
    {
        get => ManagerResearchProjectDefOf.PowerManagement.IsFinished;
    }

    public static void OnPowerResearchedFinished()
    {
        foreach (var map in Find.Maps)
        {
            ManagerTab_Power tab = Manager.For(map).Tabs.OfType<ManagerTab_Power>().First();
            tab.Selected ??= tab.ManagerJobs.First();
        }
    }

    protected override void DoTabContents(Rect canvas)
    {
        if (!Enabled)
        {
            MainTabWindow_Manager.GoTo(MainTabWindow_Manager.DefaultTab);
        }

        // set up rects
        var overviewRect = new Rect(0f, 0f, canvas.width, 150f);
        var consumtionRect = new Rect(0f, overviewRect.height + Margin,
                                       (canvas.width - Margin) / 2f,
                                       canvas.height - overviewRect.height - Margin);
        var productionRect = new Rect(consumtionRect.xMax + Margin,
                                       overviewRect.height + Margin,
                                       (canvas.width - Margin) / 2f,
                                       canvas.height - overviewRect.height - Margin);

        // draw area BG's
        Widgets.DrawMenuSection(overviewRect);
        Widgets.DrawMenuSection(consumtionRect);
        Widgets.DrawMenuSection(productionRect);

        // draw contents
        DrawOverview(overviewRect);
        DrawConsumption(consumtionRect);
        DrawProduction(productionRect);
    }

    public override void PreOpen()
    {
        base.PreOpen();

        Selected ??= ManagerJobs.First();

        // close this tab if it was selected but no longer available
        if (!SelectedJob!.AnyPoweredStationOnline && MainTabWindow_Manager.CurrentTab == this)
        {
            MainTabWindow_Manager.GoTo(MainTabWindow_Manager.DefaultTab);
            return;
        }
    }

    public override void Tick()
    {
        base.Tick();
    }

    private void DrawConsumption(Rect canvas)
    {
        // setup rects
        var plotRect = new Rect(canvas.xMin, canvas.yMin, canvas.width, (canvas.height - Margin) / 2f);
        var legendRect = new Rect(canvas.xMin, plotRect.yMax + Margin, canvas.width,
                                   (canvas.height - Margin) / 2f);

        var tradingHistory = SelectedJob!.tradingHistory;

        // draw the plot
        tradingHistory.DrawPlot(plotRect, negativeOnly: true);

        // draw the detailed legend
        _detailedLegendRendererTrading.DrawDetailedLegend(tradingHistory, legendRect, ref _consumptionScrollPos, null, false, true);
    }

    private void DrawOverview(Rect canvas)
    {
        // setup rects
        var legendRect = new Rect(canvas.xMin, canvas.yMin, (canvas.width - Margin) / 2f,
                                   canvas.height - ButtonSize.y - Margin);
        var plotRect = new Rect(legendRect.xMax + Margin, canvas.yMin,
                                 (canvas.width - Margin) / 2f, canvas.height);
        var buttonsRect = new Rect(canvas.xMin, legendRect.yMax + Margin,
                                    (canvas.width - Margin) / 2f, ButtonSize.y);

        var overallHistory = SelectedJob!.CompOfType<CompManagerJobHistory>()!.History;
        var tradingHistory = SelectedJob!.tradingHistory;

        // draw the plot
        overallHistory.DrawOptions = false;
        overallHistory.DrawInlineLegend = false;
        overallHistory.DrawPlot(plotRect);
        overallHistory.DrawOptions = true;
        overallHistory.DrawInlineLegend = true;

        // draw the detailed legend
        _detailedLegendRendererOverall.DrawDetailedLegend(overallHistory, legendRect, ref _overallScrollPos, null);

        var periodRect = buttonsRect;
        periodRect.xMin += Margin;

        // label
        Text.Anchor = TextAnchor.MiddleLeft;
        var labelTextSize = Text.CalcSize("ColonyManagerRedux.Energy.PeriodShown".Translate() + ":");
        Widgets.Label(periodRect, "ColonyManagerRedux.Energy.PeriodShown".Translate() + ":");

        var buttonTextSize = Text.CalcSize($"ColonyManagerRedux.History.PeriodShown.{overallHistory.PeriodShown}".Translate().CapitalizeFirst());
        periodRect.xMin += Margin + labelTextSize.x;
        periodRect.yMin += (periodRect.height - 30f) / 2;
        periodRect.width = buttonTextSize.x + LargeIconSize;
        periodRect.height = 30f;

        var tooltip = "ColonyManagerRedux.Energy.PeriodShownTooltip".Translate(
            $"ColonyManagerRedux.History.PeriodShown.{overallHistory.PeriodShown}".Translate());
        TooltipHandler.TipRegion(periodRect, tooltip);
        if (Widgets.ButtonText(periodRect, $"ColonyManagerRedux.History.PeriodShown.{overallHistory.PeriodShown}".Translate().CapitalizeFirst()))
        {
            var periodOptions = new List<FloatMenuOption>();
            for (var i = 0; i < History.Periods.Length; i++)
            {
                var period = History.Periods[i];
                periodOptions.Add(new FloatMenuOption(
                    $"ColonyManagerRedux.History.PeriodShown.{period}".Translate().CapitalizeFirst(),
                    delegate
                    {
                        overallHistory.PeriodShown = period;
                        tradingHistory.PeriodShown = period;
                    }));
            }

            Find.WindowStack.Add(new FloatMenu(periodOptions));
        }
    }

    private void DrawProduction(Rect canvas)
    {
        // setup rects
        var plotRect = new Rect(canvas.xMin, canvas.yMin, canvas.width, (canvas.height - Margin) / 2f);
        var legendRect = new Rect(canvas.xMin, plotRect.yMax + Margin, canvas.width,
                                   (canvas.height - Margin) / 2f);

        var tradingHistory = SelectedJob!.tradingHistory;

        // draw the plot
        tradingHistory.DrawPlot(plotRect, positiveOnly: true);

        // draw the detailed legend
        _detailedLegendRendererTrading.MaxPerChapter = false;
        _detailedLegendRendererTrading.DrawDetailedLegend(tradingHistory, legendRect, ref _productionScrollPos, null, true);
    }

    public override string GetSubLabel(ManagerJob job)
    {
        ManagerJob_Power powerJob = (ManagerJob_Power)job;
        return string.Format("{0} producers, {1} consumers, {2} batteries",
            powerJob.ProducerCount,
            powerJob.ConsumerCount,
            powerJob.BatteryCount);
    }
}
