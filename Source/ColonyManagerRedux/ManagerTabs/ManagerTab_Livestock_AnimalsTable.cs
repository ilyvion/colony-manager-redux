// ManagerTab_Overview_PawnOverviewTable.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

partial class ManagerTab_Livestock
{
    [HotSwappable]
    public abstract class PawnColumnWorker_Livestock : PawnColumnWorker
    {
#pragma warning disable CS8618
        public ManagerTab_Livestock instance;
#pragma warning restore CS8618

        protected bool IsCurrentTableWildTable => RimWorld_PawnTable_Columns.CurrentPawnTable == instance.animalsWildTable;
    }

    [HotSwappable]
    public sealed class PawnColumnWorker_LifeStage : RimWorld.PawnColumnWorker_LifeStage
    {
        public override void DoHeader(Rect rect, PawnTable table)
        {
            RimWorld_PawnColumnWorker_DoHeader.CustomIconDoHeader(this, rect, table,
                (rect, _, _, _) => DrawHeader(rect));
        }

        protected override string GetIconTip(Pawn pawn)
        {
            return pawn.ageTracker.AgeTooltipString;
        }

        private static void DrawHeader(Rect rect)
        {
            var ageRectC = new Rect(0f, 0f, SmallIconSize, SmallIconSize).CenteredIn(rect, rect.width / 2 - SmallIconSize / 2);
            var ageRectB = new Rect(0f, 0f, SmallIconSize, SmallIconSize).CenteredIn(rect, -SmallIconSize / 4);
            var ageRectA = new Rect(0f, 0f, SmallIconSize, SmallIconSize).CenteredIn(rect, -rect.width / 2);

            GUI.DrawTexture(ageRectC, Resources.GetLifeStageIcon(2));
            GUI.DrawTexture(ageRectB, Resources.GetLifeStageIcon(1));
            GUI.DrawTexture(ageRectA, Resources.GetLifeStageIcon(0));
        }

        protected override string GetHeaderTip(PawnTable table)
        {
            return "ColonyManagerRedux.Livestock.AgeHeader".Translate() + "\n\n" +
                base.GetHeaderTip(table);
        }
    }

    [HotSwappable]
    public sealed class PawnColumnWorker_ExpectedMeatYield : PawnColumnWorker
    {
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            int estimatedMeatCount = pawn.EstimatedMeatCount();
            Widgets_Labels.Label(rect, estimatedMeatCount.ToString(),
                "ColonyManagerRedux.Livestock.Yields".Translate(pawn.RaceProps.meatDef.LabelCap,
                    estimatedMeatCount),
                TextAnchor.MiddleCenter, GameFont.Tiny, margin: Margin);
        }

        protected override string GetHeaderTip(PawnTable table)
        {
            return "ColonyManagerRedux.Livestock.MeatHeader".Translate() + "\n\n" +
                base.GetHeaderTip(table);
        }

        public override int Compare(Pawn a, Pawn b)
        {
            int estimatedMeatCountA = a.EstimatedMeatCount();
            int estimatedMeatCountB = b.EstimatedMeatCount();

            return estimatedMeatCountA - estimatedMeatCountB;
        }
    }

    [HotSwappable]
    public sealed class PawnColumnWorker_MilkProgress : PawnColumnWorker_Livestock
    {
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            var milkableComp = pawn.TryGetComp<CompMilkable>();
            Widgets_Labels.Label(rect, milkableComp.Fullness.ToString("0%"),
                "ColonyManagerRedux.Livestock.Yields".Translate(milkableComp.Props.milkDef.LabelCap,
                    milkableComp.Props.milkAmount),
                TextAnchor.MiddleCenter, GameFont.Tiny, margin: Margin);
        }

        protected override string GetHeaderTip(PawnTable table)
        {
            return "ColonyManagerRedux.Livestock.MilkHeader".Translate() + "\n\n" +
                base.GetHeaderTip(table);
        }

        public override int Compare(Pawn a, Pawn b)
        {
            float milkFullnessA = a.TryGetComp<CompMilkable>().Fullness * 100;
            float milkFullnessB = b.TryGetComp<CompMilkable>().Fullness * 100;

            return (int)(milkFullnessA - milkFullnessB);
        }

        public override bool VisibleCurrently => !IsCurrentTableWildTable &&
            instance.SelectedCurrentLivestockJob!.TriggerPawnKind.pawnKind.Milkable();

        public override int GetMinWidth(PawnTable table)
        {
            return Math.Max(base.GetMinWidth(table), (int)Text.CalcSize(100.ToString("0%")).x);
        }
    }

    [HotSwappable]
    public sealed class PawnColumnWorker_ShearProgress : PawnColumnWorker_Livestock
    {
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            var shearableComp = pawn.TryGetComp<CompShearable>();
            Widgets_Labels.Label(rect, shearableComp.Fullness.ToString("0%"),
                "ColonyManagerRedux.Livestock.Yields".Translate(shearableComp.Props.woolDef.LabelCap,
                    shearableComp.Props.woolAmount),
                TextAnchor.MiddleCenter, GameFont.Tiny, margin: Margin);
        }

        protected override string GetHeaderTip(PawnTable table)
        {
            return "ColonyManagerRedux.Livestock.WoolHeader".Translate() + "\n\n" +
                base.GetHeaderTip(table);
        }

        public override int Compare(Pawn a, Pawn b)
        {
            float woolFullnessA = a.TryGetComp<CompShearable>().Fullness * 100;
            float woolFullnessB = b.TryGetComp<CompShearable>().Fullness * 100;

            return (int)(woolFullnessA - woolFullnessB);
        }

        public override bool VisibleCurrently => !IsCurrentTableWildTable &&
            instance.SelectedCurrentLivestockJob!.TriggerPawnKind.pawnKind.Shearable();

        public override int GetMinWidth(PawnTable table)
        {
            return Math.Max(base.GetMinWidth(table), (int)Text.CalcSize(100.ToString("0%")).x);
        }
    }

    private PawnTable? animalsTameTable;
    private PawnTable? animalsWildTable;
    private PawnTable CreateAnimalsTable(Func<IEnumerable<Pawn>> animalGetter, bool isWildTable)
    {
        PawnTable table = null!;
        table = (PawnTable)Activator.CreateInstance(
            ManagerPawnTableDefOf.CM_ManagerLivestockAnimalTable.workerClass,
            ManagerPawnTableDefOf.CM_ManagerLivestockAnimalTable,
#pragma warning disable IDE0004
            (Func<IEnumerable<Pawn>>)(() =>
            {
                // PawnTables aren't very customizable, so we'll hijack this function to inject our
                // instance into the columns, since we need it there
                foreach (var item in Traverse.Create(table).Field<PawnTableDef>("def").Value.columns
                    .Where(c => c.workerClass.IsSubclassOf(
                        typeof(PawnColumnWorker_Livestock))))
                {
                    PawnColumnWorker_Livestock worker = (PawnColumnWorker_Livestock)item.Worker;
                    worker.instance = this;
                }

                return animalGetter();
            }),
#pragma warning restore IDE0004
            UI.screenWidth - (int)(Margin * 2f),
            (int)(UI.screenHeight - 35 - Margin * 2f));

        return table;
    }
}
