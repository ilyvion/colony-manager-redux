// ManagerJobComp.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public abstract class ManagerJobComp
{
#pragma warning disable CS8618 // Set by ManagerJob.Initialize on creation/scribing
    public ManagerJob parent;
    public ManagerJobCompProperties props;
#pragma warning restore CS8618

    public virtual void Initialize(ManagerJobCompProperties props)
    {
        this.props = props;
    }

    public override string ToString()
    {
        return string.Concat(GetType().Name, "(parent=", parent, ")");
    }

    public virtual void CompTick()
    {
    }

    public virtual void PostExposeData()
    {
    }
}
