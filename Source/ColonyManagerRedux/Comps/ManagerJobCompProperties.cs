// ManagerJobCompProperties.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public class ManagerJobCompProperties
{
    [TranslationHandle]
    public Type? compClass;

    public virtual IEnumerable<string> ConfigErrors(ManagerDef parentDef)
    {
        if (compClass == null)
        {
            yield return "compClass is null";
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
