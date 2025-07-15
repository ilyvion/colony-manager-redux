// GravEngineExtensions.cs
// Copyright (c) 2025 Alexander Krivács Schrøder

#if !v1_5
namespace ColonyManagerRedux;

public static class GravEngineExtensions
{
    public static Thing? ManagerDatabase(this Building_GravEngine gravEngine)
    {
        foreach (IntVec3 item in gravEngine.AllConnectedSubstructure)
        {
            foreach (Thing thing in item.GetThingList(gravEngine.Map))
            {
                if (thing.def == ManagerThingDefOf.CM_ManagerDatabase)
                {
                    return thing;
                }
            }
        }
        return null;
    }
}
#endif // !v1_5