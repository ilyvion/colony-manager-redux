// Alerts.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Microsoft.Performance",
    "CA1812:AvoidUninstantiatedInternalClasses",
    Justification = "Class is instantiated via reflection")]
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
            var autoSlaughterVanillaAnimals = AutoSlaughterVanillaAnimals().ToList();
            var autoSlaugherLivestockAnimals = AutoSlaugherLivestockAnimals().ToList();

            return autoSlaughterVanillaAnimals.Intersect(autoSlaugherLivestockAnimals).ToList();
        });
    }

    public override AlertPriority Priority => AlertPriority.Medium;

    public override AlertReport GetReport()
    {
        return _overlappingAnimals.Value.Count > 0;
    }

    public override TaggedString GetExplanation()
    {
        return "ColonyManagerRedux.Alerts.AutoslaughterOverlap".Translate(
            "ColonyManagerRedux.Livestock.CullExcess".Translate(),
            "- " + _overlappingAnimals.Value.Join(a => a.race.AnyPawnKind.GetLabelPlural(), "\n- "));
    }

    private static IEnumerable<ThingDef> AutoSlaughterVanillaAnimals()
    {
        foreach (AutoSlaughterConfig config in Find.CurrentMap.autoSlaughterManager.configs)
        {
            if (config.maxTotal != -1 || config.maxFemales != -1 || config.maxFemalesYoung != -1 || config.maxMales != -1 || config.maxMalesYoung != -1)
            {
                yield return config.animal;
            }
        }
    }

    private static IEnumerable<ThingDef> AutoSlaugherLivestockAnimals()
    {
        foreach (var managerJobLivestock in Manager.For(Find.CurrentMap).JobTracker.JobsOfType<ManagerJob_Livestock>())
        {
            if (managerJobLivestock.CullExcess)
            {
                yield return managerJobLivestock.TriggerPawnKind.pawnKind.race;
            }
        }
    }
}
