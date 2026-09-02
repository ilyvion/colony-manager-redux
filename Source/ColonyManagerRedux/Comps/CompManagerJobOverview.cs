// CompManagerJobOverview.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Component that stores Overview-tab-specific state for a manager job, such as its
/// player-assigned manual group.
/// </summary>
public class CompManagerJobOverview : ManagerJobComp
{
    private string? _manualGroup;

    /// <summary>
    /// Gets or sets the name of the player-defined group this manager job has been manually
    /// assigned to, for organizing the overview list. <c>null</c> if not assigned to any group.
    /// </summary>
    public string? ManualGroup
    {
        get => _manualGroup;
        set => _manualGroup = value.NullOrEmpty() ? null : value;
    }

    /// <inheritdoc/>
    protected internal override void PostExposeData()
    {
        base.PostExposeData();
        if (Parent.Manager.ScribeSameGameData)
        {
            Scribe_Values.Look(ref _manualGroup, "manualGroup");
        }
    }
}
