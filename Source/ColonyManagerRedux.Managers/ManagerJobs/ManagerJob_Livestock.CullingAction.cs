// ManagerJob_Livestock.CullingAction.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024–2026 Alexander Krivács Schrøder

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

        /// <summary>
        /// Decides which up to <paramref name="count"/> already-culling animals for
        /// <paramref name="ageSex"/> would be picked to have their culling stopped, without
        /// touching the game.
        /// </summary>
        public abstract List<Pawn> PeekCullingToStop(AgeAndSex ageSex, int count);

        /// <summary>
        /// Applies the decision to stop culling <paramref name="animal"/> (deleting the
        /// designation, cancelling the surgery bill, etc., depending on the concrete action). A
        /// no-op if <paramref name="animal"/> is no longer actually being culled by the time
        /// this runs.
        /// </summary>
        public abstract void StopCulling(Pawn animal);
    }

    public class NoneCullingAction(ManagerJob_Livestock managerJob) : CullingAction(managerJob)
    {
        public override string TranslationKey => "None";

        public override string ActionText => "";

        public override void Cull(Pawn p) { }

        public override int GetAlreadyCullingCountForAgeSex(AgeAndSex ageSex) => 0;

        public override bool IsAlreadyCulling(Pawn p) => false;

        public override List<Pawn> PeekCullingToStop(AgeAndSex ageSex, int count) => [];

        public override void StopCulling(Pawn animal) { }
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

        public override List<Pawn> PeekCullingToStop(AgeAndSex ageSex, int count) =>
            ManagerJob.PeekDesignationsToRemove(designationDef, ageSex, count);

        public override void StopCulling(Pawn animal) =>
            ManagerJob.RemoveDesignationOn(animal, designationDef);
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
                .Count(IsAlreadyCulling)
            ?? 0;

        public override int GetAlreadyCulledForAgeSex(AgeAndSex ageSex) =>
            ManagerJob
                .TriggerPawnKind.pawnKind?.GetTame(ManagerJob.Manager, ageSex)
                .Count(IsAlreadyCulled)
            ?? 0;

        public override bool IsAlreadyCulling(Pawn p) =>
            p.health.surgeryBills.Bills.Any(b =>
                b is Bill_Medical bm && bm.recipe == RecipeDefOf.Sterilize
            );

        public override bool IsAlreadyCulled(Pawn p) =>
            p.health.hediffSet.HasHediff(HediffDefOf.Sterilized);

        public override List<Pawn> PeekCullingToStop(AgeAndSex ageSex, int count) =>
            ManagerJob.TriggerPawnKind.pawnKind == null
                ? []
                :
                [
                    .. ManagerJob
                        .TriggerPawnKind.pawnKind.GetTame(ManagerJob.Manager, ageSex)
                        .Where(IsAlreadyCulling)
                        .Take(count),
                ];

        public override void StopCulling(Pawn animal)
        {
            // Re-validate: the execute phase runs well after gather, so the surgery bill may
            // already have been cancelled or resolved by something else in the interim.
            if (!IsAlreadyCulling(animal))
            {
                return;
            }

            _ = animal.health.surgeryBills.Bills.RemoveAll(b =>
                b is Bill_Medical bm && bm.recipe == RecipeDefOf.Sterilize
            );
        }
    }
}
