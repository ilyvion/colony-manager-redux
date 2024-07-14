// Dialog_MiningDebugOptions.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ColonyManagerRedux;

[HotSwappable]
public class Dialog_MiningDebugOptions(ManagerJob_Mining job) : Dialog_DebugOptionLister
{
    private readonly ManagerJob_Mining job = job;

    protected override void DoListingItems(Rect inRect, float columnWidth)
    {

        DebugToolMap("IsValidMiningTarget", columnWidth, delegate
        {
            foreach (var thing in Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()).OfType<Mineable>())
            {
                Messages.Message(job.IsValidMiningTarget(thing).ToString(), MessageTypeDefOf.SilentInput);
            }
        }, false);


        DebugToolMap("IsValidDeconstructionTarget", columnWidth, delegate
        {
            foreach (var thing in Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()).OfType<Building>())
            {
                Messages.Message(job.IsValidDeconstructionTarget(thing).ToString(),
                                  MessageTypeDefOf.SilentInput);
            }
        }, false);

        DebugToolMap("Faction", columnWidth, delegate
        {
            foreach (var thing in Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()).OfType<Building>())
            {
                Messages.Message(thing.Faction.ToStringSafe(), MessageTypeDefOf.SilentInput);
            }
        }, false);

        DebugToolMap("AllowedBuilding", columnWidth, delegate
        {
            foreach (var thing in Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()).OfType<Building>())
            {
                Messages.Message(job.AllowedBuilding(thing.def).ToString(), MessageTypeDefOf.SilentInput);
            }
        }, false);


        DebugToolMap("AllowedMineral", columnWidth, delegate
        {
            foreach (var thing in Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()).OfType<Mineable>())
            {
                Messages.Message(job.AllowedMineral(thing.def).ToString(), MessageTypeDefOf.SilentInput);
            }
        }, false);


        DebugToolMap("IsRelevantDeconstructionTarget", columnWidth, delegate
        {
            foreach (var thing in Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()).OfType<Building>())
            {
                Messages.Message(job.IsRelevantDeconstructionTarget(thing).ToString(),
                                  MessageTypeDefOf.SilentInput);
            }
        }, false);

        DebugToolMap("IsRelevantMiningTarget", columnWidth, delegate
        {
            foreach (var thing in Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()).OfType<Mineable>())
            {
                Messages.Message(job.IsRelevantMiningTarget(thing).ToString(), MessageTypeDefOf.SilentInput);
            }
        }, false);

        DebugToolMap("IsInAllowedArea", columnWidth, delegate
        {
            foreach (var thing in Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()).OfType<Building>())
            {
                Messages.Message(job.IsInAllowedArea(thing).ToString(), MessageTypeDefOf.SilentInput);
            }
        }, false);

        DebugToolMap("IsReachable", columnWidth, delegate
        {
            foreach (var thing in Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()).OfType<Building>())
            {
                Messages.Message(job.IsReachable(thing).ToString(), MessageTypeDefOf.SilentInput);
            }
        }, false);

        DebugToolMap("IsRoomDivider", columnWidth, delegate
        {
            foreach (var thing in Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()).OfType<Building>())
            {
                Messages.Message(job.IsARoomDivider(thing).ToString(), MessageTypeDefOf.SilentInput);
            }
        }, false);

        DebugToolMap("IsRoofSupport: basic", columnWidth, delegate
        {
            foreach (var thing in Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()).OfType<Building>())
            {
                Messages.Message(job.IsARoofSupport_Basic(thing).ToString(), MessageTypeDefOf.SilentInput);
            }
        }, false); ;

        DebugToolMap("IsRoofSupport: advanced", columnWidth, delegate
        {
            foreach (var thing in Find.CurrentMap.thingGrid.ThingsAt(UI.MouseCell()).OfType<Building>())
            {
                Messages.Message(job.IsARoofSupport_Advanced(thing).ToString(), MessageTypeDefOf.SilentInput);
            }
        }, false);

        DebugAction("DrawSupportGrid", columnWidth, delegate
        {
            foreach (var cell in job.Manager.map.AllCells)
            {
                if (job.IsARoofSupport_Basic(cell))
                {
                    job.Manager.map.debugDrawer.FlashCell(cell, DebugSolidColorMats.MaterialOf(Color.green));
                }
            }
        }, false);

        DebugAction("GetBaseCenter", columnWidth, delegate
        {
            var cell = Utilities.GetBaseCenter(job.Manager);
            job.Manager.map.debugDrawer.FlashCell(cell, DebugSolidColorMats.MaterialOf(Color.blue));
        }, false);

        DebugToolMap("DrawPath", columnWidth, delegate
        {
            var source = Utilities.GetBaseCenter(job.Manager);
            var target = UI.MouseCell();

            job.Manager.debugComponent.SetPath(source, target);
        }, false
        );

        base.DoListingItems(inRect, columnWidth);
    }
}
