// ManagerJob_Production.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

[HotSwappable]
[CoroutineSettingsType]
internal sealed class ManagerJob_Production
    : ManagerJob<ManagerSettings_Production, ManagerJob_Production.ProductionWorkData>
{
    /// <summary>
    /// The decision for a single eligible bill giver, computed by
    /// <see cref="GatherJobDataCoroutine"/> and applied by <see cref="ExecuteJobDataCoroutine"/>.
    /// </summary>
    internal enum ProductionBillDecision
    {
        /// <summary>Nothing needs to change for this bill giver.</summary>
        None,

        /// <summary>No managed bill exists on this bill giver yet; one should be added.</summary>
        CreateNew,

        /// <summary>A managed bill exists but is suspended and the trigger wants it active.</summary>
        Activate,

        /// <summary>A managed bill exists and is active but the trigger is satisfied.</summary>
        Suspend,
    }

    /// <summary>
    /// Pure decision function for what should happen to a single eligible bill giver, kept
    /// separate from <see cref="GatherJobDataCoroutine"/> so it's unit-testable without a live
    /// <see cref="Building_WorkTable"/>/<see cref="Bill_Production"/>.
    /// </summary>
    internal static ProductionBillDecision DecideBillAction(
        bool hasManagedBill,
        bool managedBillSuspended,
        bool triggerActive
    ) =>
        (hasManagedBill, triggerActive, managedBillSuspended) switch
        {
            (false, true, _) => ProductionBillDecision.CreateNew,
            (true, true, true) => ProductionBillDecision.Activate,
            (true, false, false) => ProductionBillDecision.Suspend,
            _ => ProductionBillDecision.None,
        };

    /// <summary>
    /// How <see cref="AllEligibleWorkTables"/> is narrowed down to the work tables a job
    /// actually manages bills on.
    /// </summary>
    internal enum WorkbenchAssignmentMode
    {
        /// <summary>Every eligible work table on the map participates. Default.</summary>
        All,

        /// <summary>Only work tables within (or outside, if inverted) <see cref="WorkbenchArea"/>.</summary>
        Area,

        /// <summary>Only work tables explicitly listed in <see cref="SpecificWorkbenches"/>.</summary>
        Specific,
    }

    /// <summary>
    /// Pure decision function for whether a single work table is in scope, kept separate from
    /// <see cref="IsWorkTableInScope(Building_WorkTable)"/> so it's unit-testable without a live
    /// <see cref="Building_WorkTable"/>/<see cref="Area"/>.
    /// </summary>
    internal static bool IsWorkTableInScope(
        WorkbenchAssignmentMode mode,
        bool inArea,
        bool isSpecificallySelected
    ) =>
        mode switch
        {
            WorkbenchAssignmentMode.All => true,
            WorkbenchAssignmentMode.Area => inArea,
            WorkbenchAssignmentMode.Specific => isSpecificallySelected,
            _ => true,
        };

    /// <summary>
    /// Which of the two mental models this job is serving: keeping a stock of the recipe's own
    /// output topped up, or converting a surplus of some unrelated resource into the recipe's
    /// output. See Step 5 of <c>Docs/ProductionManagerRework.md</c>.
    /// </summary>
    internal enum ProductionMode
    {
        /// <summary>
        /// Trigger and output are the same thing (e.g. "keep 15 steel knives around") — the
        /// trigger filter is auto-derived from <see cref="Recipe"/>'s own product resolver, and
        /// managed bills are scheduled to produce exactly the shortfall. Default; today's only
        /// behavior until Increment B adds exact-fill scheduling.
        /// </summary>
        MaintainStock,

        /// <summary>
        /// Trigger and output are unrelated (e.g. "when cotton > 100, turn it into cotton
        /// dusters") — the trigger filter is independently configured by the player, and there's
        /// no target amount to hit; bills just run for as long as the trigger condition holds.
        /// </summary>
        ConsumeSurplus,
    }

    /// <summary>
    /// Pure comparison used to decide whether a managed bill's <see cref="Bill.allowedSkillRange"/>
    /// is out of sync with the job's <see cref="AllowedSkillRange"/> and needs to be pushed to it,
    /// kept separate from <see cref="GatherJobDataCoroutine"/> so it's unit-testable without a
    /// live <see cref="Bill_Production"/>.
    /// </summary>
    internal static bool BillNeedsSkillRangeUpdate(
        IntRange billSkillRange,
        IntRange jobSkillRange
    ) => billSkillRange != jobSkillRange;

    /// <summary>
    /// Pure comparison used to decide whether a managed bill's
    /// <see cref="Bill.ingredientSearchRadius"/> is out of sync with the job's
    /// <see cref="IngredientSearchRadius"/>, kept separate from
    /// <see cref="GatherJobDataCoroutine"/> so it's unit-testable without a live
    /// <see cref="Bill_Production"/>.
    /// </summary>
    internal static bool BillNeedsIngredientRadiusUpdate(
        float billIngredientRadius,
        float jobIngredientRadius
    ) => billIngredientRadius != jobIngredientRadius;

    /// <summary>
    /// Pure comparison used to decide whether a managed bill's store mode
    /// (<see cref="Bill.GetStoreMode"/>) is out of sync with the job's <see cref="StoreMode"/>,
    /// kept separate from <see cref="GatherJobDataCoroutine"/> so it's unit-testable without a
    /// live <see cref="Bill_Production"/>. Doesn't compare the specific stockpile/storage group
    /// itself (an <see cref="ISlotGroup"/> reference comparison, not meaningfully unit-testable)
    /// — callers must additionally compare that when <see cref="StoreMode"/> is
    /// <see cref="BillStoreModeDefOf.SpecificStockpile"/>.
    /// </summary>
    internal static bool BillNeedsStoreModeUpdate(
        BillStoreModeDef billStoreMode,
        BillStoreModeDef jobStoreMode
    ) => billStoreMode != jobStoreMode;

    /// <summary>
    /// Pure comparison used to decide whether a managed bill's <see cref="Bill.ingredientFilter"/>
    /// is out of sync with the job's <see cref="AllowedIngredients"/> and needs to be pushed to
    /// it, kept separate from <see cref="GatherJobDataCoroutine"/> so it's unit-testable without
    /// a live <see cref="Bill_Production"/>.
    /// </summary>
    internal static bool BillNeedsIngredientFilterUpdate(
        IEnumerable<ThingDef> billAllowedIngredients,
        HashSet<ThingDef> jobAllowedIngredients
    ) => !jobAllowedIngredients.SetEquals(billAllowedIngredients);

    /// <summary>
    /// Splits a total <paramref name="shortfall"/> as evenly as possible across
    /// <paramref name="tableCount"/> work tables, so <see cref="ProductionMode.MaintainStock"/>
    /// bills can be scheduled to produce exactly the shortfall between them instead of each
    /// running unbounded until a threshold poll notices and suspends them (the overshoot this
    /// step exists to fix). Remainder is distributed to the first
    /// <c>shortfall % tableCount</c> tables. Pure function, kept separate from
    /// <see cref="GatherJobDataCoroutine"/> so it's unit-testable.
    /// </summary>
    /// <returns>
    /// An array of length <paramref name="tableCount"/>; empty if <paramref name="tableCount"/>
    /// is zero or negative; all zeros if <paramref name="shortfall"/> is zero or negative.
    /// </returns>
    internal static int[] SplitShortfall(int shortfall, int tableCount)
    {
        if (tableCount <= 0)
        {
            return [];
        }

        var shares = new int[tableCount];
        if (shortfall <= 0)
        {
            return shares;
        }

        var baseShare = shortfall / tableCount;
        var remainder = shortfall % tableCount;
        for (var i = 0; i < tableCount; i++)
        {
            shares[i] = baseShare + (i < remainder ? 1 : 0);
        }
        return shares;
    }

    /// <summary>
    /// Converts a work table's item-count <paramref name="share"/> (from
    /// <see cref="SplitShortfall"/>) into the number of bill iterations needed to produce it,
    /// given how many items a single iteration of the recipe yields. Pure function, kept
    /// separate from <see cref="GatherJobDataCoroutine"/> so it's unit-testable.
    /// </summary>
    /// <param name="share">The item-count share to convert, from <see cref="SplitShortfall"/>.</param>
    /// <param name="yieldPerIteration">
    /// Items produced per iteration. Treated as <c>1</c> if zero or negative, so a recipe with
    /// an unknown/variable yield still gets a conservative (generous) iteration estimate rather
    /// than a division error.
    /// </param>
    internal static int SharesToIterations(int share, int yieldPerIteration)
    {
        if (share <= 0)
        {
            return 0;
        }

        var yield = Math.Max(1, yieldPerIteration);
        return (share + yield - 1) / yield;
    }

    /// <summary>
    /// Pure comparison used to decide whether a <see cref="ProductionMode.MaintainStock"/>
    /// managed bill's repeat mode/count is out of sync with its freshly computed
    /// <paramref name="targetRepeatCount"/> and needs to be pushed to it, kept separate from
    /// <see cref="GatherJobDataCoroutine"/> so it's unit-testable without a live
    /// <see cref="Bill_Production"/>. Also catches bills still left in
    /// <see cref="BillRepeatModeDefOf.Forever"/> mode from before this mode existed (or from an
    /// older save), migrating them to <see cref="BillRepeatModeDefOf.RepeatCount"/> the next
    /// time they're reconciled.
    /// </summary>
    internal static bool BillNeedsRepeatCountUpdate(
        BillRepeatModeDef billRepeatMode,
        int billRepeatCount,
        int targetRepeatCount
    ) => billRepeatMode != BillRepeatModeDefOf.RepeatCount || billRepeatCount != targetRepeatCount;

    /// <summary>
    /// The number of items a single iteration of <paramref name="recipe"/> is known to yield,
    /// or <c>1</c> as a conservative estimate when the yield varies (e.g.
    /// <c>RecipeProductResolver_ButcherAnimals</c>, <c>_MakeStoneBlocks</c>, <c>_Smelted</c>).
    /// Used to convert a <see cref="ProductionMode.MaintainStock"/> work table's item-count
    /// share into a <see cref="Bill_Production.repeatCount"/>. Since the split is recomputed
    /// from fresh stock counts every <see cref="GatherJobDataCoroutine"/> pass rather than
    /// trusted once, an inexact estimate self-corrects over successive passes instead of
    /// compounding error.
    /// </summary>
    internal static int YieldPerIteration(RecipeDef recipe) =>
        recipe.ProducedThingDef != null ? recipe.products[0].count : 1;

    /// <summary>
    /// Pure sum used by <see cref="ExpectedYield"/>: each RepeatCount-mode bill's
    /// <paramref name="repeatCounts"/> entry (already that work table's share of the current
    /// shortfall, see <see cref="GatherJobDataCoroutine"/>) times <paramref name="yieldPerIteration"/>.
    /// Forever-mode bills (still-unmigrated ConsumeSurplus-style bills, see
    /// <see cref="BillNeedsRepeatCountUpdate"/>) contribute nothing, since they have no fixed
    /// target amount to project a yield from.
    /// </summary>
    internal static int ExpectedYieldFromBills(
        IEnumerable<(BillRepeatModeDef RepeatMode, int RepeatCount)> repeatCounts,
        int yieldPerIteration
    ) =>
        repeatCounts
            .Where(b => b.RepeatMode == BillRepeatModeDefOf.RepeatCount)
            .Sum(b => b.RepeatCount) * yieldPerIteration;

    /// <summary>
    /// Total items this job's managed bills are currently configured to produce. Only meaningful
    /// for <see cref="ProductionMode.MaintainStock"/> — <see cref="ProductionMode.ConsumeSurplus"/>
    /// bills run <see cref="BillRepeatModeDefOf.Forever"/> with no fixed target amount to project
    /// a yield from, so this returns <c>0</c> for those.
    /// </summary>
    public int ExpectedYield =>
        Mode != ProductionMode.MaintainStock || Recipe == null
            ? 0
            : ExpectedYieldFromBills(
                _managedBills.Select(b => (b.repeatMode, b.repeatCount)),
                YieldPerIteration(Recipe)
            );

    /// <inheritdoc/>
    /// <remarks>
    /// Feeds the same generic "expected" machinery other job types use (dimmed segment on
    /// <see cref="Trigger.DrawHorizontalProgressBars"/>/<see cref="Trigger.DrawVerticalProgressBars"/>,
    /// "(+ N expected)" in <see cref="Trigger_Threshold.StatusTooltip"/>) with <see cref="ExpectedYield"/>.
    /// </remarks>
    public override int ExpectedAdditionalCount => ExpectedYield;

    /// <summary>
    /// Carries the decisions made by <see cref="GatherJobDataCoroutine"/> (which doesn't touch
    /// the game) to <see cref="ExecuteJobDataCoroutine"/> (which applies them).
    /// </summary>
    internal sealed class ProductionWorkData
    {
        public List<Bill_Production> DeadBillsToForget = [];
        public List<Bill_Production> OutOfScopeBillsToRemove = [];
        public List<Building_WorkTable> WorkTablesNeedingNewBill = [];
        public List<Bill_Production> BillsToActivate = [];
        public List<Bill_Production> BillsToSuspend = [];
        public List<Bill_Production> BillsNeedingSkillRangeUpdate = [];
        public List<Bill_Production> BillsNeedingIngredientRadiusUpdate = [];
        public List<Bill_Production> BillsNeedingStoreModeUpdate = [];
        public List<Bill_Production> BillsNeedingIngredientFilterUpdate = [];
        public Dictionary<Building_WorkTable, int> MaintainStockTablesNeedingNewBill = [];
        public List<(Bill_Production Bill, int RepeatCount)> BillsNeedingRepeatCountUpdate = [];
    }

    private List<Bill_Production> _managedBills = [];

    public IReadOnlyList<Bill_Production> ManagedBills => _managedBills;

    private RecipeDef? _recipe;

    public RecipeDef? Recipe
    {
        get => _recipe;
        set
        {
            if (_recipe == value)
            {
                return;
            }

            RemoveAllManagedBills();
            _recipe = value;
            _consumeSurplusFilterInitialized = false;
            ConfigureThresholdTriggerFilter();
            ResetAllowedIngredientsToDefault();
            Notify_TargetsChanged();
        }
    }

    private ProductionMode _mode = ProductionMode.MaintainStock;

    /// <summary>
    /// See <see cref="ProductionMode"/>. Switching into <see cref="ProductionMode.MaintainStock"/>
    /// re-derives the trigger filter from <see cref="Recipe"/>; switching into
    /// <see cref="ProductionMode.ConsumeSurplus"/> leaves whatever filter is currently set
    /// untouched, so toggling back and forth to compare doesn't discard a manually configured
    /// filter.
    /// </summary>
    public ProductionMode Mode
    {
        get => _mode;
        set
        {
            if (_mode == value)
            {
                return;
            }

            _mode = value;
            var supportedOps = SupportedOpsForMode(_mode);
            TriggerThreshold.RestrictSupportedOps(supportedOps);
            TriggerThreshold.Op = supportedOps[0];
            ConfigureThresholdTriggerFilter();
        }
    }

    /// <summary>
    /// The <see cref="Trigger_Threshold.Ops"/> a job may use in each <see cref="ProductionMode"/>
    /// — <see cref="ProductionMode.MaintainStock"/> can only add stock (matches
    /// <c>ManagerJob_Mining</c>'s own reasoning for restricting to
    /// <see cref="Trigger_Threshold.AccumulationOnlyOps"/>), while
    /// <see cref="ProductionMode.ConsumeSurplus"/> needs <see cref="Trigger_Threshold.Ops.HigherThan"/>
    /// (e.g. "cotton &gt; 100") and so supports every op.
    /// </summary>
    internal static IReadOnlyList<Trigger_Threshold.Ops> SupportedOpsForMode(ProductionMode mode) =>
        mode == ProductionMode.MaintainStock
            ? Trigger_Threshold.AccumulationOnlyOps
            :
            [
                Trigger_Threshold.Ops.HigherThan,
                Trigger_Threshold.Ops.LowerThan,
                Trigger_Threshold.Ops.Equals,
                Trigger_Threshold.Ops.NotEquals,
            ];

    public WorkbenchAssignmentMode AssignmentMode = WorkbenchAssignmentMode.All;
    public Area? WorkbenchArea;
    public bool InvertWorkbenchArea;
    public HashSet<Building_WorkTable> SpecificWorkbenches = [];

    /// <summary>
    /// Mirrors <see cref="Bill.allowedSkillRange"/>, applied to every bill this job manages.
    /// Only meaningful (and only shown in the tab) when <see cref="Recipe"/> has a
    /// <see cref="RecipeDef.workSkill"/> — matches vanilla's own <c>Dialog_BillConfig</c>, which
    /// hides the control entirely otherwise.
    /// </summary>
    public IntRange AllowedSkillRange = new(0, 20);

    /// <summary>
    /// Mirrors <see cref="Bill.ingredientSearchRadius"/>, applied to every bill this job manages.
    /// Default matches vanilla's own unlimited-radius default.
    /// </summary>
    public float IngredientSearchRadius = 999f;

    /// <summary>
    /// Mirrors <see cref="Bill_Production.GetStoreMode"/>, applied to every bill this job
    /// manages. Default matches vanilla's own <see cref="BillStoreModeDefOf.BestStockpile"/>
    /// default.
    /// </summary>
    public BillStoreModeDef StoreMode = BillStoreModeDefOf.BestStockpile;

    /// <summary>
    /// The specific stockpile/storage group to deliver to when <see cref="StoreMode"/> is
    /// <see cref="BillStoreModeDefOf.SpecificStockpile"/>; unused (and left stale) otherwise,
    /// same as vanilla's own <c>Bill_Production.storeGroup</c>.
    /// </summary>
    public ISlotGroup? StoreGroup;

    /// <summary>
    /// Which of <see cref="Recipe"/>'s raw-material options managed bills are actually allowed to
    /// consume — mirrors <see cref="Bill.ingredientFilter"/>, applied to every bill this job
    /// manages (see <see cref="AddManagedBill"/>). Distinct from
    /// <see cref="Trigger_Threshold.ThresholdFilter"/> (what's counted to decide whether the job
    /// should run at all): in <see cref="ProductionMode.MaintainStock"/> that filter is
    /// output-typed and unrelated to ingredients, while in <see cref="ProductionMode.ConsumeSurplus"/>
    /// it's ingredient-typed and, by default, kept in sync with this field (see
    /// <see cref="Sync"/>/<see cref="SyncFilterAndAllowed"/>) — but a player can decouple them,
    /// e.g. triggering on hay surplus while still only ever consuming meat for carnivore meals.
    /// Reset to every ingredient option whenever <see cref="Recipe"/> changes.
    /// </summary>
    public HashSet<ThingDef> AllowedIngredients = [];

    /// <summary>
    /// Which of <see cref="AllowedIngredients"/>/<see cref="Trigger_Threshold.ThresholdFilter"/>
    /// last changed, so the other's change handler doesn't fight back. Only consulted in
    /// <see cref="ProductionMode.ConsumeSurplus"/> — see <see cref="AllowedIngredients"/>.
    /// </summary>
    public Utilities.SyncDirection Sync = Utilities.SyncDirection.AllowedToFilter;

    /// <summary>
    /// Whether <see cref="AllowedIngredients"/> and <see cref="Trigger_Threshold.ThresholdFilter"/>
    /// should be kept in sync at all. Only meaningful (and only shown in the tab) in
    /// <see cref="ProductionMode.ConsumeSurplus"/> — see <see cref="AllowedIngredients"/>.
    /// </summary>
    public bool SyncFilterAndAllowed = true;

    private string? _tmpWorkbenchAreaLabel;

    /// <summary>
    /// Every built, billable work table on the map that could run <see cref="Recipe"/>,
    /// regardless of <see cref="AssignmentMode"/>. Used both to compute which work tables are
    /// actually in scope and to populate the "Specific" mode picker in the tab.
    /// </summary>
    public IEnumerable<Building_WorkTable> AllEligibleWorkTables
    {
        get
        {
            if (Recipe == null)
            {
                return [];
            }

            var recipeUsers = Recipe.AllRecipeUsers.ToHashSet();
            return Manager
                .map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>()
                .Where(wt => wt.billStack != null && recipeUsers.Contains(wt.def));
        }
    }

    public bool IsWorkTableInScope(Building_WorkTable workTable) =>
        IsWorkTableInScope(
            AssignmentMode,
            Utilities.IsInAllowedArea(WorkbenchArea, workTable.Position, InvertWorkbenchArea),
            SpecificWorkbenches.Contains(workTable)
        );

    public Trigger_Threshold TriggerThreshold => (Trigger_Threshold)Trigger!;

    public ManagerJob_Production(Manager manager)
        : base(manager)
    {
        Trigger = new Trigger_Threshold(this, SupportedOpsForMode(_mode))
        {
            AllowAnyThresholdChanged = ConfigureThresholdTriggerFilter,
        };
        ConfigureThresholdTriggerFilter();
        TriggerThreshold.SettingsChanged = Notify_ThresholdFilterChanged;
    }

    /// <summary>
    /// Whether <see cref="ProductionMode.ConsumeSurplus"/>'s trigger filter has already been
    /// seeded once (from <see cref="Recipe"/>'s raw-material ingredients) for the current
    /// <see cref="Recipe"/>. Set back to <see langword="false"/> whenever <see cref="Recipe"/>
    /// changes, so switching recipes re-seeds instead of keeping a stale filter — but left
    /// <see langword="true"/> across repeated <see cref="Mode"/> toggling, so a player's manual
    /// edits to the filter (via the trigger's own config UI) survive comparing modes back and
    /// forth.
    /// </summary>
    private bool _consumeSurplusFilterInitialized;

    private void ConfigureThresholdTriggerFilter()
    {
        if (_recipe == null)
        {
            return;
        }

        if (_mode == ProductionMode.MaintainStock)
        {
            var resolver = RecipeProductResolvers.ResolverFor(_recipe);

            if (!TriggerThreshold.AllowAnyThreshold)
            {
                TriggerThreshold.ParentFilter.SetDisallowAll();
                resolver?.ConfigureFilter(_recipe, TriggerThreshold.ParentFilter);
            }

            TriggerThreshold.ThresholdFilter.SetDisallowAll();
            resolver?.ConfigureFilter(_recipe, TriggerThreshold.ThresholdFilter);
            return;
        }

        // ConsumeSurplus: trigger and output are unrelated (e.g. "cotton > 100" driving a
        // cotton-duster recipe), so there's nothing to auto-derive the filter from on every
        // pass the way MaintainStock does above. Instead, seed it once with the recipe's raw
        // material ingredients — the sensible default for "surplus of what feeds this recipe"
        // — and leave it alone afterwards so a player's own edits aren't clobbered by toggling
        // modes to compare them.
        if (_consumeSurplusFilterInitialized)
        {
            return;
        }
        _consumeSurplusFilterInitialized = true;

        if (!TriggerThreshold.AllowAnyThreshold)
        {
            TriggerThreshold.ParentFilter.SetDisallowAll();
            ConfigureIngredientFilter(_recipe, TriggerThreshold.ParentFilter);
        }

        TriggerThreshold.ThresholdFilter.SetDisallowAll();
        ConfigureIngredientFilter(_recipe, TriggerThreshold.ThresholdFilter);
    }

    /// <summary>
    /// Every <see cref="ThingDef"/> that could satisfy any of <paramref name="recipe"/>'s
    /// ingredients — including fixed ingredients, since <see cref="IngredientCount.IsFixedIngredient"/>
    /// is just the case where its own <see cref="IngredientCount.filter"/> happens to allow
    /// exactly one def. This is the raw-material counterpart to <see cref="RecipeProductResolver"/>,
    /// which resolves what a recipe produces instead of what it consumes. Used both to seed
    /// <see cref="ProductionMode.ConsumeSurplus"/>'s trigger filter (<see cref="ConfigureIngredientFilter"/>)
    /// and to default <see cref="AllowedIngredients"/>.
    /// </summary>
    internal static IEnumerable<ThingDef> AllRecipeIngredientOptions(RecipeDef recipe) =>
        recipe.ingredients.SelectMany(ingredient => ingredient.filter.AllowedThingDefs).Distinct();

    /// <summary>
    /// Groups <paramref name="ingredients"/> by the category directly above each item, i.e.
    /// <see cref="ThingDef.thingCategories"/>'s first entry — the same "direct member" link
    /// <see cref="ThingCategoryDef.childThingDefs"/> uses, not a full ancestor chain. Recipes
    /// vary too much to hardcode category shortcuts the way e.g. Foraging's "Edible"/"Mushrooms"
    /// do, so the tab derives its shortcut groups from this instead. A def isn't assigned to more
    /// than one group even if it has multiple <c>thingCategories</c>, to avoid a shortcut toggle
    /// silently touching a def that appears to belong to a different group; defs with no category
    /// at all are simply omitted from the result (they still appear in the full ingredient list).
    /// </summary>
    internal static ILookup<ThingCategoryDef, ThingDef> GroupIngredientsByCategory(
        IEnumerable<ThingDef> ingredients
    ) =>
        ingredients
            .Where(thingDef => thingDef.thingCategories is { Count: > 0 })
            .ToLookup(thingDef => thingDef.thingCategories[0]);

    /// <summary>
    /// Populates <paramref name="filter"/> with every <see cref="ThingDef"/> from
    /// <see cref="AllRecipeIngredientOptions"/>.
    /// </summary>
    internal static void ConfigureIngredientFilter(RecipeDef recipe, ThingFilter filter)
    {
        foreach (var thingDef in AllRecipeIngredientOptions(recipe))
        {
            filter.SetAllow(thingDef, true);
        }
    }

    /// <summary>
    /// Resets <see cref="AllowedIngredients"/> to every ingredient option for <see cref="Recipe"/>
    /// (or empty, if there is none) — the "everything's allowed" default that matches an
    /// unrestricted vanilla bill's behavior. Called whenever <see cref="Recipe"/> changes.
    /// </summary>
    private void ResetAllowedIngredientsToDefault()
    {
        AllowedIngredients.Clear();
        if (_recipe != null)
        {
            AllowedIngredients.UnionWith(AllRecipeIngredientOptions(_recipe));
        }
    }

    /// <summary>
    /// Pushes a change to <see cref="Trigger_Threshold.ThresholdFilter"/> into
    /// <see cref="AllowedIngredients"/>, when the two are meant to stay in sync. Only applies in
    /// <see cref="ProductionMode.ConsumeSurplus"/> — in <see cref="ProductionMode.MaintainStock"/>
    /// the threshold filter is output-typed and has nothing to do with ingredients.
    /// </summary>
    private void Notify_ThresholdFilterChanged()
    {
        if (
            _recipe == null
            || _mode != ProductionMode.ConsumeSurplus
            || !SyncFilterAndAllowed
            || Sync == Utilities.SyncDirection.AllowedToFilter
        )
        {
            return;
        }

        foreach (var thingDef in AllRecipeIngredientOptions(_recipe))
        {
            _ = TriggerThreshold.ThresholdFilter.Allows(thingDef)
                ? AllowedIngredients.Add(thingDef)
                : AllowedIngredients.Remove(thingDef);
        }
        Notify_TargetsChanged();
    }

    /// <summary>
    /// Sets whether <paramref name="thingDef"/> is in <see cref="AllowedIngredients"/>, and, when
    /// <see cref="SyncFilterAndAllowed"/> and in <see cref="ProductionMode.ConsumeSurplus"/>,
    /// pushes the same change into <see cref="Trigger_Threshold.ThresholdFilter"/>.
    /// </summary>
    public void SetIngredientAllowed(ThingDef thingDef, bool allow, bool sync = true)
    {
        _ = allow ? AllowedIngredients.Add(thingDef) : AllowedIngredients.Remove(thingDef);
        Notify_TargetsChanged();

        if (_mode == ProductionMode.ConsumeSurplus && SyncFilterAndAllowed && sync)
        {
            Sync = Utilities.SyncDirection.AllowedToFilter;
            TriggerThreshold.ThresholdFilter.SetAllow(thingDef, allow);
        }
    }

    private void RemoveAllManagedBills()
    {
        foreach (var bill in _managedBills)
        {
            if (!bill.DeletedOrDereferenced)
            {
                bill.billStack.Delete(bill);
            }
        }
        _managedBills.Clear();
    }

    /// <summary>
    /// Creates a new managed <see cref="Bill_Production"/> for <see cref="Recipe"/> on
    /// <paramref name="workTable"/>, with every job-level setting
    /// (<see cref="AllowedSkillRange"/>, <see cref="IngredientSearchRadius"/>,
    /// <see cref="AllowedIngredients"/>, <see cref="StoreMode"/>) applied, and registers it as
    /// managed. Callers still need to set
    /// <see cref="Bill_Production.repeatMode"/>/<see cref="Bill_Production.repeatCount"/>
    /// themselves — those differ by <see cref="ProductionMode"/>.
    /// </summary>
    private Bill_Production AddManagedBill(Building_WorkTable workTable)
    {
        var recipe = Recipe!;
        var bill = recipe.UsesUnfinishedThing
            ? new Bill_ProductionWithUft(recipe, null)
            : new Bill_Production(recipe, null);
        bill.allowedSkillRange = AllowedSkillRange;
        bill.ingredientSearchRadius = IngredientSearchRadius;
        ApplyAllowedIngredients(bill);
        bill.SetStoreMode(
            StoreMode,
            StoreMode == BillStoreModeDefOf.SpecificStockpile ? StoreGroup : null
        );
        workTable.billStack.AddBill(bill);
        _managedBills.Add(bill);
        return bill;
    }

    /// <summary>
    /// Restricts <paramref name="bill"/>'s <see cref="Bill.ingredientFilter"/> to exactly
    /// <see cref="AllowedIngredients"/>.
    /// </summary>
    private void ApplyAllowedIngredients(Bill_Production bill)
    {
        bill.ingredientFilter.SetDisallowAll();
        foreach (var thingDef in AllowedIngredients)
        {
            bill.ingredientFilter.SetAllow(thingDef, true);
        }
    }

    public override bool IsValid => base.IsValid && Recipe != null;

    public override IEnumerable<string> Targets => Recipe != null ? [Recipe.LabelCap] : [];

    public override WorkTypeDef? WorkTypeDef => Recipe?.requiredGiverWorkType;

    public override void CleanUp(ManagerLog? jobLog = null) => RemoveAllManagedBills();

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Defs.Look(ref _recipe, "recipe");
        Scribe_Collections.Look(ref _managedBills, "managedBills", LookMode.Reference);
        Scribe_Values.Look(ref _mode, "mode", ProductionMode.MaintainStock);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            // The constructor always restricts to MaintainStock's ops (before this field's
            // saved value is loaded), so a loaded ConsumeSurplus job needs its trigger's
            // SupportedOps widened back out now that the real mode is known — the raw `op`
            // field itself already loaded correctly via Trigger_Threshold's own ExposeData,
            // this just re-syncs which ops the UI is allowed to offer going forward.
            TriggerThreshold.RestrictSupportedOps(SupportedOpsForMode(_mode));

            // _consumeSurplusFilterInitialized isn't itself scribed: _mode only reaches
            // ConsumeSurplus by going through the Mode setter at some point (this field starts
            // at MaintainStock and Scribe assigns the backing field directly, bypassing the
            // setter), and that setter always seeds the ingredient filter before ConsumeSurplus
            // becomes observable. So a loaded ConsumeSurplus job's filter is already whatever
            // that seeding (possibly since edited by the player) produced — reconstructing the
            // flag from the loaded mode is exactly as accurate as persisting it separately.
            _consumeSurplusFilterInitialized = _mode == ProductionMode.ConsumeSurplus;

            // Delegates aren't scribed; TriggerThreshold survives the load (it's part of this
            // job's own object graph), but needs its callback re-wired same as the constructor
            // does for a freshly created job.
            TriggerThreshold.SettingsChanged = Notify_ThresholdFilterChanged;
        }
        Scribe_Values.Look(ref AssignmentMode, "assignmentMode", WorkbenchAssignmentMode.All);
        Scribe_Values.Look(ref InvertWorkbenchArea, "invertWorkbenchArea");
        Scribe_Values.Look(ref AllowedSkillRange, "allowedSkillRange", new IntRange(0, 20));
        Scribe_Values.Look(ref IngredientSearchRadius, "ingredientSearchRadius", 999f);
        Scribe_Collections.Look(ref AllowedIngredients, "allowedIngredients", LookMode.Def);
        Scribe_Values.Look(ref Sync, "sync", Utilities.SyncDirection.AllowedToFilter);
        Scribe_Values.Look(ref SyncFilterAndAllowed, "syncFilterAndAllowed", true);
        Scribe_Defs.Look(ref StoreMode, "storeMode");
        StoreMode ??= BillStoreModeDefOf.BestStockpile;

        if (Manager.ScribeSameMapData)
        {
            Scribe_References.Look(ref WorkbenchArea, "workbenchArea");
            Scribe_Collections.Look(
                ref SpecificWorkbenches,
                "specificWorkbenches",
                LookMode.Reference
            );

            // ISlotGroup itself isn't directly referenceable; mirrors vanilla
            // Bill_Production.SaveSlotReferencable/LoadSlotReferencable, which scribes either
            // the group itself (storage buildings/groups) or its zone parent (stockpile zones).
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                var storeGroupReferencable = StoreGroup switch
                {
                    ILoadReferenceable loadReferenceable => loadReferenceable,
                    SlotGroup { parent: ILoadReferenceable parent } => parent,
                    _ => null,
                };
                Scribe_References.Look(ref storeGroupReferencable, "storeGroup");
            }
            else if (Scribe.mode is LoadSaveMode.LoadingVars or LoadSaveMode.ResolvingCrossRefs)
            {
                ILoadReferenceable? storeGroupReferencable = null;
                Scribe_References.Look(ref storeGroupReferencable, "storeGroup");
                StoreGroup = storeGroupReferencable switch
                {
                    ISlotGroup slotGroup => slotGroup,
                    ISlotGroupParent slotGroupParent => slotGroupParent.GetSlotGroup(),
                    _ => null,
                };
            }
        }
        else
        {
            Utilities.Scribe_AreaByLabel(
                ref WorkbenchArea,
                ref _tmpWorkbenchAreaLabel,
                "workbenchArea",
                Manager.map.areaManager
            );

            // Specific work table instances have no cross-map/template identity the way an
            // Area's label does, so they can't be carried through an export/import; the job
            // keeps AssignmentMode == Specific but with an empty selection, and the player
            // re-picks work tables in the tab after importing.
        }

        // Cross-map import intentionally drops StoreGroup (see comment above); a same-map load
        // can also legitimately fail to resolve it if the zone/storage was deleted since saving.
        // Mirrors vanilla Bill_Production.ValidateSettings' equivalent fallback.
        if (StoreGroup == null && StoreMode == BillStoreModeDefOf.SpecificStockpile)
        {
            StoreMode = BillStoreModeDefOf.BestStockpile;
        }
    }

    protected override void Notify_AreaRemoved(Area area)
    {
        if (WorkbenchArea == area)
        {
            WorkbenchArea = null;
        }
    }

    private bool IsStoreGroupStillValid(ISlotGroup slot) =>
        slot switch
        {
            SlotGroup { parent: Zone_Stockpile zone } => Manager.map.zoneManager.AllZones.Contains(
                zone
            ),
            SlotGroup { parent: Building_Storage storage } =>
                Manager.map.haulDestinationManager.AllGroups.Contains(storage.slotGroup),
            StorageGroup storageGroup => Manager.map.storageGroups.HasStorageGroup(storageGroup),
            _ => true,
        };

    [CoroutineSettingsMethod]
    protected override Coroutine GatherJobDataCoroutine(
        ManagerLog jobLog,
        AnyBoxed<ProductionWorkData?> data
    )
    {
        if (Recipe == null)
        {
            JobState = ManagerJobState.Completed;
            yield break;
        }

        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                (Func<ManagerLog, AnyBoxed<ProductionWorkData?>, Coroutine>)GatherJobDataCoroutine
            );

        // Specific-mode selections can accumulate references to work tables that were later
        // deconstructed; prune those opportunistically so they don't linger forever.
        _ = SpecificWorkbenches.RemoveWhere(wt => wt.Destroyed || !wt.Spawned);

        // Unlike areas (Notify_AreaRemoved) there's no live "zone/storage deleted" notification
        // to hook into, so validity is checked lazily here instead, mirroring vanilla
        // Bill_Production.ValidateGroup/IsZoneValid/IsBuildingValid/IsStorageGroupValid.
        if (StoreGroup != null && !IsStoreGroupStillValid(StoreGroup))
        {
            StoreGroup = null;
            StoreMode = BillStoreModeDefOf.BestStockpile;
        }

        var workData = new ProductionWorkData();

        foreach (var bill in _managedBills)
        {
            if (bill.DeletedOrDereferenced)
            {
                workData.DeadBillsToForget.Add(bill);
            }
        }
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        var liveManagedBills = _managedBills.Except(workData.DeadBillsToForget).ToList();

        var recipeUsers = Recipe.AllRecipeUsers.ToHashSet();
        var inScopeWorkTables = Manager
            .map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>()
            .Where(wt =>
                wt.billStack != null && recipeUsers.Contains(wt.def) && IsWorkTableInScope(wt)
            )
            .ToList();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        // A managed bill whose work table fell out of scope (e.g. area/specific selection
        // changed) should be forgotten too, so the bench reverts to fully unmanaged instead of
        // being stuck in whatever suspended/active state it last had.
        var inScopeBillStacks = inScopeWorkTables.Select(wt => wt.billStack).ToHashSet();
        var outOfScopeBills = liveManagedBills
            .Where(b => !inScopeBillStacks.Contains(b.billStack))
            .ToList();
        workData.OutOfScopeBillsToRemove.AddRange(outOfScopeBills);
        liveManagedBills = [.. liveManagedBills.Except(outOfScopeBills)];

        workData.BillsNeedingSkillRangeUpdate.AddRange(
            liveManagedBills.Where(b =>
                BillNeedsSkillRangeUpdate(b.allowedSkillRange, AllowedSkillRange)
            )
        );
        workData.BillsNeedingIngredientRadiusUpdate.AddRange(
            liveManagedBills.Where(b =>
                BillNeedsIngredientRadiusUpdate(b.ingredientSearchRadius, IngredientSearchRadius)
            )
        );
        workData.BillsNeedingStoreModeUpdate.AddRange(
            liveManagedBills.Where(b =>
                BillNeedsStoreModeUpdate(b.GetStoreMode(), StoreMode)
                || (
                    StoreMode == BillStoreModeDefOf.SpecificStockpile
                    && b.GetSlotGroup() != StoreGroup
                )
            )
        );
        workData.BillsNeedingIngredientFilterUpdate.AddRange(
            liveManagedBills.Where(b =>
                BillNeedsIngredientFilterUpdate(
                    b.ingredientFilter.AllowedThingDefs,
                    AllowedIngredients
                )
            )
        );

        var triggerActive = TriggerThreshold.State;
        JobState = triggerActive ? ManagerJobState.Active : ManagerJobState.Completed;

        jobLog.AddDetail(
            "ColonyManagerRedux.Production.Logs.CurrentCount".Translate(
                TriggerThreshold.GetCurrentCount(),
                TriggerThreshold.TargetCount
            )
        );

        if (Mode == ProductionMode.MaintainStock)
        {
            // Exact-fill scheduling: split the shortfall across every in-scope table and push
            // each table's share to its bill as a RepeatCount, rather than letting every table
            // run unbounded (Forever mode) until the next poll notices the threshold is met and
            // suspends them all — that's what let multiple tables overshoot the target between
            // polls. Recomputed from fresh stock counts every pass, so both an uneven starting
            // point and a variable-yield recipe's inexact YieldPerIteration estimate
            // self-correct over successive passes instead of compounding.
            var shortfall = Math.Max(
                0,
                TriggerThreshold.TargetCount - TriggerThreshold.GetCurrentCount()
            );
            var yieldPerIteration = YieldPerIteration(Recipe);
            var shares = SplitShortfall(shortfall, inScopeWorkTables.Count);

            for (var i = 0; i < inScopeWorkTables.Count; i++)
            {
                var workTable = inScopeWorkTables[i];
                var iterations = SharesToIterations(shares[i], yieldPerIteration);
                var managedBill = liveManagedBills.Find(b => b.billStack == workTable.billStack);

                if (managedBill == null)
                {
                    // No point adding a bill to an idle table just to have it sit at
                    // repeatCount 0 — it'll be created once this table is actually given a
                    // share of the shortfall.
                    if (iterations > 0)
                    {
                        workData.MaintainStockTablesNeedingNewBill[workTable] = iterations;
                    }
                    continue;
                }

                if (
                    BillNeedsRepeatCountUpdate(
                        managedBill.repeatMode,
                        managedBill.repeatCount,
                        iterations
                    )
                )
                {
                    workData.BillsNeedingRepeatCountUpdate.Add((managedBill, iterations));
                }
            }
        }
        else
        {
            foreach (var workTable in inScopeWorkTables)
            {
                var managedBill = liveManagedBills.Find(b => b.billStack == workTable.billStack);

                switch (
                    DecideBillAction(
                        managedBill != null,
                        managedBill?.suspended ?? false,
                        triggerActive
                    )
                )
                {
                    case ProductionBillDecision.CreateNew:
                        workData.WorkTablesNeedingNewBill.Add(workTable);
                        break;
                    case ProductionBillDecision.Activate:
                        workData.BillsToActivate.Add(managedBill!);
                        break;
                    case ProductionBillDecision.Suspend:
                        workData.BillsToSuspend.Add(managedBill!);
                        break;
                    case ProductionBillDecision.None:
                    default:
                        break;
                }
            }
        }

        data.Value = workData;
    }

    [CoroutineSettingsMethod]
    protected override Coroutine ExecuteJobDataCoroutine(
        ManagerLog jobLog,
        ProductionWorkData data,
        Boxed<bool> workDone
    )
    {
        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                (Func<ManagerLog, ProductionWorkData, Boxed<bool>, Coroutine>)
                    ExecuteJobDataCoroutine
            );

        foreach (var bill in data.DeadBillsToForget)
        {
            _ = _managedBills.Remove(bill);
        }

        foreach (var bill in data.OutOfScopeBillsToRemove)
        {
            if (!bill.DeletedOrDereferenced)
            {
                bill.billStack.Delete(bill);
            }
            _ = _managedBills.Remove(bill);
            workDone.Value = true;
        }

        foreach (var workTable in data.WorkTablesNeedingNewBill)
        {
            var bill = AddManagedBill(workTable);
            bill.repeatMode = BillRepeatModeDefOf.Forever;
            workDone.Value = true;

            jobLog.AddDetail(
                "ColonyManagerRedux.Production.Logs.BillAdded".Translate(
                    Recipe!.LabelCap,
                    workTable.LabelCap
                )
            );

            yield return new ResumeAfterTicks(ticksBetweenOperations);
        }

        foreach (var (workTable, repeatCount) in data.MaintainStockTablesNeedingNewBill)
        {
            var bill = AddManagedBill(workTable);
            bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
            bill.repeatCount = repeatCount;
            workDone.Value = true;

            jobLog.AddDetail(
                "ColonyManagerRedux.Production.Logs.BillAdded".Translate(
                    Recipe!.LabelCap,
                    workTable.LabelCap
                )
            );

            yield return new ResumeAfterTicks(ticksBetweenOperations);
        }

        foreach (var (bill, repeatCount) in data.BillsNeedingRepeatCountUpdate)
        {
            bill.repeatMode = BillRepeatModeDefOf.RepeatCount;
            bill.repeatCount = repeatCount;
            // MaintainStock no longer uses suspend/resume (a repeatCount of 0 already leaves
            // the bench idle) — but a bill migrating from the old Forever+suspend mechanism, or
            // one left suspended from before, still needs to be unsuspended so RepeatCount mode
            // can actually take over.
            bill.suspended = false;
            workDone.Value = true;
        }

        foreach (var bill in data.BillsToActivate)
        {
            bill.suspended = false;
            workDone.Value = true;
        }

        foreach (var bill in data.BillsToSuspend)
        {
            bill.suspended = true;
            workDone.Value = true;
        }

        foreach (var bill in data.BillsNeedingSkillRangeUpdate)
        {
            bill.allowedSkillRange = AllowedSkillRange;
            workDone.Value = true;
        }

        foreach (var bill in data.BillsNeedingIngredientRadiusUpdate)
        {
            bill.ingredientSearchRadius = IngredientSearchRadius;
            workDone.Value = true;
        }

        foreach (var bill in data.BillsNeedingStoreModeUpdate)
        {
            bill.SetStoreMode(
                StoreMode,
                StoreMode == BillStoreModeDefOf.SpecificStockpile ? StoreGroup : null
            );
            workDone.Value = true;
        }

        foreach (var bill in data.BillsNeedingIngredientFilterUpdate)
        {
            ApplyAllowedIngredients(bill);
            workDone.Value = true;
        }
    }
}
