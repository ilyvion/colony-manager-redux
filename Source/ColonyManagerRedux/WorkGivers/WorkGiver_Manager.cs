﻿// WorkGiver_Manager.cs
// Copyright Karel Kroeze, 2017-2020

using Verse.AI;

namespace ColonyManagerRedux;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Microsoft.Performance",
    "CA1812:AvoidUninstantiatedInternalClasses",
    Justification = "Class is instantiated via reflection")]
internal sealed class WorkGiver_Manage : WorkGiver_Scanner
{
    public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

    public override ThingRequest PotentialWorkThingRequest =>
        ThingRequest.ForGroup(ThingRequestGroup.PotentialBillGiver);

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced)
    {
#if DEBUG_WORKGIVER
        Log.Message( "Checking " + t.LabelCap + " for job." );
        Log.Message( "ManagerStation" + ( t as Building_ManagerStation != null ) );
        Log.Message( "Comp" + ( t.TryGetComp<CompManagerStation>() != null ) );
        Log.Message( "Incap" + ( !pawn.Dead && !pawn.Downed && !pawn.IsBurning() && !t.IsBurning() ) );
        Log.Message( "CanReserve and reach" + pawn.CanReserveAndReach( t, PathEndMode, Danger.Some ) );
        var powera = t.TryGetComp<CompPowerTrader>();
        Log.Message( "Power" + ( powera == null || powera.PowerOn ) );
        Log.Message( "Job" + ( Manager.For( pawn.Map ).JobStack.NextJob != null ) );
#endif

        if (t is not Building_ManagerStation)
        {
            return false;
        }

        if (t.TryGetComp<CompManagerStation>() == null)
        {
            return false;
        }

        if (pawn.Dead ||
             pawn.Downed ||
             pawn.IsBurning() ||
             t.IsBurning())
        {
            return false;
        }

        if (!pawn.CanReserveAndReach(t, PathEndMode, Danger.Some, ignoreOtherReservations: forced))
        {
            return false;
        }

        var power = t.TryGetComp<CompPowerTrader>();
        if (power != null &&
             !power.PowerOn)
        {
            JobFailReason.Is("ColonyManagerRedux.CannotManage.NoPower".Translate());
            return false;
        }

        if (!Manager.For(pawn.Map).JobTracker.JobsOfType<ManagerJob>().Any())
        {
            JobFailReason.Is("ColonyManagerRedux.CannotManage.NoJobs".Translate());
            return false;
        }

        if (Manager.For(pawn.Map).JobTracker.NextJob == null)
        {
            JobFailReason.Is("ColonyManagerRedux.CannotManage.NoActiveJobs".Translate());
            return false;
        }

        return true;
    }

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced)
    {
        return new Job(ManagerJobDefOf.ManagingAtManagingStation, t as Building_ManagerStation);
    }

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
    {
        return pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_ManagerStation>()
                   .Select(b => b as Thing);
    }
}
