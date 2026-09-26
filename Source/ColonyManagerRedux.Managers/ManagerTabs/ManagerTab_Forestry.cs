// ManagerTab_Forestry.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024–2026 Alexander Krivács Schrøder

using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;
using static ColonyManagerRedux.Managers.ManagerJob_Forestry;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerTab_Forestry(Manager manager)
    : ManagerTab<ManagerJob_Forestry>(manager)
{
    public sealed class DrawOverviewListEntryWorker
        : DrawOverviewListEntryWorker<ManagerJob_Forestry>
    {
        public override void ChangeDrawListEntryParameters(
            ManagerJob_Forestry job,
            ref DrawOverviewListEntryParameters parameters
        ) => parameters.ShowProgressbar = job.Type == ForestryJobType.Logging;

        public override void DrawOverviewListEntry(
            ManagerJob_Forestry job,
            ref Vector2 position,
            float width
        ) => throw new NotImplementedException();
    }

    public ManagerJob_Forestry SelectedForestryJob => SelectedJob!;

    public static string GetTreeTooltip(ThingDef tree) => ManagerTab_Foraging.GetPlantTooltip(tree);

    private const string ForestOptions = "Forestry.Options";

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
            rect.height - Margin - ButtonSize.y
        );
        var treesColumnRect = new Rect(
            optionsColumnRect.xMax,
            rect.yMin,
            rect.width * 2 / 5f,
            rect.height - Margin - ButtonSize.y
        );
        var buttonRect = new Rect(
            rect.xMax - ButtonSize.x,
            rect.yMax - ButtonSize.y,
            ButtonSize.x - Margin,
            ButtonSize.y - Margin
        );

        Widgets_Section.BeginSectionColumn(
            optionsColumnRect,
            ForestOptions,
            out var position,
            out var width
        );
        DrawSection(
            ForestOptions,
            "JobType",
            ref position,
            width,
            DrawJobType,
            "ColonyManagerRedux.Forestry.JobType".Translate()
        );

        if (SelectedForestryJob.Type == ForestryJobType.ClearArea)
        {
            DrawSection(
                ForestOptions,
                "ClearArea",
                ref position,
                width,
                DrawClearArea,
                "ColonyManagerRedux.Forestry.JobType.ClearArea".Translate()
            );
        }

        if (SelectedForestryJob.Type == ForestryJobType.Logging)
        {
            DrawSection(
                ForestOptions,
                "Threshold",
                ref position,
                width,
                DrawThreshold,
                "ColonyManagerRedux.Threshold".Translate()
            );
            DrawSection(
                ForestOptions,
                "LoggingArea",
                ref position,
                width,
                DrawAreaRestriction,
                "ColonyManagerRedux.Forestry.LoggingArea".Translate()
            );
            DrawSection(ForestOptions, "AllowSaplings", ref position, width, DrawAllowSaplings);
        }

        Widgets_Section.EndSectionColumn(ForestOptions, position);

        Widgets_Section.BeginSectionColumn(
            treesColumnRect,
            "Forestry.Trees",
            out position,
            out width
        );
        var refreshRect = new Rect(
            position.x + width - SmallIconSize - (2 * Margin),
            position.y + Margin,
            SmallIconSize,
            SmallIconSize
        );
        if (Widgets.ButtonImage(refreshRect, Resources.Refresh, Color.grey))
        {
            SelectedForestryJob.RefreshAllTrees();
        }

        var padlockRect = new Rect(
            refreshRect.x - (SmallIconSize + 1) - (2 * Margin),
            position.y + Margin,
            SmallIconSize + 1,
            SmallIconSize
        );
        if (
            Widgets.ButtonImage(
                padlockRect,
                SelectedForestryJob.PlantsLockedToMap
                    ? Resources.PadlockClosed
                    : Resources.PadlockOpen,
                Color.grey
            )
        )
        {
            SelectedForestryJob.PlantsLockedToMap = !SelectedForestryJob.PlantsLockedToMap;
        }

        Widgets_Section.Section(
            ref position,
            width,
            DrawTreeShortcuts,
            "ColonyManagerRedux.Forestry.Trees".Translate()
        );
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
        DrawLocalListEntryParameters? parameters
    )
    {
        parameters = new()
        {
            ShowProgressbar = ((ManagerJob_Forestry)job).Type == ForestryJobType.Logging,
        };

        base.DrawLocalListEntry(job, ref position, width, parameters);
    }

    public override string GetSubLabel(ManagerJob job) =>
        ((ManagerJob_Forestry)job).Type switch
        {
            ForestryJobType.Logging => base.GetSubLabel(job),
            ForestryJobType.ClearArea => "ColonyManagerRedux.Forestry.Clear"
                .Translate(string.Join(", ", job.Targets))
                .Resolve(),
            _ => throw new NotImplementedException(),
        };

    public float DrawAllowSaplings(ManagerJob_Forestry job, Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);

        // NOTE: AllowSaplings logic is the reverse from the label that is shown to the user.
        Utilities.DrawToggle(
            rowRect,
            "ColonyManagerRedux.Forestry.AllowSaplings".Translate(),
            "ColonyManagerRedux.Forestry.AllowSaplings.Tip".Translate(),
            !job.AllowSaplings,
            () => job.AllowSaplings = false,
            () => job.AllowSaplings = true
        );
        return ListEntryHeight;
    }

    public float DrawAreaRestriction(ManagerJob_Forestry job, Vector2 pos, float width) =>
        AreaAllowedGUI.DoAllowedAreaSelectorsWithInvert(
            ref pos,
            width,
            ref job.LoggingArea,
            ref job.InvertLoggingArea,
            5,
            Manager
        );

    public float DrawClearArea(ManagerJob_Forestry job, Vector2 pos, float width)
    {
        var start = pos;
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        AreaAllowedGUI.DoAllowedAreaSelectorsMC(rowRect, ref job.ClearAreas, Manager);
        pos.y += ListEntryHeight;

        return pos.y - start.y;
    }

    public static float DrawEmpty(string label, Vector2 pos, float width)
    {
        var height = Mathf.Max(Text.CalcHeight(label, width), ListEntryHeight);
        var rowRect = new Rect(pos.x, pos.y, width, height);
        IlyvionWidgets.Label(rowRect, label, TextAnchor.MiddleLeft, color: Color.gray);
        return height;
    }

    public float DrawJobType(ManagerJob_Forestry job, Vector2 pos, float width)
    {
        // type of job;
        // clear clear area | logging
        var types = (ForestryJobType[])Enum.GetValues(typeof(ForestryJobType));

        var cellWidth = width / types.Length;

        var cellRect = new Rect(pos.x, pos.y, cellWidth, ListEntryHeight);

        foreach (var type in types)
        {
            Utilities.DrawToggle(
                cellRect,
                $"ColonyManagerRedux.Forestry.JobType.{type}".Translate(),
                $"ColonyManagerRedux.Forestry.JobType.{type}.Tip".Translate(),
                job.Type == type,
                () => job.Type = type,
                () => { },
                wrap: false
            );
            cellRect.x += cellWidth;
        }

        return ListEntryHeight;
    }

    public float DrawThreshold(ManagerJob_Forestry job, Vector2 pos, float width)
    {
        var start = pos;
        var currentCount = job.TriggerThreshold.GetCurrentCount();
        _ = job.CachedCurrentDesignatedCount.DoUpdateIfNeeded();
        var designatedCount = job.CachedCurrentDesignatedCount.Value;
        var targetLabel = job.TriggerThreshold.TargetLabel;

        job.TriggerThreshold.DrawTriggerConfig(
            ref pos,
            width,
            ListEntryHeight,
            "ColonyManagerRedux.Forestry.TargetCount".Translate(
                currentCount,
                designatedCount,
                targetLabel
            ),
            "ColonyManagerRedux.Forestry.TargetCountTooltip".Translate(
                currentCount,
                designatedCount,
                targetLabel
            ),
            job.Designations,
            delegate
            {
                job.Sync = Utilities.SyncDirection.FilterToAllowed;
            },
            job.DesignationLabel
        );

        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.SyncFilterAndAllowed".Translate(),
            "ColonyManagerRedux.Forestry.SyncFilterAndAllowed.Tip".Translate(),
            ref SelectedForestryJob.SyncFilterAndAllowed
        );
        Utilities.DrawReachabilityToggle(
            ref pos,
            width,
            ref SelectedForestryJob.ShouldCheckReachable
        );
        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.Threshold.PathBasedDistance".Translate(),
            "ColonyManagerRedux.Threshold.PathBasedDistance.Tip".Translate(),
            ref SelectedForestryJob.UsePathBasedDistance,
            true
        );

        return pos.y - start.y;
    }

    public float DrawTreeList(Vector2 pos, float width)
    {
        var allowedTrees = SelectedForestryJob.AllowedTrees;

        return Utilities.DrawToggleDefList(
            pos,
            width,
            SelectedForestryJob.AllPlants,
            allowedTrees.Contains,
            (plantDef, allow) => SelectedForestryJob.SetTreeAllowed(plantDef, allow),
            plantDef => plantDef.LabelCap,
            plantDef => new TipSignal(() => GetTreeTooltip(plantDef), plantDef.GetHashCode()),
            (rect, plantDef) => Widgets.InfoCardButton(rect, plantDef)
        );
    }

    internal static bool IsTreeOfCategory(ThingDef plant, TreeCategory category) =>
        plant.plant?.treeCategory == category;

    // Anything that drops wood, or has the wood harvest tag.
    internal static bool IsWoodTree(ThingDef plant) =>
        plant.plant?.harvestTag == "Wood" || plant.plant?.harvestedThingDef == ThingDefOf.WoodLog;

    internal static bool IsFlammable(ThingDef plant) => plant.BaseFlammability > 0;

    internal static bool IsUgly(ThingDef plant) =>
        plant.statBases.GetStatValueFromList(StatDefOf.Beauty, 0) < 0;

    internal static bool ProvidesCover(ThingDef plant) =>
        plant.Fillage == FillCategory.Full
        || (plant.Fillage == FillCategory.Partial && plant.fillPercent > 0);

    internal static readonly ShortcutCategory<ThingDef>[] _treeShortcuts =
    [
        new(
            "ColonyManagerRedux.Forestry.Mini",
            "ColonyManagerRedux.Forestry.Mini.Tip",
            p => IsTreeOfCategory(p, TreeCategory.Mini)
        ),
        new(
            "ColonyManagerRedux.Forestry.Full",
            "ColonyManagerRedux.Forestry.Full.Tip",
            p => IsTreeOfCategory(p, TreeCategory.Full)
        ),
        new(
            "ColonyManagerRedux.Forestry.Super",
            "ColonyManagerRedux.Forestry.Super.Tip",
            p => IsTreeOfCategory(p, TreeCategory.Super)
        ),
    ];

    internal static readonly ShortcutCategory<ThingDef>[] _clearAreaTreeShortcuts =
    [
        new(
            "ColonyManagerRedux.Forestry.Trees",
            "ColonyManagerRedux.Forestry.Trees.Tip",
            IsWoodTree
        ),
        new(
            "ColonyManagerRedux.Forestry.Flammable",
            "ColonyManagerRedux.Forestry.Flammable.Tip",
            IsFlammable
        ),
        new("ColonyManagerRedux.Forestry.Ugly", "ColonyManagerRedux.Forestry.Ugly.Tip", IsUgly),
        new(
            "ColonyManagerRedux.Forestry.ProvidesCover",
            "ColonyManagerRedux.Forestry.ProvidesCover.Tip",
            ProvidesCover
        ),
    ];

    public float DrawTreeShortcuts(Vector2 pos, float width)
    {
        var allTrees = SelectedForestryJob.AllPlants;
        var allowedTrees = SelectedForestryJob.AllowedTrees;
        void SetAllowed(ThingDef tree, bool allow)
        {
            SelectedForestryJob.SetTreeAllowed(tree, allow);
        }

        var height = DrawShortcutRows(
            pos,
            width,
            allTrees,
            allowedTrees,
            SetAllowed,
            _treeShortcuts
        );

        if (SelectedForestryJob.Type == ForestryJobType.ClearArea)
        {
            pos.y += height;
            height += DrawShortcutRows(
                pos,
                width,
                allTrees,
                allowedTrees,
                SetAllowed,
                _clearAreaTreeShortcuts,
                includeAllRow: false
            );
        }

        return height;
    }

    public override void PostClose() => Refresh();

    public override void PreOpen() => Refresh();

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
