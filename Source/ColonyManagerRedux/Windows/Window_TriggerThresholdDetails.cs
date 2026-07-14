// Window_TriggerThresholdDetails.cs
// Copyright Karel Kroeze, 2017-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// A window for configuring the details of a threshold trigger, including filter, stockpile, and operator.
/// </summary>
[HotSwappable]
public class WindowTriggerThresholdDetails(Trigger_Threshold trigger) : Window
{
    private const string TargetCountControlName = "WindowTriggerThresholdDetails_TargetCount";

    private string _input = "";
    private readonly Trigger_Threshold _trigger = trigger;

    private readonly ThingFilterUI.UIState _uIState = new();

    /// <inheritdoc/>
    public override Vector2 InitialSize => new(300f, 500);

    /// <inheritdoc/>
    public override void DoWindowContents(Rect inRect)
    {
        var zoneRectRows = Math.Min(
            (int)
                Math.Ceiling(
                    (double)(
                        _trigger
                            .Job.Manager.map.zoneManager.AllZones.OfType<Zone_Stockpile>()
                            .Count() + 1
                    ) / StockpileGUI.StockPilesPerRow
                ),
            3
        );
        var zoneRectHeight = zoneRectRows * Constants.ListEntryHeight;

        // set up rects
        var filterRect = new Rect(inRect.ContractedBy(6f));
        filterRect.height -= (2 * Margin) + zoneRectHeight + Constants.ListEntryHeight;
        var zoneRect = new Rect(
            filterRect.xMin,
            filterRect.yMax + Margin,
            filterRect.width,
            zoneRectHeight
        );
        var buttonRect = new Rect(
            filterRect.xMin,
            zoneRect.yMax + Margin,
            (filterRect.width - Margin) / 2f,
            Constants.ListEntryHeight
        );

        // draw thingfilter
        ThingFilterUI.DoThingFilterConfigWindow(
            filterRect,
            _uIState,
            _trigger.ThresholdFilter,
            _trigger.ParentFilter
        );
        if (Event.current.type == EventType.Layout)
        {
            // For whatever reason, Rimworld adds a 90 pixel margin to the bottom of the filter
            // list in ThingFilterUI.DoThingFilterConfigWindow.
            // We don't want that, so let's remove it again.
            ThingFilterUI.viewHeight -= 90f;
        }

        // draw zone selector
        _ = StockpileGUI.DoStockpileSelectors(
            zoneRect.position,
            zoneRect.width,
            ref _trigger.StockpileRef,
            _trigger.Job.Manager
        );

        // draw operator button
        if (Widgets.ButtonText(buttonRect, _trigger.OpString))
        {
            var list = new List<FloatMenuOption>();
            if (_trigger.SupportsOp(Trigger_Threshold.Ops.LowerThan))
            {
                list.Add(
                    new(
                        "ColonyManagerRedux.Threshold.LowerThan".Translate(),
                        () => _trigger.Op = Trigger_Threshold.Ops.LowerThan
                    )
                );
            }
            if (_trigger.SupportsOp(Trigger_Threshold.Ops.Equals))
            {
                list.Add(
                    new(
                        "ColonyManagerRedux.Threshold.EqualTo".Translate(),
                        () => _trigger.Op = Trigger_Threshold.Ops.Equals
                    )
                );
            }
            if (_trigger.SupportsOp(Trigger_Threshold.Ops.NotEquals))
            {
                list.Add(
                    new(
                        "ColonyManagerRedux.Threshold.NotEqualTo".Translate(),
                        () => _trigger.Op = Trigger_Threshold.Ops.NotEquals
                    )
                );
            }
            if (_trigger.SupportsOp(Trigger_Threshold.Ops.HigherThan))
            {
                list.Add(
                    new(
                        "ColonyManagerRedux.Threshold.GreaterThan".Translate(),
                        () => _trigger.Op = Trigger_Threshold.Ops.HigherThan
                    )
                );
            }
            Find.WindowStack.Add(new FloatMenu(list));
        }
        string? opTooltip = null;
        opTooltip = _trigger.Op switch
        {
            Trigger_Threshold.Ops.LowerThan => (string)
                "ColonyManagerRedux.Threshold.LowerThan.Tip".Translate(_trigger.TargetCount),
            Trigger_Threshold.Ops.Equals => (string)
                "ColonyManagerRedux.Threshold.EqualTo.Tip".Translate(_trigger.TargetCount),
            Trigger_Threshold.Ops.NotEquals => (string)
                "ColonyManagerRedux.Threshold.NotEqualTo.Tip".Translate(_trigger.TargetCount),
            Trigger_Threshold.Ops.HigherThan => (string)
                "ColonyManagerRedux.Threshold.GreaterThan.Tip".Translate(_trigger.TargetCount),
            _ => "Unknown operator",
        };
        TooltipHandler.TipRegion(buttonRect, opTooltip);

        // move operator button canvas for count input
        buttonRect.x = buttonRect.xMax + Margin;

        // if the field isn't being edited, re-sync it from the trigger so it picks up
        // changes made elsewhere (e.g. the slider drawn on the main tab) instead of
        // silently overwriting them with a stale cached value below
        if (GUI.GetNameOfFocusedControl() != TargetCountControlName)
        {
            _input = _trigger.TargetCount.ToString(CultureInfo.InvariantCulture);
        }

        // if current input is invalid color the element red
        var oldColor = GUI.color;
        if (int.TryParse(_input, out var value))
        {
            _trigger.TargetCount = value;
            if (_trigger.TargetCount > _trigger.MaxUpperThreshold)
            {
                _trigger.MaxUpperThreshold = _trigger.TargetCount;
            }
        }
        else
        {
            GUI.color = new Color(1f, 0f, 0f);
        }

        // draw the input field
        GUI.SetNextControlName(TargetCountControlName);
        _input = Widgets.TextField(buttonRect, _input);
        TooltipHandler.TipRegion(buttonRect, opTooltip);
        GUI.color = oldColor;

        // close on enter
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return)
        {
            Event.current.Use();
            _ = Find.WindowStack.TryRemove(this);
        }
    }

    /// <inheritdoc/>
    public override void PreOpen()
    {
        base.PreOpen();
        _input = _trigger.TargetCount.ToString(CultureInfo.InvariantCulture);
    }
}
