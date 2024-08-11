// CompManagerJobHistory.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public class CompManagerJobHistory : ManagerJobComp
{
    public new CompProperties_ManagerJobHistory Props => (CompProperties_ManagerJobHistory)base.Props;

#pragma warning disable CS8618 // Set in Initialize
    private History history;
#pragma warning restore CS8618
    public History History { get => history; }

    public override void Initialize(ManagerJobCompProperties props)
    {
        base.Initialize(props);

        // create History tracker
        history = new History(Props.chapters)
        {
            AllowTogglingLegend = Props.allowTogglingLegend,
            DrawInlineLegend = Props.drawInlineLegend,
            DrawOptions = Props.drawOptions,
            DrawTargetLine = Props.drawTargetLine,

            PeriodShown = Props.periodShown,
            YAxisSuffix = Props.yAxisSuffix,
        };
    }

    public override void CompTick()
    {
        base.CompTick();

        int ticksGame = Find.TickManager.TicksGame;
        if (historyUpdateTickJitter.Count != 0 && historyUpdateTickJitter.TryGetValue(ticksGame, out int originalTick))
        {
            DoHistoryUpdate(originalTick);
            historyUpdateTickJitter.Remove(ticksGame);
        }

        if (!ColonyManagerReduxMod.Settings.RecordHistoricalData || !History.IsUpdateTick)
        {
            return;
        }

        var jitterTick = ticksGame + Rand.Range(0, 300);
        while (historyUpdateTickJitter.ContainsKey(jitterTick))
        {
            jitterTick = ticksGame + Rand.Range(0, 300);
        }
        historyUpdateTickJitter.Add(jitterTick, ticksGame);
    }

    private readonly Dictionary<int, int> historyUpdateTickJitter = [];
    private void DoHistoryUpdate(int tick)
    {
        HistoryWorker worker = Props.Worker;
        worker.HistoryUpdateTick(Parent, tick);

        if (worker.UpdatesMax)
        {
            History.UpdateMax(Props.chapters
                .Select(c => Props.Worker.GetMaxForHistoryChapter(Parent, tick, c))
                .ToArray());
        }

        History.Update(tick, Props.chapters
            .Select(c => (
                Props.Worker.GetCountForHistoryChapter(Parent, tick, c),
                Props.Worker.GetTargetForHistoryChapter(Parent, tick, c)))
            .ToArray());
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        if (Manager.Mode == Manager.ScribingMode.Normal)
        {
            Scribe_Deep.Look(ref history, "history");
        }
    }
}

public abstract class HistoryWorker
{
    public abstract int GetCountForHistoryChapter(ManagerJob managerJob, int tick, ManagerJobHistoryChapterDef chapterDef);
    public abstract int GetTargetForHistoryChapter(ManagerJob managerJob, int tick, ManagerJobHistoryChapterDef chapterDef);

    public virtual bool UpdatesMax { get; }
    public abstract int GetMaxForHistoryChapter(ManagerJob managerJob, int tick, ManagerJobHistoryChapterDef chapterDef);

    public virtual void HistoryUpdateTick(ManagerJob managerJob, int tick)
    {
    }
}

public abstract class HistoryWorker<T> : HistoryWorker where T : ManagerJob
{
    public override sealed int GetCountForHistoryChapter(ManagerJob managerJob, int tick, ManagerJobHistoryChapterDef chapterDef)
    {
        return GetCountForHistoryChapter((T)managerJob, tick, chapterDef);
    }
    public override sealed int GetTargetForHistoryChapter(ManagerJob managerJob, int tick, ManagerJobHistoryChapterDef chapterDef)
    {
        return GetTargetForHistoryChapter((T)managerJob, tick, chapterDef);
    }
    public override sealed int GetMaxForHistoryChapter(ManagerJob managerJob, int tick, ManagerJobHistoryChapterDef chapterDef)
    {
        return GetMaxForHistoryChapter((T)managerJob, tick, chapterDef);
    }
    public override sealed void HistoryUpdateTick(ManagerJob managerJob, int tick)
    {
        HistoryUpdateTick((T)managerJob, tick);
    }

    public abstract int GetCountForHistoryChapter(T managerJob, int tick, ManagerJobHistoryChapterDef chapterDef);
    public abstract int GetTargetForHistoryChapter(T managerJob, int tick, ManagerJobHistoryChapterDef chapterDef);
    public virtual int GetMaxForHistoryChapter(T managerJob, int tick, ManagerJobHistoryChapterDef chapterDef)
    {
        return 0;
    }
    public virtual void HistoryUpdateTick(T managerJob, int tick)
    {
    }
}
