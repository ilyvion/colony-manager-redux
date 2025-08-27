// CompDrawOverviewListEntry.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Component for rendering an overview list entry in the manager UI.
/// </summary>
public class CompDrawOverviewListEntry : ManagerRenderComp<CompProperties_DrawOverviewListEntry> { }

/// <summary>
/// Abstract base class for workers that handle drawing overview list entries.
/// </summary>
public abstract class DrawOverviewListEntryWorker
{
    /// <summary>
    /// Allows modification of draw list entry parameters before rendering.
    /// </summary>
    /// <param name="job">The manager job associated with the entry.</param>
    /// <param name="parameters">The parameters to modify.</param>
    public virtual void ChangeDrawListEntryParameters(
        ManagerJob job,
        ref DrawOverviewListEntryParameters parameters
    ) { }

    /// <summary>
    /// Draws the overview list entry for the specified job.
    /// </summary>
    /// <param name="job">The manager job to draw.</param>
    /// <param name="position">The position to draw at.</param>
    /// <param name="width">The width of the entry.</param>
    public abstract void DrawOverviewListEntry(ManagerJob job, ref Vector2 position, float width);
}

/// <summary>
/// Generic abstract base class for workers that handle drawing overview list entries for a specific job type.
/// </summary>
/// <typeparam name="T">The type of manager job.</typeparam>
public abstract class DrawOverviewListEntryWorker<T> : DrawOverviewListEntryWorker
    where T : ManagerJob
{
    /// <inheritdoc/>
    public sealed override void ChangeDrawListEntryParameters(
        ManagerJob job,
        ref DrawOverviewListEntryParameters parameters
    ) => ChangeDrawListEntryParameters((T)job, ref parameters);

    /// <inheritdoc/>
    public sealed override void DrawOverviewListEntry(
        ManagerJob job,
        ref Vector2 position,
        float width
    ) => DrawOverviewListEntry((T)job, ref position, width);

    /// <summary>
    /// Allows modification of draw list entry parameters for a specific job type before rendering.
    /// </summary>
    /// <param name="job">The manager job of type <typeparamref name="T"/>.</param>
    /// <param name="parameters">The parameters to modify.</param>
    public virtual void ChangeDrawListEntryParameters(
        T job,
        ref DrawOverviewListEntryParameters parameters
    ) { }

    /// <summary>
    /// Draws the overview list entry for the specified job of type <typeparamref name="T"/>.
    /// </summary>
    /// <param name="job">The manager job to draw.</param>
    /// <param name="position">The position to draw at.</param>
    /// <param name="width">The width of the entry.</param>
    public abstract void DrawOverviewListEntry(T job, ref Vector2 position, float width);
}
