// GravShipExtensions.cs
// Copyright (c) 2025 Alexander Krivács Schrøder

#if !v1_5
using RimWorld.Planet;

namespace ColonyManagerRedux;

internal static class GravShipExtensions
{
    public static Thing? ManagerDatabase(this Gravship gravship)
    {
        foreach (var thing in gravship.Things)
        {
            if (thing.def == ManagerThingDefOf.CM_ManagerDatabase)
            {
                return thing;
            }
        }
        return null;
    }
}
#endif // !v1_5
