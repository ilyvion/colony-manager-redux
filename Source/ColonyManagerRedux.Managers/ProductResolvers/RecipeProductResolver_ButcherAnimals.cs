// RecipeProductResolver_ButcherAnimals.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

/// <summary>
/// Resolver for butchery recipes (no static product; matches vanilla's own
/// <see cref="RecipeWorkerCounter_ButcherAnimals"/>, which always counts
/// <see cref="ThingCategoryDefOf.MeatRaw"/>'s child defs regardless of which animal was
/// butchered).
/// </summary>
internal sealed class RecipeProductResolver_ButcherAnimals : RecipeProductResolver
{
    public override bool CanResolve(RecipeDef recipe) =>
        recipe.workerCounterClass == typeof(RecipeWorkerCounter_ButcherAnimals);

    public override void ConfigureFilter(RecipeDef recipe, ThingFilter filter) =>
        filter.SetAllow(ThingCategoryDefOf.MeatRaw, true, null, null);

    /// <summary>
    /// Unlike <see cref="ConfigureFilter"/>'s whole-<see cref="ThingCategoryDefOf.MeatRaw"/>-tree
    /// approximation (which exists purely to mirror vanilla's stock-progress counter), butchery
    /// really only ever yields whatever a butchered race's own
    /// <see cref="RaceProperties.meatDef"/> is - it can never yield e.g. raw fish, even though
    /// Odyssey nests the "Fish" category under "MeatRaw" in the def tree. Linking a job to
    /// "butcher creature" for an ingredient like fish would offer a producer that could never
    /// actually supply it, so job-linking uses this precise set instead.
    /// </summary>
    public override void ConfigureLinkableProductFilter(RecipeDef recipe, ThingFilter filter)
    {
        foreach (var meatDef in RealButcheryMeatDefs())
        {
            filter.SetAllow(meatDef, true);
        }
    }

    /// <summary>
    /// Unlike <see cref="ConfigureLinkableProductFilter"/> (which only needs the *set* of real
    /// meat outputs), restricting a producer's ingredients requires the reverse mapping from a
    /// specific corpse <see cref="ThingDef"/> to the single meat def butchering it yields, so a
    /// linked producer's <c>AllowedIngredients</c> can be narrowed to exactly the corpses that
    /// cover <paramref name="desiredOutputs"/> instead of every corpse regardless of what its
    /// linked consumers actually need. Pure over its inputs (kept separate from the
    /// <see cref="DefDatabase{T}"/> scan in <see cref="CorpseToMeatDef"/> so it's independently
    /// unit-testable) - see <see cref="ManagerJob_Production.FilterIngredientsProducingDesiredOutputs"/>.
    /// </summary>
    public override IEnumerable<ThingDef> IngredientsProducing(
        RecipeDef recipe,
        IEnumerable<ThingDef> candidateIngredients,
        IReadOnlyCollection<ThingDef> desiredOutputs
    ) =>
        ManagerJob_Production.FilterIngredientsProducingDesiredOutputs(
            candidateIngredients,
            CorpseToMeatDef(),
            desiredOutputs
        );

    private static IEnumerable<ThingDef> RealButcheryMeatDefs() =>
        DefDatabase<ThingDef>
            .AllDefsListForReading.Where(d => d.race?.meatDef != null)
            .Select(d => d.race!.meatDef)
            .Distinct();

    private static Dictionary<ThingDef, ThingDef> CorpseToMeatDef()
    {
        var map = new Dictionary<ThingDef, ThingDef>();
        foreach (var raceDef in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (raceDef.race?.corpseDef != null && raceDef.race.meatDef != null)
            {
                map[raceDef.race.corpseDef] = raceDef.race.meatDef;
            }
        }
        return map;
    }
}
