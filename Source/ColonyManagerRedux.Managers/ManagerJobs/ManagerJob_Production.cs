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
            ConfigureThresholdTriggerFilter();
            Notify_TargetsChanged();
        }
    }

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
        Trigger = new Trigger_Threshold(this, Trigger_Threshold.AccumulationOnlyOps)
        {
            AllowAnyThresholdChanged = ConfigureThresholdTriggerFilter,
        };
        ConfigureThresholdTriggerFilter();
    }

    private void ConfigureThresholdTriggerFilter()
    {
        if (!TriggerThreshold.AllowAnyThreshold)
        {
            TriggerThreshold.ParentFilter.SetDisallowAll();
            if (_recipe?.ProducedThingDef is { } parentProducedThingDef)
            {
                TriggerThreshold.ParentFilter.SetAllow(parentProducedThingDef, true);
            }
        }

        TriggerThreshold.ThresholdFilter.SetDisallowAll();
        if (_recipe?.ProducedThingDef is { } producedThingDef)
        {
            TriggerThreshold.ThresholdFilter.SetAllow(producedThingDef, true);
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

    public override bool IsValid => base.IsValid && Recipe != null;

    public override IEnumerable<string> Targets => Recipe != null ? [Recipe.LabelCap] : [];

    public override WorkTypeDef? WorkTypeDef => Recipe?.requiredGiverWorkType;

    public override void CleanUp(ManagerLog? jobLog = null) => RemoveAllManagedBills();

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Defs.Look(ref _recipe, "recipe");
        Scribe_Collections.Look(ref _managedBills, "managedBills", LookMode.Reference);
        Scribe_Values.Look(ref AssignmentMode, "assignmentMode", WorkbenchAssignmentMode.All);
        Scribe_Values.Look(ref InvertWorkbenchArea, "invertWorkbenchArea");
        Scribe_Values.Look(ref AllowedSkillRange, "allowedSkillRange", new IntRange(0, 20));
        Scribe_Values.Look(ref IngredientSearchRadius, "ingredientSearchRadius", 999f);
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

        var triggerActive = TriggerThreshold.State;
        JobState = triggerActive ? ManagerJobState.Active : ManagerJobState.Completed;

        jobLog.AddDetail(
            "ColonyManagerRedux.Production.Logs.CurrentCount".Translate(
                TriggerThreshold.GetCurrentCount(),
                TriggerThreshold.TargetCount
            )
        );

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
            var recipe = Recipe!;
            var bill = recipe.UsesUnfinishedThing
                ? new Bill_ProductionWithUft(recipe, null)
                : new Bill_Production(recipe, null);
            bill.repeatMode = BillRepeatModeDefOf.Forever;
            bill.allowedSkillRange = AllowedSkillRange;
            bill.ingredientSearchRadius = IngredientSearchRadius;
            bill.SetStoreMode(
                StoreMode,
                StoreMode == BillStoreModeDefOf.SpecificStockpile ? StoreGroup : null
            );
            workTable.billStack.AddBill(bill);
            _managedBills.Add(bill);
            workDone.Value = true;

            jobLog.AddDetail(
                "ColonyManagerRedux.Production.Logs.BillAdded".Translate(
                    recipe.LabelCap,
                    workTable.LabelCap
                )
            );

            yield return new ResumeAfterTicks(ticksBetweenOperations);
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
    }
}
