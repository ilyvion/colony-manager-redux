// CompManagerJobHistory.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Properties for a manager job history component, including worker type, chapters, and display options.
/// </summary>
public class CompProperties_ManagerJobHistory : ManagerJobCompProperties
{
    /// <summary>
    /// The type of the worker class used for history tracking.
    /// </summary>
    public Type workerClass = typeof(HistoryWorker);

    /// <summary>
    /// The list of chapter definitions for the job history.
    /// </summary>
    public List<ManagerJobHistoryChapterDef> chapters;

    /// <summary>
    /// Whether the legend can be toggled in the UI.
    /// </summary>
    public bool allowTogglingLegend = true;

    /// <summary>
    /// Whether to draw the legend inline with the graph.
    /// </summary>
    public bool drawInlineLegend = true;

    /// <summary>
    /// Whether to draw options for the graph.
    /// </summary>
    public bool drawOptions = true;

    /// <summary>
    /// Whether to draw a target line on the graph.
    /// </summary>
    public bool drawTargetLine = true;

    /// <summary>
    /// The period shown on the graph (e.g., day, week).
    /// </summary>
    public Period periodShown = Period.Day;

    /// <summary>
    /// The suffix to display on the Y axis.
    /// </summary>
    public string yAxisSuffix = string.Empty;

#pragma warning disable CS8618
    /// <summary>
    /// Initializes a new instance of the <see cref="CompProperties_ManagerJobHistory"/> class.
    /// </summary>
    public CompProperties_ManagerJobHistory()
#pragma warning restore CS8618
    {
        compClass = typeof(CompManagerJobHistory);
    }

    private HistoryWorker? workerInt;

    /// <summary>
    /// Gets the history worker instance for this component.
    /// </summary>
    public HistoryWorker Worker
    {
        get
        {
            workerInt ??= (HistoryWorker)Activator.CreateInstance(workerClass);
            return workerInt;
        }
    }

    /// <inheritdoc/>
    public override IEnumerable<string> ConfigErrors(ManagerDef parentDef)
    {
        if (parentDef == null)
        {
            throw new ArgumentNullException(nameof(parentDef));
        }

        foreach (var item in base.ConfigErrors(parentDef))
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
