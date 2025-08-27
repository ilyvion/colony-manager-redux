// ManagerTab_Overview_PawnOverviewTable.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.Extensions;
using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;
using static ColonyManagerRedux.Managers.ManagerJob_Livestock;

namespace ColonyManagerRedux.Managers;

internal partial class ManagerTab_Livestock
{
    [HotSwappable]
    public abstract class PawnColumnWorker_Livestock : PawnColumnWorker
    {
#pragma warning disable CS8618
        public ManagerTab_Livestock instance;
        public Func<ManagerJob_Livestock?> jobGetter;
#pragma warning restore CS8618

        protected bool IsCurrentTableWildTable => instance.animalsWildTable.IsCurrentTable();

        public override void DoHeader(Rect rect, PawnTable table)
        {
            base.DoHeader(rect, table);
            if (!def.HeaderInteractable)
            {
                HeaderInteraction(rect, table);
            }
        }

        protected void HeaderInteraction(Rect rect, PawnTable table)
        {
            var interactableHeaderRect = GetInteractableHeaderRect(rect, table);
            if (Mouse.IsOver(interactableHeaderRect))
            {
                Widgets.DrawHighlight(interactableHeaderRect);
                var headerTip = GetHeaderTip(table);
                if (!headerTip.NullOrEmpty())
                {
                    TooltipHandler.TipRegion(interactableHeaderRect, headerTip);
                }
            }
        }
    }

    [HotSwappable]
    public sealed class PawnColumnWorker_LifeStage : RimWorld.PawnColumnWorker_LifeStage
    {
        public override void DoHeader(Rect rect, PawnTable table) =>
            this.CustomIconDoHeader(rect, table, (rect, _, _, _) => DrawHeader(rect));

        protected override string GetIconTip(Pawn pawn) => pawn.ageTracker.AgeTooltipString;

        private static void DrawHeader(Rect rect)
        {
            var ageRectC = new Rect(0f, 0f, SmallIconSize, SmallIconSize).CenteredIn(
                rect,
                (rect.width / 2) - (SmallIconSize / 2)
            );
            var ageRectB = new Rect(0f, 0f, SmallIconSize, SmallIconSize).CenteredIn(
                rect,
                -SmallIconSize / 4
            );
            var ageRectA = new Rect(0f, 0f, SmallIconSize, SmallIconSize).CenteredIn(
                rect,
                -rect.width / 2
            );

            GUI.DrawTexture(ageRectC, Resources.GetLifeStageIcon(2));
            GUI.DrawTexture(ageRectB, Resources.GetLifeStageIcon(1));
            GUI.DrawTexture(ageRectA, Resources.GetLifeStageIcon(0));
        }

        protected override string GetHeaderTip(PawnTable table) =>
            "ColonyManagerRedux.Livestock.AgeHeader".Translate()
            + "\n\n"
            + base.GetHeaderTip(table);
    }

    [HotSwappable]
    public sealed class PawnColumnWorker_ExpectedMeatYield : PawnColumnWorker
    {
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            var estimatedMeatCount = pawn.EstimatedMeatCount();
            IlyvionWidgets.Label(
                rect,
                estimatedMeatCount.ToString(CultureInfo.InvariantCulture),
                "ColonyManagerRedux.Livestock.Yields".Translate(
                    pawn.RaceProps.meatDef.LabelCap,
                    estimatedMeatCount
                ),
                TextAnchor.MiddleCenter,
                GameFont.Tiny,
                leftMargin: Margin
            );
        }

        protected override string GetHeaderTip(PawnTable table) =>
            "ColonyManagerRedux.Livestock.MeatHeader".Translate()
            + "\n\n"
            + base.GetHeaderTip(table);

        public override int Compare(Pawn a, Pawn b)
        {
            var estimatedMeatCountA = a.EstimatedMeatCount();
            var estimatedMeatCountB = b.EstimatedMeatCount();

            return estimatedMeatCountA - estimatedMeatCountB;
        }

        public override int GetMinWidth(PawnTable table) => (int)Text.CalcSize("MMM").x;
    }

    [HotSwappable]
    public sealed class PawnColumnWorker_MilkProgress : PawnColumnWorker_Livestock
    {
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            var milkableComp = pawn.TryGetComp<CompMilkable>();
            IlyvionWidgets.Label(
                rect,
                milkableComp.Fullness.ToString("0%", CultureInfo.InvariantCulture),
                "ColonyManagerRedux.Livestock.Yields".Translate(
                    milkableComp.Props.milkDef.LabelCap,
                    milkableComp.Props.milkAmount
                ),
                TextAnchor.MiddleCenter,
                GameFont.Tiny,
                leftMargin: Margin
            );
        }

        protected override string GetHeaderTip(PawnTable table) =>
            "ColonyManagerRedux.Livestock.MilkHeader".Translate()
            + "\n\n"
            + base.GetHeaderTip(table);

        public override int Compare(Pawn a, Pawn b)
        {
            var milkFullnessA = a.TryGetComp<CompMilkable>().Fullness * 100;
            var milkFullnessB = b.TryGetComp<CompMilkable>().Fullness * 100;

            return (int)(milkFullnessA - milkFullnessB);
        }

        public override bool VisibleCurrently =>
            !IsCurrentTableWildTable
            && instance.SelectedJob!.TriggerPawnKind.pawnKind?.Milkable() == true;

        public override int GetMinWidth(PawnTable table) =>
            Math.Max(
                base.GetMinWidth(table),
                (int)Text.CalcSize(100.ToString("0%", CultureInfo.InvariantCulture)).x
            );
    }

    [HotSwappable]
    public sealed class PawnColumnWorker_ShearProgress : PawnColumnWorker_Livestock
    {
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            var shearableComp = pawn.TryGetComp<CompShearable>();
            IlyvionWidgets.Label(
                rect,
                shearableComp.Fullness.ToString("0%", CultureInfo.InvariantCulture),
                "ColonyManagerRedux.Livestock.Yields".Translate(
                    shearableComp.Props.woolDef.LabelCap,
                    shearableComp.Props.woolAmount
                ),
                TextAnchor.MiddleCenter,
                GameFont.Tiny,
                leftMargin: Margin
            );
        }

        protected override string GetHeaderTip(PawnTable table) =>
            "ColonyManagerRedux.Livestock.WoolHeader".Translate()
            + "\n\n"
            + base.GetHeaderTip(table);

        public override int Compare(Pawn a, Pawn b)
        {
            var woolFullnessA = a.TryGetComp<CompShearable>().Fullness * 100;
            var woolFullnessB = b.TryGetComp<CompShearable>().Fullness * 100;

            return (int)(woolFullnessA - woolFullnessB);
        }

        public override bool VisibleCurrently =>
            !IsCurrentTableWildTable
            && instance.SelectedJob!.TriggerPawnKind.pawnKind?.Shearable() == true;

        public override int GetMinWidth(PawnTable table) =>
            Math.Max(
                base.GetMinWidth(table),
                (int)Text.CalcSize(100.ToString("0%", CultureInfo.InvariantCulture)).x
            );
    }

    [HotSwappable]
    public sealed class PawnColumnWorker_Tame : PawnColumnWorker_Livestock
    {
        public override bool VisibleCurrently => IsCurrentTableWildTable;

        protected override string GetHeaderTip(PawnTable table) =>
            "ColonyManagerRedux.Livestock.TamingHeader".Translate();

        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            if (pawn.Map.designationManager.DesignationOn(pawn, DesignationDefOf.Tame) != null)
            {
                GUI.DrawTexture(rect, Resources.Tame);
                TooltipHandler.TipRegion(
                    rect,
                    "ColonyManagerRedux.Livestock.AnimalIsDesignatedFor".Translate(
                        "ColonyManagerRedux.Livestock.TamingHeader"
                            .Translate()
                            .ToString()
                            .UncapitalizeFirst()
                    )
                );
            }
        }
    }

    [HotSwappable]
    public sealed class PawnColumnWorker_Cull : PawnColumnWorker_Livestock
    {
        public override bool VisibleCurrently =>
            !IsCurrentTableWildTable && jobGetter()!.CullExcess;

        public override void DoHeader(Rect rect, PawnTable table)
        {
            //base.DoHeader(rect, table);
            HeaderInteraction(rect, table);
            this.CustomIconDoHeader(rect, table, (rect, _, _, _) => DrawHeader(rect));
        }

        private void DrawHeader(Rect rect)
        {
            var job = jobGetter()!;
            var texture = GetCullingStrategyTexture(job);

            var iconRect = new Rect(0f, 0f, 26, 26).CenteredIn(rect);

            GUI.DrawTexture(iconRect, texture);
        }

        private static Texture2D GetCullingStrategyTexture(ManagerJob_Livestock job) =>
            job.CullingStrategy switch
            {
                LivestockCullingStrategy.Butcher => Resources.Slaughter,
                LivestockCullingStrategy.Release => Resources.ReleaseToTheWild,
                _ => throw new NotImplementedException(),
            };

        protected override string GetHeaderTip(PawnTable table)
        {
            var job = jobGetter()!;
            return "ColonyManagerRedux.Livestock.WhetherAnimalIsDesignatedFor".Translate(
                $"ColonyManagerRedux.Livestock.Logs.{job.CullingDesignationDef.defName}.Action".Translate()
            );
        }

        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            var job = jobGetter()!;
            if (pawn.Map.designationManager.DesignationOn(pawn, job.CullingDesignationDef) != null)
            {
                GUI.DrawTexture(rect.ContractedBy(2), GetCullingStrategyTexture(job));
                TooltipHandler.TipRegion(
                    rect,
                    "ColonyManagerRedux.Livestock.AnimalIsDesignatedFor".Translate(
                        $"ColonyManagerRedux.Livestock.Logs.{job.CullingDesignationDef.defName}.Action".Translate()
                    )
                );
            }
        }
    }

    private PawnTable? animalsTameTable;
    private PawnTable? animalsWildTable;

    private PawnTable CreateAnimalsTable(
        Func<IEnumerable<Pawn>> animalGetter,
        Func<ManagerJob_Livestock?> jobGetter
    )
    {
        PawnTable table = null!;
        table = (PawnTable)
            Activator.CreateInstance(
                ManagerPawnTableDefOf.CM_ManagerLivestockAnimalTable.workerClass,
                ManagerPawnTableDefOf.CM_ManagerLivestockAnimalTable,
#pragma warning disable IDE0004
                (Func<IEnumerable<Pawn>>)(
                    () =>
                    {
                        // PawnTables aren't very customizable, so we'll hijack this function to inject our
                        // instance into the columns, since we need it there
                        foreach (
                            var item in table.def.columns.Where(c =>
                                c.workerClass.IsSubclassOf(typeof(PawnColumnWorker_Livestock))
                            )
                        )
                        {
                            var worker = (PawnColumnWorker_Livestock)item.Worker;
                            worker.instance = this;
                            worker.jobGetter = jobGetter;
                        }

                        return animalGetter();
                    }
                ),
#pragma warning restore IDE0004
                UI.screenWidth - (int)(Margin * 2f),
                (int)(UI.screenHeight - 35 - (Margin * 2f))
            );

        return table;
    }
}
