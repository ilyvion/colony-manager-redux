// RecipeProductResolver_Smelted.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

/// <summary>
/// Resolver for smelting/destroy recipes (<see cref="SpecialProductType.Smelted"/>). The exact
/// output depends on which item gets smelted, which isn't known until the bill actually runs, so
/// there's no single static product — but the set of possible outputs is still static: vanilla's
/// own <see cref="Thing.SmeltProducts"/> only ever yields a def from the smelted item's
/// (adjusted) cost list, filtered to defs that are themselves stuff and smeltable (Steel,
/// Plasteel, Silver, Gold, Uranium, Bioferrite), plus whatever a def's own
/// <see cref="ThingDef.smeltProducts"/> lists (e.g. slag → Steel; those targets are always
/// stuff/smeltable too).
/// </summary>
internal sealed class RecipeProductResolver_Smelted : RecipeProductResolver
{
    public override bool CanResolve(RecipeDef recipe) =>
        recipe.specialProducts?.Contains(SpecialProductType.Smelted) ?? false;

    public override void ConfigureFilter(RecipeDef recipe, ThingFilter filter)
    {
        foreach (var thingDef in DefDatabase<ThingDef>.AllDefsListForReading)
        {
            if (thingDef.IsStuff && thingDef.smeltable)
            {
                filter.SetAllow(thingDef, true);
            }
        }
    }
}
