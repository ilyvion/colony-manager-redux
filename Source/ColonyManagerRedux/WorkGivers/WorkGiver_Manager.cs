// WorkGiver_Manager.cs
// Copyright Karel Kroeze, 2017-2020
// Copyright (c) 2024–2025 Alexander Krivács Schrøder

using Verse.AI;

namespace ColonyManagerRedux;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Microsoft.Performance",
    "CA1812:AvoidUninstantiatedInternalClasses",
    Justification = "Class is instantiated via reflection"
)]
internal sealed class WorkGiver_Manage : WorkGiver_Scanner
{
    public override PathEndMode PathEndMode => PathEndMode.InteractionCell;

    public override ThingRequest PotentialWorkThingRequest =>
        ThingRequest.ForGroup(ThingRequestGroup.PotentialBillGiver);

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced)
    {
#if DEBUG_WORKGIVER
        Log.Message("Checking " + t.LabelCap + " for job.");
        Log.Message("ManagerStation" + (t as Building_ManagerStation != null));
        Log.Message("Comp" + (t.TryGetComp<CompManagerStation>() != null));
        Log.Message("Incap" + (!pawn.Dead && !pawn.Downed && !pawn.IsBurning() && !t.IsBurning()));
        Log.Message("CanReserve and reach" + pawn.CanReserveAndReach(t, PathEndMode, Danger.Some));
        var powera = t.TryGetComp<CompPowerTrader>();
        Log.Message("Power" + (powera == null || powera.PowerOn));
        Log.Message("Job" + (Manager.For(pawn.Map).JobStack.NextJob != null));
#endif

        var reason = Evaluate(
            t is Building_ManagerStation,
            () => t.TryGetComp<CompManagerStation>() != null,
            () =>
                pawn
                    .Map.listerBuildings.AllBuildingsColonistOfClass<Building_AIManager>()
                    .Any(b => b.Powered),
            pawn.Dead || pawn.Downed || pawn.IsBurning() || t.IsBurning(),
            () =>
                pawn.CanReserveAndReach(
                    t,
                    PathEndMode,
                    Danger.Some,
                    ignoreOtherReservations: forced
                ),
            t.TryGetComp<CompPowerTrader>() is { } power ? power.PowerOn : null,
            () => Manager.For(pawn.Map).JobTracker.JobList.Count,
            () => Manager.For(pawn.Map).JobTracker.NextJob != null
        );

        var failKey = reason switch
        {
            ManageBlockReason.AIManagerPowered => "ColonyManagerRedux.CannotManage.AIManager",
            ManageBlockReason.NoPower => "ColonyManagerRedux.CannotManage.NoPower",
            ManageBlockReason.NoJobs => "ColonyManagerRedux.CannotManage.NoJobs",
            ManageBlockReason.NoActiveJobs => "ColonyManagerRedux.CannotManage.NoActiveJobs",
            ManageBlockReason.None
            or ManageBlockReason.NotAStation
            or ManageBlockReason.NoStationComp
            or ManageBlockReason.PawnIncapable
            or ManageBlockReason.CannotReserveOrReach => null,
            _ => null,
        };
        if (failKey != null)
        {
            JobFailReason.Is(failKey.Translate());
        }

        return reason == ManageBlockReason.None;
    }

    /// <summary>Why a pawn cannot manage at a station; <see cref="None"/> means it can.</summary>
    internal enum ManageBlockReason
    {
        None,
        NotAStation,
        NoStationComp,
        AIManagerPowered,
        PawnIncapable,
        CannotReserveOrReach,
        NoPower,
        NoJobs,
        NoActiveJobs,
    }

    /// <summary>
    /// The first reason, in check order, that blocks managing. The delegate checks are only
    /// evaluated when every earlier check passed.
    /// </summary>
    internal static ManageBlockReason Evaluate(
        bool isStation,
        Func<bool> hasStationComp,
        Func<bool> aiManagerPowered,
        bool pawnOrStationIncapable,
        Func<bool> canReserveAndReach,
        bool? powerOn,
        Func<int> jobCount,
        Func<bool> hasNextJob
    ) =>
        !isStation ? ManageBlockReason.NotAStation
        : !hasStationComp() ? ManageBlockReason.NoStationComp
        : aiManagerPowered() ? ManageBlockReason.AIManagerPowered
        : pawnOrStationIncapable ? ManageBlockReason.PawnIncapable
        : !canReserveAndReach() ? ManageBlockReason.CannotReserveOrReach
        : powerOn == false ? ManageBlockReason.NoPower
        : jobCount() == 0 ? ManageBlockReason.NoJobs
        : !hasNextJob() ? ManageBlockReason.NoActiveJobs
        : ManageBlockReason.None;

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced) =>
        new(ManagerJobDefOf.ManagingAtManagingStation, t);

    public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn) =>
        pawn.Map.listerBuildings.AllBuildingsColonistOfClass<Building_ManagerStation>();
}
