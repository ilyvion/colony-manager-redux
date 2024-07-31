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
            Suffix = Props.suffix,
        };
    }

    public override void CompTick()
    {
        base.CompTick();

        if (!History.IsUpdateTick)
        {
            return;
        }

        HistoryWorker worker = Props.Worker;
        worker.HistoryUpdateTick(parent);

        if (worker.UpdatesMax)
        {
            History.UpdateMax(Props.chapters
                .Select(c => Props.Worker.GetMaxForHistoryChapter(parent, c))
                .ToArray());
        }

        History.Update(Props.chapters
            .Select(c => (
                Props.Worker.GetCountForHistoryChapter(parent, c),
                Props.Worker.GetTargetForHistoryChapter(parent, c)))
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
    public string suffix = string.Empty;

    private HistoryWorker workerInt;

#pragma warning disable CS8618
    public CompProperties_ManagerJobHistory()
#pragma warning restore CS8618
    {
        compClass = typeof(CompManagerJobHistory);
    }

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
    public abstract int GetCountForHistoryChapter(ManagerJob managerJob, ManagerJobHistoryChapterDef chapterDef);
    public abstract int GetTargetForHistoryChapter(ManagerJob managerJob, ManagerJobHistoryChapterDef chapterDef);

    public virtual bool UpdatesMax { get; }
    public abstract int GetMaxForHistoryChapter(ManagerJob managerJob, ManagerJobHistoryChapterDef chapterDef);

    public virtual void HistoryUpdateTick(ManagerJob managerJob)
    {
    }
}

public abstract class HistoryWorker<T> : HistoryWorker where T : ManagerJob
{
    public override sealed int GetCountForHistoryChapter(ManagerJob managerJob, ManagerJobHistoryChapterDef chapterDef)
    {
        return GetCountForHistoryChapter((T)managerJob, chapterDef);
    }
    public override sealed int GetTargetForHistoryChapter(ManagerJob managerJob, ManagerJobHistoryChapterDef chapterDef)
    {
        return GetTargetForHistoryChapter((T)managerJob, chapterDef);
    }
    public override sealed int GetMaxForHistoryChapter(ManagerJob managerJob, ManagerJobHistoryChapterDef chapterDef)
    {
        return GetMaxForHistoryChapter((T)managerJob, chapterDef);
    }
    public override sealed void HistoryUpdateTick(ManagerJob managerJob)
    {
        HistoryUpdateTick((T)managerJob);
    }

    public abstract int GetCountForHistoryChapter(T managerJob, ManagerJobHistoryChapterDef chapterDef);
    public abstract int GetTargetForHistoryChapter(T managerJob, ManagerJobHistoryChapterDef chapterDef);
    public virtual int GetMaxForHistoryChapter(T managerJob, ManagerJobHistoryChapterDef chapterDef)
    {
        return 0;
    }
    public virtual void HistoryUpdateTick(T managerJob)
    {
    }
}
