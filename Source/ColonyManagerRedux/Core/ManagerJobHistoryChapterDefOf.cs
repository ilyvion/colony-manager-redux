// ManagerJobHistoryChapterDefOf.cs
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
#pragma warning restore CS8618
