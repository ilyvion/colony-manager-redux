// ManagerTab_Production.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;
using TabRecord = ilyvion.Laboratory.UI.TabRecord;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerTab_Production(Manager manager)
    : ManagerTab<ManagerJob_Production, ManagerSettings_Production>(manager)
{
    private const string ProductionOptions = "Production.Options";
    private const float RecipeRowHeight = 42f;

    public ManagerJob_Production SelectedProductionJob => SelectedJob!;

    protected override bool CreateNewSelectedJobOnMake => false;
    protected override bool ShouldHaveNewJobButton => false;

    private readonly List<RecipeDef> _availableRecipes = [];
    private readonly QuickSearchWidget _quickSearchWidget = new();
    private List<RecipeDef> _visibleRecipes = [];
    private readonly ScrollViewStatus _availableScrollViewStatus = new();

    public override void PreOpen() => Refresh();

    protected override void Refresh()
    {
        var recipesInUse = Manager
            .JobTracker.JobsOfType<ManagerJob_Production>()
            .Select(job => job.Recipe)
            .ToHashSet();

        // Only offer recipes that can actually be worked right now; a recipe whose work table
        // hasn't been built yet would just sit idle. Jobs for such recipes can still be set up
        // via job import (e.g. from a template), which bypasses this picker entirely.
        var builtWorkTableDefs = Manager
            .map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>()
            .Select(wt => wt.def)
            .ToHashSet();

        _availableRecipes.Clear();
        _availableRecipes.AddRange(
            DefDatabase<RecipeDef>
                .AllDefsListForReading.Where(r =>
                    r.ProducedThingDef != null
                    && !recipesInUse.Contains(r)
                    && r.AllRecipeUsers.Any(builtWorkTableDefs.Contains)
                )
                .OrderBy(r => r.LabelCap.ToString(), StringComparer.OrdinalIgnoreCase)
        );
        UpdateVisibleRecipes();
    }

    protected override void PostSelect()
    {
        if (Selected != null)
        {
            _currentTab = TabList[1].Tab;
        }
    }

    private Tab? _currentTab;
    private List<TabRecord> TabList =>
        field ??= [
            new TabRecord(new AvailableTab(this), () => ref _currentTab!),
            new TabRecord(new CurrentTab(base.DoJobList), () => ref _currentTab!),
        ];

    protected override void DoJobList(Rect rect)
    {
        rect.yMin += 31f;

        _currentTab ??= TabList[0].Tab;

        using (GUIScope.WidgetGroup(rect))
        {
            _currentTab.DoTabContents(rect.AtZero());
        }

        _ = TabDrawer.DrawTabs(rect, TabList);
    }

    private sealed class CurrentTab(Action<Rect> doJobList) : Tab
    {
        public override string Title => "ColonyManagerRedux.Thresholds.Current".Translate();

        public override void DoTabContents(Rect inRect) => doJobList(inRect);
    }

    private sealed class AvailableTab(ManagerTab_Production managerTab) : Tab
    {
        public override string Title => "ColonyManagerRedux.Thresholds.Available".Translate();

        public override void DoTabContents(Rect inRect) =>
            managerTab.DrawAvailableRecipeList(inRect);
    }

    private void UpdateVisibleRecipes() =>
        _visibleRecipes = _quickSearchWidget.filter.Active
            ?
            [
                .. _availableRecipes.Where(r =>
                    _quickSearchWidget.filter.Matches(r.LabelCap.ToString())
                ),
            ]
            : _availableRecipes;

    private void DrawAvailableRecipeList(Rect rect)
    {
        Widgets.DrawMenuSection(rect);

        var searchRect = new Rect(
            Margin,
            Margin,
            rect.width - (2 * Margin),
            QuickSearchWidget.WidgetHeight
        );
        _quickSearchWidget.OnGUI(searchRect, UpdateVisibleRecipes);

        var listRect = new Rect(
            0f,
            searchRect.yMax + Margin,
            rect.width,
            rect.height - searchRect.yMax - Margin
        );

        using var scrollView = GUIScope.ScrollView(listRect, _availableScrollViewStatus);
        var cur = Vector2.zero;
        for (var i = 0; i < _visibleRecipes.Count; i++)
        {
            var recipe = _visibleRecipes[i];
            var rowRect = new Rect(0f, cur.y, scrollView.ViewRect.width, RecipeRowHeight);

            if (!scrollView.CanCull(rowRect.height, rowRect.y))
            {
                if (i % 2 == 0)
                {
                    Widgets.DrawAltRect(rowRect);
                }
                Widgets.DrawHighlightIfMouseover(rowRect);

                if (Widgets.ButtonInvisible(rowRect))
                {
                    var job = (ManagerJob_Production)MakeNewJob()!;
                    job.Recipe = recipe;
                    Selected = job;
                }

                var workstations = recipe
                    .AllRecipeUsers.Select(td => td.LabelCap.ToString())
                    .ToCommaList();
                var labelRect = new Rect(
                    rowRect.x + Margin,
                    rowRect.y,
                    rowRect.width - (2 * Margin),
                    rowRect.height
                );
                IlyvionWidgets.Label(
                    labelRect,
                    $"{recipe.LabelCap}\n<i>{workstations}</i>",
                    TextAnchor.MiddleLeft
                );
            }

            cur.y += RecipeRowHeight;
        }

        if (_visibleRecipes.Count == 0)
        {
            Widgets.Label(
                new Rect(Margin, cur.y, scrollView.ViewRect.width - (2 * Margin), RecipeRowHeight),
                "ColonyManagerRedux.Production.NoRecipesFound".Translate()
            );
            cur.y += RecipeRowHeight;
        }

        if (Event.current.type == EventType.Layout)
        {
            scrollView.Height = cur.y;
        }
    }

    protected override void DoMainContent(Rect rect)
    {
        Widgets.DrawMenuSection(rect);

        var optionsColumnRect = new Rect(
            rect.xMin,
            rect.yMin,
            rect.width,
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
            ProductionOptions,
            out var position,
            out var width
        );
        DrawSection(
            ProductionOptions,
            "Recipe",
            ref position,
            width,
            DrawRecipeInfo,
            "ColonyManagerRedux.Production.Recipe".Translate()
        );
        DrawSection(
            ProductionOptions,
            "Threshold",
            ref position,
            width,
            DrawThreshold,
            "ColonyManagerRedux.Threshold".Translate()
        );
        DrawSection(ProductionOptions, "Status", ref position, width, DrawStatus);
        Widgets_Section.EndSectionColumn(ProductionOptions, position);

        if (!SelectedProductionJob.IsManaged)
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.Common.Manage".Translate()))
            {
                SelectedProductionJob.IsManaged = true;
                Manager.JobTracker.Add(SelectedProductionJob);
                Refresh();
            }
        }
        else
        {
            if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.Common.Delete".Translate()))
            {
                Manager.JobTracker.Delete(SelectedProductionJob);
                Selected = null;
                _currentTab = TabList[0].Tab;
                Refresh();
            }
        }
    }

    private static float DrawRecipeInfo(ManagerJob_Production job, Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Widgets.Label(rowRect, job.Recipe!.LabelCap);
        return ListEntryHeight;
    }

    private static float DrawThreshold(ManagerJob_Production job, Vector2 pos, float width)
    {
        var start = pos;
        job.TriggerThreshold.DrawTriggerConfig(ref pos, width, ListEntryHeight, targets: []);
        return pos.y - start.y;
    }

    private static float DrawStatus(ManagerJob_Production job, Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Widgets.Label(
            rowRect,
            "ColonyManagerRedux.Production.ManagedBillCount".Translate(job.ManagedBills.Count)
        );
        return ListEntryHeight;
    }
}
