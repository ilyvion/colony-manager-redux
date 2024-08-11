// CompManagerJobHistory.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

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
