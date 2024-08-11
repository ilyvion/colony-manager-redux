// ManagerRenderComp.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public abstract class ManagerRenderComp<TProps> : ManagerJobComp
    where TProps : ManagerJobCompProperties
{
    public new TProps Props => (TProps)base.Props;
}

public abstract class ManagerRenderCompProperties<TComp, TWorker> : ManagerJobCompProperties
    where TComp : ManagerJobComp
{
#pragma warning disable CA1051 // CompProperties use public fields
    public Type workerClass = typeof(TWorker);
    public bool takeOverRendering;
#pragma warning restore CA1051

    protected ManagerRenderCompProperties()
    {
        compClass = typeof(TComp);
    }

    private TWorker? workerInt;
    public TWorker Worker
    {
        get
        {
            workerInt ??= (TWorker)Activator.CreateInstance(workerClass);
            return workerInt;
        }
    }

    public override IEnumerable<string> ConfigErrors(ManagerDef parentDef)
    {
        if (parentDef == null)
        {
            throw new ArgumentNullException(nameof(parentDef));
        }

        foreach (string item in base.ConfigErrors(parentDef))
        {
            yield return item;
        }

        if (workerClass == null)
        {
            yield return $"{nameof(workerClass)} is null";
        }
        if (!typeof(TWorker).IsAssignableFrom(workerClass))
        {
            yield return
                $"{nameof(workerClass)} is not a subclass of {typeof(TWorker).Name}";
        }
    }
}
