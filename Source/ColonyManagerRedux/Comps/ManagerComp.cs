// ManagerComp.cs
// Copyright (c) 2024 Alexander Krivács Schrøder



namespace ColonyManagerRedux;

/// <summary>
/// Abstract base class for manager components, providing lifecycle hooks and references to parent objects.
/// </summary>
public abstract class ManagerComp
{
#pragma warning disable CS8618 // Set by ManagerJob.Initialize on creation/scribing
    /// <summary>
    /// Gets the parent <see cref="Manager"/> for this component.
    /// </summary>
    public Manager Manager
    {
        get; internal set;
    }

    /// <summary>
    /// Gets or sets the properties for this manager component.
    /// </summary>
    public ManagerCompProperties Props
    {
        get; set;
    }
#pragma warning restore CS8618

    /// <summary>
    /// Initializes this component with the specified properties.
    /// </summary>
    /// <param name="props">The properties to initialize with.</param>
    internal void InitializeInt(ManagerCompProperties props)
    {
        Props = props;
        Initialize();
    }

    /// <summary>
    /// Called when the component is initialized. Override to provide custom initialization logic.
    /// </summary>
    public virtual void Initialize()
    {
    }

    /// <summary>
    /// Called every game tick. Override to provide per-tick logic.
    /// </summary>
    public virtual void CompTick()
    {
    }

    /// <summary>
    /// Called every update cycle. Override to provide per-update logic.
    /// </summary>
    public virtual void CompUpdate()
    {
    }

    /// <summary>
    /// Called to expose data for saving/loading. Override to provide custom serialization logic.
    /// </summary>
    public virtual void PostExposeData()
    {
    }

    /// <summary>
    /// Called to finalize initialization of the comp (called from MapComponent).
    /// </summary>
    protected internal virtual void FinalizeInit()
    {
    }

    /// <inheritdoc/>
    public override string ToString() => string.Concat(GetType().Name, "(parent=", Manager, ")");
}
