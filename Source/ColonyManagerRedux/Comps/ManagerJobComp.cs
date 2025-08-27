// ManagerJobComp.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Abstract base class for manager job components in Colony Manager Redux.
/// </summary>
public abstract class ManagerJobComp
{
#pragma warning disable CS8618 // Set by ManagerJob.Initialize on creation/scribing

    /// <summary>
    /// Gets the parent <see cref="ManagerJob"/> for this component.
    /// </summary>
    public ManagerJob Parent { get; internal set; }

    /// <summary>
    /// Gets the properties for this manager job component.
    /// </summary>
    public ManagerJobCompProperties Props { get; private set; }

#pragma warning restore CS8618

    internal void InitializeInt(ManagerJobCompProperties props)
    {
        Props = props;
        Initialize();
    }

    /// <summary>
    /// Called when the component is initialized; override to perform custom initialization logic.
    /// </summary>
    protected internal virtual void Initialize() { }

    /// <summary>
    /// Called every tick to update the component; override to implement custom ticking logic.
    /// </summary>
    protected internal virtual void CompTick() { }

    /// <summary>
    /// Called to expose data for saving/loading; override to implement custom serialization logic.
    /// </summary>
    protected internal virtual void PostExposeData() { }

    /// <summary>
    /// Called before rendering a section in the UI; override to perform custom pre-render logic.
    /// </summary>
    /// <param name="sectionColumn">The column of the section being rendered.</param>
    /// <param name="section">The name of the section being rendered.</param>
    /// <param name="position">The position vector for rendering, passed by reference.</param>
    /// <param name="width">The width available for rendering.</param>
    protected internal virtual void PreRenderSection(
        string sectionColumn,
        string section,
        ref Vector2 position,
        float width
    ) { }

    /// <summary>
    /// Called after rendering a section in the UI; override to perform custom post-render logic.
    /// </summary>
    /// <param name="sectionColumn">The column of the section being rendered.</param>
    /// <param name="section">The name of the section being rendered.</param>
    /// <param name="position">The position vector for rendering, passed by reference.</param>
    /// <param name="width">The width available for rendering.</param>
    protected internal virtual void PostRenderSection(
        string sectionColumn,
        string section,
        ref Vector2 position,
        float width
    ) { }

    /// <inheritdoc/>
    public override string ToString() => string.Concat(GetType().Name, "(parent=", Parent, ")");
}
