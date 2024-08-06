// Utilities_Livestock.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.Extensions;
using System.Reflection;

namespace ColonyManagerRedux;

// NOTE: These enum names are used to name save game labels, do not change them without the proper
// care, as it'll cause save/load issues for players.
public enum AgeAndSex
{
    AdultFemale = 0,
    AdultMale = 1,
    JuvenileFemale = 2,
    JuvenileMale = 3
}

[Flags]
internal enum MasterMode
{
    Manual = 0,
    Hunters = 1,
    Trainers = 2,
    Melee = 4,
    Ranged = 8,
    Violent = 16,
    NonViolent = 32,
    All = Hunters | Trainers | Melee | Ranged | Violent | NonViolent,
    Specific = 64
}

[HotSwappable]
internal static class Utilities_Livestock
{
    public static AgeAndSex[] AgeSexArray = (AgeAndSex[])Enum.GetValues(typeof(AgeAndSex));
    public static MasterMode[] MasterModeArray => (MasterMode[])Enum.GetValues(typeof(MasterMode));

    private static readonly CachedValues<(PawnKindDef, int), List<Pawn>> AllCache = new(5);

    private static readonly CachedValues<(PawnKindDef, int, AgeAndSex), List<Pawn>>
        AllSexedCache = new(5);

    private static readonly Dictionary<Pawn, CachedValue<List<Pawn>>> FollowerCache = [];

    private static readonly
        Dictionary<(PawnKindDef, Map, MasterMode), CachedValue<List<Pawn>>> MasterCache = [];

    private static readonly Dictionary<Pawn, CachedValue<bool>> MilkablePawn = [];

    private static readonly Dictionary<PawnKindDef, CachedValue<bool>> MilkablePawnkind = [];

    private static readonly Dictionary<Pawn, CachedValue<bool>> ShearablePawn = [];

    private static readonly Dictionary<PawnKindDef, CachedValue<bool>> ShearablePawnkind = [];

    private static readonly CachedValues<(PawnKindDef, int, bool), List<Pawn>> TameCache = new(5);

    private static readonly CachedValues<(PawnKindDef, int, AgeAndSex, bool), List<Pawn>>
        TameSexedCache = new(5);

    private static readonly CachedValues<(PawnKindDef, int), List<Pawn>> WildCache = new(5);

    private static readonly CachedValues<(PawnKindDef, int, AgeAndSex), List<Pawn>>
        WildSexedCache = new(5);

    public static bool BondedWithColonist(this Pawn pawn)
    {
        return pawn?.relations?.GetFirstDirectRelationPawn(PawnRelationDefOf.Bond, p => p.IsColonist) != null;
    }

    public static bool IsGuest(this Pawn pawn)
    {
        return pawn?.Faction == Faction.OfPlayer && pawn.HasExtraHomeFaction();
    }

    public static IEnumerable<Pawn>? GetAll(this PawnKindDef pawnKind, Map map)
    {
        // check if we have a cached version
        var key = (pawnKind, map.uniqueID);
        if (AllCache.TryGetValue(key, out var pawns))
        {
            return pawns;
        }

        // if not, set up a cache
        List<Pawn> getter() => map.mapPawns.AllPawnsSpawned
            .Where(p => p.RaceProps.Animal          // is animal
                    && !p.Dead                      // is alive
                    && p.kindDef == pawnKind        // is our managed pawnkind
                    && !p.IsHiddenFromPlayer()      // is not hidden from us
                    && !p.Position.Fogged(map)      // is somewhere we can see
                ).ToList();

        AllCache.Add(key, getter);
        return getter();
    }


    public static IEnumerable<Pawn>? GetAll(this PawnKindDef pawnKind, Map map, AgeAndSex ageSex)
    {
        var key = (pawnKind, map.uniqueID, ageSex);
        if (AllSexedCache.TryGetValue(key, out var pawns))
        {
            return pawns;
        }

        List<Pawn> getter() =>
                pawnKind.GetAll(map).Where(p => PawnIsOfAgeSex(p, ageSex)).ToList(); // is of age and sex we want
        AllSexedCache.Add(key, getter);
        return getter();
    }

    public static List<Pawn> GetFollowers(this Pawn pawn)
    {
        // check if we have a cached version

        // does it exist at all?
        var cacheExists = FollowerCache.ContainsKey(pawn);

        // is it up to date?
        if (cacheExists && FollowerCache[pawn].TryGetValue(out var cached) && cached != null)
        {
            return cached.ToList();
        }

        // if not, get a new list.
        cached = pawn.MapHeld.mapPawns.PawnsInFaction(pawn.Faction)
            .Where(p => !p.Dead &&
                p.RaceProps.Animal &&
                p.playerSettings.Master == pawn
            ).ToList();

        // update if key exists
        if (cacheExists)
        {
            FollowerCache[pawn].Update(cached);
        }

        // else add it
        else
        {
            // severely limit cache to only apply for one cycle (one job)
            FollowerCache.Add(pawn, new(cached, 2));
        }

        return cached.ToList();
    }

    public static List<Pawn> GetFollowers(this Pawn pawn, PawnKindDef pawnKind)
    {
        return GetFollowers(pawn).Where(f => f.kindDef == pawnKind).ToList();
    }

    public static MasterMode GetMasterMode(this Pawn pawn)
    {
        var mode = MasterMode.Manual;

        if (pawn.workSettings.WorkIsActive(WorkTypeDefOf.Hunting))
        {
            mode |= MasterMode.Hunters;
        }

        if (pawn.workSettings.WorkIsActive(WorkTypeDefOf.Handling))
        {
            mode |= MasterMode.Trainers;
        }

        if (pawn.equipment.Primary?.def.IsMeleeWeapon ?? true) // no weapon = melee 
        {
            mode |= MasterMode.Melee;
        }

        if (pawn.equipment.Primary?.def.IsRangedWeapon ?? false)
        {
            mode |= MasterMode.Ranged;
        }

        if (!pawn.WorkTagIsDisabled(WorkTags.Violent))
        {
            mode |= MasterMode.Violent;
        }
        else
        {
            mode |= MasterMode.NonViolent;
        }

        return mode;
    }

    public static List<Pawn> GetMasterOptions(this PawnKindDef pawnkind, Map map, MasterMode mode)
    {
        // check if we have a cached version

        // does it exist at all?
        var key = (pawnkind, map, mode);
        var cacheExists = MasterCache.ContainsKey(key);

        // is it up to date?
        if (cacheExists &&
             MasterCache[key].TryGetValue(out var cached) && cached != null)
        {
            return cached.ToList();
        }

        // if not, get a new list.
        cached = map.mapPawns.FreeColonistsSpawned
            .Where(p => !p.Dead &&
                // matches mode
                (p.GetMasterMode() & mode) != MasterMode.Manual
            ).ToList();

        // update if key exists
        if (cacheExists)
        {
            MasterCache[key].Update(cached);
        }

        // else add it
        else
        {
            // severely limit cache to only apply for one cycle (one job)
            MasterCache.Add(key, new(cached, 2));
        }

        return cached.ToList();
    }

    public static IEnumerable<Pawn>? GetTame(this PawnKindDef pawnKind, Map map, bool includeGuests = true)
    {
        var key = (pawnKind, map.uniqueID, includeGuests);
        if (TameCache.TryGetValue(key, out var pawns))
        {
            return pawns;
        }

        List<Pawn> getter() => pawnKind.GetAll(map)
            .Where(p => p.Faction == Faction.OfPlayer && (includeGuests || !p.IsGuest()))
            .ToList();
        TameCache.Add(key, getter);
        return getter();
    }

    public static IEnumerable<Pawn> GetTame(this PawnKindDef pawnKind, Map map, AgeAndSex ageSex, bool cached = true, bool includeGuests = true)
    {
#if DEBUG_LIFESTOCK_COUNTS
        List<Pawn> tame = GetAll( ageSex ).Where( p => p.Faction == Faction.OfPlayer ).ToList();
        Log.Message( "Tamecount " + ageSex + ": " + tame.Count );
        return tame;
#else
        var key = (pawnKind, map.uniqueID, ageSex, includeGuests);
        if (!cached)
        {
            TameSexedCache.Invalidate(key);
        }
        if (TameSexedCache.TryGetValue(key, out var pawns) && pawns != null)
        {
            return pawns;
        }

        List<Pawn> getter() => pawnKind.GetAll(map, ageSex)
            .Where(p => p.Faction == Faction.OfPlayer && (includeGuests || !p.IsGuest())).ToList();
        TameSexedCache.Add(key, getter);
        return getter();
#endif
    }

    public static List<Pawn> GetTrainers(this PawnKindDef pawnkind, Map map, MasterMode mode)
    {
        return pawnkind.GetMasterOptions(map, mode)
            .Where(p =>
                // skill high enough to handle (copied from StatWorker_MinimumHandlingSkill)
                // NOTE: This does NOT apply postprocessing, so scenario and other offsets DO NOT apply.
                // we can't actually use StatRequests because they're hardcoded for either Things or BuildableDefs.
                p.skills.GetSkill(SkillDefOf.Animals).Level >=
                Mathf.Clamp(
                    GenMath.LerpDouble(
                        0.3f, 1f, 0f, 9f,
                        pawnkind.RaceProps.wildness), 0f, 20f))
            .ToList();
    }

    public static IEnumerable<Pawn>? GetWild(this PawnKindDef pawnKind, Map map)
    {
        var key = (pawnKind, map.uniqueID);
        if (WildCache.TryGetValue(key, out var pawns))
        {
            return pawns;
        }

        List<Pawn> getter() => pawnKind.GetAll(map).Where(p => p.Faction == null).ToList();
        WildCache.Add(key, getter);
        return getter();
    }

    public static IEnumerable<Pawn>? GetWild(this PawnKindDef pawnKind, Map map, AgeAndSex ageSex)
    {
#if DEBUG_LIFESTOCK_COUNTS
        foreach (Pawn p in GetAll( ageSex )) Log.Message(p.Faction?.GetCallLabel() ?? "NULL" );
        List<Pawn> wild = GetAll( ageSex ).Where( p => p.Faction == null ).ToList();
        Log.Message( "Wildcount " + ageSex + ": " + wild.Count );
        return wild;
#else
        var key = (pawnKind, map.uniqueID, ageSex);
        if (WildSexedCache.TryGetValue(key, out var pawns))
        {
            return pawns;
        }

        List<Pawn> getter() => pawnKind.GetAll(map, ageSex).Where(p => p.Faction == null).ToList();
        WildSexedCache.Add(key, getter);
        return getter();
#endif
    }


    public static bool Juvenile(this AgeAndSex ageSex)
    {
        return ageSex == AgeAndSex.JuvenileFemale || ageSex == AgeAndSex.JuvenileMale;
    }

    public static bool Milkable(this PawnKindDef pawnKind)
    {
        if (pawnKind == null)
        {
            return false;
        }

        var ret = false;
        if (MilkablePawnkind.TryGetValue(pawnKind, out CachedValue<bool>? cachedValue))
        {
            if (cachedValue.TryGetValue(out ret))
            {
                return ret;
            }

            ret = pawnKind.race.comps.OfType<CompProperties_Milkable>().Any(cp => cp.milkDef != null);
            cachedValue.Update(ret);
            return ret;
        }

        ret = pawnKind.race.comps.OfType<CompProperties_Milkable>().Any(cp => cp.milkDef != null);
        MilkablePawnkind.Add(pawnKind, new CachedValue<bool>(ret, int.MaxValue));
        return ret;
    }

    public static bool Milkable(this Pawn pawn)
    {
        if (MilkablePawn.TryGetValue(pawn, out var cachedValue))
        {
            if (cachedValue.TryGetValue(out var value))
            {
                return value;
            }

            value = pawn.IsPawnMilkable();
            MilkablePawn[pawn].Update(value);
            return value;
        }

        var ret = pawn.IsPawnMilkable();
        MilkablePawn.Add(pawn, new CachedValue<bool>(ret, 5000));
        return ret;
    }

    public static bool PawnIsOfAgeSex(this Pawn p, AgeAndSex ageSex)
    {
        // note; we're making the assumption here that anything with a lifestage
        // index of 2 or greater is adult - so baby, juvenile, adult, ... this
        // works for vanilla and all modded animals that I know off.

        // note; we're treating anything non-male as female. I know, I'm sorry.

        return ageSex switch
        {
            AgeAndSex.AdultFemale => p.gender != Gender.Male && p.ageTracker.CurLifeStageIndex >= 2,
            AgeAndSex.AdultMale => p.gender == Gender.Male && p.ageTracker.CurLifeStageIndex >= 2,
            AgeAndSex.JuvenileFemale => p.gender != Gender.Male && p.ageTracker.CurLifeStageIndex < 2,
            AgeAndSex.JuvenileMale => p.gender == Gender.Male && p.ageTracker.CurLifeStageIndex < 2,
            _ => throw new ArgumentOutOfRangeException(nameof(ageSex), ageSex, null),
        };
    }

    public static bool Shearable(this PawnKindDef pawnKind)
    {
        if (pawnKind == null)
        {
            return false;
        }

        var ret = false;
        if (ShearablePawnkind.TryGetValue(pawnKind, out CachedValue<bool>? cachedValue))
        {
            if (cachedValue.TryGetValue(out ret))
            {
                return ret;
            }

            ret = pawnKind.race.comps.OfType<CompProperties_Shearable>().Any(cp => cp.woolDef != null);
            cachedValue.Update(ret);
            return ret;
        }

        ret = pawnKind.race.comps.OfType<CompProperties_Shearable>().Any(cp => cp.woolDef != null);
        ShearablePawnkind.Add(pawnKind, new CachedValue<bool>(ret, int.MaxValue));
        return ret;
    }

    public static bool Shearable(this Pawn pawn)
    {
        if (ShearablePawn.TryGetValue(pawn, out var cachedValue))
        {
            if (cachedValue.TryGetValue(out var value))
            {
                return value;
            }

            value = pawn.IsPawnShearable();
            ShearablePawn[pawn].Update(value);
            return value;
        }

        var ret = pawn.IsPawnShearable();
        ShearablePawn.Add(pawn, new CachedValue<bool>(ret, 5000));
        return ret;
    }

    public static int TicksTillHarvestable(this CompHasGatherableBodyResource comp)
    {
        var interval = Traverse.Create(comp).Property("GatherResourcesIntervalDays").GetValue<int>();
        var growthRatePerTick = 1f / (interval * GenDate.TicksPerDay);

        if (comp.parent is not Pawn)
        {
            throw new ArgumentException("harvestable should always be on a Pawn");
        }

        growthRatePerTick *= PawnUtility.BodyResourceGrowthSpeed((Pawn)comp.parent);

        // ColonyManagerReduxMod.Instance.LogDebug( $"rate: {growthRatePerTick}, interval: {interval}");

        return Mathf.CeilToInt((1 - comp.Fullness) / growthRatePerTick);
    }

    public static bool VisiblyPregnant(this Pawn pawn)
    {
        return pawn?.health.hediffSet.GetFirstHediff<Hediff_Pregnant>()?.Visible ?? false;
    }

    private static readonly MethodInfo CompMilkable_Active = AccessTools.PropertyGetter(typeof(CompMilkable), "Active");
    private static bool IsPawnMilkable(this Pawn pawn)
    {
        var comp = pawn?.TryGetComp<CompMilkable>();
        object active = false;
        if (comp != null)
        {
            active = CompMilkable_Active.Invoke(comp, []);
        }

        return (bool)active;
    }

    private static readonly MethodInfo CompShearable_Active = AccessTools.PropertyGetter(typeof(CompShearable), "Active");
    private static bool IsPawnShearable(this Pawn pawn)
    {
        var comp = pawn?.TryGetComp<CompShearable>();
        object active = false;
        if (comp != null)
        {
            active = CompShearable_Active.Invoke(comp, []);
        }

        return (bool)active;
    }
}
