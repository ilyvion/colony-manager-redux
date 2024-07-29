// ManagerTab_Foraging.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Text;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[HotSwappable]
internal sealed class ManagerTab_Foraging(Manager manager) : ManagerTab<ManagerJob_Foraging>(manager)
{
    public override string Label => "ColonyManagerRedux.Foraging".Translate();

    public ManagerJob_Foraging SelectedForagingJob => SelectedJob!;

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
        Widgets_Section.Section(ref position, width, DrawThreshold, "ColonyManagerRedux.Threshold".Translate());
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
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.Common.Manage".Translate()))
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
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.Common.Delete".Translate()))
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

        DrawShortcutToggle(allPlants, allowedPlants, (p, v) => SelectedForagingJob.SetPlantAllowed(p, v), rowRect, "ColonyManagerRedux.Shortcuts.All", null);

        // toggle edible
        rowRect.y += ListEntryHeight;
        var edible = allPlants.Where(p => p.plant?.harvestedThingDef?.IsNutritionGivingIngestible ?? false).ToList();
        DrawShortcutToggle(edible, allowedPlants, (p, v) => SelectedForagingJob.SetPlantAllowed(p, v), rowRect,
            "ColonyManagerRedux.Foraging.Edible", "ColonyManagerRedux.Foraging.Edible.Tip");

        // toggle shrooms
        rowRect.y += ListEntryHeight;
        var shrooms = allPlants.Where(p => p.plant?.cavePlant ?? false).ToList();
        DrawShortcutToggle(shrooms, allowedPlants, (p, v) => SelectedForagingJob.SetPlantAllowed(p, v), rowRect,
            "ColonyManagerRedux.Foraging.Mushrooms", "ColonyManagerRedux.Foraging.Mushrooms.Tip");

        return rowRect.yMax - start.y;
    }

    public float DrawThreshold(Vector2 pos, float width)
    {
        var currentCount = SelectedForagingJob.TriggerThreshold.GetCurrentCount();
        var designatedCount = SelectedForagingJob.GetCurrentDesignatedCount();
        var targetCount = SelectedForagingJob.TriggerThreshold.TargetCount;
        var start = pos;

        SelectedForagingJob.TriggerThreshold.DrawTriggerConfig(ref pos, width, ListEntryHeight,
            "ColonyManagerRedux.Foraging.TargetCount".Translate(
                currentCount, designatedCount, targetCount),
            "ColonyManagerRedux.Foraging.TargetCountTooltip".Translate(
                currentCount, designatedCount, targetCount),
            SelectedForagingJob.Designations,
            () => SelectedForagingJob.Sync = Utilities.SyncDirection.FilterToAllowed,
            SelectedForagingJob.DesignationLabel);

        Utilities.DrawToggle(ref pos, width,
            "ColonyManagerRedux.Foraging.SyncFilterAndAllowed".Translate(),
            "ColonyManagerRedux.Foraging.SyncFilterAndAllowed.Tip".Translate(),
            ref SelectedForagingJob.SyncFilterAndAllowed);
        Utilities.DrawReachabilityToggle(ref pos, width, ref SelectedForagingJob.ShouldCheckReachable);
        Utilities.DrawToggle(ref pos, width, "ColonyManagerRedux.Threshold.PathBasedDistance".Translate(),
            "ColonyManagerRedux.Threshold.PathBasedDistance.Tip".Translate(), ref SelectedForagingJob.UsePathBasedDistance,
            true);

        return pos.y - start.y;
    }

    public override void PreOpen()
    {
        Refresh();
    }

    protected override void Refresh()
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
