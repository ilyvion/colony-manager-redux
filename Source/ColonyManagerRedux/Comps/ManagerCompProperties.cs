// ManagerCompProperties.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public class ManagerCompProperties
{
    [TranslationHandle]
    public Type? compClass;

    public virtual IEnumerable<string> ConfigErrors(ManagerDef parentDef)
    {
        if (parentDef == null)
        {
            throw new ArgumentNullException(nameof(parentDef));
        }

        if (compClass == null)
        {
            yield return "compClass is null";
        }
        if (!typeof(ManagerComp).IsAssignableFrom(compClass))
        {
            yield return $"{nameof(compClass)} is not a subclass of {nameof(ManagerComp)}";
        }
        for (int i = 0; i < parentDef.jobComps.Count; i++)
        {
            if (parentDef.managerComps[i] != this && parentDef.jobComps[i].compClass == compClass)
            {
                yield return "two manager comps with same compClass: " + compClass;
            }
        }
    }
}
