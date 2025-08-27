// ManagerSettings.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.UI;

namespace ColonyManagerRedux;

/// <summary>
/// Base class for manager job settings tabs, providing serialization and UI logic.
/// </summary>
[HotSwappable]
public abstract class ManagerSettings : Tab, IExposable
{
#pragma warning disable CS8618 // Set by ManagerDefMaker
    private ManagerDef def;

    /// <summary>
    /// Gets the manager definition associated with these settings.
    /// </summary>
    public ManagerDef Def
    {
        get => def;
        internal set => def = value;
    }
#pragma warning restore CS8618

    /// <summary>
    /// Gets the label for this settings tab.
    /// </summary>
    public virtual string Label => def.label.CapitalizeFirst();

    /// <summary>
    /// Called after the settings object is created.
    /// </summary>
    public virtual void PostMake() { }

    /// <inheritdoc/>
    public virtual void ExposeData() => Scribe_Defs.Look(ref def, "def");

    /// <summary>
    /// (Obsolete) Draws the panel contents for this settings tab. Use <see cref="DoTabContents"/> instead.
    /// </summary>
    /// <param name="rect">The rectangle in which to draw.</param>
    [Obsolete(
        "Move to overriding DoTabContents instead; this method will be removed in a future version"
    )]
    public virtual void DoPanelContents(Rect rect) { }

    /// <summary>
    /// Draws the tab contents for this settings tab.
    /// </summary>
    /// <param name="inRect">The rectangle in which to draw.</param>
    public override void DoTabContents(Rect inRect) =>
#pragma warning disable CS0618
        DoPanelContents(inRect);
#pragma warning restore CS0618

    /// <summary>
    /// Gets the title for this settings tab.
    /// </summary>
    public override string Title => Label;

    // We need to seal this so subclasses can't "override" the DisabledManagers setting.
    /// <summary>
    /// Gets whether this settings tab should be shown (sealed to prevent override).
    /// </summary>
    public sealed override bool Show =>
        !ColonyManagerReduxMod.Settings.DisabledManagers.Contains(def) && ShowSettingTab;

    // We instead offer this one as a replacement
    /// <summary>
    /// Gets whether this settings tab should be shown (can be overridden by subclasses).
    /// </summary>
    public virtual bool ShowSettingTab => true;
}
