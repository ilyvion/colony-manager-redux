// TabRecordWithTip.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Represents a tab record with an associated tooltip.
/// </summary>
/// <param name="label">The label of the tab.</param>
/// <param name="tooltip">The tooltip to display for the tab.</param>
/// <param name="clickedAction">The action to perform when the tab is clicked.</param>
/// <param name="selected">Whether the tab is selected.</param>
public class TabRecordWithTip(string label, string tooltip, Action clickedAction, bool selected) : TabRecord(label, clickedAction, selected)
{
    private readonly string _tooltip = tooltip;

    /// <inheritdoc/>
    public override string GetTip() => _tooltip;
}
