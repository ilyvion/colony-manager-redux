// ManagerJob_Livestock.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

partial class ManagerJob_Livestock
{
    public sealed class LivestockCachesComp : ManagerComp
    {
        internal readonly CachedValues<(PawnKindDef, int), List<Pawn>> AllCache = new(5);

        internal readonly CachedValues<(PawnKindDef, int, AgeAndSex), List<Pawn>>
            AllSexedCache = new(5);

        internal readonly Dictionary<Pawn, CachedValue<List<Pawn>>> FollowerCache = [];

        internal readonly
            Dictionary<(PawnKindDef, Map, MasterMode), CachedValue<List<Pawn>>> MasterCache = [];

        internal readonly Dictionary<Pawn, CachedValue<bool>> MilkablePawnCache = [];

        internal readonly Dictionary<Pawn, CachedValue<bool>> ShearablePawnCache = [];

        internal readonly CachedValues<(PawnKindDef, int, bool), List<Pawn>> TameCache = new(5);

        internal readonly CachedValues<(PawnKindDef, int, AgeAndSex, bool), List<Pawn>>
            TameSexedCache = new(5);

        internal readonly CachedValues<(PawnKindDef, int), List<Pawn>> WildCache = new(5);

        internal readonly CachedValues<(PawnKindDef, int, AgeAndSex), List<Pawn>>
            WildSexedCache = new(5);
    }
}

internal static class ManagerJob_Livestock_ManagerCacheExtensions
{
    public static ManagerJob_Livestock.LivestockCachesComp LivestockCaches(this Manager manager)
    {
        return manager.CompOfType<ManagerJob_Livestock.LivestockCachesComp>()!;
    }
}
