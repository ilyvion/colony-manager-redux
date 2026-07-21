// CompDrawExportListEntry.cs
// Copyright (c) 2024–2025 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// A render component for drawing export list entries in the Colony Manager Redux mod.
/// </summary>
public class CompDrawExportListEntry : ManagerRenderComp<CompProperties_DrawExportListEntry> { }

/// <summary>
/// Properties for the CompDrawExportListEntry render component.
/// </summary>
public class CompProperties_DrawExportListEntry
    : ManagerRenderCompProperties<CompDrawExportListEntry, DrawExportListEntryWorker> { }

/// <summary>
/// Abstract base class for workers that handle drawing export list entries.
/// </summary>
public abstract class DrawExportListEntryWorker
{
    /// <summary>
    /// Draws an export list entry for the specified job at the given position and width.
    /// </summary>
    /// <param name="job">The manager job to draw the export list entry for.</param>
    /// <param name="position">The position to start drawing at. This should be updated by the method to point at the end of the drawn entry.</param>
    /// <param name="width">The width available for drawing the entry.</param>
    public abstract void DrawExportListEntry(ManagerJob job, ref Vector2 position, float width);
}

/// <summary>
/// Generic abstract base class for workers that handle drawing export list entries for a specific ManagerJob type.
/// </summary>
/// <typeparam name="T">The type of ManagerJob this worker handles.</typeparam>
public abstract class DrawExportListEntryWorker<T> : DrawExportListEntryWorker
    where T : ManagerJob
{
    /// <inheritdoc/>
    public sealed override void DrawExportListEntry(
        ManagerJob job,
        ref Vector2 position,
        float width
    ) => DrawExportListEntry((T)job, ref position, width);

    /// <summary>
    /// Draws an export list entry for the specified job of type <typeparamref name="T"/> at the given position and width.
    /// </summary>
    /// <param name="job">The manager job of type <typeparamref name="T"/> to draw the export list entry for.</param>
    /// <param name="position">The position to start drawing at. This should be updated by the method to point at the end of the drawn entry.</param>
    /// <param name="width">The width available for drawing the entry.</param>
    public abstract void DrawExportListEntry(T job, ref Vector2 position, float width);
}
