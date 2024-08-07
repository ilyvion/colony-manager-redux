// ManagerJobSettings_Hunting.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[HotSwappable]
internal sealed class ManagerJobSettings_Hunting : ManagerJobSettings
{
    public bool DefaultAllowHumanLikeMeat;
    public bool DefaultAllowInsectMeat;
    public bool DefaultUnforbidCorpses = true;

    public override void DoPanelContents(Rect rect)
    {
        var panelRect = new Rect(
            rect.xMin,
            rect.yMin,
            rect.width,
            rect.height - Margin);

        Widgets_Section.BeginSectionColumn(panelRect, "Hunting.Settings", out Vector2 position, out float width);
        Widgets_Section.Section(ref position, width, DrawAllowWeirdMeat, "ColonyManagerRedux.ManagerJobSettings.DefaultThresholdSettings".Translate());
        Widgets_Section.Section(ref position, width, DrawUnforbidCorpses);
        Widgets_Section.EndSectionColumn("Hunting.Settings", position);
    }

    public float DrawAllowWeirdMeat(Vector2 pos, float width)
    {
        var start = pos;
        Utilities.DrawToggle(ref pos, width,
            "ColonyManagerRedux.Hunting.AllowHumanMeat".Translate(),
            "ColonyManagerRedux.Hunting.AllowHumanMeat.Tip".Translate(),
            ref DefaultAllowHumanLikeMeat);
        Utilities.DrawToggle(ref pos, width,
            "ColonyManagerRedux.Hunting.AllowInsectMeat".Translate(),
            "ColonyManagerRedux.Hunting.AllowInsectMeat.Tip".Translate(),
            ref DefaultAllowInsectMeat);

        return pos.y - start.y;
    }

    public float DrawUnforbidCorpses(Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.Hunting.UnforbidCorpses".Translate(),
            "ColonyManagerRedux.Hunting.UnforbidCorpses.Tip".Translate(),
            ref DefaultUnforbidCorpses);
        return ListEntryHeight;
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref DefaultAllowHumanLikeMeat, "defaultAllowHumanLikeMeat", false);
        Scribe_Values.Look(ref DefaultAllowInsectMeat, "defaultAllowInsectMeat", false);
        Scribe_Values.Look(ref DefaultUnforbidCorpses, "defaultUnforbidCorpses", true);
    }
}
