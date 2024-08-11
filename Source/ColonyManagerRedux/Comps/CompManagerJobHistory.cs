// CompManagerJobHistory.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public class CompManagerJobHistory : ManagerJobComp
{
    public CompProperties_ManagerJobHistory Props => (CompProperties_ManagerJobHistory)props;

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
        worker.HistoryUpdateTick(parent, tick);

        if (worker.UpdatesMax)
        {
            History.UpdateMax(Props.chapters
                .Select(c => Props.Worker.GetMaxForHistoryChapter(parent, tick, c))
                .ToArray());
        }

        History.Update(tick, Props.chapters
            .Select(c => (
                Props.Worker.GetCountForHistoryChapter(parent, tick, c),
                Props.Worker.GetTargetForHistoryChapter(parent, tick, c)))
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

public class CompProperties_ManagerJobHistory : ManagerJobCompProperties
{
    public Type workerClass = typeof(HistoryWorker);
    public List<ManagerJobHistoryChapterDef> chapters;

    public bool allowTogglingLegend = true;
    public bool drawInlineLegend = true;
    public bool drawOptions = true;
    public bool drawTargetLine = true;
    public Period periodShown = Period.Day;
    public string yAxisSuffix = string.Empty;


#pragma warning disable CS8618
    public CompProperties_ManagerJobHistory()
#pragma warning restore CS8618
    {
        compClass = typeof(CompManagerJobHistory);
    }

    private HistoryWorker? workerInt;
    public HistoryWorker Worker
    {
        get
        {
            workerInt ??= (HistoryWorker)Activator.CreateInstance(workerClass);
            return workerInt;
        }
    }

    public override IEnumerable<string> ConfigErrors(ManagerDef parentDef)
    {
        if (parentDef == null)
        {
            throw new ArgumentNullException(nameof(parentDef));
        }

        foreach (string item in base.ConfigErrors(parentDef))
        {
            yield return item;
        }

        if (workerClass == null)
        {
            yield return $"{nameof(workerClass)} is null";
        }
        if (!typeof(HistoryWorker).IsAssignableFrom(workerClass))
        {
            yield return $"{nameof(workerClass)} is not a subclass of {nameof(HistoryWorker)}";
        }

        if (chapters.NullOrEmpty())
        {
            yield return parentDef.defName + " is missing chapters";
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
