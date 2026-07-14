// ManagerRenderComp.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Abstract base class for manager render components in Colony Manager Redux.
/// </summary>
/// <typeparam name="TProps">The type of properties for this render component.</typeparam>
public abstract class ManagerRenderComp<TProps> : ManagerJobComp
    where TProps : ManagerJobCompProperties
{
    /// <summary>
    /// Gets the strongly-typed properties for this render component.
    /// </summary>
    public new TProps Props => (TProps)base.Props;
}

/// <summary>
/// Abstract base class for manager render component properties in Colony Manager Redux.
/// </summary>
/// <typeparam name="TComp">The type of the component.</typeparam>
/// <typeparam name="TWorker">The type of the worker.</typeparam>
public abstract class ManagerRenderCompProperties<TComp, TWorker> : ManagerJobCompProperties
    where TComp : ManagerJobComp
{
#pragma warning disable CA1051 // CompProperties use public fields
    /// <summary>
    /// The type of the worker class associated with this render component.
    /// </summary>
    public Type workerClass = typeof(TWorker);

    /// <summary>
    /// Indicates whether this component takes over rendering.
    /// </summary>
    public bool takeOverRendering;
#pragma warning restore CA1051

    /// <summary>
    /// Initializes a new instance of the <see cref="ManagerRenderCompProperties{TComp, TWorker}"/> class.
    /// </summary>
    protected ManagerRenderCompProperties()
    {
        compClass = typeof(TComp);
    }

    /// <summary>
    /// Gets the instance of the worker associated with this render component.
    /// </summary>
    public TWorker Worker => field ??= (TWorker)Activator.CreateInstance(workerClass);

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
        if (!typeof(TWorker).IsAssignableFrom(workerClass))
        {
            yield return $"{nameof(workerClass)} is not a subclass of {typeof(TWorker).Name}";
        }
    }
}
