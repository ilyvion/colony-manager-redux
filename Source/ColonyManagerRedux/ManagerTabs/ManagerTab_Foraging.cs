// ManagerTab_Foraging.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Text;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[HotSwappable]
internal sealed class ManagerTab_Foraging(Manager manager) : ManagerTab(manager)
{
    private float _leftRowHeight;
    private Vector2 _scrollPosition = Vector2.zero;

    public override string Label => "ColonyManagerRedux.Foraging.Foraging".Translate();

    public ManagerJob_Foraging SelectedForagingJob => (ManagerJob_Foraging)Selected!;

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
        var plantsColumnRect = new Rect(
            optionsColumnRect.xMax,
            rect.yMin,
            rect.width * 2 / 5f,
            rect.height - Margin - ButtonSize.y);
        var buttonRect = new Rect(
            rect.xMax - ButtonSize.x,
            rect.yMax - ButtonSize.y,
            ButtonSize.x - Margin,
            ButtonSize.y - Margin);

        Widgets_Section.BeginSectionColumn(optionsColumnRect, "Foraging.Options", out Vector2 position, out float width);
        Widgets_Section.Section(ref position, width, DrawThreshold, "ColonyManagerRedux.ManagerThreshold".Translate());
        Widgets_Section.Section(ref position, width, DrawAreaRestriction, "ColonyManagerRedux.Foraging.ForagingArea".Translate());
        Widgets_Section.Section(ref position, width, DrawMaturePlants);
        Widgets_Section.EndSectionColumn("Foraging.Options", position);

        Widgets_Section.BeginSectionColumn(plantsColumnRect, "Foraging.Plants", out position, out width);
        var refreshRect = new Rect(
            position.x + width - SmallIconSize - 2 * Margin,
            position.y + Margin,
            SmallIconSize,
            SmallIconSize);
        if (Widgets.ButtonImage(refreshRect, Resources.Refresh, Color.grey))
        {
            SelectedForagingJob.RefreshAllPlants();
        }

        Widgets_Section.Section(ref position, width, DrawPlantShortcuts, "ColonyManagerRedux.Foraging.Plants".Translate());
        Widgets_Section.Section(ref position, width, DrawPlantList);
        Widgets_Section.EndSectionColumn("Foraging.Plants", position);


        // do the button
        if (!SelectedForagingJob.IsManaged)
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.ManagerManage".Translate()))
            {
                // activate job, add it to the stack
                SelectedForagingJob.IsManaged = true;
                manager.JobTracker.Add(SelectedForagingJob);

                // refresh source list
                Refresh();
            }
        }
        else
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.ManagerDelete".Translate()))
            {
                // inactivate job, remove from the stack.
                manager.JobTracker.Delete(SelectedForagingJob);

                // remove content from UI
                Selected = MakeNewJob();

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

        foreach (var job in manager.JobTracker.JobsOfType<ManagerJob_Foraging>())
        {
            var row = new Rect(0f, cur.y, scrollContent.width, LargeListEntryHeight);
            Widgets.DrawHighlightIfMouseover(row);
            if (SelectedForagingJob == job)
            {
                Widgets.DrawHighlightSelected(row);
            }

            if (i++ % 2 == 1)
            {
                Widgets.DrawAltRect(row);
            }

            var jobRect = row;

            if (DrawOrderButtons(new Rect(row.xMax - 50f, row.yMin, 50f, 50f), manager, job))
            {
                Refresh();
            }

            jobRect.width -= 50f;

            DrawListEntry(job, jobRect, ListEntryDrawMode.Local);
            if (Widgets.ButtonInvisible(jobRect))
            {
                Selected = job;
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
        Widgets.Label(newRect, "<" + "ColonyManagerRedux.Foraging.NewForagingJob".Translate().Resolve() + ">");
        Text.Anchor = TextAnchor.UpperLeft;

        if (Widgets.ButtonInvisible(newRect))
        {
            Selected = MakeNewJob();
        }

        TooltipHandler.TipRegion(newRect, "ColonyManagerRedux.Foraging.NewForagingJobTooltip".Translate().Resolve());

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

    public override void DrawListEntry(ManagerJob job, Rect rect, ListEntryDrawMode mode, bool active = true)
    {
        // (detailButton) | name | (bar | last update)/(stamp) -> handled in Utilities.DrawStatusForListEntry

        var foragingJob = (ManagerJob_Foraging)job;

        // set up rects
        var labelRect = new Rect(
            Margin,
            Margin,
            rect.width - (active ? StatusRectWidth + 4 * Margin : 2 * Margin),
            rect.height - 2 * Margin);
        var statusRect = new Rect(labelRect.xMax + Margin, Margin, StatusRectWidth, rect.height - 2 * Margin);

        // create label string
        var text = Label + "\n";
        var subtext = string.Join(", ", foragingJob.Targets);
        if (subtext.Fits(labelRect))
        {
            text += subtext.Italic();
        }
        else
        {
            text += "ColonyManagerRedux.Multiple".Translate().Resolve().Italic();
        }

        // do the drawing
        GUI.BeginGroup(rect);

        // draw label
        Widgets_Labels.Label(labelRect, text, subtext, TextAnchor.MiddleLeft);

        // if the bill has a manager job, give some more info.
        if (active)
        {
            foragingJob.DrawStatusForListEntry(statusRect, foragingJob.Trigger, mode == ListEntryDrawMode.Export);
        }

        GUI.EndGroup();
    }

    public float DrawAreaRestriction(Vector2 pos, float width)
    {
        var start = pos;
        AreaAllowedGUI.DoAllowedAreaSelectors(ref pos, width, ref SelectedForagingJob.ForagingArea, manager);
        return pos.y - start.y;
    }

    public float DrawMaturePlants(Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(
            rowRect,
            "ColonyManagerRedux.Foraging.ForceFullyMature".Translate(),
            "ColonyManagerRedux.Foraging.ForceFullyMature.Tip".Translate(),
            ref SelectedForagingJob.ForceFullyMature);

        return ListEntryHeight;
    }

    public float DrawPlantList(Vector2 pos, float width)
    {
        var start = pos;

        // list of keys in allowed trees list (all plans that yield wood in biome, static)
        var allowedPlants = SelectedForagingJob.AllowedPlants;
        var allPlants = SelectedForagingJob.AllPlants;

        var rowRect = new Rect(
            pos.x,
            pos.y,
            width,
            ListEntryHeight);

        // toggle for each plant
        foreach (var plantDef in allPlants)
        {
            Utilities.DrawToggle(rowRect, plantDef.LabelCap,
                new TipSignal(() => GetPlantTooltip(plantDef), plantDef.GetHashCode()), allowedPlants.Contains(plantDef),
                () => SelectedForagingJob.SetPlantAllowed(plantDef, !allowedPlants.Contains(plantDef)));
            rowRect.y += ListEntryHeight;
        }

        return rowRect.yMin - start.y;
    }

    public static string GetPlantTooltip(ThingDef plant)
    {
        var sb = new StringBuilder();
        sb.Append(plant.description);
        if (plant.plant != null && plant.plant.harvestYield >= 1f && plant.plant.harvestedThingDef != null)
        {
            sb.Append("\n\n");
            sb.Append(I18n.YieldOne(plant.plant.harvestYield, plant.plant.harvestedThingDef));
        }
        return sb.ToString();
    }

    public float DrawPlantShortcuts(Vector2 pos, float width)
    {
        var start = pos;

        // list of keys in allowed trees list (all plans that yield wood in biome, static)
        var allowedPlants = SelectedForagingJob.AllowedPlants;
        var allPlants = SelectedForagingJob.AllPlants;

        var rowRect = new Rect(
            pos.x,
            pos.y,
            width,
            ListEntryHeight);

        DrawShortcutToggle(allPlants, allowedPlants, (p, v) => SelectedForagingJob.SetPlantAllowed(p, v), rowRect, "ManagerAll", null);

        // toggle edible
        rowRect.y += ListEntryHeight;
        var edible = allPlants.Where(p => p.plant?.harvestedThingDef?.IsNutritionGivingIngestible ?? false).ToList();
        DrawShortcutToggle(edible, allowedPlants, (p, v) => SelectedForagingJob.SetPlantAllowed(p, v), rowRect,
            "ManagerForaging.Edible", "ManagerForaging.Edible.Tip");

        // toggle shrooms
        rowRect.y += ListEntryHeight;
        var shrooms = allPlants.Where(p => p.plant?.cavePlant ?? false).ToList();
        DrawShortcutToggle(shrooms, allowedPlants, (p, v) => SelectedForagingJob.SetPlantAllowed(p, v), rowRect,
            "ManagerForaging.Mushrooms", "ManagerForaging.Mushrooms.Tip");

        return rowRect.yMax - start.y;
    }

    public float DrawThreshold(Vector2 pos, float width)
    {
        var currentCount = SelectedForagingJob.Trigger.CurrentCount;
        var designatedCount = SelectedForagingJob.CurrentDesignatedCount;
        var targetCount = SelectedForagingJob.Trigger.TargetCount;
        var start = pos;

        SelectedForagingJob.Trigger.DrawTriggerConfig(ref pos, width, ListEntryHeight,
            "ColonyManagerRedux.Foraging.TargetCount".Translate(
                currentCount, designatedCount, targetCount),
            "ColonyManagerRedux.Foraging.TargetCountTooltip".Translate(
                currentCount, designatedCount, targetCount),
            SelectedForagingJob.Designations,
            () => SelectedForagingJob.Sync = Utilities.SyncDirection.FilterToAllowed,
            SelectedForagingJob.DesignationLabel);

        Utilities.DrawToggle(ref pos, width,
            "ColonyManagerRedux.ManagerForaging.SyncFilterAndAllowed".Translate(),
            "ColonyManagerRedux.ManagerForaging.SyncFilterAndAllowed.Tip".Translate(),
            ref SelectedForagingJob.SyncFilterAndAllowed);
        Utilities.DrawReachabilityToggle(ref pos, width, ref SelectedForagingJob.ShouldCheckReachable);
        Utilities.DrawToggle(ref pos, width, "ColonyManagerRedux.ManagerPathBasedDistance".Translate(),
            "ColonyManagerRedux.ManagerPathBasedDistance.Tip".Translate(), ref SelectedForagingJob.UsePathBasedDistance,
            true);

        return pos.y - start.y;
    }

    public override void PreOpen()
    {
        Refresh();
    }

    public void Refresh()
    {
        // update plant options
        foreach (var job in manager.JobTracker.JobsOfType<ManagerJob_Foraging>())
        {
            job.RefreshAllPlants();
        }

        // update selected ( also update thingfilter _only_ if the job is not managed yet )
        SelectedForagingJob?.RefreshAllPlants();
    }
}
