// Trigger.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

// TODO: The fact that we need this feels a bit like an anti-pattern; see if it's possible to get
// rid of at some point.
public class Trigger_Power(ManagerJob job) : Trigger(job)
{
    public override bool State => true;

    public override void DrawTriggerConfig(
        ref Vector2 cur,
        float width,
        float entryHeight,
        string? label = null,
        string? tooltip = null,
        List<Designation>? targets = null,
        Action? onOpenFilterDetails = null,
        Func<Designation, string>? designationLabelGetter = null)
    {
    }
}
