// CompDrawExportListEntry.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public class CompDrawExportListEntry : ManagerRenderComp<CompProperties_DrawExportListEntry>
{
}

public class CompProperties_DrawExportListEntry
    : ManagerRenderCompProperties<CompDrawExportListEntry, DrawExportListEntryWorker>
{
}

public abstract class DrawExportListEntryWorker
{
    public abstract void DrawExportListEntry(ManagerJob job, ref Vector2 position, float width);
}

public abstract class DrawExportListEntryWorker<T> : DrawExportListEntryWorker where T : ManagerJob
{
    public override sealed void DrawExportListEntry(
        ManagerJob job,
        ref Vector2 position,
        float width)
    {
        DrawExportListEntry((T)job, ref position, width);
    }

    public abstract void DrawExportListEntry(T job, ref Vector2 position, float width);
}
