// ManagerComp.cs
// Copyright (c) 2024 Alexander Krivács Schrøder


namespace ColonyManagerRedux;

public abstract class ManagerComp
{
#pragma warning disable CS8618 // Set by ManagerJob.Initialize on creation/scribing
    private Manager _manager;
    public Manager Manager { get => _manager; internal set => _manager = value; }

    private ManagerCompProperties _props;
    public ManagerCompProperties Props { get => _props; set => _props = value; }
#pragma warning restore CS8618

    internal void InitializeInt(ManagerCompProperties props)
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

    public virtual void CompUpdate()
    {
    }

    public virtual void PostExposeData()
    {
    }

    public override string ToString()
    {
        return string.Concat(GetType().Name, "(parent=", _manager, ")");
    }
}
