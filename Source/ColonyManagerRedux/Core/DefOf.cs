// DefOf.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

#pragma warning disable CS8618

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

    static ManagerJobHistoryChapterDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerJobHistoryChapterDefOf));
    }
}

[DefOf]
public static class ManagerThingDefOf
{
    public static ThingDef CM_AIManager;
    public static ThingDef Meat_Megaspider;

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
