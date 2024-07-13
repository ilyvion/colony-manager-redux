// ResearchWorkers.cs
// Copyright Karel Kroeze, 2017-2020

using Verse;

namespace ColonyManagerRedux
{
    public class UnlockPowerTab : ResearchMod
    {
        public override void Apply()
        {
            ManagerTab_Power.unlocked = true;
        }
    }
}
