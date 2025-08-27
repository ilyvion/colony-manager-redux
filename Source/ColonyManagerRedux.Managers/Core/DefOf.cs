// DefOf.cs
// Copyright (c) 2024–2025 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

#pragma warning disable CS8618,CA2211

[DefOf]
internal static class ManagerDefOf
{
    public static ManagerDef CM_LogsManager;

    static ManagerDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerDefOf));
    }
}

[DefOf]
internal static class ManagerJobHistoryChapterDefOf
{
    public static ManagerJobHistoryChapterDef CM_HistoryStock;
    public static ManagerJobHistoryChapterDef CM_HistoryDesignated;
    public static ManagerJobHistoryChapterDef CM_HistoryCorpses;
    public static ManagerJobHistoryChapterDef CM_HistoryChunks;

    public static ManagerJobHistoryChapterDef CM_HistoryAdultFemale;
    public static ManagerJobHistoryChapterDef CM_HistoryAdultMale;
    public static ManagerJobHistoryChapterDef CM_HistoryJuvenileFemale;
    public static ManagerJobHistoryChapterDef CM_HistoryJuvenileMale;

    public static ManagerJobHistoryChapterDef CM_HistoryProduction;
    public static ManagerJobHistoryChapterDef CM_HistoryConsumption;
    public static ManagerJobHistoryChapterDef CM_HistoryBatteries;

    static ManagerJobHistoryChapterDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerJobHistoryChapterDefOf));
    }
}

[DefOf]
internal static class ManagerThingDefOf
{
    public static ThingDef Meat_Megaspider;

    [MayRequireAnomaly]
    public static ThingDef Meat_Twisted;

    [MayRequireSurvivalistsAdditions]
    public static ThingDef SRV_PlantTurnip;

    [MayRequireSurvivalistsAdditions]
    public static ThingDef SRV_Turnip;

    [MayRequireSurvivalistsAdditions]
    public static ThingDef SRV_Turnip_Green;

    static ManagerThingDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerThingDefOf));
    }
}

[DefOf]
internal static class ManagerWorkTypeDefOf
{
    public static WorkTypeDef Managing;

    static ManagerWorkTypeDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerWorkTypeDefOf));
    }
}

[DefOf]
internal static class ManagerPawnTableDefOf
{
    public static PawnTableDef CM_ManagerJobWorkTable;
    public static PawnTableDef CM_ManagerLivestockAnimalTable;

    static ManagerPawnTableDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerPawnTableDefOf));
    }
}

[DefOf]
internal static class ManagerResearchProjectDefOf
{
    public static ResearchProjectDef PowerManagement;

    static ManagerResearchProjectDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerResearchProjectDefOf));
    }
}

[DefOf]
internal static class ManagerWorkGiverDefOf
{
    public static WorkGiverDef Milk;
    public static WorkGiverDef Shear;
    public static WorkGiverDef Train;
    public static WorkGiverDef Slaughter;

    static ManagerWorkGiverDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerWorkGiverDefOf));
    }
}
