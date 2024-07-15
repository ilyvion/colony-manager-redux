// ManagerTab_Forestry.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using static ColonyManagerRedux.Constants;
using static ColonyManagerRedux.ManagerJob_Forestry;
using static ColonyManagerRedux.Widgets_Labels;

namespace ColonyManagerRedux;

internal class ManagerTab_Forestry : ManagerTab
{
    private List<ManagerJob_Forestry> _jobs = [];
    private float _leftRowHeight = 9999f;
    private Vector2 _scrollPosition = Vector2.zero;

    public ManagerTab_Forestry(Manager manager) : base(manager)
    {
        SelectedForestryJob = new(manager);
    }

    public override string Label => "ColonyManagerRedux.Forestry.Forestry".Translate();

    public ManagerJob_Forestry SelectedForestryJob
    {
        get => (ManagerJob_Forestry)Selected!;
        set => Selected = value;
    }

    public static string GetTreeTooltip(ThingDef tree)
    {
        return ManagerTab_Foraging.GetPlantTooltip(tree);
    }

    public void DoContent(Rect rect)
    {
        // layout: settings | trees
        // draw background
        Widgets.DrawMenuSection(rect);


        // rects
        var optionsColumnRect = new Rect(
            rect.xMin,
            rect.yMin,
            rect.width * 3 / 5f,
            rect.height - Margin - ButtonSize.y);
        var treesColumnRect = new Rect(
            optionsColumnRect.xMax,
            rect.yMin,
            rect.width * 2 / 5f,
            rect.height - Margin - ButtonSize.y);
        var buttonRect = new Rect(
            rect.xMax - ButtonSize.x,
            rect.yMax - ButtonSize.y,
            ButtonSize.x - Margin,
            ButtonSize.y - Margin);

        Widgets_Section.BeginSectionColumn(optionsColumnRect, "Forestry.Options", out Vector2 position, out float width);
        Widgets_Section.Section(ref position, width, DrawJobType, "ColonyManagerRedux.Forestry.JobType".Translate());

        if (SelectedForestryJob.Type == ManagerJob_Forestry.ForestryJobType.ClearArea)
        {
            Widgets_Section.Section(ref position, width, DrawClearArea, "ColonyManagerRedux.Forestry.JobType.ClearArea".Translate());
        }

        if (SelectedForestryJob.Type == ManagerJob_Forestry.ForestryJobType.Logging)
        {
            Widgets_Section.Section(ref position, width, DrawThreshold, "ColonyManagerRedux.ManagerThreshold".Translate());
            Widgets_Section.Section(ref position, width, DrawAreaRestriction, "ColonyManagerRedux.Forestry.LoggingArea".Translate());
            Widgets_Section.Section(ref position, width, DrawAllowSaplings);
        }

        Widgets_Section.EndSectionColumn("Forestry.Options", position);

        Widgets_Section.BeginSectionColumn(treesColumnRect, "Forestry.Trees", out position, out width);
        Widgets_Section.Section(ref position, width, DrawTreeShortcuts, "ColonyManagerRedux.Forestry.Trees".Translate());
        Widgets_Section.Section(ref position, width, DrawTreeList);
        Widgets_Section.EndSectionColumn("Forestry.Trees", position);

        // do the button
        if (!SelectedForestryJob.IsManaged)
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.ManagerManage".Translate()))
            {
                // activate job, add it to the stack
                SelectedForestryJob.IsManaged = true;
                manager.JobStack.Add(SelectedForestryJob);

                // refresh source list
                Refresh();
            }
        }
        else
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.ManagerDelete".Translate()))
            {
                // inactivate job, remove from the stack.
                manager.JobStack.Delete(SelectedForestryJob);

                // remove content from UI
                SelectedForestryJob = new ManagerJob_Forestry(manager);

                // refresh source list
                Refresh();
            }
        }
    }

    public void DoLeftRow(Rect rect)
    {
        Widgets.DrawMenuSection(rect);

        // content
        var height = _leftRowHeight;
        var scrollView = new Rect(0f, 0f, rect.width, height);
        if (height > rect.height)
        {
            scrollView.width -= ScrollbarWidth;
        }

        Widgets.BeginScrollView(rect, ref _scrollPosition, scrollView);
        var scrollContent = scrollView;

        GUI.BeginGroup(scrollContent);
        var cur = Vector2.zero;
        var i = 0;

        foreach (var job in _jobs)
        {
            var row = new Rect(0f, cur.y, scrollContent.width, LargeListEntryHeight);
            Widgets.DrawHighlightIfMouseover(row);
            if (SelectedForestryJob == job)
            {
                Widgets.DrawHighlightSelected(row);
            }

            if (i++ % 2 == 1)
            {
                Widgets.DrawAltRect(row);
            }

            var jobRect = row;

            if (ManagerTab_Overview.DrawOrderButtons(new Rect(row.xMax - 50f, row.yMin, 50f, 50f), manager,
                                                       job))
            {
                Refresh();
            }

            jobRect.width -= 50f;

            job.DrawListEntry(jobRect, false);
            if (Widgets.ButtonInvisible(jobRect))
            {
                SelectedForestryJob = job;
            }

            cur.y += LargeListEntryHeight;
        }

        // row for new job.
        var newRect = new Rect(0f, cur.y, scrollContent.width, LargeListEntryHeight);
        Widgets.DrawHighlightIfMouseover(newRect);

        if (i % 2 == 1)
        {
            Widgets.DrawAltRect(newRect);
        }

        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(newRect, "<" + "ColonyManagerRedux.Forestry.NewForestryJob".Translate().Resolve() + ">");
        Text.Anchor = TextAnchor.UpperLeft;

        if (Widgets.ButtonInvisible(newRect))
        {
            Selected = new ManagerJob_Forestry(manager);
        }

        TooltipHandler.TipRegion(newRect, "ColonyManagerRedux.Forestry.NewForestryJobTooltip".Translate());

        cur.y += LargeListEntryHeight;

        _leftRowHeight = cur.y;
        GUI.EndGroup();
        Widgets.EndScrollView();
    }

    public override void DoWindowContents(Rect canvas)
    {
        // set up rects
        var leftRow = new Rect(0f, 0f, DefaultLeftRowSize, canvas.height);
        var contentCanvas = new Rect(leftRow.xMax + Margin, 0f, canvas.width - leftRow.width - Margin,
                                      canvas.height);

        // draw overview row
        DoLeftRow(leftRow);

        // draw job interface if something is selected.
        if (Selected != null)
        {
            DoContent(contentCanvas);
        }
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
            !SelectedForestryJob.AllowSaplings,
            () => SelectedForestryJob.AllowSaplings = false,
            () => SelectedForestryJob.AllowSaplings = true);
        return ListEntryHeight;
    }

    public float DrawAreaRestriction(Vector2 pos, float width)
    {
        var start = pos;
        AreaAllowedGUI.DoAllowedAreaSelectors(ref pos, width, ref SelectedForestryJob.LoggingArea, manager);
        return pos.y - start.y;
    }

    public float DrawClearArea(Vector2 pos, float width)
    {
        var start = pos;
        var rowRect = new Rect(
            pos.x,
            pos.y,
            width,
            ListEntryHeight);
        AreaAllowedGUI.DoAllowedAreaSelectorsMC(rowRect, ref SelectedForestryJob.ClearAreas, manager);
        pos.y += ListEntryHeight;

        return pos.y - start.y;
    }

    public float DrawEmpty(string label, Vector2 pos, float width)
    {
        var height = Mathf.Max(Text.CalcHeight(label, width), ListEntryHeight);
        var rowRect = new Rect(
            pos.x,
            pos.y,
            width,
            height);
        Label(rowRect, label, TextAnchor.MiddleLeft, color: Color.gray);
        return height;
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
                SelectedForestryJob.Type == type,
                () => SelectedForestryJob.Type = type,
                () => { },
                wrap: false);
            cellRect.x += cellWidth;
        }

        return ListEntryHeight;
    }

    public float DrawThreshold(Vector2 pos, float width)
    {
        var start = pos;
        var currentCount = SelectedForestryJob.Trigger.CurrentCount;
        var designatedCount = SelectedForestryJob.GetWoodInDesignations();
        var targetCount = SelectedForestryJob.Trigger.TargetCount;

        SelectedForestryJob.Trigger.DrawTriggerConfig(ref pos, width, ListEntryHeight,
                                             "ColonyManagerRedux.Forestry.TargetCount".Translate(
                                                 currentCount, designatedCount, targetCount),
                                             "ColonyManagerRedux.Forestry.TargetCountTooltip".Translate(
                                                 currentCount, designatedCount, targetCount),
                                             SelectedForestryJob.Designations, null, SelectedForestryJob.DesignationLabel);

        Utilities.DrawReachabilityToggle(ref pos, width, ref SelectedForestryJob.ShouldCheckReachable);
        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.ManagerPathBasedDistance".Translate(),
            "ColonyManagerRedux.ManagerPathBasedDistance.Tip".Translate(),
            ref SelectedForestryJob.UsePathBasedDistance,
            true);

        return pos.y - start.y;
    }

    public float DrawTreeList(Vector2 pos, float width)
    {
        var start = pos;
        var rowRect = new Rect(
            pos.x,
            pos.y,
            width,
            ListEntryHeight);
        var allowedTrees = SelectedForestryJob.AllowedTrees;

        // toggle for each tree
        foreach (var plantDef in SelectedForestryJob.AllPlants)
        {
            Utilities.DrawToggle(rowRect, plantDef.LabelCap,
                new TipSignal(() => GetTreeTooltip(plantDef), plantDef.GetHashCode()),
                allowedTrees.Contains(plantDef),
                () =>
                {
                    if (allowedTrees.Contains(plantDef))
                    {
                        allowedTrees.Remove(plantDef);
                    }
                    else
                    {
                        allowedTrees.Add(plantDef);
                    }
                });
            rowRect.y += ListEntryHeight;
        }

        return rowRect.yMin - start.y;
    }

    public float DrawTreeShortcuts(Vector2 pos, float width)
    {
        var start = pos;
        var rowRect = new Rect(
            pos.x,
            pos.y,
            width,
            ListEntryHeight);
        var allowedTrees = SelectedForestryJob.AllowedTrees;
        var allPlants = SelectedForestryJob.AllPlants;

        DrawShortcutToggle(allPlants, allowedTrees, SelectedForestryJob.SetTreeAllowed, rowRect, "ManagerAll", null);

        if (SelectedForestryJob.Type == ForestryJobType.ClearArea)
        {
            // trees (anything that drops wood, or has the correct harvest tag).
            rowRect.y += ListEntryHeight;
            var trees = allPlants
                .Where(tree => tree.plant.harvestTag == "Wood" ||
                    tree.plant.harvestedThingDef == ThingDefOf.WoodLog)
                .ToList();
            DrawShortcutToggle(trees, allowedTrees, SelectedForestryJob.SetTreeAllowed, rowRect,
                "Forestry.Trees", "Forestry.Trees.Tip");

            // flammable (probably all - might be modded stuff).
            rowRect.y += ListEntryHeight;
            var flammable = allPlants.Where(tree => tree.BaseFlammability > 0).ToList();
            if (flammable.Count != allPlants.Count)
            {
                DrawShortcutToggle(flammable, allowedTrees, SelectedForestryJob.SetTreeAllowed, rowRect,
                    "Forestry.Flammable", "Forestry.Flammable.Tip");
                rowRect.y += ListEntryHeight;
            }

            // ugly (possibly none - modded stuff).
            var ugly = allPlants.Where(tree => tree.statBases.GetStatValueFromList(StatDefOf.Beauty, 0) < 0)
                .ToList();
            if (!ugly.NullOrEmpty())
            {
                DrawShortcutToggle(ugly, allowedTrees, SelectedForestryJob.SetTreeAllowed, rowRect,
                    "Forestry.Ugly", "Forestry.Ugly.Tip");
                rowRect.y += ListEntryHeight;
            }

            // provides cover
            var cover = allPlants
                .Where(tree => tree.Fillage == FillCategory.Full ||
                    tree.Fillage == FillCategory.Partial && tree.fillPercent > 0)
                .ToList();
            DrawShortcutToggle(cover, allowedTrees, SelectedForestryJob.SetTreeAllowed, rowRect,
                "Forestry.ProvidesCover", "Forestry.ProvidesCover.Tip");
        }

        return rowRect.yMax - start.y;
    }

    public override void PostClose()
    {
        Refresh();
    }

    public override void PreOpen()
    {
        Refresh();
    }

    public void Refresh()
    {
        _jobs = manager.JobStack.FullStack<ManagerJob_Forestry>();

        // makes sure the list of possible areas is up-to-date with the area in the game.
        foreach (var job in _jobs)
        {
            job.UpdateClearAreas();
        }


        // update plant options
        foreach (var job in _jobs)
        {
            job.RefreshAllTrees();
        }

        // also for selected job
        SelectedForestryJob?.RefreshAllTrees();
    }
}
