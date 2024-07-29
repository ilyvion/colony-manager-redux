﻿// ManagerTab_Forestry.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using static ColonyManagerRedux.Constants;
using static ColonyManagerRedux.ManagerJob_Forestry;
using static ColonyManagerRedux.Widgets_Labels;

namespace ColonyManagerRedux;

[HotSwappable]
internal sealed class ManagerTab_Forestry(Manager manager) : ManagerTab<ManagerJob_Forestry>(manager)
{
    public override string Label => "ColonyManagerRedux.Forestry.Forestry".Translate();

    public ManagerJob_Forestry SelectedForestryJob => SelectedJob!;

    public static string GetTreeTooltip(ThingDef tree)
    {
        return ManagerTab_Foraging.GetPlantTooltip(tree);
    }

    protected override void DoMainContent(Rect rect)
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

        if (SelectedForestryJob.Type == ForestryJobType.ClearArea)
        {
            Widgets_Section.Section(ref position, width, DrawClearArea, "ColonyManagerRedux.Forestry.JobType.ClearArea".Translate());
        }

        if (SelectedForestryJob.Type == ForestryJobType.Logging)
        {
            Widgets_Section.Section(ref position, width, DrawThreshold, "ColonyManagerRedux.Threshold".Translate());
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
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.Common.Manage".Translate()))
            {
                // activate job, add it to the stack
                SelectedForestryJob.IsManaged = true;
                manager.JobTracker.Add(SelectedForestryJob);

                // refresh source list
                Refresh();
            }
        }
        else
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.Common.Delete".Translate()))
            {
                // inactivate job, remove from the stack.
                manager.JobTracker.Delete(SelectedForestryJob);

                // remove content from UI
                Selected = MakeNewJob();

                // refresh source list
                Refresh();
            }
        }
    }

    public override string GetSubLabel(ManagerJob job, ListEntryDrawMode mode)
    {
        return ((ManagerJob_Forestry)job).Type switch
        {
            ForestryJobType.Logging => base.GetSubLabel(job, mode),
            _ => "ColonyManagerRedux.Forestry.Clear".Translate(string.Join(", ", job.Targets)).Resolve(),
        };
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

    public static float DrawEmpty(string label, Vector2 pos, float width)
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
            (ForestryJobType[])
            Enum.GetValues(typeof(ForestryJobType));

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
        var currentCount = SelectedForestryJob.TriggerThreshold.GetCurrentCount();
        var designatedCount = SelectedForestryJob.GetCurrentDesignatedCount();
        var targetCount = SelectedForestryJob.TriggerThreshold.TargetCount;

        SelectedForestryJob.TriggerThreshold.DrawTriggerConfig(ref pos, width, ListEntryHeight,
            "ColonyManagerRedux.Forestry.TargetCount".Translate(
                currentCount, designatedCount, targetCount),
            "ColonyManagerRedux.Forestry.TargetCountTooltip".Translate(
                currentCount, designatedCount, targetCount),
            SelectedForestryJob.Designations, null, SelectedForestryJob.DesignationLabel);

        Utilities.DrawReachabilityToggle(ref pos, width, ref SelectedForestryJob.ShouldCheckReachable);
        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.Threshold.PathBasedDistance".Translate(),
            "ColonyManagerRedux.Threshold.PathBasedDistance.Tip".Translate(),
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
#pragma warning disable CA1868 // Unnecessary call to 'Contains(item)'
                    if (allowedTrees.Contains(plantDef))
                    {
                        allowedTrees.Remove(plantDef);
                    }
                    else
                    {
                        allowedTrees.Add(plantDef);
                    }
#pragma warning restore CA1868 // Unnecessary call to 'Contains(item)'
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

        DrawShortcutToggle(allPlants, allowedTrees, SelectedForestryJob.SetTreeAllowed, rowRect, "ColonyManagerRedux.Shortcuts.All", null);

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

    protected override void Refresh()
    {
        // makes sure the list of possible areas is up-to-date with the area in the game.
        foreach (var job in manager.JobTracker.JobsOfType<ManagerJob_Forestry>())
        {
            job.UpdateClearAreas();
            job.RefreshAllTrees();
        }

        // also for selected job
        SelectedForestryJob?.RefreshAllTrees();
    }
}
