﻿// Window_TriggerThresholdDetails.cs
// Copyright Karel Kroeze, 2017-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Reflection;

namespace ColonyManagerRedux;

[HotSwappable]
public class WindowTriggerThresholdDetails(Trigger_Threshold trigger) : Window
{
    public string Input = "";
    public Trigger_Threshold Trigger = trigger;

    private ThingFilterUI.UIState _uIState = new();

    public override Vector2 InitialSize => new(300f, 500);

    private static FieldInfo _viewHeightField = AccessTools.Field(typeof(ThingFilterUI), "viewHeight");
    public override void DoWindowContents(Rect inRect)
    {
        var zoneRectRows = Math.Min((int)Math.Ceiling(
            (double)(Trigger.job.Manager.map.zoneManager.AllZones.OfType<Zone_Stockpile>().Count() + 1) /
            StockpileGUI.StockPilesPerRow), 3);
        var zoneRectHeight = zoneRectRows * Constants.ListEntryHeight;

        // set up rects
        var filterRect = new Rect(inRect.ContractedBy(6f));
        filterRect.height -= 2 * Margin + zoneRectHeight + Constants.ListEntryHeight;
        var zoneRect = new Rect(filterRect.xMin, filterRect.yMax + Margin, filterRect.width,
            zoneRectHeight);
        var buttonRect = new Rect(filterRect.xMin, zoneRect.yMax + Margin,
            filterRect.width, Constants.ListEntryHeight);

        // draw thingfilter
        ThingFilterUI.DoThingFilterConfigWindow(filterRect, _uIState, Trigger.ThresholdFilter, Trigger.ParentFilter);
        if (Event.current.type == EventType.Layout)
        {
            // For whatever reason, Rimworld adds a 90 pixel margin to the bottom of the filter
            // list. We don't want that, so remove it again.
            _viewHeightField.SetValue(null, (float)_viewHeightField.GetValue(null) - 90f);
        }

        // draw zone selector
        StockpileGUI.DoStockpileSelectors(zoneRect.position, zoneRect.width, ref Trigger.stockpile, Trigger.job.Manager);

        // draw operator button
        // if (Widgets.ButtonText(buttonRect, Trigger.OpString))
        // {
        //     var list = new List<FloatMenuOption>
        //     {
        //         new( "Lower than",
        //                              delegate { Trigger.Op = Trigger_Threshold.Ops.LowerThan; } ),
        //         new( "Equal to", delegate { Trigger.Op = Trigger_Threshold.Ops.Equals; } ),
        //         new( "Greater than",
        //                              delegate { Trigger.Op = Trigger_Threshold.Ops.HigherThan; } )
        //     };
        //     Find.WindowStack.Add(new FloatMenu(list));
        // }

        // move operator button canvas for count input
        //buttonRect.x = buttonRect.xMax + Margin;

        // if current input is invalid color the element red
        var oldColor = GUI.color;
        if (int.TryParse(Input, out var value))
        {
            Trigger.TargetCount = value;
            if (Trigger.TargetCount > Trigger.MaxUpperThreshold)
            {
                Trigger.MaxUpperThreshold = Trigger.TargetCount;
            }
        }
        else
        {
            GUI.color = new Color(1f, 0f, 0f);
        }

        // draw the input field
        Input = Widgets.TextField(buttonRect, Input);
        GUI.color = oldColor;

        // close on enter
        if (Event.current.type == EventType.KeyDown &&
             Event.current.keyCode == KeyCode.Return)
        {
            Event.current.Use();
            Find.WindowStack.TryRemove(this);
        }
    }

    public override void PreOpen()
    {
        base.PreOpen();
        Input = Trigger.TargetCount.ToString();
    }
}
