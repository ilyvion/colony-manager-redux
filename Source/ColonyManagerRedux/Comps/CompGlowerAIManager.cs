// CompManagerStation.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public class CompGlowerAIManager : CompGlower
{
    private bool _lit;

    public bool IsLit
    {
        get => _lit; set
        {
            _lit = value;
            UpdateLit(parent.Map);
        }
    }

    protected override bool ShouldBeLitNow => _lit;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref _lit, "lit");
    }
}
