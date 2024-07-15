// ManagerTab.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ColonyManagerRedux;

#pragma warning disable CS8618 // Set by ManagerTabMaker.MakeManagerTab
public abstract class ManagerTab(Manager manager)
#pragma warning restore CS8618
{
    public float DefaultLeftRowSize = 300f;

    public ManagerTabDef def;

    public Manager manager = manager;
    private ManagerJob? selected;

    public virtual string DisabledReason => "";

    public virtual bool Enabled => true;

    public virtual string Label => GetType().ToString();

    public ManagerJob? Selected
    {
        get => selected;
        set
        {
            PreSelect();
            selected = value;
            PostSelect();
        }
    }

    public abstract void DoWindowContents(Rect canvas);

    public virtual void PostMake()
    {
    }

    public virtual void PostClose()
    {
    }

    public virtual void PostOpen()
    {
    }

    public virtual void PreClose()
    {
    }

    public virtual void PreOpen()
    {
    }

    public virtual void Tick()
    {
    }

    protected virtual void PreSelect()
    {
    }

    protected virtual void PostSelect()
    {
    }

    protected void DrawShortcutToggle<T>(List<T> options, HashSet<T> selected, Action<T, bool> setAllowed, Rect rect, string labelKey, string? toolTipKey)
    {
        var allSelected = options.All(selected.Contains);
        var noneSelected = options.All(p => !selected.Contains(p));

        Utilities.DrawToggle(
            rect,
            "<i>" + $"ColonyManagerRedux.{labelKey}".Translate() + "</i>",
            toolTipKey != null ? $"ColonyManagerRedux.{toolTipKey}".Translate() : string.Empty,
            allSelected,
            noneSelected,
            () => options.ForEach(p => setAllowed(p, true)),
            () => options.ForEach(p => setAllowed(p, false)));
    }
}
