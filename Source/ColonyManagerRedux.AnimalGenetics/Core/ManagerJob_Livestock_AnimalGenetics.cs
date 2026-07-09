// ManagerJob_Livestock_AnimalGenetics.cs
// Copyright gregorycurrie
// Copyright Mlie
// Copyright (c) 2024 Alexander Krivács Schrøder

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

        foreach (var gene in AffectedStats)
        {
            _values[gene] = 1.0f / AffectedStats.Count;
        }
    }

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
        if (sectionColumn == ManagerTab_Livestock.LivestockOptions && section == "Culling")
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

        _values[gene] = newValue;

        var otherKeys = _values.Keys.Where(key => key != gene).ToList();
        var others = otherKeys.Sum(key => _values[key]);
        var modifier = (1.0f - newValue) / others;

        foreach (var key in otherKeys)
        {
            _values[key] = Math.Min(Math.Max(modifier * _values[key], MinGeneValue), MaxGeneValue);
        }

        // Clamping can push the other genes' sum away from (1 - newValue); re-normalize
        // them so the full set still sums to 1.0 instead of drifting over repeated edits.
        var clampedOthersSum = otherKeys.Sum(key => _values[key]);
        if (clampedOthersSum > 0f)
        {
            var target = 1.0f - newValue;
            foreach (var key in otherKeys)
            {
                _values[key] = _values[key] / clampedOthersSum * target;
            }
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
        !_useForCulling
            ? OriginalCullingPawnSorter(ageAndSex, pawns)
            : pawns.OrderBy(CalculatePreferenceScore);

    private float TamingPawnSortScore(Pawn pawn, float distance)
    {
        if (!_useForTaming)
        {
            return OriginalTamingPawnSortScore(pawn, distance);
        }

        var preferenceScore = CalculatePreferenceScore(pawn);
        ColonyManagerReduxMod.Instance.LogDebug(pawn + " preference score: " + preferenceScore);

        return preferenceScore;
    }

    private float CalculatePreferenceScore(Pawn pawn) =>
        AffectedStats.Sum(gene => GetGene(pawn, gene) * _values[gene]);

    private static float GetGene(Pawn pawn, StatDef gene) =>
        gene == global::AnimalGenetics.AnimalGenetics.GatherYield
        && !global::AnimalGenetics.Genes.Gatherable(pawn)
            ? 0.0f
            : global::AnimalGenetics.Genes.GetGene(pawn, gene);
}
