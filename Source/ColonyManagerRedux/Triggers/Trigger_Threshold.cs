// Trigger_Threshold.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.Extensions;
using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

/// <summary>
/// Represents a trigger that activates based on a threshold value for a product or resource.
/// </summary>
[HotSwappable]
public sealed class Trigger_Threshold : Trigger
{
    /// <summary>
    /// Supported comparison operations for the threshold trigger.
    /// </summary>
    public enum Ops
    {
        /// <summary>
        /// The current count is less than the target count.
        /// </summary>
        LowerThan,

        /// <summary>
        /// The current count is equal to the target count.
        /// </summary>
        Equals,

        /// <summary>
        /// The current count is greater than the target count.
        /// </summary>
        HigherThan,

        /// <summary>
        /// The current count is not equal to the target count.
        /// </summary>
        NotEquals,
    }

    /// <summary>
    /// Indicates which direction, if any, a job should move its managed count in order to
    /// satisfy this trigger.
    /// </summary>
    public enum Directive
    {
        /// <summary>
        /// The count should be increased (e.g. more designations should be added).
        /// </summary>
        Increase,

        /// <summary>
        /// The count should be decreased (e.g. designations should be removed).
        /// </summary>
        Decrease,

        /// <summary>
        /// The count already satisfies the trigger; no directional change is needed.
        /// </summary>
        Hold,
    }

    /// <summary>
    /// All defined <see cref="Ops"/> values, in order of preference — the first entry is used
    /// as the fallback/default op (see <see cref="DefaultOp"/>).
    /// </summary>
    public static readonly IReadOnlyList<Ops> AllOps =
    [
        Ops.LowerThan,
        Ops.Equals,
        Ops.HigherThan,
        Ops.NotEquals,
    ];

    /// <summary>
    /// Ops usable by jobs that can only add to their tracked count (via designations) or
    /// stop/undo pending work — never actively deplete stock that's already been collected.
    /// Excludes <see cref="Ops.HigherThan"/>, whose entire purpose is driving an active
    /// decrease.
    /// </summary>
    public static readonly IReadOnlyList<Ops> AccumulationOnlyOps =
    [
        Ops.LowerThan,
        Ops.Equals,
        Ops.NotEquals,
    ];

    private IReadOnlyList<Ops> _supportedOps;
    private HashSet<Ops> _supportedOpsSet;

    /// <summary>
    /// Gets the set of <see cref="Ops"/> values that the owning job supports.
    /// </summary>
    public IReadOnlyCollection<Ops> SupportedOps => _supportedOpsSet;

    /// <summary>
    /// Gets the op used as this job's default and as the fallback when a saved game specifies
    /// an op that's no longer in <see cref="SupportedOps"/> — the first entry of the
    /// <c>supportedOps</c> collection passed to the constructor.
    /// </summary>
    public Ops DefaultOp => _supportedOps[0];

    /// <summary>
    /// Gets whether the given <paramref name="op"/> is supported by the owning job.
    /// </summary>
    public bool SupportsOp(Ops op) => _supportedOpsSet.Contains(op);

    /// <summary>
    /// Narrows this trigger's supported ops to <paramref name="supportedOps"/>, migrating the
    /// current op to its new <see cref="DefaultOp"/> if it's no longer supported.
    /// </summary>
    /// <param name="supportedOps">
    /// The <see cref="Ops"/> values the owning job supports, in order of preference — see
    /// <see cref="DefaultOp"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="supportedOps"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="supportedOps"/> is empty.</exception>
    public void RestrictSupportedOps(IReadOnlyList<Ops> supportedOps)
    {
        if (supportedOps == null)
        {
            throw new ArgumentNullException(nameof(supportedOps));
        }
        if (supportedOps.Count == 0)
        {
            throw new ArgumentException(
                $"{nameof(supportedOps)} must not be empty.",
                nameof(supportedOps)
            );
        }
        _supportedOps = supportedOps;
        _supportedOpsSet = [.. supportedOps];

        if (!SupportsOp(op))
        {
            var unsupportedOp = op;
            op = MigrateUnsupportedOp(op, SupportedOps, DefaultOp);
            ColonyManagerReduxMod.Instance.LogWarningOnce(
                $"Trigger_Threshold operator '{unsupportedOp}' is no longer supported by "
                    + $"this job; falling back to {op}.",
                ref _hasReportedUnsupportedOperator
            );
        }
    }

    private bool allowAnyThreshold;

    /// <summary>
    /// Gets or sets whether any threshold is allowed (ignores parent filter restrictions).
    /// </summary>
    public bool AllowAnyThreshold
    {
        get => allowAnyThreshold;
        set
        {
            allowAnyThreshold = value;
            if (allowAnyThreshold)
            {
                ParentFilter = ThingFilter.CreateOnlyEverStorableThingFilter();
            }
        }
    }

    private bool countAllOnMap;

    /// <summary>
    /// Gets or sets whether to count all matching items on the map, not just in stockpiles.
    /// </summary>
    public bool CountAllOnMap
    {
        get => countAllOnMap;
        set => countAllOnMap = value;
    }

    private int maxUpperThreshold;

    /// <summary>
    /// Gets or sets the maximum allowed value for the upper threshold.
    /// </summary>
    public int MaxUpperThreshold
    {
        get => maxUpperThreshold;
        set => maxUpperThreshold = value;
    }

    private Ops op;

    /// <summary>
    /// Gets or sets the comparison operation for the threshold.
    /// </summary>
    public Ops Op
    {
        get => op;
        set
        {
            op = value;
            _hasReportedIncorrectOperator = false;
        }
    }

    /// <summary>
    /// Gets the parent filter used for allowed things.
    /// </summary>
    public ThingFilter ParentFilter { get; private set; }

    private Zone_Stockpile? stockpile;

    /// <summary>
    /// Gets or sets the stockpile associated with this trigger.
    /// </summary>
    public Zone_Stockpile? Stockpile
    {
        get => stockpile;
        set => stockpile = value;
    }

    /// <summary>
    /// Gets a reference to the stockpile associated with this trigger.
    /// </summary>
    public ref Zone_Stockpile? StockpileRef => ref stockpile;

    private int targetCount;

    /// <summary>
    /// Gets or sets the target count for the threshold.
    /// </summary>
    public int TargetCount
    {
        get => targetCount;
        set => targetCount = value;
    }

    /// <summary>
    /// Gets a label representing the operation and target count.
    /// </summary>
    public string TargetLabel => $"{OpString} {targetCount}";

    private ThingFilter thresholdFilter;

    /// <summary>
    /// Gets the filter used to determine which things are counted toward the threshold.
    /// </summary>
    public ThingFilter ThresholdFilter => thresholdFilter;
    private readonly CachedValue<int> _cachedCurrentCount = new(0);

    private string? _stockpile_scribe;

    /// <summary>
    /// Event invoked when settings are changed.
    /// </summary>
    public Action? SettingsChanged { get; set; }

    /// <summary>
    /// Event invoked when AllowAnyThreshold is changed.
    /// </summary>
    public Action? AllowAnyThresholdChanged { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Trigger_Threshold"/> class.
    /// Starts out supporting <see cref="AllOps"/>.
    /// </summary>
    /// <param name="job">The manager job associated with this trigger.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="job"/> is null.</exception>
    public Trigger_Threshold(ManagerJob job)
        : this(job, AllOps) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="Trigger_Threshold"/> class.
    /// </summary>
    /// <param name="job">The manager job associated with this trigger.</param>
    /// <param name="supportedOps">
    /// The <see cref="Ops"/> values the owning job supports, in order of preference — the
    /// first entry becomes <see cref="DefaultOp"/>, used both as this trigger's initial op and
    /// as the fallback when migrating a saved game whose op is no longer supported.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="job"/> or <paramref name="supportedOps"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown if <paramref name="supportedOps"/> is empty.</exception>
    public Trigger_Threshold(ManagerJob job, IReadOnlyList<Ops> supportedOps)
        : base(job)
    {
        if (job == null)
        {
            throw new ArgumentNullException(nameof(job));
        }
        if (supportedOps == null)
        {
            throw new ArgumentNullException(nameof(supportedOps));
        }
        if (supportedOps.Count == 0)
        {
            throw new ArgumentException(
                $"{nameof(supportedOps)} must not be empty.",
                nameof(supportedOps)
            );
        }
        _supportedOps = supportedOps;
        _supportedOpsSet = [.. _supportedOps];

        var settings = ColonyManagerReduxMod.Settings;
        countAllOnMap = settings.DefaultCountAllOnMap;

        ParentFilter = ThingFilter.CreateOnlyEverStorableThingFilter();

        thresholdFilter = new ThingFilter(ThresholdFilter_SettingsChanged);
        ThresholdFilter.SetDisallowAll();

        op = DefaultOp;
        maxUpperThreshold = job.MaxUpperThreshold;
        targetCount = settings.DefaultTargetCount;
    }

    private void ThresholdFilter_SettingsChanged()
    {
        _cachedCurrentCount.Invalidate();
        SettingsChanged?.Invoke();
    }

    private int CurrentCountRaw =>
        Job.Manager.map.CountProducts(ThresholdFilter, stockpile, CountAllOnMap);

    /// <summary>
    /// Gets the current count of items matching the threshold filter.
    /// </summary>
    /// <param name="cached">Whether to use the cached value if available.</param>
    /// <returns>The current count.</returns>
    public int GetCurrentCount(bool cached = true) =>
        cached && _cachedCurrentCount.TryGetValue(out var value)
            ? value
            : _cachedCurrentCount.Update(CurrentCountRaw);

    /// <summary>
    /// Gets a coroutine that updates the count of items matching the threshold filter.
    /// </summary>
    /// <param name="count">A boxed integer to store the result.</param>
    /// <returns>A coroutine for updating the count.</returns>
    public Coroutine GetCurrentCountCoroutine(Boxed<int> count) =>
        Job.Manager.map.CountProductsCoroutine(ThresholdFilter, count, stockpile, CountAllOnMap);

    /// <summary>
    /// Gets a window displaying details for this threshold trigger.
    /// </summary>
    public WindowTriggerThresholdDetails DetailsWindow
    {
        get
        {
            var window = new WindowTriggerThresholdDetails(this)
            {
                closeOnClickedOutside = true,
                draggable = true,
            };
            return window;
        }
    }

    /// <summary>
    /// Gets whether the trigger is valid (at least one allowed def in the filter).
    /// </summary>
    public bool IsValid => ThresholdFilter.AllowedDefCount > 0;

    /// <summary>
    /// Gets the string representation of the current operation.
    /// </summary>
    public string OpString =>
        op switch
        {
            Ops.LowerThan => "<\u200B",
            Ops.Equals => "=",
            Ops.HigherThan => ">",
            Ops.NotEquals => "!=",
            _ => "?",
        };

    private bool _hasReportedIncorrectOperator;
    private bool _hasReportedUnsupportedOperator;

    /// <summary>
    /// Gets the current state of the trigger (whether the job should be active).
    /// </summary>
    public override bool State => !DoesCountMeetTarget(GetCurrentCount());

    /// <inheritdoc/>
    public override string StatusTooltip =>
        Job.ExpectedAdditionalCount > 0
            ? "ColonyManagerRedux.Thresholds.ThresholdCountWithExpected".Translate(
                GetCurrentCount(),
                Job.ExpectedAdditionalCount,
                TargetLabel
            )
            : "ColonyManagerRedux.Thresholds.ThresholdCount".Translate(
                GetCurrentCount(),
                TargetLabel
            );

    /// <inheritdoc/>
    public override void DrawVerticalProgressBars(Rect progressRect, bool active)
    {
        progressRect.xMin += progressRect.width - 10;
        DrawVerticalProgressBar(
            progressRect,
            GetCurrentCount(),
            targetCount,
            StatusTooltip,
            active,
            Resources.BarBackgroundActiveTexture,
            Job.ExpectedAdditionalCount
        );
    }

    /// <inheritdoc/>
    public override void DrawHorizontalProgressBars(Rect progressRect, bool active)
    {
        progressRect.height = SmallIconSize;
        DrawHorizontalProgressBar(
            progressRect,
            GetCurrentCount(),
            targetCount,
            StatusTooltip,
            active,
            Resources.BarBackgroundActiveTexture,
            Job.ExpectedAdditionalCount
        );
    }

    /// <inheritdoc/>
    public override void DrawTriggerConfig(
        ref Vector2 cur,
        float width,
        float entryHeight,
        string? label = null,
        string? tooltip = null,
        List<Designation>? targets = null,
        Action? onOpenFilterDetails = null,
        Func<Designation, string?>? designationLabelGetter = null
    )
    {
        if (targets == null)
        {
            throw new ArgumentNullException(nameof(targets));
        }

        var hasTargets = !targets.NullOrEmpty();

        // target threshold
        var thresholdLabelRect = new Rect(
            cur.x,
            cur.y,
            width - (hasTargets ? SmallIconSize + (Margin * 2) : 0f),
            entryHeight
        );
        var detailsWindowButtonRect = new Rect(
            thresholdLabelRect.xMax - SmallIconSize - Margin,
            cur.y + ((entryHeight - SmallIconSize) / 2f),
            SmallIconSize,
            SmallIconSize
        );
        var targetsButtonRect = new Rect(
            thresholdLabelRect.xMax + Margin,
            cur.y + ((entryHeight - SmallIconSize) / 2f),
            SmallIconSize,
            SmallIconSize
        );
        cur.y += entryHeight;

        var thresholdRect = new Rect(cur.x, cur.y, width, SliderHeight);
        cur.y += SliderHeight;

        Widgets.DrawHighlightIfMouseover(thresholdLabelRect);
        if (label.NullOrEmpty())
        {
            label =
                "ColonyManagerRedux.Thresholds.ThresholdCount".Translate(
                    GetCurrentCount(),
                    targetCount
                ) + ":";
        }

        if (tooltip.NullOrEmpty())
        {
            tooltip = "ColonyManagerRedux.Thresholds.ThresholdCountTooltip".Translate(
                GetCurrentCount(),
                targetCount
            );
        }

        IlyvionWidgets.Label(thresholdLabelRect, label!, tooltip, TextAnchor.MiddleLeft);

        // add a little icon to mark interactivity
        GUI.color = Mouse.IsOver(thresholdLabelRect) ? GenUI.MouseoverColor : Color.white;
        GUI.DrawTexture(detailsWindowButtonRect, Resources.Cog);
        GUI.color = Color.white;
        if (Widgets.ButtonInvisible(thresholdLabelRect))
        {
            onOpenFilterDetails?.Invoke();
            Find.WindowStack.Add(DetailsWindow);
        }

        // target list
        if (hasTargets)
        {
            if (Widgets.ButtonImage(targetsButtonRect, Resources.Search))
            {
                var options = new List<FloatMenuOption>();
                foreach (var designation in targets!)
                {
                    var option = string.Empty;
                    Action? onClick = () =>
                        Find.WindowStack.TryRemove(typeof(MainTabWindow_Manager), false);
                    Action<Rect>? onHover = null;
                    if (designation.target.HasThing)
                    {
                        var thing = designation.target.Thing;
                        option = designationLabelGetter?.Invoke(designation) ?? thing.LabelCap;
                        onClick += () => CameraJumper.TryJumpAndSelect(thing);
                        onHover += (c) =>
                        {
                            if (!Find.CameraDriver.IsPanning())
                            {
                                CameraJumper.TryJump(thing);
                            }
                        };
                    }
                    else
                    {
                        var cell = designation.target.Cell;
                        if (cell.IsValid)
                        {
                            var map = designation.Map;
                            option =
                                designationLabelGetter?.Invoke(designation)
                                ?? cell.GetTerrain(map).LabelCap;
                            onClick += () => CameraJumper.TryJump(cell, map);
                            onHover += (c) =>
                            {
                                if (!Find.CameraDriver.IsPanning())
                                {
                                    CameraJumper.TryJump(cell, map);
                                }
                            };
                        }
                        else
                        {
                            option = "Invalid designation. This should never happen.";
                            onClick = null;
                        }
                    }

                    options.Add(
                        new FloatMenuOption(option, onClick, MenuOptionPriority.Default, onHover)
                    );
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        // var allowAnyThresholdLabel = "ColonyManagerRedux.Threshold.AllowAnyThreshold".Translate();
        // Text.Font = GameFont.Medium;
        // var allowAnyThresholdLabelHeight = Text.CalcHeight(allowAnyThresholdLabel, width - Margin);

        var allowAnyThresholdRect = new Rect(
            cur.x,
            cur.y,
            width,
            //allowAnyThresholdLabelHeight
            entryHeight
        );
        cur.y += entryHeight; //allowAnyThresholdLabelHeight;

        var currentAllowAnyThreshold = allowAnyThreshold;
        //allowAnyThresholdRect.height = allowAnyThresholdLabelHeight;
        Utilities.DrawToggle(
            allowAnyThresholdRect,
            "ColonyManagerRedux.Threshold.AllowAnyThreshold".Translate(),
            "ColonyManagerRedux.Threshold.AllowAnyThreshold.Tip".Translate(),
            ref allowAnyThreshold,
            leaveRoomForAdditionalIcon: false
        );
        if (currentAllowAnyThreshold != allowAnyThreshold)
        {
            if (allowAnyThreshold)
            {
                // if we allow any threshold, we need to reset the parent filter
                ParentFilter = ThingFilter.CreateOnlyEverStorableThingFilter();
            }
            AllowAnyThresholdChanged?.Invoke();
            if (!allowAnyThreshold)
            {
                // if we disallow any threshold, we need to disallow all items not in the parent filter
                ThresholdFilter.SetDisallowAll(ParentFilter.AllowedThingDefs);
            }
        }

        // var iconRect = new Rect(
        //     allowAnyThresholdRect.xMax - SmallIconSize - Margin,
        //     0f,
        //     SmallIconSize,
        //     SmallIconSize).CenteredOnYIn(allowAnyThresholdRect);
        // iconRect.x -= SmallIconSize + Margin;
        // TooltipHandler.TipRegion(
        //     iconRect,
        //     "ColonyManagerRedux.Threshold.AllowAnyThreshold.Warning".Translate());
        // GUI.color = allowAnyThreshold
        //     ? Resources.Orange
        //     : Color.grey;
        // GUI.DrawTexture(iconRect, Resources.Warning);
        // GUI.color = Color.white;

        var countAllOnMapRect = new Rect(cur.x, cur.y, width, entryHeight);
        cur.y += entryHeight;

        Utilities.DrawToggle(
            countAllOnMapRect,
            "ColonyManagerRedux.Threshold.CountAllOnMap".Translate(),
            "ColonyManagerRedux.Threshold.CountAllOnMap.Tip".Translate(),
            ref countAllOnMap,
            true
        );
        targetCount = (int)
            Widgets.HorizontalSlider(thresholdRect, targetCount, 0, maxUpperThreshold);
    }

    /// <inheritdoc />
    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref targetCount, "count");
        Scribe_Values.Look(ref maxUpperThreshold, "maxUpperThreshold");
        Scribe_Values.Look(ref op, "operator");
        Scribe_Deep.Look(
            ref thresholdFilter,
            "thresholdFilter",
            (object)ThresholdFilter_SettingsChanged
        );
        Scribe_Values.Look(ref allowAnyThreshold, "allowAnyThreshold");
        Scribe_Values.Look(ref countAllOnMap, "countAllOnMap");

        // stockpile needs special treatment - is not referenceable.
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            _stockpile_scribe = stockpile?.ToString() ?? "null";
        }

        Scribe_Values.Look(ref _stockpile_scribe, "stockpile", "null");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            stockpile =
                Job.Manager.map.zoneManager.AllZones.FirstOrDefault(z =>
                    z is Zone_Stockpile && z.label == _stockpile_scribe
                ) as Zone_Stockpile;
        }
    }

    /// <summary>
    /// Determines whether the given count meets the target based on the current operation.
    /// </summary>
    /// <param name="count">The count to check.</param>
    /// <returns>True if the count meets the target; otherwise, false.</returns>
    public bool DoesCountMeetTarget(int count)
    {
        var result = Evaluate(op, count, targetCount);
        if (result is null)
        {
            ColonyManagerReduxMod.Instance.LogWarningOnce(
                "Trigger_ThingThreshold was defined without a correct operator",
                ref _hasReportedIncorrectOperator
            );
            return true;
        }
        return result.Value;
    }

    /// <summary>
    /// Evaluates <paramref name="count"/> against <paramref name="targetCount"/> using
    /// <paramref name="op"/>. Pure function, kept separate from <see cref="DoesCountMeetTarget"/>
    /// so the comparison logic is unit-testable without a live <see cref="ManagerJob"/>.
    /// </summary>
    /// <returns>The comparison result, or <see langword="null"/> if <paramref name="op"/> is not a recognized value.</returns>
    internal static bool? Evaluate(Ops op, int count, int targetCount) =>
        op switch
        {
            Ops.LowerThan => count >= targetCount,
            Ops.Equals => count == targetCount,
            Ops.HigherThan => count <= targetCount,
            Ops.NotEquals => count != targetCount,
            _ => null,
        };

    /// <summary>
    /// Determines whether the given count should be increased, decreased, or held in order
    /// to satisfy this trigger, based on the current operation. Where
    /// <see cref="DoesCountMeetTarget"/> only answers "is this satisfied", this answers
    /// "which way do I need to move it".
    /// </summary>
    /// <param name="count">The count to evaluate.</param>
    /// <returns>
    /// The direction the count should move in. Falls back to <see cref="Directive.Hold"/>
    /// (mirroring <see cref="DoesCountMeetTarget"/>'s "assume satisfied" fallback) and logs a
    /// warning once if the operation is not recognized.
    /// </returns>
    public Directive GetDirective(int count)
    {
        var result = EvaluateDirective(op, count, targetCount);
        if (result is null)
        {
            ColonyManagerReduxMod.Instance.LogWarningOnce(
                "Trigger_ThingThreshold was defined without a correct operator",
                ref _hasReportedIncorrectOperator
            );
            return Directive.Hold;
        }
        return result.Value;
    }

    /// <summary>
    /// Evaluates <paramref name="count"/> against <paramref name="targetCount"/> using
    /// <paramref name="op"/>, producing the directive for which way the count should move.
    /// Pure function, kept separate from <see cref="GetDirective"/> so the logic is
    /// unit-testable without a live <see cref="ManagerJob"/>. For every op, a
    /// <see cref="Directive.Hold"/> result corresponds exactly to <see cref="Evaluate"/>
    /// returning <see langword="true"/> for the same arguments.
    /// </summary>
    /// <returns>The directive, or <see langword="null"/> if <paramref name="op"/> is not a recognized value.</returns>
    internal static Directive? EvaluateDirective(Ops op, int count, int targetCount) =>
        op switch
        {
            Ops.LowerThan => count < targetCount ? Directive.Increase : Directive.Hold,
            Ops.Equals => count < targetCount ? Directive.Increase
            : count > targetCount ? Directive.Decrease
            : Directive.Hold,
            Ops.HigherThan => count > targetCount ? Directive.Decrease : Directive.Hold,
            // count == targetCount must move away from target but direction is arbitrary;
            // default to Increase, mirroring LowerThan's bias toward accumulation.
            Ops.NotEquals => count == targetCount ? Directive.Increase : Directive.Hold,
            _ => null,
        };

    /// <summary>
    /// Returns <paramref name="op"/> unchanged if it's in <paramref name="supportedOps"/>,
    /// otherwise falls back to <paramref name="fallbackOp"/> — used when a saved game
    /// specifies an op that the owning job no longer supports (e.g. after a job's
    /// supported-ops set is narrowed in an update).
    /// </summary>
    internal static Ops MigrateUnsupportedOp(
        Ops op,
        IReadOnlyCollection<Ops> supportedOps,
        Ops fallbackOp
    ) => supportedOps.Contains(op) ? op : fallbackOp;
}
