// ManagerTab_Production.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using System.Text;
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
    private const string TargetCountControlName = "ManagerTab_Production_TargetCount";

    public ManagerJob_Production SelectedProductionJob => SelectedJob!;

    protected override bool CreateNewSelectedJobOnMake => false;
    protected override bool ShouldHaveNewJobButton => false;

    private readonly List<RecipeDef> _availableRecipes = [];
    private readonly QuickSearchWidget _quickSearchWidget = new();
    private List<RecipeDef> _visibleRecipes = [];
    private readonly ScrollViewStatus _availableScrollViewStatus = new();
    private string _targetCountInput = "";

    public override void PreOpen() => Refresh();

    protected override void Refresh()
    {
        // Only offer recipes that can actually be worked right now; a recipe whose work table
        // hasn't been built yet would just sit idle. Jobs for such recipes can still be set up
        // via job import (e.g. from a template), which bypasses this picker entirely.
        //
        // A recipe already claimed by a job is still offered: a job's Mode/threshold give it a
        // distinct purpose (e.g. one MaintainStock job keeping steel knives topped up alongside a
        // separate ConsumeSurplus job turning excess cotton into dusters from the same recipe),
        // and bill ownership/reconciliation is fully job-scoped (see ManagerJob_Production's
        // _managedBills), so nothing here depends on recipes being claimed by at most one job.
        var builtWorkTableDefs = Manager
            .map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>()
            .Select(wt => wt.def)
            .ToHashSet();

        _availableRecipes.Clear();
        _availableRecipes.AddRange(
            DefDatabase<RecipeDef>
                .AllDefsListForReading.Where(r =>
                    RecipeProductResolvers.ResolverFor(r) != null
                    && r.AllRecipeUsers.Any(builtWorkTableDefs.Contains)
                    // Same gate work tables themselves use (research/ideology/faction
                    // prerequisites) - a recipe that isn't actually addable as a bill yet
                    // shouldn't be offered here either.
                    && r.AvailableNow
                )
                .OrderBy(r => r.LabelCap.ToString(), StringComparer.OrdinalIgnoreCase)
        );
        UpdateVisibleRecipes();
    }

    // Multiple jobs can now target the same recipe (see Refresh's picker filter above), so the
    // default recipe-only sub-label (job.TargetsLabel) is no longer enough to tell them apart in
    // the job list. Append the job's mode and target count (just the number, matching the
    // "no numbers" at-a-glance level of detail other job types' sub-labels use).
    public override string GetSubLabel(ManagerJob job)
    {
        var productionJob = (ManagerJob_Production)job;
        var subLabel = base.GetSubLabel(job);
        subLabel +=
            $" | {$"ColonyManagerRedux.Production.Mode.{productionJob.Mode}".Translate()}"
            + (
                productionJob.Mode == ManagerJob_Production.ProductionMode.MaintainStock
                    ? $" ({productionJob.TriggerThreshold.TargetCount})"
                    : $" ({productionJob.TriggerThreshold.OpString} {productionJob.TriggerThreshold.TargetCount})"
            );
        return subLabel;
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
        // Only added when there's actually a link to show, avoiding an empty section otherwise.
        if (
            SelectedProductionJob.LinkedProducers.Count > 0
            || SelectedProductionJob.AutoTargetFromLinks
            || ComputeLinkedConsumerDemands(SelectedProductionJob).Count > 0
        )
        {
            DrawSection(
                ProductionOptions,
                "LinkedJobs",
                ref position,
                width,
                DrawLinkedJobs,
                "ColonyManagerRedux.Production.LinkedJobs".Translate()
            );
        }
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

    // Mirrors the level of detail vanilla's own Dialog_BillConfig shows for a recipe (icon,
    // description, work amount, ingredient requirements) — the plain recipe-name label this used
    // to be gave no way to tell recipes apart or judge them without leaving the tab.
    private float DrawRecipeInfo(ManagerJob_Production job, Vector2 pos, float width)
    {
        var start = pos;
        var recipe = job.Recipe!;

        // Mirrors Dialog_BillConfig's own icon pick (RecipeDef.UIIconThing/UIIcon) so the icon
        // shown here matches what players already see in the bill dialog; e.g. butchery recipes
        // set uiIconThing explicitly (to a nice cut of meat) rather than leaving it to fall back
        // to ProducedThingDef, which is null for them anyway. Only recipes with neither (mostly
        // other category-based resolvers, like stonecutting/smelting) fall back to the
        // resolver's own representative def.
        Def? iconDef =
            recipe.UIIconThing != null || recipe.UIIcon != null
                ? recipe
                : RecipeProductResolvers.RepresentativeThingDef(recipe);
        if (iconDef != null)
        {
            var iconRect = new Rect(pos.x, pos.y, RecipeRowHeight, RecipeRowHeight);
            Widgets.DefIcon(iconRect, iconDef);
            if (
                ((iconDef as RecipeDef)?.UIIconThing ?? iconDef as ThingDef) is { } infoCardDef
                && ColonyManagerReduxMod.Settings.ShowInfoCardButtonsWherePossible
            )
            {
                var infoRect = new Rect(
                    iconRect.xMax - SmallIconSize,
                    iconRect.yMax - SmallIconSize,
                    SmallIconSize,
                    SmallIconSize
                );
                _ = Widgets.InfoCardButton(infoRect, infoCardDef);
            }
        }

        var labelRect = new Rect(
            pos.x + RecipeRowHeight + Margin,
            pos.y,
            width - RecipeRowHeight - Margin,
            RecipeRowHeight
        );
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, recipe.LabelCap);
        Text.Anchor = TextAnchor.UpperLeft;
        pos.y += RecipeRowHeight + Margin;

        // Built as a single StringBuilder and drawn with one Label call, same as
        // Dialog_BillConfig does, so line spacing matches the bill dialog instead of leaving
        // large gaps from separately-positioned Label calls at ListEntryHeight.
        var text = new StringBuilder();
        if (!recipe.description.NullOrEmpty())
        {
            _ = text.AppendLine(recipe.description);
            _ = text.AppendLine();
        }

        _ = text.AppendLine(
            "WorkAmount".Translate() + ": " + recipe.WorkAmountTotal(null).ToStringWorkAmount()
        );
        _ = text.AppendLine("BillRequires".Translate() + ":");
        foreach (var ingredientCount in recipe.ingredients)
        {
            if (ingredientCount.filter.Summary.NullOrEmpty())
            {
                continue;
            }

            _ = text.AppendLine(
                " - "
                    + recipe.IngredientValueGetter.BillRequirementsDescription(
                        recipe,
                        ingredientCount
                    )
            );
        }

        var textString = text.ToString().TrimEnd();
        var textHeight = Text.CalcHeight(textString, width);
        Widgets.Label(new Rect(pos.x, pos.y, width, textHeight), textString);
        pos.y += textHeight;

        // Recipe swap: only meaningful in MaintainStock mode, where the trigger tracks the
        // recipe's own output — in ConsumeSurplus mode trigger and output are deliberately
        // unrelated, so "another recipe with the same output" isn't a meaningful notion there.
        if (job.Mode == ManagerJob_Production.ProductionMode.MaintainStock)
        {
            var swapCandidates = ComputeRecipeSwapCandidates(job);
            if (swapCandidates.Count > 0)
            {
                var swapRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
                if (
                    Widgets.ButtonText(
                        swapRect,
                        "ColonyManagerRedux.Production.OtherRecipesAvailable".Translate()
                    )
                )
                {
                    Find.WindowStack.Add(
                        new FloatMenu(BuildRecipeSwapOptions(job, swapCandidates))
                    );
                }
                TooltipHandler.TipRegion(
                    swapRect,
                    "ColonyManagerRedux.Production.OtherRecipesAvailable.Tip".Translate()
                );
                pos.y += ListEntryHeight;
            }
        }

        return pos.y - start.y;
    }

    // Candidate pool: recipes already offered in the Available tab (built, recipe-compatible
    // work table on the map, see Refresh()) whose resolver-derived output overlaps what this
    // job is currently tracking. Reuses that pool instead of a fresh DefDatabase scan.
    private List<RecipeDef> ComputeRecipeSwapCandidates(ManagerJob_Production job)
    {
        var currentOutputs = job.TriggerThreshold.ThresholdFilter.AllowedThingDefs;
        var candidates = new List<RecipeDef>();
        foreach (var candidate in _availableRecipes)
        {
            if (candidate == job.Recipe)
            {
                continue;
            }
            if (RecipeProductResolvers.ResolverFor(candidate) is not { } resolver)
            {
                continue;
            }

            var filter = new ThingFilter();
            resolver.ConfigureFilter(candidate, filter);
            if (ManagerJob_Production.RecipeSharesOutput(filter.AllowedThingDefs, currentOutputs))
            {
                candidates.Add(candidate);
            }
        }
        return candidates;
    }

    // Selecting an option is just job.Recipe = candidate — the setter already tears down and
    // reseeds everything recipe-derived (see ManagerJob_Production.Recipe's setter), leaving
    // the trigger's identity/history and all bill-config settings untouched.
    private static List<FloatMenuOption> BuildRecipeSwapOptions(
        ManagerJob_Production job,
        List<RecipeDef> candidates
    )
    {
        var opts = new List<FloatMenuOption>();
        foreach (
            var candidate in candidates.OrderBy(
                r => r.LabelCap.ToString(),
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            var candidateLocal = candidate;
            var workstations = candidateLocal
                .AllRecipeUsers.Select(td => td.LabelCap.ToString())
                .ToCommaList();
            opts.Add(
                new FloatMenuOption(
                    $"{candidateLocal.LabelCap} ({workstations})",
                    () => job.Recipe = candidateLocal
                )
            );
        }
        return opts;
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

    private float DrawThreshold(ManagerJob_Production job, Vector2 pos, float width)
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
    // the target-count slider plus an exact-value text field, and the count-all-on-map toggle.
    private void DrawThresholdReadOnly(ManagerJob_Production job, ref Vector2 pos, float width)
    {
        var trigger = job.TriggerThreshold;

        // The slider alone can only land on values a whole pixel apart, which is too coarse to
        // reliably hit an exact target — pair it with a click-to-type field, mirroring the
        // exact-count text field in WindowTriggerThresholdDetails (same re-sync-when-unfocused /
        // red-on-invalid-parse pattern). Placed where that dialog's cog icon would otherwise sit,
        // since that icon (and the dialog it opens) isn't available in MaintainStock mode.
        const float TargetCountFieldWidth = 60f;
        var labelRect = new Rect(
            pos.x,
            pos.y,
            width - TargetCountFieldWidth - Margin,
            ListEntryHeight
        );
        var targetCountFieldRect = new Rect(
            labelRect.xMax + Margin,
            pos.y,
            TargetCountFieldWidth,
            ListEntryHeight
        );
        // StatusTooltip already folds in "(+ N expected)" from ExpectedAdditionalCount when
        // there's a shortfall being worked on (see ManagerJob_Production.ExpectedAdditionalCount),
        // matching how Foraging/Mining/Forestry/Hunting surface expected yield in their own
        // custom labels.
        var label = trigger.StatusTooltip;
        var tooltip = "ColonyManagerRedux.Thresholds.ThresholdCountTooltip".Translate(
            trigger.GetCurrentCount(),
            trigger.TargetLabel
        );
        IlyvionWidgets.Label(labelRect, label, tooltip, TextAnchor.MiddleLeft);
        pos.y += ListEntryHeight;

        // While AutoTargetFromLinks is on, the target is recomputed from linked jobs' demand
        // every gather pass (see ManagerJob_Production.GatherJobDataCoroutine) - the slider and
        // text field are hidden rather than left editable-but-overwritten, since a player
        // typing a value only to see it silently reverted a moment later reads as broken, not
        // as "this field is computed."
        if (job.AutoTargetFromLinks)
        {
            var computedRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
            IlyvionWidgets.Label(
                computedRect,
                "ColonyManagerRedux.Production.AutoTargetFromLinks.ComputedTarget".Translate(
                    trigger.TargetCount
                ),
                "ColonyManagerRedux.Production.AutoTargetFromLinks.ComputedTarget.Tip".Translate(),
                TextAnchor.MiddleLeft
            );
            pos.y += ListEntryHeight;
        }
        else
        {
            var sliderRect = new Rect(pos.x, pos.y, width, SliderHeight);
            pos.y += SliderHeight;
            trigger.TargetCount = (int)
                Widgets.HorizontalSlider(
                    sliderRect,
                    trigger.TargetCount,
                    0,
                    trigger.MaxUpperThreshold
                );

            if (GUI.GetNameOfFocusedControl() != TargetCountControlName)
            {
                _targetCountInput = trigger.TargetCount.ToString(CultureInfo.InvariantCulture);
            }
            var oldColor = GUI.color;
            if (int.TryParse(_targetCountInput, out var typedTargetCount) && typedTargetCount >= 0)
            {
                trigger.TargetCount = typedTargetCount;
                if (trigger.TargetCount > trigger.MaxUpperThreshold)
                {
                    trigger.MaxUpperThreshold = trigger.TargetCount;
                }
            }
            else
            {
                GUI.color = new Color(1f, 0f, 0f);
            }
            GUI.SetNextControlName(TargetCountControlName);
            _targetCountInput = Widgets.TextField(targetCountFieldRect, _targetCountInput);
            GUI.color = oldColor;
        }

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

    // Job linking: a dedicated section so both directions of a link (a producer's "who consumes
    // me" list and a consumer's "who do I link to" list) get equal, uncramped space instead of
    // being squeezed into the Threshold section or the ingredient list, neither of which has
    // room for a multi-line row. Rendered right under Threshold since it's conceptually still
    // about "what target this job is aiming for," just broken out for space. Call site
    // (DoMainContent) only adds this section at all when there's something to show, avoiding an
    // empty section otherwise.
    private float DrawLinkedJobs(ManagerJob_Production job, Vector2 pos, float width)
    {
        var start = pos;

        if (job.LinkedProducers.Count > 0)
        {
            DrawLinkedProducers(job, ref pos, width);
        }

        DrawLinkedConsumers(job, ref pos, width);

        return pos.y - start.y;
    }

    // Consumer-side: how much of this job's own target a linked producer should keep enough
    // stock buffered for, plus every producer job this job currently links an ingredient to.
    private void DrawLinkedProducers(ManagerJob_Production job, ref Vector2 pos, float width)
    {
        if (job.Mode == ManagerJob_Production.ProductionMode.MaintainStock)
        {
            DrawLinkedDemandBufferCount(job, ref pos, width);
        }

        foreach (
            var producer in job.LinkedProducers.OrderBy(
                GetSubLabel,
                StringComparer.OrdinalIgnoreCase
            )
        )
        {
            var covered =
                producer.Recipe != null
                    ? job
                        .AllowedIngredients.Where(
                            ManagerJob_Production.ResolvedOutputDefs(producer.Recipe).Contains
                        )
                        .ToList()
                    : [];
            var (summary, tooltip) = SummarizeCoveredIngredients(covered);
            pos.y += DrawLinkRow(
                pos,
                width,
                producer,
                "ColonyManagerRedux.Production.LinkedRow.Supplies".Translate(summary),
                tooltip
            );
        }
    }

    // How many of this job's own product a linked producer should keep enough ingredient stock
    // to build from empty (ManagerJob_Production.LinkedDemandBufferCount) — clamped to
    // [1, TargetCount] here purely for slider bounds; the field itself is clamped at read time
    // instead (see that field's own doc comment), so this doesn't need to write back a clamped
    // value on every frame.
    private static void DrawLinkedDemandBufferCount(
        ManagerJob_Production job,
        ref Vector2 pos,
        float width
    )
    {
        var maxCount = Math.Max(1, job.TriggerThreshold.TargetCount);
        var bufferCount = Math.Min(Math.Max(1, job.LinkedDemandBufferCount), maxCount);

        var labelRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        IlyvionWidgets.Label(
            labelRect,
            "ColonyManagerRedux.Production.LinkedDemandBufferCount".Translate(bufferCount),
            "ColonyManagerRedux.Production.LinkedDemandBufferCount.Tip".Translate(),
            TextAnchor.MiddleLeft
        );
        pos.y += ListEntryHeight;

        var sliderRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        job.LinkedDemandBufferCount = (int)
            Widgets.HorizontalSlider(sliderRect, bufferCount, 1, maxCount);
        pos.y += ListEntryHeight;
    }

    // Names beyond this many are collapsed into "and N more" inline, with the full list moved
    // to the row's tooltip instead — a covered-ingredient list is very often "every allowed meat
    // type" or similar, and spelling all of them out inline was the single biggest source of the
    // section reading as an unreadable wall of text.
    private const int MaxLinkedIngredientNamesShown = 3;

    private static (string Summary, string? Tooltip) SummarizeCoveredIngredients(
        List<ThingDef> covered
    )
    {
        var names = covered.Select(t => t.LabelCap.ToString()).ToList();
        if (names.Count <= MaxLinkedIngredientNamesShown)
        {
            return (names.ToCommaList(), null);
        }

        var shown = names.Take(MaxLinkedIngredientNamesShown).ToCommaList();
        var summary = "ColonyManagerRedux.Production.LinkedRow.SuppliesMore".Translate(
            shown,
            names.Count - MaxLinkedIngredientNamesShown
        );
        return (summary, names.ToCommaList());
    }

    // Producer-side: the auto-target toggle/aggregation choice, plus every job currently linked
    // to consume this job's output, each paired with the demand it's contributing.
    private void DrawLinkedConsumers(ManagerJob_Production job, ref Vector2 pos, float width)
    {
        var consumerDemands = ComputeLinkedConsumerDemands(job);
        // Kept visible even with zero current consumers as long as AutoTargetFromLinks is
        // already on, so a job that lost all its linked consumers (e.g. they were deleted)
        // still has a way to turn the toggle back off instead of getting stuck silently
        // targeting zero with no UI to fix it.
        if (consumerDemands.Count == 0 && !job.AutoTargetFromLinks)
        {
            return;
        }

        var toggleRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(
            toggleRect,
            "ColonyManagerRedux.Production.AutoTargetFromLinks".Translate(),
            "ColonyManagerRedux.Production.AutoTargetFromLinks.Tip".Translate(),
            ref job.AutoTargetFromLinks
        );
        pos.y += ListEntryHeight;

        var restrictToggleRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Utilities.DrawToggle(
            restrictToggleRect,
            "ColonyManagerRedux.Production.AutoRestrictIngredientsFromLinks".Translate(),
            "ColonyManagerRedux.Production.AutoRestrictIngredientsFromLinks.Tip".Translate(),
            ref job.AutoRestrictIngredientsFromLinks
        );
        pos.y += ListEntryHeight;

        if (job.AutoTargetFromLinks)
        {
            var modes = (ManagerJob_Production.LinkedDemandAggregation[])
                Enum.GetValues(typeof(ManagerJob_Production.LinkedDemandAggregation));
            var cellWidth = width / modes.Length;
            var cellRect = new Rect(pos.x, pos.y, cellWidth, ListEntryHeight);
            foreach (var mode in modes)
            {
                Utilities.DrawToggle(
                    cellRect,
                    $"ColonyManagerRedux.Production.DemandAggregation.{mode}".Translate(),
                    $"ColonyManagerRedux.Production.DemandAggregation.{mode}.Tip".Translate(),
                    job.DemandAggregation == mode,
                    () => job.DemandAggregation = mode,
                    () => { },
                    wrap: false
                );
                cellRect.x += cellWidth;
            }
            pos.y += ListEntryHeight;
        }

        foreach (var (consumer, coveredIngredients, demand) in consumerDemands)
        {
            var (summary, tooltip) = SummarizeCoveredIngredients(coveredIngredients);
            pos.y += DrawLinkRow(
                pos,
                width,
                consumer,
                "ColonyManagerRedux.Production.LinkedRow.SuppliesWithDemand".Translate(
                    summary,
                    demand
                ),
                tooltip
            );
        }
    }

    // Two-line row (job sub-label + italic detail) mirroring DrawAvailableRecipeList's own row
    // style, shared by both directions of a job link so they read as one visual idiom instead of
    // two competing single-line summaries. An optional tooltip carries the full, untruncated
    // ingredient list when SummarizeCoveredIngredients has collapsed it for the inline detail.
    private float DrawLinkRow(
        Vector2 pos,
        float width,
        ManagerJob target,
        string detail,
        string? tooltip = null
    )
    {
        var text = $"{GetSubLabel(target)}\n<i>{detail}</i>";
        var height = Text.CalcHeight(text, width);
        var rowRect = new Rect(pos.x, pos.y, width, height);
        Widgets.DrawHighlightIfMouseover(rowRect);
        if (tooltip != null)
        {
            TooltipHandler.TipRegion(rowRect, tooltip);
        }
        if (Widgets.ButtonInvisible(rowRect))
        {
            Selected = target;
        }
        IlyvionWidgets.Label(rowRect, text, TextAnchor.UpperLeft);
        return height;
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

    // Candidate recipes that could supply the given ingredient, reusing _availableRecipes
    // (already "has a resolver and a built work table", see Refresh()) the same way
    // ComputeRecipeSwapCandidates does — just checking a single ThingDef instead of
    // set-intersecting two filters.
    private List<RecipeDef> ComputeIngredientSourceCandidates(ThingDef ingredient)
    {
        var candidates = new List<RecipeDef>();
        foreach (var candidate in _availableRecipes)
        {
            if (ManagerJob_Production.ResolvedOutputDefs(candidate).Contains(ingredient))
            {
                candidates.Add(candidate);
            }
        }
        return candidates;
    }

    // Existing MaintainStock jobs (other than the consumer itself, and excluding any already
    // linked - those are offered as "unlink" instead) whose resolved output includes the given
    // ingredient and wouldn't create a cycle if linked to.
    private List<ManagerJob_Production> ComputeExistingProducerCandidates(
        ManagerJob_Production consumer,
        ThingDef ingredient
    )
    {
        var candidates = new List<ManagerJob_Production>();
        foreach (var job in Manager.JobTracker.JobsOfType<ManagerJob_Production>())
        {
            if (
                job == consumer
                || job.Mode != ManagerJob_Production.ProductionMode.MaintainStock
                || job.Recipe == null
                || consumer.LinkedProducers.Contains(job)
                || !ManagerJob_Production.ResolvedOutputDefs(job.Recipe).Contains(ingredient)
            )
            {
                continue;
            }

            if (ManagerJob_Production.WouldCreateCycle(consumer, job, j => j.LinkedProducers))
            {
                continue;
            }

            candidates.Add(job);
        }
        return candidates;
    }

    // FloatMenu idiom, same shape as BuildRecipeSwapOptions/BuildStoreModeOptions: unlink any
    // currently-linked producer that covers this ingredient, link to an existing eligible job
    // (job-level - covers every currently-allowed ingredient that job produces, not just this
    // one row), or create a new one.
    private List<FloatMenuOption> BuildIngredientLinkOptions(
        ManagerJob_Production job,
        ThingDef ingredient
    )
    {
        var opts = new List<FloatMenuOption>();

        foreach (
            var linked in job
                .LinkedProducers.Where(p =>
                    p.Recipe != null
                    && ManagerJob_Production.ResolvedOutputDefs(p.Recipe).Contains(ingredient)
                )
                .OrderBy(GetSubLabel, StringComparer.OrdinalIgnoreCase)
        )
        {
            var linkedLocal = linked;
            opts.Add(
                new FloatMenuOption(
                    "ColonyManagerRedux.Production.Unlink".Translate(GetSubLabel(linkedLocal)),
                    () => job.LinkedProducers.Remove(linkedLocal)
                )
            );
        }

        foreach (
            var producer in ComputeExistingProducerCandidates(job, ingredient)
                .OrderBy(GetSubLabel, StringComparer.OrdinalIgnoreCase)
        )
        {
            var producerLocal = producer;
            opts.Add(
                new FloatMenuOption(
                    "ColonyManagerRedux.Production.LinkToExisting".Translate(
                        GetSubLabel(producerLocal)
                    ),
                    () => job.LinkedProducers.Add(producerLocal)
                )
            );
        }

        foreach (
            var recipe in ComputeIngredientSourceCandidates(ingredient)
                .OrderBy(r => r.LabelCap.ToString(), StringComparer.OrdinalIgnoreCase)
        )
        {
            var recipeLocal = recipe;
            opts.Add(
                new FloatMenuOption(
                    "ColonyManagerRedux.Production.CreateLinkedJob".Translate(recipeLocal.LabelCap),
                    () => CreateLinkedProducerJob(job, recipeLocal)
                )
            );
        }

        return opts;
    }

    // The new job defaults both AutoTargetFromLinks and AutoRestrictIngredientsFromLinks to
    // true: that's the entire point of choosing "create new job" from a link menu, so requiring
    // manual steps to enable them would be poor UX. Immediately managed (not left in the
    // Available-tab-style unmanaged state) so it starts working right away, same as clicking a
    // row in the Available tab followed by "Manage" would.
    private void CreateLinkedProducerJob(ManagerJob_Production consumer, RecipeDef recipe)
    {
        var producer = (ManagerJob_Production)MakeNewJob()!;
        producer.Recipe = recipe;
        producer.AutoTargetFromLinks = true;
        producer.AutoRestrictIngredientsFromLinks = true;
        producer.IsManaged = true;
        Manager.JobTracker.Add(producer);
        _ = consumer.LinkedProducers.Add(producer);
        Refresh();
    }

    // Every job (of any mode — a ConsumeSurplus consumer can still link a producer for
    // documentation/traceability, it just never contributes a demand number) currently linking
    // to this producer, paired with which of its currently-allowed ingredients this producer
    // actually covers and the resulting combined demand (0 for a ConsumeSurplus consumer, which
    // has no bounded demand to compute).
    private List<(
        ManagerJob_Production Consumer,
        List<ThingDef> CoveredIngredients,
        int Demand
    )> ComputeLinkedConsumerDemands(ManagerJob_Production producer)
    {
        if (producer.Recipe == null)
        {
            return [];
        }

        var myOutputs = ManagerJob_Production.ResolvedOutputDefs(producer.Recipe).ToHashSet();
        var result = new List<(ManagerJob_Production, List<ThingDef>, int)>();
        foreach (var consumer in Manager.JobTracker.JobsOfType<ManagerJob_Production>())
        {
            if (!consumer.LinkedProducers.Contains(producer))
            {
                continue;
            }

            var covered = consumer.AllowedIngredients.Where(myOutputs.Contains).ToList();
            var demand =
                consumer.Mode == ManagerJob_Production.ProductionMode.MaintainStock
                && consumer.Recipe != null
                    ? ManagerJob_Production.ComputeIngredientDemand(
                        consumer.Recipe,
                        ManagerJob_Production.EffectiveLinkedDemandBufferCount(
                            consumer.LinkedDemandBufferCount,
                            consumer.TriggerThreshold.TargetCount
                        ),
                        covered
                    )
                    : 0;
            result.Add((consumer, covered, demand));
        }
        return result;
    }

    private float DrawIngredientList(ManagerJob_Production job, Vector2 pos, float width)
    {
        var start = pos;

        pos.y += Utilities.DrawToggleDefList(
            pos,
            width,
            ManagerJob_Production.AllRecipeIngredientOptions(job.Recipe!),
            job.AllowedIngredients.Contains,
            (thingDef, allow) => job.SetIngredientAllowed(thingDef, allow),
            thingDef => thingDef.LabelCap,
            thingDef => (TipSignal)thingDef.LabelCap,
            (rect, thingDef) => Widgets.InfoCardButton(rect, thingDef),
            (rect, thingDef, _) => DrawIngredientLinkIcon(job, rect, thingDef)
        );

        return pos.y - start.y;
    }

    // Drawn to the left of DrawToggleDefList's own checkbox icon (one SmallIconSize+Margin slot
    // further left, the same slot DrawToggle's "expensive" icon would occupy) — filled when a
    // currently-linked producer covers this ingredient, outlined when linkable but not covered
    // by any current link, absent entirely when no producing recipe exists for this def at all.
    // Linking is job-level (see ManagerJob_Production.LinkedProducers): the icon reflects
    // whether *any* linked producer happens to cover this specific row, not a per-row link of
    // its own.
    private void DrawIngredientLinkIcon(ManagerJob_Production job, Rect rowRect, ThingDef thingDef)
    {
        // Linking only makes sense once this job actually exists as a manageable job - an
        // unmanaged job (still being set up from the Available tab) has no stable identity for
        // another job's LinkedProducers to point at yet.
        if (!job.IsManaged)
        {
            return;
        }

        var coveringProducers = job
            .LinkedProducers.Where(p =>
                p.Recipe != null
                && ManagerJob_Production.ResolvedOutputDefs(p.Recipe).Contains(thingDef)
            )
            .ToList();
        var linked = coveringProducers.Count > 0;
        if (!linked && ComputeIngredientSourceCandidates(thingDef).Count == 0)
        {
            return;
        }

        var iconRect = new Rect(
            rowRect.xMax - (2 * (SmallIconSize + Margin)),
            0f,
            SmallIconSize,
            SmallIconSize
        ).CenteredOnYIn(rowRect);

        GUI.DrawTexture(iconRect, linked ? Resources.LinkLinked : Resources.LinkUnlinked);
        TooltipHandler.TipRegion(
            iconRect,
            linked
                ? "ColonyManagerRedux.Production.IngredientLinked.Tip".Translate(
                    coveringProducers.Select(GetSubLabel).ToCommaList()
                )
                : "ColonyManagerRedux.Production.IngredientNotLinked.Tip".Translate()
        );
        Widgets.DrawHighlightIfMouseover(iconRect);
        if (Widgets.ButtonInvisible(iconRect))
        {
            Find.WindowStack.Add(new FloatMenu(BuildIngredientLinkOptions(job, thingDef)));
        }
    }

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
    // filter against whatever the recipe's registered RecipeProductResolver says the recipe
    // actually produces — a single ThingDef for simple recipes, or every def in a category for
    // e.g. butchery/stonecutting. This is always the recipe's output, regardless of job mode:
    // in ConsumeSurplus mode, TriggerThreshold.ThresholdFilter is seeded from the recipe's raw
    // ingredients (what triggers the bill), not its product, so checking it here would filter
    // stockpiles by the wrong side of the recipe. A recipe with no registered resolver at all
    // can't currently be tracked by this job, so it's treated as always compatible (matches
    // vanilla's own fallback when CanCountProducts is false).
    private static bool CanPossiblyStore(ManagerJob_Production job, ISlotGroup slotGroup) =>
        CanPossiblyStore(
            RecipeProductResolvers.ResolverFor(job.Recipe!),
            job.Recipe!,
            slotGroup.Settings.AllowedToAccept
        );

    /// <summary>
    /// Same decision as <see cref="CanPossiblyStore(ManagerJob_Production, ISlotGroup)"/>, but
    /// takes an already-resolved <paramref name="resolver"/> and a plain acceptance predicate
    /// instead of a live job/slot group — kept separate so the resolver-vs-mode logic is
    /// unit-testable without a live <see cref="DefDatabase{T}"/> or <see cref="ISlotGroup"/>.
    /// </summary>
    internal static bool CanPossiblyStore(
        RecipeProductResolver? resolver,
        RecipeDef recipe,
        Func<ThingDef, bool> canAccept
    )
    {
        if (resolver is null)
        {
            return true;
        }

        var filter = new ThingFilter();
        resolver.ConfigureFilter(recipe, filter);
        return filter.AllowedThingDefs.Any(canAccept);
    }

    private static float DrawStatus(ManagerJob_Production job, Vector2 pos, float width)
    {
        var start = pos;

        var headerRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Widgets.Label(
            headerRect,
            "ColonyManagerRedux.Production.ManagedBillCount".Translate(job.ManagedBills.Count)
        );
        pos.y += ListEntryHeight;

        // AddManagedBill only ever puts one managed bill on a given work table's billStack
        // (see the job's own liveManagedBills.Find(b => b.billStack == workTable.billStack)
        // matching elsewhere in this class), so each managed bill maps to exactly one distinct
        // work table — no grouping/counting needed here.
        foreach (var bill in job.ManagedBills)
        {
            if (bill.billStack.billGiver is not Building_WorkTable workTable)
            {
                continue;
            }

            var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
            if (!Widgets_Section.CanCull(rowRect.y, rowRect.height))
            {
                Widgets.DrawHighlightIfMouseover(rowRect);
                if (Widgets.ButtonInvisible(rowRect))
                {
                    CameraJumper.TryJumpAndSelect(workTable);
                    // Selecting alone only shows the "Bills" tab button, same as clicking the
                    // work table in the world would — it doesn't open the tab itself. CurTabs
                    // (what OpenTab searches) is computed live from the current selection, so
                    // this is safe to call immediately after TryJumpAndSelect.
                    _ = InspectPaneUtility.OpenTab(typeof(ITab_Bills));
                }

                var iconRect = new Rect(rowRect.x, rowRect.y, ListEntryHeight, ListEntryHeight);
                Widgets.DefIcon(iconRect, workTable.def);

                var labelRect = new Rect(
                    iconRect.xMax + Margin,
                    rowRect.y,
                    rowRect.width - iconRect.width - (2 * Margin),
                    rowRect.height
                );
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, workTable.LabelCap);
                Text.Anchor = TextAnchor.UpperLeft;

                // Same "hover to pan/point the camera" behavior as DrawSpecificWorkbenches.
                if (Mouse.IsOver(rowRect) && !Find.CameraDriver.IsPanning())
                {
                    CameraJumper.TryJump(workTable);
                }
            }

            pos.y += ListEntryHeight;
        }

        return pos.y - start.y;
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
