// ManagerJob_Livestock_AnimalGenetics.cs
// Copyright gregorycurrie
// Copyright Mlie
// Copyright (c) 2024 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;

namespace ColonyManagerRedux.AnimalGenetics;

[HotSwappable]
public class ManagerJob_Livestock_AnimalGenetics : ManagerJobComp
{
    private bool _useForCulling;
    private bool _useForTaming;
    private Dictionary<StatDef, float> _values = [];

#pragma warning disable CS8618 // Set in Initialize
    private Func<Pawn, float, float> OriginalTamingPawnSortScore;
    private Func<AgeAndSex, IEnumerable<Pawn>, IEnumerable<Pawn>> OriginalCullingPawnSorter;
#pragma warning restore CS8618

    public override void Initialize()
    {
        ColonyManagerReduxMod.Instance.LogDevMessage("AnimalGenetics job comp initialized!");

        ManagerJob_Livestock livestockJob = (ManagerJob_Livestock)Parent;

        OriginalTamingPawnSortScore = livestockJob.TamingPawnSortScore;
        livestockJob.TamingPawnSortScore = TamingPawnSortScore;

        OriginalCullingPawnSorter = livestockJob.CullingPawnSorter;
        livestockJob.CullingPawnSorter = CullingPawnSorter;

        foreach (var gene in global::AnimalGenetics.Constants.affectedStats)
        {
            _values[gene] = 1.0f / global::AnimalGenetics.Constants.affectedStats.Count;
        }
    }

    public override void PostExposeData()
    {
        Scribe_Values.Look(ref _useForTaming, "useAnimalGeneticsForTaming");
        Scribe_Values.Look(ref _useForCulling, "useAnimalGeneticsForCulling");
        Scribe_Collections.Look(ref _values, "valuesAnimalGenetics", LookMode.Def);
    }

    public override void PostRenderSection(
        string sectionColumn, string section, ref Vector2 position, float width)
    {
        if (sectionColumn == ManagerTab_Livestock.LivestockOptions && section == "Culling")
        {
            Widgets_Section.Section(ref position, width, DrawAnimalGeneticsOverridesSection,
                "ColonyManagerRedux.Livestock.AnimalGenetics.Use".Translate());
            if (_useForTaming || _useForCulling)
            {
                Widgets_Section.Section(
                    ref position,
                    width,
                    DrawAnimalGeneticsGeneBiasSection,
                    "ColonyManagerRedux.Livestock.AnimalGenetics.GeneBias".Translate());
            }
        }
    }

    private float DrawAnimalGeneticsGeneBiasSection(Vector2 position, float width)
    {
        var rect = new Rect(position, new Vector2(width, 1000));

        var listingStandard = new Listing_Standard();
        listingStandard.Begin(rect);

        Text.Font = GameFont.Tiny;

        foreach (var gene in global::AnimalGenetics.Constants.affectedStats)
        {
            var label = global::AnimalGenetics.Constants.GetLabel(gene);
            var description = global::AnimalGenetics.Constants.GetDescription(gene);

            var highlightRect = listingStandard.Label(label, -1f, description);

            highlightRect.height += DoGeneSlider(listingStandard, gene);

            Widgets.DrawHighlightIfMouseover(highlightRect);
        }

        listingStandard.End();

        return listingStandard.CurHeight;
    }

    private float DoGeneSlider(Listing_Standard listingStandard, StatDef gene)
    {
        var startHeight = listingStandard.CurHeight;

        var newValue = listingStandard.Slider(_values[gene], 0.0001f, 0.999f);

        if (newValue == _values[gene])
        {
            return listingStandard.CurHeight - startHeight;
        }

        _values[gene] = newValue;

        var others = _values.Where(kv => kv.Key != gene).Sum(kv => kv.Value);
        var modifier = (1.0f - newValue) / others;

        foreach (var key in _values.Keys.ToList().Where(key => key != gene))
        {
            _values[key] = Math.Min(Math.Max(modifier * _values[key], 0.01f), 0.99f);
        }

        return listingStandard.CurHeight - startHeight;
    }

    private float DrawAnimalGeneticsOverridesSection(Vector2 pos, float width)
    {
        var start = pos;

        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.Livestock.AnimalGenetics.OverrideTaming".Translate(),
            "ColonyManagerRedux.Livestock.AnimalGenetics.OverrideTamingTip".Translate(),
            ref _useForTaming);
        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.Livestock.AnimalGenetics.OverrideCulling".Translate(),
            "ColonyManagerRedux.Livestock.AnimalGenetics.OverrideCullingTip".Translate(),
            ref _useForCulling);

        return pos.y - start.y;
    }

    private IEnumerable<Pawn> CullingPawnSorter(
        AgeAndSex ageAndSex, IEnumerable<Pawn> pawns)
    {
        if (!_useForCulling)
        {
            return OriginalCullingPawnSorter(ageAndSex, pawns);
        }
        return pawns.OrderBy(CalculatePreferenceScore);
    }

    private float TamingPawnSortScore(Pawn pawn, float distance)
    {
        if (!_useForTaming)
        {
            return OriginalTamingPawnSortScore(pawn, distance);
        }

        float preferenceScore = CalculatePreferenceScore(pawn);
        ColonyManagerReduxMod.Instance.LogDebug(pawn + " preference score: " + preferenceScore);

        return preferenceScore;
    }

    private float CalculatePreferenceScore(Pawn pawn)
    {
        return global::AnimalGenetics.Constants.affectedStats
            .Select(gene => GetGene(pawn, gene) * _values[gene])
            .Sum();
    }

    private static float GetGene(Pawn pawn, StatDef gene)
    {
        if (gene == global::AnimalGenetics.AnimalGenetics.GatherYield
            && !global::AnimalGenetics.Genes.Gatherable(pawn))
            return 0.0f;
        return global::AnimalGenetics.Genes.GetGene(pawn, gene);
    }
}
