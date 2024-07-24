// TabRecordWithTip.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public class TabRecordWithTip : TabRecord
{
    private readonly string _tooltip;

    public TabRecordWithTip(string label, string tooltip, Action clickedAction, bool selected) : base(label, clickedAction, selected)
    {
        _tooltip = tooltip;
    }

    public override string GetTip()
    {
        return _tooltip;
    }
}
