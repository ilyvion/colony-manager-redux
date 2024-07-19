﻿// Trigger.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HotSwappable]
public abstract class Trigger(ManagerJob job) : IExposable
{
    public ManagerJob job = job;

    public abstract bool State { get; }
    public virtual string StatusTooltip { get; } = string.Empty;

    public virtual void ExposeData()
    {
        if (Manager.Mode == Manager.ScribingMode.Normal)
        {
            Scribe_References.Look(ref job, "job");
        }
    }

    public virtual void DrawProgressBar(Rect progressRect, bool active)
    {
    }

    public abstract void DrawTriggerConfig(ref Vector2 cur, float width, float entryHeight,
        string? label = null, string? tooltip = null,
        List<Designation>? targets = null, Action? onOpenFilterDetails = null,
        Func<Designation, string>? designationLabelGetter = null);
}
