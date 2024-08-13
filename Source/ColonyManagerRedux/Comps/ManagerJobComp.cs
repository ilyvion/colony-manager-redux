// ManagerJobComp.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public abstract class ManagerJobComp
{
#pragma warning disable CS8618 // Set by ManagerJob.Initialize on creation/scribing

    private ManagerJob _parent;
    public ManagerJob Parent { get => _parent; internal set => _parent = value; }

    private ManagerJobCompProperties _props;
    public ManagerJobCompProperties Props { get => _props; set => _props = value; }

#pragma warning restore CS8618

    internal void InitializeInt(ManagerJobCompProperties props)
    {
        _props = props;
        Initialize();
    }
    public virtual void Initialize()
    {
    }

    public virtual void CompTick()
    {
    }

    public virtual void PostExposeData()
    {
    }

    public override string ToString()
    {
        return string.Concat(GetType().Name, "(parent=", _parent, ")");
    }
}
