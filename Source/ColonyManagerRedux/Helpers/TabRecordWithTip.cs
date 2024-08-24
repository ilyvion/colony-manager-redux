// TabRecordWithTip.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public class TabRecordWithTip(string label, string tooltip, Action clickedAction, bool selected) : TabRecord(label, clickedAction, selected)
{
    private readonly string _tooltip = tooltip;

    public override string GetTip()
    {
        return _tooltip;
    }
}
