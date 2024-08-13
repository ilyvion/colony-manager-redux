// CompDrawOverviewListEntry.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public class CompProperties_DrawOverviewListEntry
    : ManagerRenderCompProperties<CompDrawOverviewListEntry, DrawOverviewListEntryWorker>
{
    public DrawOverviewListEntryParameters drawListEntryParameters = new();
}

public class DrawOverviewListEntryParameters
{
    public bool ShowProgressbar { get; set; } = true;
}
