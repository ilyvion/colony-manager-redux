// ManagerJob_Livestock_AnimalGenetics.cs
// Copyright gregorycurrie
// Copyright Mlie
// Copyright (c) 2024–2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;

namespace ColonyManagerRedux.AnimalGenetics.Core;

[HotSwappable]
internal sealed class ManagerJob_Livestock_AnimalGenetics : ManagerJobComp
{
    private bool _useForCulling;
    private bool _useForTaming;
    private Dictionary<StatDef, float> _values = [];

#pragma warning disable CS8618 // Set in Initialize
    private Func<Pawn, float, float> OriginalTamingPawnSortScore;
    private Func<AgeAndSex, IEnumerable<Pawn>, IEnumerable<Pawn>> OriginalCullingPawnSorter;
#pragma warning restore CS8618

    private static List<StatDef> AffectedStats =>
#if v1_5
        global::AnimalGenetics.Constants.affectedStats;
#else
        global::AnimalGenetics.Constants.AffectedStats;
#endif

    protected override void Initialize()
    {
        ColonyManagerReduxMod.Instance.LogDevMessage("AnimalGenetics job comp initialized!");

        var livestockJob = (ManagerJob_Livestock)Parent;

        OriginalTamingPawnSortScore = livestockJob.TamingPawnSortScore;
        livestockJob.TamingPawnSortScore = TamingPawnSortScore;

        OriginalCullingPawnSorter = livestockJob.CullingPawnSorter;
        livestockJob.CullingPawnSorter = CullingPawnSorter;

        foreach (var (gene, weight) in InitialGeneWeights(AffectedStats))
        {
            _values[gene] = weight;
        }
    }

    /// <summary>Every gene weighted equally, summing to 1.</summary>
    internal static Dictionary<TKey, float> InitialGeneWeights<TKey>(
        IReadOnlyCollection<TKey> genes
    )
        where TKey : notnull => genes.ToDictionary(gene => gene, _ => 1.0f / genes.Count);

    protected override void PostExposeData()
    {
        Scribe_Values.Look(ref _useForTaming, "useAnimalGeneticsForTaming");
        Scribe_Values.Look(ref _useForCulling, "useAnimalGeneticsForCulling");
        Scribe_Collections.Look(ref _values, "valuesAnimalGenetics", LookMode.Def);
    }

    protected override void PostRenderSection(
        string sectionColumn,
        string section,
        ref Vector2 position,
        float width
    )
    {
        if (
            sectionColumn == ManagerTab_Livestock.LivestockOptions
            && section == "CullingExceptions"
        )
        {
            Widgets_Section.Section(
                ref position,
                width,
                DrawAnimalGeneticsOverridesSection,
                "ColonyManagerRedux.Livestock.AnimalGenetics.Use".Translate()
            );
            if (_useForTaming || _useForCulling)
            {
                Widgets_Section.Section(
                    ref position,
                    width,
                    DrawAnimalGeneticsGeneBiasSection,
                    "ColonyManagerRedux.Livestock.AnimalGenetics.GeneBias".Translate()
                );
            }
        }
    }

    private float DrawAnimalGeneticsGeneBiasSection(Vector2 position, float width)
    {
        var rect = new Rect(position, new Vector2(width, 1000));

        var listingStandard = new Listing_Standard();
        listingStandard.Begin(rect);

        Text.Font = GameFont.Tiny;

        foreach (var gene in AffectedStats)
        {
            var label = global::AnimalGenetics.Constants.GetLabel(gene);
            var description = global::AnimalGenetics.Constants.GetDescription(gene);

#if v1_5
            var highlightRect = listingStandard.Label(label, -1f, description);
#else
            var highlightRect = listingStandard.Label(label, -1f, (TipSignal?)description);
#endif

            highlightRect.height += DoGeneSlider(listingStandard, gene);

            Widgets.DrawHighlightIfMouseover(highlightRect);
        }

        listingStandard.End();

        return listingStandard.CurHeight;
    }

    private const float MinGeneValue = 0.0001f;
    private const float MaxGeneValue = 0.999f;

    private float DoGeneSlider(Listing_Standard listingStandard, StatDef gene)
    {
        var startHeight = listingStandard.CurHeight;

        var newValue = listingStandard.Slider(_values[gene], MinGeneValue, MaxGeneValue);

        if (newValue == _values[gene])
        {
            return listingStandard.CurHeight - startHeight;
        }

        _values = RebalanceAfterSliderChange(_values, gene, newValue, MinGeneValue, MaxGeneValue);

        return listingStandard.CurHeight - startHeight;
    }

    // Redistributes 1 - newValue proportionally across the other genes, clamps each to
    // [min, max], then re-normalizes the clamped others so the full set still sums to 1.0
    // instead of drifting over repeated edits (regression test for the bug fixed in d3700fa).
    internal static Dictionary<TKey, float> RebalanceAfterSliderChange<TKey>(
        IReadOnlyDictionary<TKey, float> values,
        TKey changedKey,
        float newValue,
        float min,
        float max
    )
        where TKey : notnull
    {
        var result = new Dictionary<TKey, float>(values) { [changedKey] = newValue };

        var otherKeys = result.Keys.Where(key => !key.Equals(changedKey)).ToList();
        var target = 1.0f - newValue;
        var others = otherKeys.Sum(key => result[key]);

        // When the other genes carry no weight to scale from, share the remainder evenly.
        foreach (var key in otherKeys)
        {
            var scaled = others > 0f ? target / others * result[key] : target / otherKeys.Count;
            result[key] = Math.Min(Math.Max(scaled, min), max);
        }

        var clampedOthersSum = otherKeys.Sum(key => result[key]);
        if (clampedOthersSum > 0f)
        {
            foreach (var key in otherKeys)
            {
                result[key] = result[key] / clampedOthersSum * target;
            }
        }

        return result;
    }

    private float DrawAnimalGeneticsOverridesSection(Vector2 pos, float width)
    {
        var start = pos;

        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.Livestock.AnimalGenetics.OverrideTaming".Translate(),
            "ColonyManagerRedux.Livestock.AnimalGenetics.OverrideTamingTip".Translate(),
            ref _useForTaming
        );
        Utilities.DrawToggle(
            ref pos,
            width,
            "ColonyManagerRedux.Livestock.AnimalGenetics.OverrideCulling".Translate(),
            "ColonyManagerRedux.Livestock.AnimalGenetics.OverrideCullingTip".Translate(),
            ref _useForCulling
        );

        return pos.y - start.y;
    }

    private IEnumerable<Pawn> CullingPawnSorter(AgeAndSex ageAndSex, IEnumerable<Pawn> pawns) =>
        OrderForCulling(
            _useForCulling,
            pawns,
            CalculatePreferenceScore,
            () => OriginalCullingPawnSorter(ageAndSex, pawns)
        );

    private float TamingPawnSortScore(Pawn pawn, float distance) =>
        TamingSortScore(
            _useForTaming,
            () =>
            {
                var preferenceScore = CalculatePreferenceScore(pawn);
                ColonyManagerReduxMod.Instance.LogDebug(
                    pawn + " preference score: " + preferenceScore
                );
                return preferenceScore;
            },
            () => OriginalTamingPawnSortScore(pawn, distance)
        );

    /// <summary>
    /// Lowest preference score first, so the animals with the worst genes are culled first.
    /// </summary>
    internal static IEnumerable<T> OrderForCulling<T>(
        bool useOverride,
        IEnumerable<T> items,
        Func<T, float> preferenceScore,
        Func<IEnumerable<T>> original
    ) => useOverride ? items.OrderBy(preferenceScore) : original();

    /// <summary>
    /// The preference score itself, which sorts descending, so the best genes are tamed first.
    /// </summary>
    internal static float TamingSortScore(
        bool useOverride,
        Func<float> preferenceScore,
        Func<float> original
    ) => useOverride ? preferenceScore() : original();

    private float CalculatePreferenceScore(Pawn pawn) =>
        CalculatePreferenceScore(AffectedStats, _values, pawn, GetGene);

    /// <summary>
    /// Weighted dot-product of per-gene values against a pawn's actual gene stats: the score
    /// used to sort/rank animals for taming and culling when the AnimalGenetics override is
    /// active.
    /// </summary>
    internal static float CalculatePreferenceScore<TStat, TPawn>(
        IEnumerable<TStat> affectedStats,
        IReadOnlyDictionary<TStat, float> values,
        TPawn pawn,
        Func<TPawn, TStat, float> geneValue
    )
        where TStat : notnull => affectedStats.Sum(gene => geneValue(pawn, gene) * values[gene]);

    private static float GetGene(Pawn pawn, StatDef gene) =>
        gene == global::AnimalGenetics.AnimalGenetics.GatherYield
        && !global::AnimalGenetics.Genes.Gatherable(pawn)
            ? 0.0f
            : global::AnimalGenetics.Genes.GetGene(pawn, gene);
}
