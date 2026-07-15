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
    /// Carries the decisions made by <see cref="GatherJobDataCoroutine"/> (which doesn't touch
    /// the game) to <see cref="ExecuteJobDataCoroutine"/> (which applies them).
    /// </summary>
    internal sealed class ProductionWorkData
    {
        public List<Bill_Production> DeadBillsToForget = [];
        public List<Building_WorkTable> WorkTablesNeedingNewBill = [];
        public List<Bill_Production> BillsToActivate = [];
        public List<Bill_Production> BillsToSuspend = [];
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
    }

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
        var eligibleWorkTables = Manager
            .map.listerBuildings.allBuildingsColonist.OfType<Building_WorkTable>()
            .Where(wt => wt.billStack != null && recipeUsers.Contains(wt.def))
            .ToList();
        yield return new ResumeAfterTicks(ticksBetweenOperations);

        var triggerActive = TriggerThreshold.State;
        JobState = triggerActive ? ManagerJobState.Active : ManagerJobState.Completed;

        jobLog.AddDetail(
            "ColonyManagerRedux.Production.Logs.CurrentCount".Translate(
                TriggerThreshold.GetCurrentCount(),
                TriggerThreshold.TargetCount
            )
        );

        foreach (var workTable in eligibleWorkTables)
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

        foreach (var workTable in data.WorkTablesNeedingNewBill)
        {
            var recipe = Recipe!;
            var bill = recipe.UsesUnfinishedThing
                ? new Bill_ProductionWithUft(recipe, null)
                : new Bill_Production(recipe, null);
            bill.repeatMode = BillRepeatModeDefOf.Forever;
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
    }
}
