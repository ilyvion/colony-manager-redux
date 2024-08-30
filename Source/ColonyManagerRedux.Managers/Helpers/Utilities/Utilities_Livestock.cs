// Utilities_Livestock.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

// NOTE: These enum names are used to name save game labels, do not change them without the proper
// care, as it'll cause save/load issues for players.
public enum AgeAndSex
{
    AdultFemale = 0,
    AdultMale = 1,
    JuvenileFemale = 2,
    JuvenileMale = 3
}

public static class AgeAndSexExtensions
{
    public static string GetLabel(this AgeAndSex ageAndSex, bool plural = false)
    {
        return $"ColonyManagerRedux.AgeAndSex.Order".Translate(
            GetAgeLabel(ageAndSex), GetSexLabel(ageAndSex, plural));
    }

    public static string GetAgeLabel(this AgeAndSex ageAndSex)
    {
        return ageAndSex.IsAdult()
            ? "ColonyManagerRedux.AgeAndSex.Adult".Translate()
            : "ColonyManagerRedux.AgeAndSex.Juvenile".Translate();
    }

    public static string GetSexLabel(this AgeAndSex ageAndSex, bool plural = false)
    {
        return ageAndSex.IsMale()
            ? $"ColonyManagerRedux.AgeAndSex.Male{(plural ? ".Plural" : "")}".Translate()
            : $"ColonyManagerRedux.AgeAndSex.Female{(plural ? ".Plural" : "")}".Translate();
    }

    public static bool IsAdult(this AgeAndSex ageAndSex)
    {
        return ageAndSex == AgeAndSex.AdultFemale ||
            ageAndSex == AgeAndSex.AdultMale;
    }

    public static bool IsMale(this AgeAndSex ageAndSex)
    {
        return ageAndSex == AgeAndSex.JuvenileMale ||
            ageAndSex == AgeAndSex.AdultMale;
    }
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

    private static readonly Dictionary<PawnKindDef, CachedValue<bool>> MilkablePawnKindCache = [];

    private static readonly Dictionary<PawnKindDef, CachedValue<bool>> ShearablePawnKindCache = [];

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
        var allCache = Manager.For(map).LivestockCaches().AllCache;
        var key = (pawnKind, map.uniqueID);
        if (allCache.TryGetValue(key, out var pawns))
        {
            return pawns;
        }

        // if not, set up a cache
        List<Pawn> getter() => map.mapPawns.AllPawnsSpawned
            .Where(p => p.RaceProps.Animal      // is animal
                && !p.Dead                      // is alive
                && p.kindDef == pawnKind        // is our managed pawnkind
                && !p.IsHiddenFromPlayer()      // is not hidden from us
                && !p.Position.Fogged(map)      // is somewhere we can see
            ).ToList();

        allCache.Add(key, getter);
        return getter();
    }


    public static IEnumerable<Pawn>? GetAll(this PawnKindDef pawnKind, Map map, AgeAndSex ageSex)
    {
        var allSexedCache = Manager.For(map).LivestockCaches().AllSexedCache;
        var key = (pawnKind, map.uniqueID, ageSex);
        if (allSexedCache.TryGetValue(key, out var pawns))
        {
            return pawns;
        }

        // is of age and sex we want
        List<Pawn> getter() => pawnKind.GetAll(map).Where(p => PawnIsOfAgeSex(p, ageSex)).ToList();
        allSexedCache.Add(key, getter);
        return getter();
    }

    public static List<Pawn> GetFollowers(this Pawn pawn)
    {
        // check if we have a cached version
        var followerCache = Manager.For(pawn.Map).LivestockCaches().FollowerCache;

        // does it exist at all?
        var cacheExists = followerCache.ContainsKey(pawn);

        // is it up to date?
        if (cacheExists && followerCache[pawn].TryGetValue(out var cached) && cached != null)
        {
            return cached;
        }

        // if not, get a new list.
        cached = pawn.MapHeld.mapPawns.PawnsInFaction(pawn.Faction)
            .Where(p => !p.Dead && p.RaceProps.Animal && p.playerSettings.Master == pawn).ToList();

        // update if key exists
        if (cacheExists)
        {
            followerCache[pawn].Update(cached);
        }

        // else add it
        else
        {
            // severely limit cache to only apply for one cycle (one job)
            followerCache.Add(pawn, new(cached, 2));
        }

        return cached;
    }

    public static IEnumerable<Pawn> GetFollowers(this Pawn pawn, PawnKindDef pawnKind)
    {
        return GetFollowers(pawn).Where(f => f.kindDef == pawnKind);
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
        var masterCache = Manager.For(map).LivestockCaches().MasterCache;

        // does it exist at all?
        var key = (pawnkind, map, mode);
        var cacheExists = masterCache.ContainsKey(key);

        // is it up to date?
        if (cacheExists &&
            masterCache[key].TryGetValue(out var cached) && cached != null)
        {
            return cached;
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
            masterCache[key].Update(cached);
        }

        // else add it
        else
        {
            // severely limit cache to only apply for one cycle (one job)
            masterCache.Add(key, new(cached, 2));
        }

        return cached;
    }

    public static IEnumerable<Pawn> GetTame(this PawnKindDef pawnKind, Map map, bool includeGuests = true)
    {
        var tameCache = Manager.For(map).LivestockCaches().TameCache;

        var key = (pawnKind, map.uniqueID, includeGuests);
        if (tameCache.TryGetValue(key, out var pawns))
        {
            return pawns!;
        }

        List<Pawn> getter() => pawnKind.GetAll(map)
            .Where(p => p.Faction == Faction.OfPlayer && (includeGuests || !p.IsGuest()))
            .ToList();
        tameCache.Add(key, getter);
        return getter();
    }

    public static IEnumerable<Pawn> GetTame(this PawnKindDef pawnKind, Map map, AgeAndSex ageSex, bool cached = true, bool includeGuests = true)
    {
        var tameSexedCache = Manager.For(map).LivestockCaches().TameSexedCache;

        var key = (pawnKind, map.uniqueID, ageSex, includeGuests);
        if (!cached)
        {
            tameSexedCache.Invalidate(key);
        }
        if (tameSexedCache.TryGetValue(key, out var pawns) && pawns != null)
        {
            return pawns;
        }

        List<Pawn> getter() => pawnKind.GetAll(map, ageSex)
            .Where(p => p.Faction == Faction.OfPlayer && (includeGuests || !p.IsGuest())).ToList();
        tameSexedCache.Add(key, getter);
        return getter();
    }

    public static IEnumerable<Pawn> GetTrainers(this PawnKindDef pawnkind, Map map, MasterMode mode)
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
                        pawnkind.RaceProps.wildness), 0f, 20f));
    }

    public static IEnumerable<Pawn>? GetWild(this PawnKindDef pawnKind, Map map)
    {
        var wildCache = Manager.For(map).LivestockCaches().WildCache;

        var key = (pawnKind, map.uniqueID);
        if (wildCache.TryGetValue(key, out var pawns))
        {
            return pawns;
        }

        List<Pawn> getter() => pawnKind.GetAll(map).Where(p => p.Faction == null).ToList();
        wildCache.Add(key, getter);
        return getter();
    }

    public static IEnumerable<Pawn> GetWild(this PawnKindDef pawnKind, Map map, AgeAndSex ageSex)
    {
        var wildSexedCache = Manager.For(map).LivestockCaches().WildSexedCache;

        var key = (pawnKind, map.uniqueID, ageSex);
        if (wildSexedCache.TryGetValue(key, out var pawns))
        {
            return pawns;
        }

        List<Pawn> getter() => pawnKind.GetAll(map, ageSex).Where(p => p.Faction == null).ToList();
        wildSexedCache.Add(key, getter);
        return getter();
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
        if (MilkablePawnKindCache.TryGetValue(pawnKind, out CachedValue<bool>? cachedValue))
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
        MilkablePawnKindCache.Add(pawnKind, new CachedValue<bool>(ret, int.MaxValue));
        return ret;
    }

    public static bool Milkable(this Pawn pawn)
    {
        var milkablePawnCache = Manager.For(pawn.Map).LivestockCaches().MilkablePawnCache;

        if (milkablePawnCache.TryGetValue(pawn, out var cachedValue))
        {
            if (cachedValue.TryGetValue(out var value))
            {
                return value;
            }

            value = pawn.IsPawnMilkable();
            milkablePawnCache[pawn].Update(value);
            return value;
        }

        var ret = pawn.IsPawnMilkable();
        milkablePawnCache.Add(pawn, new CachedValue<bool>(ret, 5000));
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
        if (ShearablePawnKindCache.TryGetValue(pawnKind, out CachedValue<bool>? cachedValue))
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
        ShearablePawnKindCache.Add(pawnKind, new CachedValue<bool>(ret, int.MaxValue));
        return ret;
    }

    public static bool Shearable(this Pawn pawn)
    {
        var shearablePawnCache = Manager.For(pawn.Map).LivestockCaches().ShearablePawnCache;

        if (shearablePawnCache.TryGetValue(pawn, out var cachedValue))
        {
            if (cachedValue.TryGetValue(out var value))
            {
                return value;
            }

            value = pawn.IsPawnShearable();
            shearablePawnCache[pawn].Update(value);
            return value;
        }

        var ret = pawn.IsPawnShearable();
        shearablePawnCache.Add(pawn, new CachedValue<bool>(ret, 5000));
        return ret;
    }

    public static int TicksTillHarvestable(this CompHasGatherableBodyResource comp)
    {
        var growthRatePerTick = 1f / (comp.GatherResourcesIntervalDays * GenDate.TicksPerDay);

        if (comp.parent is not Pawn)
        {
            throw new ArgumentException("harvestable should always be on a Pawn");
        }

        growthRatePerTick *= PawnUtility.BodyResourceGrowthSpeed((Pawn)comp.parent);

        return Mathf.CeilToInt((1 - comp.Fullness) / growthRatePerTick);
    }

    public static bool VisiblyPregnant(this Pawn pawn) =>
        pawn?.health.hediffSet.GetFirstHediff<Hediff_Pregnant>()?.Visible ?? false;

    private static bool IsPawnMilkable(this Pawn pawn) =>
        pawn?.TryGetComp<CompMilkable>()?.Active ?? false;

    private static bool IsPawnShearable(this Pawn pawn) =>
        pawn?.TryGetComp<CompShearable>()?.Active ?? false;
}
