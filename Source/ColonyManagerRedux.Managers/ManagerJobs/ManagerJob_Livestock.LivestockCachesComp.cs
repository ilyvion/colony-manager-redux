// ManagerJob_Livestock.LivestockCachesComp.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024–2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

internal partial class ManagerJob_Livestock
{
    public sealed class LivestockCachesComp : ManagerComp
    {
        // Dead/despawned pawns used as dictionary keys below are never removed by the normal
        // cache refresh logic (that only trims the *values*), so without this the dictionaries
        // grow for as long as the game session lasts, and keep dead Pawn objects alive in memory.
        private const int PruneIntervalTicks = 5000;
        private int _lastPruneTick = -PruneIntervalTicks;

        internal readonly CachedValues<(PawnKindDef, int), List<Pawn>> AllCache = new(5);

        internal readonly CachedValues<(PawnKindDef, int, AgeAndSex), List<Pawn>> AllSexedCache =
            new(5);

        internal readonly Dictionary<Pawn, CachedValue<List<Pawn>>> FollowerCache = [];

        internal readonly Dictionary<
            (PawnKindDef, Map, MasterMode),
            CachedValue<List<Pawn>>
        > MasterCache = [];

        internal readonly Dictionary<Pawn, CachedValue<bool>> MilkablePawnCache = [];

        internal readonly Dictionary<Pawn, CachedValue<bool>> ShearablePawnCache = [];

        internal readonly CachedValues<(PawnKindDef, int, bool), List<Pawn>> TameCache = new(5);

        internal readonly CachedValues<
            (PawnKindDef, int, AgeAndSex, bool),
            List<Pawn>
        > TameSexedCache = new(5);

        internal readonly CachedValues<(PawnKindDef, int), List<Pawn>> WildCache = new(5);

        internal readonly CachedValues<(PawnKindDef, int, AgeAndSex), List<Pawn>> WildSexedCache =
            new(5);

        public override void CompTick()
        {
            var currentTick = Find.TickManager.TicksGame;
            if (!ShouldPrune(currentTick, _lastPruneTick, PruneIntervalTicks))
            {
                return;
            }
            _lastPruneTick = currentTick;

            PruneDeadPawns(FollowerCache);
            PruneDeadPawns(MilkablePawnCache);
            PruneDeadPawns(ShearablePawnCache);
        }

        /// <summary>
        /// Pure interval-gating check extracted from <see cref="CompTick"/> so it's unit-testable
        /// without a live <see cref="TickManager"/>.
        /// </summary>
        internal static bool ShouldPrune(int currentTick, int lastPruneTick, int intervalTicks) =>
            currentTick - lastPruneTick >= intervalTicks;

        private static void PruneDeadPawns<TValue>(Dictionary<Pawn, TValue> cache)
        {
            List<Pawn>? deadKeys = null;
            foreach (var pawn in cache.Keys)
            {
                if (pawn.DestroyedOrNull() || pawn.Dead)
                {
                    (deadKeys ??= []).Add(pawn);
                }
            }

            if (deadKeys == null)
            {
                return;
            }

            foreach (var pawn in deadKeys)
            {
                _ = cache.Remove(pawn);
            }
        }
    }
}

internal static class ManagerJob_Livestock_ManagerCacheExtensions
{
    public static ManagerJob_Livestock.LivestockCachesComp LivestockCaches(this Manager manager) =>
        manager.CompOfType<ManagerJob_Livestock.LivestockCachesComp>()!;
}
