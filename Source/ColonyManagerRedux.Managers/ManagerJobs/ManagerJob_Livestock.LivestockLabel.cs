// ManagerJob_Livestock.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

partial class ManagerJob_Livestock
{
    public sealed class LivestockLabel : HistoryLabel
    {
        private AgeAndSex ageAndSex;

        public override string Label => ageAndSex.GetLabel(true);

        public override void ExposeData()
        {
            Scribe_Values.Look(ref ageAndSex, "ageAndSex");
        }
    }
}
