// UnlockPowerTab.cs
// Copyright Karel Kroeze, 2017-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

public class UnlockPowerTab : ResearchMod
{
    public override void Apply()
    {
        ManagerTab_Power.OnPowerResearchedFinished();
    }
}
