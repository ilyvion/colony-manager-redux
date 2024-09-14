// DefOf.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

#pragma warning disable CS8618,CA2211

[DefOf]
public static class ManagerDefOf
{
    public static ManagerDef CM_LogsManager;

    static ManagerDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerDefOf));
    }
}

[DefOf]
public static class ManagerJobHistoryChapterDefOf
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
public static class ManagerThingDefOf
{
    public static ThingDef CM_AIManager;
    public static ThingDef CM_BasicManagerStation;
    public static ThingDef CM_ManagerStation;
    public static ThingDef Meat_Megaspider;
    [MayRequireAnomaly]
    public static ThingDef Meat_Twisted;

    static ManagerThingDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerThingDefOf));
    }
}

[DefOf]
public static class ManagerWorkTypeDefOf
{
    public static WorkTypeDef Managing;

    static ManagerWorkTypeDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerWorkTypeDefOf));
    }
}

[DefOf]
public static class ManagerJobDefOf
{
    public static JobDef ManagingAtManagingStation;

    static ManagerJobDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerJobDefOf));
    }
}

[DefOf]
public static class ManagerStatDefOf
{
    public static StatDef ManagingSpeed;

    static ManagerStatDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerStatDefOf));
    }
}

[DefOf]
public static class ManagerThingCategoryDefOf
{
    public static ThingCategoryDef FoodRaw;

    static ManagerThingCategoryDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerThingCategoryDefOf));
    }
}

[DefOf]
public static class ManagerPawnTableDefOf
{
    public static PawnTableDef CM_ManagerJobWorkTable;
    public static PawnTableDef CM_ManagerLivestockAnimalTable;

    static ManagerPawnTableDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerPawnTableDefOf));
    }
}

[DefOf]
public static class ManagerResearchProjectDefOf
{
    public static ResearchProjectDef PowerManagement;
    public static ResearchProjectDef ManagingSoftware;
    public static ResearchProjectDef AdvancedManagingSoftware;

    static ManagerResearchProjectDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerResearchProjectDefOf));
    }
}

[DefOf]
public static class ManagerWorkGiverDefOf
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

[DefOf]
public static class ManagerMainButtonDefOf
{
    public static MainButtonDef Work;
    public static MainButtonDef ColonyManagerRedux_Manager;

    static ManagerMainButtonDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerMainButtonDefOf));
    }
}
