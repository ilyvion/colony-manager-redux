// ManagerSettings_Mining.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerSettings_Mining : ManagerSettings
{
    public bool DefaultSyncFilterAndAllowed = true;

    public bool DefaultDeconstructBuildings;
    public bool DefaultDeconstructAncientDangerWhenFogged;

    public bool DefaultTakeOwnershipOfMiningJobs;
    public bool DefaultHaulMapChunks = true;
    public bool DefaultHaulMinedChunks = true;

    public bool DefaultMineThickRoofs = true;
    public bool DefaultCheckRoofSupport = true;
    public bool DefaultCheckRoofSupportAdvanced;
    public bool DefaultCheckRoomDivision = true;

    public override void DoTabContents(Rect rect)
    {
        var panelRect = new Rect(
            rect.xMin,
            rect.yMin,
            rect.width,
            rect.height - Margin);

        Widgets_Section.BeginSectionColumn(panelRect, "Mining.Settings", out Vector2 position, out float width);
        Widgets_Section.Section(ref position, width, DrawSyncFilterAndAllowed, "ColonyManagerRedux.ManagerSettings.DefaultThresholdSettings".Translate());
        Widgets_Section.Section(ref position, width, DrawMining, "ColonyManagerRedux.Mining.ManagerSettings.DefaultMining".Translate());
        Widgets_Section.Section(ref position, width, DrawHaulChunks, "ColonyManagerRedux.Mining.ManagerSettings.DefaultChunks".Translate());
        Widgets_Section.Section(ref position, width, DrawDeconstructBuildings);
        Widgets_Section.Section(ref position, width, DrawRoofRoomChecks, "ColonyManagerRedux.Mining.ManagerSettings.DefaultHealthAndSafety".Translate());
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
            "ColonyManagerRedux.SyncFilterAndAllowed".Translate(),
            "ColonyManagerRedux.Mining.SyncFilterAndAllowed.Tip".Translate(),
            ref DefaultSyncFilterAndAllowed);

        return ListEntryHeight;
    }

    public float DrawMining(Vector2 pos, float width)
    {
        var start = pos;

        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.Mining.TakeOwnershipOfMiningJobs".Translate(),
            "ColonyManagerRedux.Mining.TakeOwnershipOfMiningJobs.Tip".Translate(),
            ref DefaultTakeOwnershipOfMiningJobs);

        return rowRect.yMax - pos.y;
    }

    public float DrawHaulChunks(Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.Mining.HaulMapChunks".Translate(),
            "ColonyManagerRedux.Mining.HaulMapChunks.Tip".Translate(),
            ref DefaultHaulMapChunks);

        rowRect.y += ListEntryHeight;
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.Mining.HaulMinedChunks".Translate(),
            "ColonyManagerRedux.Mining.HaulMinedChunks.Tip".Translate(),
            ref DefaultHaulMinedChunks);

        return rowRect.yMax - pos.y;
    }

    public float DrawDeconstructBuildings(Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.Mining.DeconstructBuildings".Translate(),
            "ColonyManagerRedux.Mining.DeconstructBuildings.Tip".Translate(),
            ref DefaultDeconstructBuildings);

        rowRect.y += ListEntryHeight;
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.Mining.DeconstructAncientDangerWhenFogged".Translate(),
            "ColonyManagerRedux.Mining.DeconstructAncientDangerWhenFogged.Tip".Translate(),
            ref DefaultDeconstructAncientDangerWhenFogged);

        return rowRect.yMax - pos.y;
    }

    public float DrawRoofRoomChecks(Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.Mining.MineThickRoofs".Translate(),
            "ColonyManagerRedux.Mining.MineThickRoofs.Tip".Translate(),
            ref DefaultMineThickRoofs);

        rowRect.y += ListEntryHeight;
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.Mining.CheckRoofSupport".Translate(),
            "ColonyManagerRedux.Mining.CheckRoofSupport.Tip".Translate(),
            ref DefaultCheckRoofSupport);

        rowRect.y += ListEntryHeight;
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.Mining.CheckRoofSupportAdvanced".Translate(),
            "ColonyManagerRedux.Mining.CheckRoofSupportAdvanced.Tip".Translate(),
            ref DefaultCheckRoofSupportAdvanced, true);

        rowRect.y += ListEntryHeight;
        Utilities.DrawToggle(rowRect,
            "ColonyManagerRedux.Mining.CheckRoomDivision".Translate(),
            "ColonyManagerRedux.Mining.CheckRoomDivision.Tip".Translate(),
            ref DefaultCheckRoomDivision, true);

        return rowRect.yMax - pos.y;
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref DefaultSyncFilterAndAllowed, "defaultSyncFilterAndAllowed", true);

        Scribe_Values.Look(ref DefaultDeconstructBuildings, "defaultDeconstructBuildings", false);
        Scribe_Values.Look(
            ref DefaultDeconstructAncientDangerWhenFogged,
            "defaultDeconstructAncientDangerWhenFogged",
            false);

        Scribe_Values.Look(
            ref DefaultTakeOwnershipOfMiningJobs, "defaultTakeOwnershipOfMiningJobs", false);
        Scribe_Values.Look(ref DefaultHaulMapChunks, "defaultHaulMapChunks", true);
        Scribe_Values.Look(ref DefaultHaulMinedChunks, "defaultHaulMinedChunks", true);

        Scribe_Values.Look(ref DefaultMineThickRoofs, "defaultMineThickRoofs", true);
        Scribe_Values.Look(ref DefaultCheckRoofSupport, "defaultCheckRoofSupport", true);
        Scribe_Values.Look(
            ref DefaultCheckRoofSupportAdvanced, "defaultCheckRoofSupportAdvanced", false);
        Scribe_Values.Look(ref DefaultCheckRoomDivision, "defaultCheckRoomDivision", true);
    }
}
