// ManagerSettings_Hunting.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerSettings_Hunting : ManagerSettings
{
    public bool DefaultSyncFilterAndAllowed = true;
    public ManagerJob_Hunting.HuntingTargetResource DefaultTargetResource =
        ManagerJob_Hunting.HuntingTargetResource.Meat;
    public bool DefaultAllowHumanLikeMeat;
    public bool DefaultAllowInsectMeat;
    public bool DefaultUnforbidCorpses = true;

    public override void DoTabContents(Rect rect)
    {
        var panelRect = new Rect(
            rect.xMin,
            rect.yMin,
            rect.width,
            rect.height - Margin);

        Widgets_Section.BeginSectionColumn(
            panelRect, "Hunting.Settings", out Vector2 position, out float width);
        Widgets_Section.Section(
            ref position,
            width,
            DrawTargetResource,
            "ColonyManagerRedux.Hunting.ManagerSettings.DefaultTargetResource".Translate());
        Widgets_Section.Section(
            ref position,
            width,
            DrawSyncFilterAndAllowed,
            "ColonyManagerRedux.ManagerSettings.DefaultThresholdSettings".Translate());
        Widgets_Section.Section(
            ref position,
            width,
            DrawAllowWeirdMeat,
            "ColonyManagerRedux.ManagerSettings.DefaultThresholdSettings".Translate());
        Widgets_Section.Section(ref position, width, DrawUnforbidCorpses);
        Widgets_Section.EndSectionColumn("Hunting.Settings", position);
    }

    public float DrawSyncFilterAndAllowed(Vector2 pos, float width)
    {
        var rowRect = new Rect(
            pos.x,
            pos.y,
            width,
            ListEntryHeight);

        Utilities.DrawToggle(
            rowRect,
            "ColonyManagerRedux.SyncFilterAndAllowed".Translate(),
            "ColonyManagerRedux.Hunting.SyncFilterAndAllowed.Tip".Translate(),
            ref DefaultSyncFilterAndAllowed);

        return ListEntryHeight;
    }

    public float DrawTargetResource(Vector2 pos, float width)
    {
        var targetResource =
            (ManagerJob_Hunting.HuntingTargetResource[])
            Enum.GetValues(typeof(ManagerJob_Hunting.HuntingTargetResource));

        var cellWidth = width / targetResource.Length;

        var cellRect = new Rect(
            pos.x,
            pos.y,
            cellWidth,
            ListEntryHeight);

        foreach (var type in targetResource)
        {
            Utilities.DrawToggle(
                cellRect,
                $"ColonyManagerRedux.Hunting.TargetResource.{type}".Translate(),
                $"ColonyManagerRedux.Hunting.TargetResource.{type}.Tip".Translate(),
                DefaultTargetResource == type,
                () => DefaultTargetResource = type,
                () => { },
                wrap: false);
            cellRect.x += cellWidth;
        }

        return ListEntryHeight;
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

        Scribe_Values.Look(ref DefaultSyncFilterAndAllowed, "defaultSyncFilterAndAllowed", true);
        Scribe_Values.Look(ref DefaultTargetResource, "defaultTargetResource",
            ManagerJob_Hunting.HuntingTargetResource.Meat);
        Scribe_Values.Look(ref DefaultAllowHumanLikeMeat, "defaultAllowHumanLikeMeat", false);
        Scribe_Values.Look(ref DefaultAllowInsectMeat, "defaultAllowInsectMeat", false);
        Scribe_Values.Look(ref DefaultUnforbidCorpses, "defaultUnforbidCorpses", true);
    }
}
