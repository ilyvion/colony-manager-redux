// ManagerJobSettings_Forestry.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[HotSwappable]
internal sealed class ManagerJobSettings_Forestry : ManagerJobSettings
{
    public ManagerJob_Forestry.ForestryJobType DefaultForestryJobType = ManagerJob_Forestry.ForestryJobType.Logging;
    public bool DefaultAllowSaplings;

    public override string Label => "ColonyManagerRedux.Forestry.Forestry".Translate();

    public override void DoPanelContents(Rect rect)
    {
        var panelRect = new Rect(
            rect.xMin,
            rect.yMin,
            rect.width,
            rect.height - Margin);

        Widgets_Section.BeginSectionColumn(panelRect, "Forestry.Settings", out Vector2 position, out float width);
        Widgets_Section.Section(ref position, width, DrawJobType, "ColonyManagerRedux.ForestryJobSettings.DefaultJobType".Translate());
        Widgets_Section.Section(ref position, width, DrawAllowSaplings);
        Widgets_Section.EndSectionColumn("Forestry.Settings", position);
    }

    public float DrawJobType(Vector2 pos, float width)
    {
        // type of job;
        // clear clear area | logging
        var types =
            (ManagerJob_Forestry.ForestryJobType[])
            Enum.GetValues(typeof(ManagerJob_Forestry.ForestryJobType));


        var cellWidth = width / types.Length;

        var cellRect = new Rect(
            pos.x,
            pos.y,
            cellWidth,
            ListEntryHeight);

        foreach (var type in types)
        {
            Utilities.DrawToggle(
                cellRect,
                $"ColonyManagerRedux.Forestry.JobType.{type}".Translate(),
                $"ColonyManagerRedux.Forestry.JobType.{type}.Tip".Translate(),
                DefaultForestryJobType == type,
                () => DefaultForestryJobType = type,
                () => { },
                wrap: false);
            cellRect.x += cellWidth;
        }

        return ListEntryHeight;
    }

    public float DrawAllowSaplings(Vector2 pos, float width)
    {
        var rowRect = new Rect(
            pos.x,
            pos.y,
            width,
            ListEntryHeight);

        // NOTE: AllowSaplings logic is the reverse from the label that is shown to the user.
        Utilities.DrawToggle(
            rowRect,
            "ColonyManagerRedux.Forestry.AllowSaplings".Translate(),
            "ColonyManagerRedux.Forestry.AllowSaplings.Tip".Translate(),
            !DefaultAllowSaplings,
            () => DefaultAllowSaplings = false,
            () => DefaultAllowSaplings = true);
        return ListEntryHeight;
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref DefaultForestryJobType, "defaultForestryJobType", ManagerJob_Forestry.ForestryJobType.Logging);
        Scribe_Values.Look(ref DefaultAllowSaplings, "defaultAllowSaplings", false);
    }
}
