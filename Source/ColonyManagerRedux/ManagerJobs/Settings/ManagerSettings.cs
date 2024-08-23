// ManagerSettings.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.UI;

namespace ColonyManagerRedux;

[HotSwappable]
public abstract class ManagerSettings : Tab, IExposable
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

    [Obsolete("Move to overriding DoTabContents instead; " +
        "this method will be removed in a future version")]
    public virtual void DoPanelContents(Rect rect) { }

    public override void DoTabContents(Rect inRect)
    {
#pragma warning disable CS0618
        DoPanelContents(inRect);
#pragma warning restore CS0618
    }

    public override string Title { get => Label; }

    // We need to seal this so subclasses can't "override" the DisabledManagers setting.
    public override sealed bool Show =>
        !ColonyManagerReduxMod.Settings.DisabledManagers.Contains(def) && ShowSettingTab;

    // We instead offer this one as a replacement
    public virtual bool ShowSettingTab => true;
}
