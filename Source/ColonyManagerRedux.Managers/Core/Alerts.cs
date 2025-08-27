// Alerts.cs
// Copyright (c) 2024–2025 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class Alert_AutoslaughterOverlap : Alert
{
    private readonly CachedValue<List<ThingDef>> _overlappingAnimals;

    public Alert_AutoslaughterOverlap()
    {
        defaultLabel = "ColonyManagerRedux.Alerts.AutoslaughterOverlapLabel".Translate();
        defaultExplanation = "ColonyManagerRedux.Alerts.AutoslaughterOverlap".Translate();

        _overlappingAnimals = new CachedValue<List<ThingDef>>(() =>
        {
            var autoSlaughterVanillaAnimals = AutoSlaughterVanillaAnimals();
            var autoSlaugherLivestockAnimals = AutoSlaugherLivestockAnimals();

            return [.. autoSlaughterVanillaAnimals.Intersect(autoSlaugherLivestockAnimals)];
        });
    }

    public override AlertPriority Priority => AlertPriority.Medium;

    public override AlertReport GetReport() => _overlappingAnimals.Value.Count > 0;

    public override TaggedString GetExplanation() =>
        "ColonyManagerRedux.Alerts.AutoslaughterOverlap".Translate(
            "ColonyManagerRedux.Livestock.CullExcess".Translate(),
            "- " + _overlappingAnimals.Value.Join(a => a.race.AnyPawnKind.GetLabelPlural(), "\n- ")
        );

    private static IEnumerable<ThingDef> AutoSlaughterVanillaAnimals()
    {
        var currentMap = Find.CurrentMap;
        if (currentMap == null)
        {
            yield break;
        }

        foreach (var config in currentMap.autoSlaughterManager.configs)
        {
            if (
                config.maxTotal != -1
                || config.maxFemales != -1
                || config.maxFemalesYoung != -1
                || config.maxMales != -1
                || config.maxMalesYoung != -1
            )
            {
                yield return config.animal;
            }
        }
    }

    private static IEnumerable<ThingDef> AutoSlaugherLivestockAnimals()
    {
        var currentMap = Find.CurrentMap;
        if (currentMap == null)
        {
            yield break;
        }

        var manager = Manager.For(currentMap);
        foreach (var managerJobLivestock in manager.JobTracker.JobsOfType<ManagerJob_Livestock>())
        {
            if (
                managerJobLivestock.CullExcess
                && managerJobLivestock.TriggerPawnKind.pawnKind != null
            )
            {
                yield return managerJobLivestock.TriggerPawnKind.pawnKind.race;
            }
        }
    }
}
