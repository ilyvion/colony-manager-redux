// CompDrawOverviewListEntry.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public class CompDrawOverviewListEntry : ManagerRenderComp<CompProperties_DrawOverviewListEntry>
{
}

public abstract class DrawOverviewListEntryWorker
{
    public virtual void ChangeDrawListEntryParameters(
        ManagerJob job,
        ref DrawOverviewListEntryParameters parameters)
    {
    }
    public abstract void DrawOverviewListEntry(ManagerJob job, ref Vector2 position, float width);
}

public abstract class DrawOverviewListEntryWorker<T> : DrawOverviewListEntryWorker where T : ManagerJob
{
    public override sealed void ChangeDrawListEntryParameters(
        ManagerJob job,
        ref DrawOverviewListEntryParameters parameters)
    {
        ChangeDrawListEntryParameters((T)job, ref parameters);
    }
    public override sealed void DrawOverviewListEntry(
        ManagerJob job,
        ref Vector2 position,
        float width)
    {
        DrawOverviewListEntry((T)job, ref position, width);
    }

    public virtual void ChangeDrawListEntryParameters(
        T job,
        ref DrawOverviewListEntryParameters parameters)
    {
    }
    public abstract void DrawOverviewListEntry(T job, ref Vector2 position, float width);
}
