
namespace ColonyManagerRedux;

public partial class Manager
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
