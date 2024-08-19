// ManagerTab_Forestry.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;
using static ColonyManagerRedux.Managers.ManagerJob_Forestry;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerTab_Forestry(Manager manager) : ManagerTab<ManagerJob_Forestry>(manager)
{
    public sealed class DrawOverviewListEntryWorker : DrawOverviewListEntryWorker<ManagerJob_Forestry>
    {
        public override void ChangeDrawListEntryParameters(
            ManagerJob_Forestry job,
            ref DrawOverviewListEntryParameters parameters)
        {
            parameters.ShowProgressbar = job.Type == ForestryJobType.Logging;
        }
        public override void DrawOverviewListEntry(ManagerJob_Forestry job, ref Vector2 position, float width)
        {
            throw new NotImplementedException();
        }
    }

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
                Manager.JobTracker.Add(SelectedForestryJob);

                // refresh source list
                Refresh();
            }
        }
        else
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.Common.Delete".Translate()))
            {
                // inactivate job, remove from the stack.
                Manager.JobTracker.Delete(SelectedForestryJob);

                // remove content from UI
                Selected = MakeNewJob();

                // refresh source list
                Refresh();
            }
        }
    }

    public override void DrawLocalListEntry(
        ManagerJob job,
        ref Vector2 position,
        float width,
        DrawLocalListEntryParameters? parameters)
    {
        parameters = new()
        {
            ShowProgressbar = ((ManagerJob_Forestry)job).Type == ForestryJobType.Logging
        };

        base.DrawLocalListEntry(job, ref position, width, parameters);
    }

    public override string GetSubLabel(ManagerJob job)
    {
        return ((ManagerJob_Forestry)job).Type switch
        {
            ForestryJobType.Logging => base.GetSubLabel(job),
            _ => "ColonyManagerRedux.Forestry.Clear"
                .Translate(string.Join(", ", job.Targets)).Resolve(),
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
        AreaAllowedGUI.DoAllowedAreaSelectors(ref pos, width, ref SelectedForestryJob.LoggingArea, 5, Manager);
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
        AreaAllowedGUI.DoAllowedAreaSelectorsMC(rowRect, ref SelectedForestryJob.ClearAreas, Manager);
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
        IlyvionWidgets.Label(rowRect, label, TextAnchor.MiddleLeft, color: Color.gray);
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
            SelectedForestryJob.Designations,
            delegate { SelectedForestryJob.Sync = Utilities.SyncDirection.FilterToAllowed; },
            SelectedForestryJob.DesignationLabel);

        Utilities.DrawToggle(ref pos, width,
            "ColonyManagerRedux.SyncFilterAndAllowed".Translate(),
            "ColonyManagerRedux.Forestry.SyncFilterAndAllowed.Tip".Translate(),
            ref SelectedForestryJob.SyncFilterAndAllowed);
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
                () => SelectedForestryJob
                    .SetTreeAllowed(plantDef, !allowedTrees.Contains(plantDef)));
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

        DrawShortcutToggle(allPlants, allowedTrees, (t, v) => SelectedForestryJob.SetTreeAllowed(t, v), rowRect, "ColonyManagerRedux.Shortcuts.All", null);

        if (SelectedForestryJob.Type == ForestryJobType.ClearArea)
        {
            // trees (anything that drops wood, or has the correct harvest tag).
            rowRect.y += ListEntryHeight;
            var trees = allPlants
                .Where(tree => tree.plant.harvestTag == "Wood" ||
                    tree.plant.harvestedThingDef == ThingDefOf.WoodLog)
                .ToList();
            DrawShortcutToggle(trees, allowedTrees, (t, v) => SelectedForestryJob.SetTreeAllowed(t, v), rowRect,
                "ColonyManagerRedux.Forestry.Trees", "ColonyManagerRedux.Forestry.Trees.Tip");

            // flammable (probably all - might be modded stuff).
            rowRect.y += ListEntryHeight;
            var flammable = allPlants.Where(tree => tree.BaseFlammability > 0).ToList();
            if (flammable.Count != allPlants.Count)
            {
                DrawShortcutToggle(flammable, allowedTrees, (t, v) => SelectedForestryJob.SetTreeAllowed(t, v), rowRect,
                    "ColonyManagerRedux.Forestry.Flammable", "ColonyManagerRedux.Forestry.Flammable.Tip");
                rowRect.y += ListEntryHeight;
            }

            // ugly (possibly none - modded stuff).
            var ugly = allPlants.Where(tree => tree.statBases.GetStatValueFromList(StatDefOf.Beauty, 0) < 0)
                .ToList();
            if (!ugly.NullOrEmpty())
            {
                DrawShortcutToggle(ugly, allowedTrees, (t, v) => SelectedForestryJob.SetTreeAllowed(t, v), rowRect,
                    "ColonyManagerRedux.Forestry.Ugly", "ColonyManagerRedux.Forestry.Ugly.Tip");
                rowRect.y += ListEntryHeight;
            }

            // provides cover
            var cover = allPlants
                .Where(tree => tree.Fillage == FillCategory.Full ||
                    tree.Fillage == FillCategory.Partial && tree.fillPercent > 0)
                .ToList();
            DrawShortcutToggle(cover, allowedTrees, (t, v) => SelectedForestryJob.SetTreeAllowed(t, v), rowRect,
                "ColonyManagerRedux.Forestry.ProvidesCover", "ColonyManagerRedux.Forestry.ProvidesCover.Tip");
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
        foreach (var job in Manager.JobTracker.JobsOfType<ManagerJob_Forestry>())
        {
            job.UpdateClearAreas();
            job.RefreshAllTrees();
        }

        // also for selected job
        SelectedForestryJob?.RefreshAllTrees();
    }
}
