// Trigger.cs
// Copyright Karel Kroeze, 2018-2020

namespace ColonyManagerRedux;

public abstract class Trigger(Manager manager) : IExposable
{
    public Manager manager = manager;

    public abstract bool State { get; }
    public virtual string StatusTooltip { get; } = string.Empty;

    public virtual void ExposeData()
    {
        Scribe_References.Look(ref manager, "manager");
    }

    public virtual void DrawProgressBar(Rect progressRect, bool active)
    {
    }

    public abstract void DrawTriggerConfig(ref Vector2 cur, float width, float entryHeight,
                                            string? label = null, string? tooltip = null,
                                            List<Designation>? targets = null, Action? onOpenFilterDetails = null,
                                            Func<Designation, string>? designationLabelGetter = null);
}
