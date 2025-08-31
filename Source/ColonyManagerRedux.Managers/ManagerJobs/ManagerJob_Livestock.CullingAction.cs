// ManagerJob_Livestock.LivestockCachesComp.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Diagnostics.CodeAnalysis;

namespace ColonyManagerRedux.Managers;

internal partial class ManagerJob_Livestock
{
    public abstract class CullingAction(ManagerJob_Livestock managerJob)
    {
        protected ManagerJob_Livestock ManagerJob => managerJob;

        public abstract string TranslationKey { get; }
        public abstract string ActionText { get; }
        public virtual bool CullingRemovesAnimals => true;

        public abstract bool IsAlreadyCulling(Pawn p);

        public virtual bool IsAlreadyCulled(Pawn p) => false;

        public abstract int GetAlreadyCullingCountForAgeSex(AgeAndSex ageSex);

        public virtual int GetAlreadyCulledForAgeSex(AgeAndSex ageSex) => 0;

        public abstract void Cull(Pawn p);

        public abstract bool TryStopCulling(AgeAndSex ageSex, [NotNullWhen(true)] out Pawn? p);
    }

    public class NoneCullingAction(ManagerJob_Livestock managerJob) : CullingAction(managerJob)
    {
        public override string TranslationKey => "None";

        public override string ActionText => "";

        public override void Cull(Pawn p) { }

        public override int GetAlreadyCullingCountForAgeSex(AgeAndSex ageSex) => 0;

        public override bool IsAlreadyCulling(Pawn p) => false;

        public override bool TryStopCulling(AgeAndSex ageSex, [NotNullWhen(true)] out Pawn? p)
        {
            p = null;
            return false;
        }
    }

    public class DesignationCullingAction(
        ManagerJob_Livestock managerJob,
        DesignationDef designationDef
    ) : CullingAction(managerJob)
    {
        private readonly List<Designation> _tmpDesignations = [];

        public DesignationDef DesignationDef => designationDef;

        public override string TranslationKey => designationDef.defName;

        public override string ActionText => designationDef.ActionText();

        public override int GetAlreadyCullingCountForAgeSex(AgeAndSex ageSex)
        {
            using var _ = new DoOnDispose(_tmpDesignations.Clear);
            ManagerJob.DesignationsOfOn(designationDef, ageSex, _tmpDesignations);
            var alreadyCullingCount = _tmpDesignations.Count;
            return alreadyCullingCount;
        }

        public override bool IsAlreadyCulling(Pawn p) =>
            ManagerJob.Manager.map.designationManager.DesignationOn(p, designationDef) != null;

        public override void Cull(Pawn p) => ManagerJob.AddDesignation(new(p, designationDef));

        public override bool TryStopCulling(AgeAndSex ageSex, [NotNullWhen(true)] out Pawn? p) =>
            ManagerJob.TryRemoveDesignation(ageSex, designationDef, out p);
    }

    public class SterilizeCullingAction(ManagerJob_Livestock managerJob) : CullingAction(managerJob)
    {
        public override string TranslationKey => nameof(RecipeDefOf.Sterilize);

        public override string ActionText => RecipeDefOf.Sterilize.label;
        public override bool CullingRemovesAnimals => false;

        public override void Cull(Pawn p) =>
            HealthCardUtility.CreateSurgeryBill(p, RecipeDefOf.Sterilize, null);

        public override int GetAlreadyCullingCountForAgeSex(AgeAndSex ageSex) =>
            ManagerJob
                .TriggerPawnKind.pawnKind?.GetTame(ManagerJob.Manager, ageSex)
                .Count(IsAlreadyCulling) ?? 0;

        public override int GetAlreadyCulledForAgeSex(AgeAndSex ageSex) =>
            ManagerJob
                .TriggerPawnKind.pawnKind?.GetTame(ManagerJob.Manager, ageSex)
                .Count(IsAlreadyCulled) ?? 0;

        public override bool IsAlreadyCulling(Pawn p) =>
            p.health.surgeryBills.Bills.Any(b =>
                b is Bill_Medical bm && bm.recipe == RecipeDefOf.Sterilize
            );

        public override bool IsAlreadyCulled(Pawn p) =>
            p.health.hediffSet.HasHediff(HediffDefOf.Sterilized);

        private readonly List<Pawn> _tmpPawns = [];

        public override bool TryStopCulling(AgeAndSex ageSex, [NotNullWhen(true)] out Pawn? p)
        {
            p = null;

            if (ManagerJob.TriggerPawnKind.pawnKind == null)
            {
                return false;
            }

            using var _clear = new DoOnDispose(_tmpPawns.Clear);
            _tmpPawns.AddRange(
                ManagerJob
                    .TriggerPawnKind.pawnKind.GetTame(ManagerJob.Manager, ageSex)
                    .Where(IsAlreadyCulling)
            );
            if (_tmpPawns.Count == 0)
            {
                return false;
            }

            p = _tmpPawns[0];
            _ = p.health.surgeryBills.Bills.RemoveAll(b =>
                b is Bill_Medical bm && bm.recipe == RecipeDefOf.Sterilize
            );
            return true;
        }
    }
}
