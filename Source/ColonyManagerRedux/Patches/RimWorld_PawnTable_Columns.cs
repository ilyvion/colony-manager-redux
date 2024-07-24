// RimWorld_PawnTable_Columns.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HarmonyPatch(typeof(PawnTable), nameof(PawnTable.Columns), MethodType.Getter)]
internal static class RimWorld_PawnTable_Columns
{
    // This field is used from some PawnColumnWorker implementations to tell which table we're
    // being asked to decide VisibleCurrently status. Since it's a property on the
    // PawnColumnWorker (which is shared between all tables with said PawnColumn) and properties
    // don't get handed any parameters (like which table whose status we're answering for)
    // we need this little patch to store that info for us.
    //
    // I suspect the reason why we're not getting this information is that in vanilla, this field
    // is only used to hide columns based on DLC active status, which is entirely unrelated to the
    // active PawnTable.
    public static PawnTable? CurrentPawnTable;
    private static void Prefix(PawnTable __instance)
    {
        CurrentPawnTable = __instance;
    }

    private static void Postfix()
    {
        CurrentPawnTable = null;
    }
}
