// ManagerJobSettings.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HotSwappable]
public abstract class ManagerJobSettings : IExposable
{
#pragma warning disable CS8618 // Set by ManagerDefMaker
    private ManagerDef def;
    public ManagerDef Def { get => def; internal set => def = value; }
#pragma warning restore CS8618

    public virtual string Label => def.label.CapitalizeFirst();

    public virtual void PostMake()
    {
    }

    public virtual void ExposeData()
    {
        Scribe_Defs.Look(ref def, "def");
    }

    public abstract void DoPanelContents(Rect rect);
}
