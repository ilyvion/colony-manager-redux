// DefOf.cs
// Copyright (c) 2024–2025 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[DefOf]
internal static class ManagerThingDefOf
{
    public static ThingDef CM_AIManager;
    public static ThingDef CM_BasicManagerStation;
    public static ThingDef CM_ManagerStation;

#if !v1_5
    [MayRequireOdyssey]
    public static ThingDef CM_ManagerDatabase;
#endif

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
internal static class ManagerJobDefOf
{
    public static JobDef ManagingAtManagingStation;

    static ManagerJobDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerJobDefOf));
    }
}

[DefOf]
internal static class ManagerStatDefOf
{
    public static StatDef ManagingSpeed;

    static ManagerStatDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerStatDefOf));
    }
}

[DefOf]
internal static class ManagerResearchProjectDefOf
{
    public static ResearchProjectDef ManagingSoftware;
    public static ResearchProjectDef AdvancedManagingSoftware;

    static ManagerResearchProjectDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerResearchProjectDefOf));
    }
}

[DefOf]
internal static class ManagerMainButtonDefOf
{
    public static MainButtonDef Work;
    public static MainButtonDef ColonyManagerRedux_Manager;

    static ManagerMainButtonDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerMainButtonDefOf));
    }
}
