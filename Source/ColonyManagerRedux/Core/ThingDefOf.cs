// ManagerJobHistoryChapterDefOf.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;
#pragma warning disable CS8618
[DefOf]
public static class ManagerThingDefOf
{
    public static ThingDef CM_AIManager;

    static ManagerThingDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ManagerThingDefOf));
    }
}
#pragma warning restore CS8618
