// ManagerTab_Production.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ilyvion.Laboratory.Extensions;
using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;
using TabRecord = ilyvion.Laboratory.UI.TabRecord;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerTab_Production(Manager manager)
    : ManagerTab<ManagerJob_Production, ManagerSettings_Production>(manager)
{
    private const string ProductionOptions = "Production.Options";
    private const string ProductionIngredients = "Production.Ingredients";
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
                    RecipeProductResolvers.ResolverFor(r) != null
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
            rect.width * 3 / 5f,
            rect.height - Margin - ButtonSize.y
        );
        var ingredientsColumnRect = new Rect(
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
            "Mode",
            ref position,
            width,
            DrawModeSelector,
            "ColonyManagerRedux.Production.Mode".Translate()
        );
        DrawSection(
            ProductionOptions,
            "Threshold",
            ref position,
            width,
            DrawThreshold,
            "ColonyManagerRedux.Threshold".Translate()
        );
        DrawSection(
            ProductionOptions,
            "WorkbenchScope",
            ref position,
            width,
            DrawWorkbenchScope,
            "ColonyManagerRedux.Production.WorkbenchScope".Translate()
        );
        DrawSection(
            ProductionOptions,
            "JobSettings",
            ref position,
            width,
            DrawJobSettings,
            "ColonyManagerRedux.Production.JobSettings".Translate()
        );
        DrawSection(ProductionOptions, "Status", ref position, width, DrawStatus);
        Widgets_Section.EndSectionColumn(ProductionOptions, position);

        Widgets_Section.BeginSectionColumn(
            ingredientsColumnRect,
            ProductionIngredients,
            out var ingredientsPosition,
            out var ingredientsWidth
        );
        DrawSection(
            ProductionIngredients,
            "Ingredients",
            ref ingredientsPosition,
            ingredientsWidth,
            DrawIngredientShortcuts,
            "ColonyManagerRedux.Production.Ingredients".Translate()
        );
        DrawSection(
            ProductionIngredients,
            "IngredientsList",
            ref ingredientsPosition,
            ingredientsWidth,
            DrawIngredientList
        );
        Widgets_Section.EndSectionColumn(ProductionIngredients, ingredientsPosition);

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

    // Same layout/widget as DrawAssignmentModeSelector: one DrawToggle cell per enum value, in a
    // single row.
    private static float DrawModeSelector(ManagerJob_Production job, Vector2 pos, float width)
    {
        var start = pos;

        var modes = (ManagerJob_Production.ProductionMode[])
            Enum.GetValues(typeof(ManagerJob_Production.ProductionMode));
        var cellWidth = width / modes.Length;
        var cellRect = new Rect(pos.x, pos.y, cellWidth, ListEntryHeight);

        foreach (var mode in modes)
        {
            Utilities.DrawToggle(
                cellRect,
                $"ColonyManagerRedux.Production.Mode.{mode}".Translate(),
                $"ColonyManagerRedux.Production.Mode.{mode}.Tip".Translate(),
                job.Mode == mode,
                () => job.Mode = mode,
                () => { },
                wrap: false
            );
            cellRect.x += cellWidth;
        }

        pos.y += ListEntryHeight;
        return pos.y - start.y;
    }

    private static float DrawThreshold(ManagerJob_Production job, Vector2 pos, float width)
    {
        var start = pos;

        if (job.Mode == ManagerJob_Production.ProductionMode.MaintainStock)
        {
            DrawThresholdReadOnly(job, ref pos, width);
        }
        else
        {
            job.TriggerThreshold.DrawTriggerConfig(ref pos, width, ListEntryHeight, targets: []);
        }

        return pos.y - start.y;
    }

    // MaintainStock mode auto-derives the trigger filter from the recipe (see
    // ConfigureThresholdTriggerFilter), so unlike the full DrawTriggerConfig widget used in
    // ConsumeSurplus mode, this doesn't expose the cog icon that would let a player override
    // that auto-derived filter directly, nor the now-irrelevant "allow any threshold" toggle —
    // only the parts that stay meaningful when trigger == output: the current/target readout,
    // the target-count slider, and the count-all-on-map toggle.
    private static void DrawThresholdReadOnly(
        ManagerJob_Production job,
        ref Vector2 pos,
        float width
    )
    {
        var trigger = job.TriggerThreshold;

        var labelRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        var label =
            "ColonyManagerRedux.Thresholds.ThresholdCount".Translate(
                trigger.GetCurrentCount(),
                trigger.TargetLabel
            ) + ":";
        var tooltip = "ColonyManagerRedux.Thresholds.ThresholdCountTooltip".Translate(
            trigger.GetCurrentCount(),
            trigger.TargetLabel
        );
        IlyvionWidgets.Label(labelRect, label, tooltip, TextAnchor.MiddleLeft);
        pos.y += ListEntryHeight;

        var sliderRect = new Rect(pos.x, pos.y, width, SliderHeight);
        pos.y += SliderHeight;
        trigger.TargetCount = (int)
            Widgets.HorizontalSlider(sliderRect, trigger.TargetCount, 0, trigger.MaxUpperThreshold);

        var countAllOnMapRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        pos.y += ListEntryHeight;
        var countAllOnMap = trigger.CountAllOnMap;
        Utilities.DrawToggle(
            countAllOnMapRect,
            "ColonyManagerRedux.Threshold.CountAllOnMap".Translate(),
            "ColonyManagerRedux.Threshold.CountAllOnMap.Tip".Translate(),
            ref countAllOnMap,
            true
        );
        trigger.CountAllOnMap = countAllOnMap;
    }

    // Which raw materials managed bills are actually allowed to consume (Bill.ingredientFilter),
    // as opposed to DrawThreshold's filter (what's counted toward the trigger). The sync toggle
    // is only shown in ConsumeSurplus mode: in MaintainStock the threshold filter is output-typed
    // and has no relationship to ingredients at all, so syncing wouldn't mean anything there.
    // Kept in its own section, separate from DrawIngredientList, matching Foraging's
    // DrawPlantShortcuts/DrawPlantList split.
    private static float DrawIngredientShortcuts(
        ManagerJob_Production job,
        Vector2 pos,
        float width
    )
    {
        var start = pos;

        if (job.Mode == ManagerJob_Production.ProductionMode.ConsumeSurplus)
        {
            Utilities.DrawToggle(
                ref pos,
                width,
                "ColonyManagerRedux.SyncFilterAndAllowed".Translate(),
                "ColonyManagerRedux.Production.SyncFilterAndAllowed.Tip".Translate(),
                ref job.SyncFilterAndAllowed
            );
        }

        var allIngredients = ManagerJob_Production.AllRecipeIngredientOptions(job.Recipe!).ToList();

        void SetAllowed(ThingDef thingDef, bool allow)
        {
            job.SetIngredientAllowed(thingDef, allow);
        }

        var shortcutRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        DrawShortcutToggle(
            allIngredients,
            job.AllowedIngredients,
            SetAllowed,
            shortcutRect,
            "ColonyManagerRedux.Shortcuts.All",
            null
        );
        pos.y += ListEntryHeight;

        foreach (
            var category in ManagerJob_Production
                .GroupIngredientsByCategory(allIngredients)
                .OrderBy(g => g.Key.LabelCap.Resolve(), StringComparer.OrdinalIgnoreCase)
        )
        {
            shortcutRect.y = pos.y;
            DrawShortcutToggle(
                [.. category],
                job.AllowedIngredients,
                SetAllowed,
                shortcutRect,
                category.Key.LabelCap
            );
            pos.y += ListEntryHeight;
        }

        return pos.y - start.y;
    }

    private static float DrawIngredientList(ManagerJob_Production job, Vector2 pos, float width) =>
        Utilities.DrawToggleDefList(
            pos,
            width,
            ManagerJob_Production.AllRecipeIngredientOptions(job.Recipe!),
            job.AllowedIngredients.Contains,
            (thingDef, allow) => job.SetIngredientAllowed(thingDef, allow),
            thingDef => thingDef.LabelCap,
            thingDef => (TipSignal)thingDef.LabelCap,
            (rect, thingDef) => Widgets.InfoCardButton(rect, thingDef)
        );

    // Combines skill range, ingredient radius and store mode into a single section instead of
    // one section per setting; each sub-widget already carries its own inline label, so a
    // section header per setting was just wasted vertical space.
    private static float DrawJobSettings(ManagerJob_Production job, Vector2 pos, float width)
    {
        var start = pos;

        if (job.Recipe?.workSkill != null)
        {
            pos.y += DrawSkillRange(job, pos, width);
        }
        pos.y += DrawIngredientRadius(job, pos, width);
        pos.y += DrawStoreMode(job, pos, width);

        return pos.y - start.y;
    }

    private static float DrawSkillRange(ManagerJob_Production job, Vector2 pos, float width)
    {
        var start = pos;
        var workSkill = job.Recipe!.workSkill!;

        var labelRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Widgets.Label(
            labelRect,
            "ColonyManagerRedux.Production.SkillRange.Label".Translate(workSkill.LabelCap)
        );
        pos.y += ListEntryHeight;

        var rangeRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Widgets.IntRange(rangeRect, job.GetHashCode(), ref job.AllowedSkillRange, 0, 20);
        pos.y += ListEntryHeight;

        return pos.y - start.y;
    }

    // Mirrors vanilla Dialog_BillConfig.DoIngredientConfigPane's radius sub-widget: a label
    // showing the current radius (or "Unlimited" once it hits 999) above a 3-100 slider that
    // snaps to unlimited at its top end.
    private static float DrawIngredientRadius(ManagerJob_Production job, Vector2 pos, float width)
    {
        var start = pos;

        var labelRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        var valueLabel =
            job.IngredientSearchRadius >= 999f
                ? "Unlimited".Translate().ToString()
                : job.IngredientSearchRadius.ToString("F0", CultureInfo.InvariantCulture);
        Widgets.Label(labelRect, "IngredientSearchRadius".Translate() + ": " + valueLabel);
        pos.y += ListEntryHeight;

        var sliderRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        var displayedRadius = job.IngredientSearchRadius > 100f ? 100f : job.IngredientSearchRadius;
        var newRadius = Widgets.HorizontalSlider(sliderRect, displayedRadius, 3f, 100f);
        job.IngredientSearchRadius = newRadius >= 100f ? 999f : newRadius;
        pos.y += ListEntryHeight;

        return pos.y - start.y;
    }

    // Mirrors vanilla Dialog_BillConfig.DoWindowContents' store-mode button + FloatMenu, and
    // FillOutputDropdownOptions/FillSlotGroupOptions/ShouldCollapseGroup for populating it with
    // every stockpile zone/storage building/storage group on the map.
    private static float DrawStoreMode(ManagerJob_Production job, Vector2 pos, float width)
    {
        var start = pos;

        var buttonRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        var groupLabel = job.StoreGroup != null ? SlotGroup.GetGroupLabel(job.StoreGroup) : "";
        var label = string.Format(CultureInfo.InvariantCulture, job.StoreMode.LabelCap, groupLabel);
        if (Widgets.ButtonText(buttonRect, label))
        {
            Find.WindowStack.Add(new FloatMenu(BuildStoreModeOptions(job)));
        }
        pos.y += ListEntryHeight;

        return pos.y - start.y;
    }

    private static List<FloatMenuOption> BuildStoreModeOptions(ManagerJob_Production job)
    {
        var opts = new List<FloatMenuOption>();
        foreach (
            var storeModeDef in DefDatabase<BillStoreModeDef>.AllDefsListForReading.OrderBy(sm =>
                sm.listOrder
            )
        )
        {
            if (storeModeDef == BillStoreModeDefOf.SpecificStockpile)
            {
                FillSpecificStockpileOptions(job, opts);
                continue;
            }

            var storeModeDefLocal = storeModeDef;
            opts.Add(
                new FloatMenuOption(
                    storeModeDefLocal.LabelCap,
                    () =>
                    {
                        job.StoreMode = storeModeDefLocal;
                        job.StoreGroup = null;
                    }
                )
            );
        }
        return opts;
    }

    private static void FillSpecificStockpileOptions(
        ManagerJob_Production job,
        List<FloatMenuOption> opts
    )
    {
        var prefix = BillStoreModeDefOf.SpecificStockpile.LabelCap;
        var groupsByLabel = new Dictionary<string, List<ISlotGroup>>();
        foreach (
            var slotGroup in job.Manager.map.haulDestinationManager.AllGroupsListInPriorityOrder
        )
        {
            if (slotGroup.StorageGroup != null)
            {
                var storageGroup = slotGroup.StorageGroup;
                if (!groupsByLabel.TryGetValue(storageGroup.GroupingLabel, out var list))
                {
                    groupsByLabel[storageGroup.GroupingLabel] = list = [];
                }
                if (!list.Contains(storageGroup))
                {
                    list.Add(storageGroup);
                }
            }
            else if (slotGroup.parent is not Building_Storage or IRenameable)
            {
                if (!groupsByLabel.TryGetValue(slotGroup.GroupingLabel, out var list))
                {
                    groupsByLabel[slotGroup.GroupingLabel] = list = [];
                }
                list.Add(slotGroup);
            }
        }

        var orderedGroups = groupsByLabel
            .OrderBy(kvp => kvp.Value.Count > 0 ? kvp.Value[0].GroupingOrder : 0)
            .ToList();
        foreach (var (label, groups) in orderedGroups)
        {
            var collapse =
                groups.Count > 2
                && groupsByLabel.Any(kvp => kvp.Key != label && kvp.Value.Count > 0);
            if (collapse)
            {
                opts.Add(
                    new FloatMenuOption(
                        label,
                        () =>
                            Find.WindowStack.Add(
                                new FloatMenu(FillSlotGroupOptions(job, groups, prefix))
                            )
                    )
                );
            }
        }
        foreach (var (label, groups) in orderedGroups)
        {
            var collapse =
                groups.Count > 2
                && groupsByLabel.Any(kvp => kvp.Key != label && kvp.Value.Count > 0);
            if (!collapse)
            {
                opts.AddRange(FillSlotGroupOptions(job, groups, prefix));
            }
        }
    }

    private static List<FloatMenuOption> FillSlotGroupOptions(
        ManagerJob_Production job,
        List<ISlotGroup> groups,
        string prefix
    )
    {
        var opts = new List<FloatMenuOption>();
        foreach (var group in groups)
        {
            var groupLocal = group;
            var label = string.Format(
                CultureInfo.InvariantCulture,
                prefix,
                SlotGroup.GetGroupLabel(groupLocal)
            );

            if (!CanPossiblyStore(job, groupLocal))
            {
                opts.Add(
                    new FloatMenuOption(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} ({1})",
                            label,
                            "IncompatibleLower".Translate()
                        ),
                        null
                    )
                );
                continue;
            }

            opts.Add(
                new FloatMenuOption(
                    label,
                    () =>
                    {
                        job.StoreMode = BillStoreModeDefOf.SpecificStockpile;
                        job.StoreGroup = groupLocal;
                    }
                )
            );
        }
        return opts;
    }

    // Mirrors vanilla RecipeWorkerCounter.CanPossiblyStore: checks the slot group's storage
    // filter against whatever this job is actually tracking. In MaintainStock mode that's
    // whatever the recipe's registered RecipeProductResolver (see
    // Docs/ProductionManagerRework.md Step 4) says the recipe produces — a single ThingDef for
    // simple recipes, or every def in a category for e.g. butchery/stonecutting. A recipe with
    // no registered resolver at all can't currently be tracked by this job, so it's treated as
    // always compatible (matches vanilla's own fallback when CanCountProducts is false). In
    // ConsumeSurplus mode the resolver-derived filter is irrelevant (trigger and output aren't
    // the same thing), so this checks the job's own, independently configured
    // TriggerThreshold.ThresholdFilter instead.
    private static bool CanPossiblyStore(ManagerJob_Production job, ISlotGroup slotGroup)
    {
        if (job.Mode != ManagerJob_Production.ProductionMode.MaintainStock)
        {
            return job.TriggerThreshold.ThresholdFilter.AllowedThingDefs.Any(
                slotGroup.Settings.AllowedToAccept
            );
        }

        if (RecipeProductResolvers.ResolverFor(job.Recipe!) is not { } resolver)
        {
            return true;
        }

        var filter = new ThingFilter();
        resolver.ConfigureFilter(job.Recipe!, filter);
        return filter.AllowedThingDefs.Any(slotGroup.Settings.AllowedToAccept);
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

    private static float DrawWorkbenchScope(ManagerJob_Production job, Vector2 pos, float width)
    {
        var start = pos;

        DrawAssignmentModeSelector(job, ref pos, width);

        switch (job.AssignmentMode)
        {
            case ManagerJob_Production.WorkbenchAssignmentMode.Area:
                AreaAllowedGUI.DoAllowedAreaSelectors(
                    ref pos,
                    width,
                    ref job.WorkbenchArea,
                    5,
                    job.Manager
                );
                Utilities.DrawToggle(
                    ref pos,
                    width,
                    "ColonyManagerRedux.InvertArea".Translate(),
                    "ColonyManagerRedux.InvertArea.Tip".Translate(),
                    ref job.InvertWorkbenchArea
                );
                break;
            case ManagerJob_Production.WorkbenchAssignmentMode.Specific:
                pos.y += DrawSpecificWorkbenches(job, pos, width);
                break;
            case ManagerJob_Production.WorkbenchAssignmentMode.All:
            default:
                break;
        }

        return pos.y - start.y;
    }

    // Same layout/widget as ManagerTab_Forestry.DrawJobType's "Clear areas | Wood logging"
    // selector: one DrawToggle cell per enum value, in a single row.
    private static void DrawAssignmentModeSelector(
        ManagerJob_Production job,
        ref Vector2 pos,
        float width
    )
    {
        var modes = (ManagerJob_Production.WorkbenchAssignmentMode[])
            Enum.GetValues(typeof(ManagerJob_Production.WorkbenchAssignmentMode));
        var cellWidth = width / modes.Length;
        var cellRect = new Rect(pos.x, pos.y, cellWidth, ListEntryHeight);

        foreach (var mode in modes)
        {
            Utilities.DrawToggle(
                cellRect,
                $"ColonyManagerRedux.Production.WorkbenchScope.{mode}".Translate(),
                $"ColonyManagerRedux.Production.WorkbenchScope.{mode}.Tip".Translate(),
                job.AssignmentMode == mode,
                () => job.AssignmentMode = mode,
                () => { },
                wrap: false
            );
            cellRect.x += cellWidth;
        }

        pos.y += ListEntryHeight;
    }

    private static float DrawSpecificWorkbenches(
        ManagerJob_Production job,
        Vector2 pos,
        float width
    )
    {
        var workTables = job.AllEligibleWorkTables.ToList();

        if (workTables.Count == 0)
        {
            var emptyRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
            Widgets.Label(
                emptyRect,
                "ColonyManagerRedux.Production.WorkbenchScope.NoEligibleWorkbenches".Translate()
            );
            return ListEntryHeight;
        }

        var start = pos;
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        foreach (var workTable in workTables)
        {
            if (!Widgets_Section.CanCull(rowRect.y, rowRect.height))
            {
                var allowed = job.SpecificWorkbenches.Contains(workTable);
                Utilities.DrawToggle(
                    rowRect,
                    workTable.LabelCap,
                    (TipSignal)workTable.LabelCap,
                    allowed,
                    () => _ = job.SpecificWorkbenches.Add(workTable),
                    () => _ = job.SpecificWorkbenches.Remove(workTable)
                );

                // Same "hover to pan/point the camera" behavior as the trigger's target
                // search FloatMenu (Trigger_Threshold.DrawTriggerConfig's onHover), just
                // applied to a persistent list row instead of a menu option.
                if (Mouse.IsOver(rowRect) && !Find.CameraDriver.IsPanning())
                {
                    CameraJumper.TryJump(workTable);
                }
            }

            rowRect.y += ListEntryHeight;
        }

        return rowRect.y - start.y;
    }
}
