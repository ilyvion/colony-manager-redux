// CompManagerStation.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// A glower component for AI manager stations, allowing dynamic control of light emission.
/// </summary>
public class CompGlowerAIManager : CompGlower
{
    private bool _lit;

    /// <summary>
    /// Gets or sets whether the glower is currently lit.
    /// Setting this property updates the light state in the game world.
    /// </summary>
    public bool IsLit
    {
        get => _lit;
        set
        {
            _lit = value;
            UpdateLit(parent.Map);
        }
    }

    /// <summary>
    /// Gets whether the glower should be lit now, based on the internal state.
    /// </summary>
    protected override bool ShouldBeLitNow => _lit;

    /// <summary>
    /// Exposes data for saving and loading the component's state.
    /// </summary>
    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref _lit, "lit");
    }
}
