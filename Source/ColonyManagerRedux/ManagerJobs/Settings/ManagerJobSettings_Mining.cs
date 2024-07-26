// ManagerJobSettings_Mining.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[HotSwappable]
internal sealed class ManagerJobSettings_Mining : ManagerJobSettings
{
    public bool DefaultSyncFilterAndAllowed = true;

    public bool DefaultDeconstructBuildings;
    public bool DefaultHaulChunks = true;

    public bool DefaultCheckRoofSupport = true;
    public bool DefaultCheckRoofSupportAdvanced;
    public bool DefaultCheckRoomDivision = true;

    public override string Label => "ColonyManagerRedux.ManagerMining".Translate();

    public override void DoPanelContents(Rect rect)
    {
        var panelRect = new Rect(
            rect.xMin,
            rect.yMin,
            rect.width,
            rect.height - Margin);

        Widgets_Section.BeginSectionColumn(panelRect, "Mining.Settings", out Vector2 position, out float width);
        Widgets_Section.Section(ref position, width, DrawSyncFilterAndAllowed, "ColonyManagerRedux.ManagerJobSettings.DefaultThresholdSettings".Translate());
        Widgets_Section.Section(ref position, width, DrawHaulChunks);
        Widgets_Section.Section(ref position, width, DrawDeconstructBuildings);
        Widgets_Section.Section(ref position, width, DrawRoofRoomChecks, "ColonyManagerRedux.MiningJobSettings.DefaultHealthAndSafety".Translate());
        Widgets_Section.EndSectionColumn("Mining.Settings", position);
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
            "ColonyManagerRedux.ManagerForaging.SyncFilterAndAllowed".Translate(),
            "ColonyManagerRedux.ManagerForaging.SyncFilterAndAllowed.Tip".Translate(),
            ref DefaultSyncFilterAndAllowed);

        return ListEntryHeight;
    }

    public float DrawHaulChunks(Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(rowRect,
                              "ColonyManagerRedux.ManagerMining.HaulChunks".Translate(),
                              "ColonyManagerRedux.ManagerMining.HaulChunks.Tip".Translate(),
                              ref DefaultHaulChunks);
        return ListEntryHeight;
    }

    public float DrawDeconstructBuildings(Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(rowRect,
                              "ColonyManagerRedux.ManagerMining.DeconstructBuildings".Translate(),
                              "ColonyManagerRedux.ManagerMining.DeconstructBuildings.Tip".Translate(),
                              ref DefaultDeconstructBuildings);
        return ListEntryHeight;
    }

    public float DrawRoofRoomChecks(Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.ManagerMining.CheckRoofSupport".Translate(),
            "ColonyManagerRedux.ManagerMining.CheckRoofSupport.Tip".Translate(),
            ref DefaultCheckRoofSupport);

        rowRect.y += ListEntryHeight;


        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.ManagerMining.CheckRoofSupportAdvanced".Translate(),
            "ColonyManagerRedux.ManagerMining.CheckRoofSupportAdvanced.Tip".Translate(),
            ref DefaultCheckRoofSupportAdvanced, true);


        rowRect.y += ListEntryHeight;
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.ManagerMining.CheckRoomDivision".Translate(),
            "ColonyManagerRedux.ManagerMining.CheckRoomDivision.Tip".Translate(),
            ref DefaultCheckRoomDivision, true);

        return rowRect.yMax - pos.y;
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref DefaultSyncFilterAndAllowed, "defaultSyncFilterAndAllowed", true);

        Scribe_Values.Look(ref DefaultDeconstructBuildings, "defaultDeconstructBuildings", false);

        Scribe_Values.Look(ref DefaultCheckRoofSupport, "defaultCheckRoofSupport", true);
        Scribe_Values.Look(ref DefaultCheckRoofSupportAdvanced, "defaultCheckRoofSupportAdvanced", false);
        Scribe_Values.Look(ref DefaultCheckRoomDivision, "defaultCheckRoomDivision", true);
    }
}
