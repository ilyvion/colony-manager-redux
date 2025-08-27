// CompDrawOverviewListEntry.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Properties for the component that draws an overview list entry in the manager UI.
/// </summary>
public class CompProperties_DrawOverviewListEntry
    : ManagerRenderCompProperties<CompDrawOverviewListEntry, DrawOverviewListEntryWorker>
{
    /// <summary>
    /// Parameters for drawing the overview list entry.
    /// </summary>
    public DrawOverviewListEntryParameters drawListEntryParameters = new();
}

/// <summary>
/// Parameters for drawing an overview list entry.
/// </summary>
public class DrawOverviewListEntryParameters
{
    /// <summary>
    /// Gets or sets a value indicating whether to show a progress bar in the list entry.
    /// </summary>
    public bool ShowProgressbar { get; set; } = true;
}
