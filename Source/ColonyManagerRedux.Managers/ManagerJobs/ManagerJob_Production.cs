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

    /// <summary>
    /// Finds the job among <paramref name="jobs"/> whose managed items (as given by
    /// <paramref name="managedItemsSelector"/>) contain <paramref name="item"/>, if any. Used to
    /// resolve a <see cref="Bill_Production"/>'s owning job on demand (e.g. for drawing a
    /// "managed by" indicator on the bill's vanilla UI row) instead of maintaining a separate
    /// reverse index that would need to be kept in sync across every mutation site. Generic over
    /// both the job and item types (rather than fixed to <see cref="ManagerJob_Production"/>/
    /// <see cref="Bill_Production"/>) so tests can exercise the matching logic with plain fakes —
    /// constructing a real <see cref="Bill_Production"/> requires a loaded game
    /// (<see cref="Bill.InitializeAfterClone"/> calls <c>Find.UniqueIDsManager</c>), which isn't
    /// available to this test suite.
    /// </summary>
    internal static TJob? FindOwningJob<TJob, TItem>(
        IEnumerable<TJob> jobs,
        Func<TJob, IReadOnlyList<TItem>> managedItemsSelector,
        TItem item
    )
        where TJob : class => jobs.FirstOrDefault(j => managedItemsSelector(j).Contains(item));

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
    /// always re-derives the trigger filter from <see cref="Recipe"/>'s output, discarding
    /// whatever was there before; switching into <see cref="ProductionMode.ConsumeSurplus"/>
    /// re-seeds it from the recipe's ingredients only the first time after that (see
    /// <see cref="_consumeSurplusFilterInitialized"/>), so a player's manual edits survive
    /// toggling back and forth to compare modes without a MaintainStock trip in between.
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
    /// changes, so switching recipes re-seeds instead of keeping a stale filter. Also cleared
    /// whenever <see cref="ProductionMode.MaintainStock"/>'s branch below runs, since that
    /// unconditionally overwrites the same shared <see cref="Trigger_Threshold.ThresholdFilter"/>
    /// with the recipe's output — leaving this <see langword="true"/> across that would make the
    /// next switch back into <see cref="ProductionMode.ConsumeSurplus"/> skip re-seeding and keep
    /// showing the (now wrong) output filter instead of ingredients.
    /// </summary>
    private bool _consumeSurplusFilterInitialized;

    /// <summary>
    /// Pure state transition for <see cref="_consumeSurplusFilterInitialized"/>, extracted for
    /// testability. Entering
    /// <see cref="ProductionMode.MaintainStock"/> must reset this to <see langword="false"/>,
    /// since that mode's branch unconditionally overwrites the same shared
    /// <see cref="Trigger_Threshold.ThresholdFilter"/> with the recipe's output; otherwise the
    /// next switch back into <see cref="ProductionMode.ConsumeSurplus"/> would skip re-seeding
    /// and keep showing that (now wrong) output filter instead of ingredients.
    /// </summary>
    internal static bool NextConsumeSurplusFilterInitialized(ProductionMode mode) =>
        mode == ProductionMode.ConsumeSurplus;

    private void ConfigureThresholdTriggerFilter()
    {
        if (_recipe == null)
        {
            return;
        }

        if (_mode == ProductionMode.MaintainStock)
        {
            _consumeSurplusFilterInitialized = NextConsumeSurplusFilterInitialized(_mode);

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
        _consumeSurplusFilterInitialized = NextConsumeSurplusFilterInitialized(_mode);

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
    /// <para>
    /// Each <see cref="IngredientCount.filter"/> is often broader than what the recipe actually
    /// accepts — e.g. "butcher creature"'s single ingredient filter just says "Corpses", while
    /// <see cref="RecipeDef.fixedIngredientFilter"/> narrows that down further (excluding
    /// mechanoid/drone corpses and anything that doesn't produce meat). Vanilla bill ingredient
    /// search always applies both, so this must too, or the UI ends up offering ingredients (like
    /// mechanoid corpses) that the bill would never actually accept.
    /// </para>
    /// </summary>
    internal static IEnumerable<ThingDef> AllRecipeIngredientOptions(RecipeDef recipe) =>
        recipe
            .ingredients.SelectMany(ingredient => ingredient.filter.AllowedThingDefs)
            .Where(thingDef =>
                recipe.fixedIngredientFilter == null
                || recipe.fixedIngredientFilter.Allows(thingDef)
            )
            .Distinct();

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

    /// <summary>
    /// Whether a candidate recipe's resolved output overlaps <see cref="Recipe"/>'s currently
    /// tracked output, i.e. whether swapping to it would still track "the same thing, produced
    /// a different way" (Step 5 of <c>Docs/ProductionManagerRework.md</c>, "recipe swap").
    /// Compares whole output sets rather than a single <see cref="ThingDef"/> so it generalizes
    /// to the category-based resolvers from Step 4 — e.g. two different butchery-style recipes
    /// both resolve to the <see cref="ThingCategoryDefOf.MeatRaw"/> category and should match
    /// each other even though neither has a literal <see cref="RecipeDef.products"/> entry. Pure
    /// function, kept separate from <c>ManagerTab_Production</c> so it's unit-testable.
    /// </summary>
    internal static bool RecipeSharesOutput(
        IEnumerable<ThingDef> candidateOutputs,
        IEnumerable<ThingDef> currentOutputs
    ) => candidateOutputs.Intersect(currentOutputs).Any();

    /// <summary>
    /// How <see cref="AggregateLinkedDemand"/> combines multiple linked consumers' individual
    /// demand for the same producer. See <c>Docs/ProductionManagerRework.md</c> Step 6.
    /// </summary>
    internal enum LinkedDemandAggregation
    {
        /// <summary>
        /// Every linked consumer's demand is added together. Default — RimWorld can run
        /// multiple consuming bills concurrently on separate work tables, so this is the only
        /// option that avoids one consumer starving another when both draw the shared stock
        /// down at once.
        /// </summary>
        Sum,

        /// <summary>
        /// Only the single largest linked consumer's demand is used. Deliberately not the
        /// default: it economizes colonist effort at the cost of occasional contention between
        /// consumers sharing the same producer.
        /// </summary>
        MaxOfConsumers,
    }

    /// <summary>
    /// Every producer job this job links to, at the job level rather than per ingredient
    /// <see cref="ThingDef"/> — linking a job once covers every currently-<see cref="AllowedIngredients"/>
    /// def that producer's own <see cref="Recipe"/> resolves to (see <see cref="ResolvedOutputDefs"/>),
    /// so e.g. linking one "butcher creature" job supplies every allowed meat type at once
    /// instead of needing one link per meat def. Only meaningful for a value job in
    /// <see cref="ProductionMode.MaintainStock"/> — see <see cref="ComputeIngredientDemand"/>.
    /// See <c>Docs/ProductionManagerRework.md</c> Step 6.
    /// </summary>
    public HashSet<ManagerJob_Production> LinkedProducers = [];

    /// <summary>
    /// Whether this job's <see cref="Trigger_Threshold.TargetCount"/> is recomputed every
    /// gather pass from every other job's <see cref="LinkedProducers"/> entry pointing
    /// at this job (see <see cref="AggregateLinkedDemand"/>), instead of being left to the
    /// player's own manual slider. Deliberately an explicit opt-in rather than "linked implies
    /// automatic": a player may still want to hand-set a higher target (e.g. building a buffer
    /// ahead of an expansion) even while linked from other jobs.
    /// </summary>
    public bool AutoTargetFromLinks;

    /// <summary>
    /// See <see cref="LinkedDemandAggregation"/>. Only consulted when
    /// <see cref="AutoTargetFromLinks"/> is <see langword="true"/>.
    /// </summary>
    public LinkedDemandAggregation DemandAggregation = LinkedDemandAggregation.Sum;

    /// <summary>
    /// Whether <see cref="AllowedIngredients"/> is recomputed every gather pass to exactly the
    /// ingredients that can produce something at least one linked consumer currently allows,
    /// instead of being left to the player's own checkboxes. E.g. if one linked consumer only
    /// allows bear meat and another only allows muffalo and ibex meat, a "butcher creature"
    /// producer with this on restricts itself to bear/muffalo/ibex corpses instead of every
    /// corpse regardless of whether anything downstream can use the result. Only meaningful for
    /// resolvers with a real ingredient-to-output mapping (see
    /// <see cref="RecipeProductResolver.IngredientsProducing"/>) - for every other recipe this is
    /// a no-op, since there's nothing to narrow down. Deliberately an explicit opt-in, mirroring
    /// <see cref="AutoTargetFromLinks"/>: overwriting a player's own ingredient choices as a side
    /// effect of merely being linked would be surprising, and a player may still want a producer
    /// to keep making ingredients nothing currently consumes (e.g. for trade).
    /// </summary>
    public bool AutoRestrictIngredientsFromLinks;

    private int _linkedDemandBufferCount = 5;

    /// <summary>
    /// How many of this job's own product a linked producer should keep enough ingredient stock
    /// to build from empty, instead of the full <see cref="Trigger_Threshold.TargetCount"/>.
    /// Sizing a linked producer's stock for a full refill of a consumer that maintains e.g. 500
    /// meals would mean permanently keeping 500 meals' worth of raw meat around "just in case,"
    /// which is rarely what's wanted — a handful of iterations' worth of buffer is normally
    /// enough to smooth over the gap until the producer job runs again. Always used clamped to
    /// <c>[1, TargetCount]</c> at read time (see <see cref="GatherJobDataCoroutine"/> and the
    /// tab's consumer-demand summary) rather than clamped on write, so a later drop in
    /// <see cref="Trigger_Threshold.TargetCount"/> doesn't need this field kept in sync.
    /// On change, every currently-<see cref="LinkedProducers"/> job is <c>Untouch</c>ed so
    /// it recomputes its own <see cref="AutoTargetFromLinks"/> target on the very next gather
    /// pass instead of waiting up to its own <see cref="UpdateInterval"/> (a full in-game day by
    /// default) to notice - otherwise moving this slider appeared to do nothing until whatever
    /// day-long window the linked producer happened to next be due for.
    /// </summary>
    public int LinkedDemandBufferCount
    {
        get => _linkedDemandBufferCount;
        set
        {
            var producersToTouch = ProducersNeedingTouchOnBufferCountChange(
                _linkedDemandBufferCount,
                value,
                LinkedProducers
            );
            _linkedDemandBufferCount = value;
            foreach (var producer in producersToTouch)
            {
                producer.Untouch();
            }
        }
    }

    /// <summary>
    /// Which of <paramref name="linkedProducers"/> need <c>Untouch</c>ing after
    /// <see cref="LinkedDemandBufferCount"/> changed from <paramref name="oldBufferCount"/> to
    /// <paramref name="newBufferCount"/> - none if the value didn't actually change (e.g. a
    /// slider re-set to its current position), otherwise every linked producer, since any of
    /// them could be relying on the old buffer for its own computed target. Generic over
    /// <typeparamref name="TJob"/> (rather than <see cref="ManagerJob_Production"/> directly),
    /// the same trick <see cref="WouldCreateCycle{TJob}"/> uses, so it's unit-testable with plain
    /// fakes instead of a live <see cref="Manager"/>.
    /// </summary>
    internal static IEnumerable<TJob> ProducersNeedingTouchOnBufferCountChange<TJob>(
        int oldBufferCount,
        int newBufferCount,
        IEnumerable<TJob> linkedProducers
    ) => oldBufferCount == newBufferCount ? [] : linkedProducers;

    /// <summary>
    /// <paramref name="bufferCount"/> clamped down to <paramref name="targetCount"/> when the
    /// target itself has shrunk below the configured buffer (e.g. a linked consumer's target was
    /// lowered after <see cref="LinkedDemandBufferCount"/> was set) - never clamped up, so a
    /// target of <c>0</c> correctly yields <c>0</c> demand instead of the buffer's own minimum.
    /// Pure function, kept separate from its call sites (<see cref="GatherJobDataCoroutine"/> and
    /// the tab's consumer-demand summary) so it's independently unit-testable.
    /// </summary>
    internal static int EffectiveLinkedDemandBufferCount(int bufferCount, int targetCount) =>
        Math.Min(bufferCount, targetCount);

    /// <summary>
    /// How many raw items are needed per iteration of <paramref name="recipe"/> to satisfy every
    /// ingredient slot that any def in <paramref name="candidateIngredients"/> could fill,
    /// summed across slots (mirrors <see cref="AllRecipeIngredientOptions"/>'s union-across-slots
    /// approach). Uses vanilla's own <see cref="IngredientCount.CountRequiredOfFor"/> — which
    /// converts a slot's raw <see cref="IngredientCount.GetBaseCount"/> (often a nutrition or
    /// volume value, not an item count; e.g. a 0.5-nutrition meal slot needs 10 units of a
    /// 0.05-nutrition meat, not 0) via the recipe's own <see cref="RecipeDef.IngredientValueGetter"/>
    /// — instead of treating <see cref="IngredientCount.GetBaseCount"/> as an item count
    /// directly, which silently truncated fractional nutrition/volume slots to <c>0</c>. Since a
    /// slot is satisfied by any one matching def, not all of them, this can't just sum every
    /// candidate's own required count (that would multiply demand by however many defs happen to
    /// be allowed); instead it takes the highest per-slot requirement among the candidates — the
    /// conservative "enough regardless of which specific item ends up being used" amount. Pure
    /// function, kept separate from <see cref="ComputeIngredientDemand"/> so it's independently
    /// unit-testable.
    /// </summary>
    internal static int IngredientCountPerIteration(
        RecipeDef recipe,
        IEnumerable<ThingDef> candidateIngredients
    )
    {
        var candidates = candidateIngredients as ICollection<ThingDef> ?? [.. candidateIngredients];
        if (candidates.Count == 0)
        {
            return 0;
        }

        var total = 0;
        foreach (var ingredientCount in recipe.ingredients)
        {
            var matching = candidates.Where(ingredientCount.filter.Allows).ToList();
            if (matching.Count == 0)
            {
                continue;
            }
            total += matching.Max(d => ingredientCount.CountRequiredOfFor(d, recipe));
        }
        return total;
    }

    /// <summary>
    /// The buffer of raw items (from <paramref name="coveredIngredients"/>) needed to fully
    /// refill a <see cref="ProductionMode.MaintainStock"/> consumer's own target from empty — the
    /// amount a linked producer should aim to keep in stock for this one consumer. Reuses
    /// <see cref="SharesToIterations"/>/<see cref="YieldPerIteration"/> verbatim rather than the
    /// old pre-Redux mod's <c>Math.Sqrt(count) * baseCount</c> heuristic (see
    /// <c>Docs/ProductionManagerRework.md</c> Step 6), which produced a number with no
    /// transparent relationship to the consumer's actual target. Pure function, kept separate
    /// from <see cref="GatherJobDataCoroutine"/> so it's unit-testable.
    /// </summary>
    internal static int ComputeIngredientDemand(
        RecipeDef consumerRecipe,
        int consumerTargetCount,
        IEnumerable<ThingDef> coveredIngredients
    )
    {
        var iterations = SharesToIterations(consumerTargetCount, YieldPerIteration(consumerRecipe));
        return iterations * IngredientCountPerIteration(consumerRecipe, coveredIngredients);
    }

    /// <summary>
    /// Every <see cref="ThingDef"/> <paramref name="recipe"/> resolves to producing, via its
    /// registered <see cref="RecipeProductResolver"/> (see <c>Docs/ProductionManagerRework.md</c>
    /// Step 4) — empty if it has none. Used to determine which of a linked consumer's
    /// <see cref="AllowedIngredients"/> a given producer job actually covers.
    /// </summary>
    internal static IEnumerable<ThingDef> ResolvedOutputDefs(RecipeDef recipe)
    {
        if (RecipeProductResolvers.ResolverFor(recipe) is not { } resolver)
        {
            return [];
        }

        var filter = new ThingFilter();
        resolver.ConfigureLinkableProductFilter(recipe, filter);
        return filter.AllowedThingDefs;
    }

    /// <summary>
    /// Pure filter used by <see cref="RecipeProductResolver.IngredientsProducing"/> overrides
    /// that have a real, precomputed ingredient-to-output mapping (e.g. corpse ThingDef → meat
    /// ThingDef for butchery) - kept here (rather than duplicated per resolver) so it's
    /// independently unit-testable without a live <see cref="DefDatabase{T}"/>.
    /// </summary>
    internal static IEnumerable<ThingDef> FilterIngredientsProducingDesiredOutputs(
        IEnumerable<ThingDef> candidateIngredients,
        IReadOnlyDictionary<ThingDef, ThingDef> ingredientToOutput,
        IReadOnlyCollection<ThingDef> desiredOutputs
    ) =>
        candidateIngredients.Where(candidate =>
            ingredientToOutput.TryGetValue(candidate, out var output)
            && desiredOutputs.Contains(output)
        );

    /// <summary>
    /// Combines every linked consumer's individual <see cref="ComputeIngredientDemand"/> result
    /// for the same producer into the single target count that producer should aim for, per
    /// <see cref="LinkedDemandAggregation"/>. Pure function, kept separate from
    /// <see cref="GatherJobDataCoroutine"/> so it's unit-testable.
    /// </summary>
    internal static int AggregateLinkedDemand(
        IEnumerable<int> consumerDemands,
        LinkedDemandAggregation mode
    ) =>
        mode == LinkedDemandAggregation.Sum
            ? consumerDemands.Sum()
            : consumerDemands.DefaultIfEmpty(0).Max();

    /// <summary>
    /// Whether linking <paramref name="consumer"/> to <paramref name="candidateProducer"/>
    /// would create a cycle in the linked-job graph (<paramref name="candidateProducer"/>
    /// already depends, directly or transitively via its own <see cref="LinkedProducers"/>,
    /// on <paramref name="consumer"/>). A plain DFS with a visited set to guard against a
    /// pathological chain revisiting the same job twice. Generic over a
    /// <paramref name="linkedSources"/> selector (rather than reading
    /// <see cref="LinkedProducers"/> directly) so it's unit-testable with plain fakes,
    /// the same trick <see cref="FindOwningJob{TJob, TItem}"/> uses.
    /// </summary>
    internal static bool WouldCreateCycle<TJob>(
        TJob consumer,
        TJob candidateProducer,
        Func<TJob, IEnumerable<TJob>> linkedSources
    )
        where TJob : class
    {
        if (candidateProducer == consumer)
        {
            return true;
        }

        var visited = new HashSet<TJob> { candidateProducer };
        var stack = new Stack<TJob>();
        stack.Push(candidateProducer);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var upstream in linkedSources(current))
            {
                if (upstream == consumer)
                {
                    return true;
                }
                if (visited.Add(upstream))
                {
                    stack.Push(upstream);
                }
            }
        }
        return false;
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

    // Scrubs this job out of every other job's LinkedProducers so a deleted producer doesn't
    // leave consumers holding a stale reference — demand computation would otherwise need
    // defensive null-checks that this avoids needing at all.
    public override void CleanUp(ManagerLog? jobLog = null)
    {
        RemoveAllManagedBills();
        foreach (var job in Manager.JobTracker.JobsOfType<ManagerJob_Production>())
        {
            _ = job.LinkedProducers.Remove(this);
        }
    }

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

        Scribe_Values.Look(ref AutoTargetFromLinks, "autoTargetFromLinks");
        Scribe_Values.Look(ref DemandAggregation, "demandAggregation", LinkedDemandAggregation.Sum);
        Scribe_Values.Look(ref _linkedDemandBufferCount, "linkedDemandBufferCount", 5);
        Scribe_Values.Look(
            ref AutoRestrictIngredientsFromLinks,
            "autoRestrictIngredientsFromLinks"
        );

        if (Manager.ScribeSameMapData)
        {
            Scribe_References.Look(ref WorkbenchArea, "workbenchArea");
            Scribe_Collections.Look(
                ref SpecificWorkbenches,
                "specificWorkbenches",
                LookMode.Reference
            );

            // Linked jobs have no cross-map/template identity the way an Area's label does
            // (same reasoning as SpecificWorkbenches just above) — deliberately not scribed on
            // cross-map import, and lost the same way SpecificWorkbenches is.
            Scribe_Collections.Look(ref LinkedProducers, "linkedProducers", LookMode.Reference);

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

        // Recomputed before triggerActive/JobState below, so a target bump from a linked
        // consumer's growing demand is reflected in this very pass, not one pass late. Only
        // MaintainStock consumers contribute a demand number (see ComputeIngredientDemand) —
        // a ConsumeSurplus consumer's own target is meaningless for this purpose and is simply
        // never matched here, since its recipe change/target don't drive a bounded need.
        if (
            Mode == ProductionMode.MaintainStock
            && (AutoTargetFromLinks || AutoRestrictIngredientsFromLinks)
        )
        {
            var myOutputs = ResolvedOutputDefs(Recipe).ToHashSet();
            var linkedConsumers = Manager
                .JobTracker.JobsOfType<ManagerJob_Production>()
                .Where(consumer => consumer.LinkedProducers.Contains(this))
                .ToList();

            if (AutoTargetFromLinks)
            {
                var linkedDemands = linkedConsumers
                    .Where(consumer => consumer.Mode == ProductionMode.MaintainStock)
                    .Select(consumer =>
                        ComputeIngredientDemand(
                            consumer.Recipe!,
                            EffectiveLinkedDemandBufferCount(
                                consumer.LinkedDemandBufferCount,
                                consumer.TriggerThreshold.TargetCount
                            ),
                            consumer.AllowedIngredients.Where(myOutputs.Contains)
                        )
                    );
                TriggerThreshold.TargetCount = AggregateLinkedDemand(
                    linkedDemands,
                    DemandAggregation
                );
            }

            // A ConsumeSurplus consumer still legitimately narrows what a producer should make
            // (it has just as real an AllowedIngredients as a MaintainStock one), even though it
            // can't contribute a target-count demand number above — so every linked consumer
            // counts here regardless of mode, unlike the AutoTargetFromLinks branch.
            if (
                AutoRestrictIngredientsFromLinks
                && RecipeProductResolvers.ResolverFor(Recipe) is { } resolver
            )
            {
                var desiredOutputs = linkedConsumers
                    .SelectMany(consumer => consumer.AllowedIngredients.Where(myOutputs.Contains))
                    .ToHashSet();

                // No linked consumer currently wants anything this producer makes - leave
                // AllowedIngredients alone rather than restricting to an empty set, so a job
                // that momentarily lost all its consumers (e.g. they were deleted) doesn't end
                // up unable to produce anything with no UI-visible reason why.
                if (desiredOutputs.Count > 0)
                {
                    var restricted = resolver
                        .IngredientsProducing(
                            Recipe,
                            AllRecipeIngredientOptions(Recipe),
                            desiredOutputs
                        )
                        .ToHashSet();
                    if (!AllowedIngredients.SetEquals(restricted))
                    {
                        AllowedIngredients.Clear();
                        AllowedIngredients.UnionWith(restricted);
                        Notify_TargetsChanged();
                    }
                }
            }
        }

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
