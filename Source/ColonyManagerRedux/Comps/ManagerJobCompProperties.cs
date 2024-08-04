// ManagerJobCompProperties.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public class ManagerJobCompProperties
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
        if (!typeof(ManagerJobComp).IsAssignableFrom(compClass))
        {
            yield return $"{nameof(compClass)} is not a subclass of {nameof(ManagerJobComp)}";
        }
        for (int i = 0; i < parentDef.comps.Count; i++)
        {
            if (parentDef.comps[i] != this && parentDef.comps[i].compClass == compClass)
            {
                yield return "two comps with same compClass: " + compClass;
            }
        }
    }
}
