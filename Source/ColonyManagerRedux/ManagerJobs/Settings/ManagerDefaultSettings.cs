// ManagerDefaultSettings.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Base class for a manager's default job settings, rendered in the Job Defaults tab's
/// right-hand panel for the selected job type.
/// </summary>
[HotSwappable]
public abstract class ManagerDefaultSettings : IExposable, IManagerDefOwnedSettings
{
#pragma warning disable CS8618 // Set by ManagerDefMaker
    private ManagerDef def;

    /// <summary>
    /// Gets the manager definition associated with these default settings.
    /// </summary>
    public ManagerDef Def
    {
        get => def;
        internal set => def = value;
    }
#pragma warning restore CS8618

    /// <summary>
    /// Called after the default settings object is created.
    /// </summary>
    public virtual void PostMake() { }

    /// <summary>
    /// Copies values out of <paramref name="legacy"/>, the old per-job
    /// <see cref="ManagerSettings"/> instance that held this job's default-value settings before
    /// they moved to a dedicated <see cref="ManagerDefaultSettings"/> subclass, so a save written
    /// by an older version of the mod doesn't lose them. Returns whether <paramref name="legacy"/>
    /// was of the expected legacy type for this settings class.
    /// </summary>
    /// <param name="legacy">The deserialized legacy settings instance to migrate from.</param>
    public virtual bool MigrateFrom(ManagerSettings legacy) => false;

    /// <inheritdoc/>
    public virtual void ExposeData() => Scribe_Defs.Look(ref def, "def");

    /// <summary>
    /// Draws the default settings for this manager's job type.
    /// </summary>
    /// <param name="inRect">The rectangle in which to draw.</param>
    public abstract void DoTabContents(Rect inRect);
}
