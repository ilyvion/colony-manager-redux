// ManagerJobCompProperties.cs
// Copyright (c) 2024–2025 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Properties for a manager job component, including its type and validation logic.
/// </summary>
public class ManagerJobCompProperties
{
    /// <summary>
    /// The type of the component class for this job component.
    /// </summary>
    [TranslationHandle]
    public Type? compClass;

    /// <summary>
    /// Returns configuration errors for this job component in the context of the specified parent definition.
    /// </summary>
    /// <param name="parentDef">The parent <see cref="ManagerDef"/> to validate against.</param>
    /// <returns>An enumerable of error messages, if any.</returns>
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
        for (var i = 0; i < parentDef.jobComps.Count; i++)
        {
            if (parentDef.jobComps[i] != this && parentDef.jobComps[i].compClass == compClass)
            {
                yield return "two job comps with same compClass: " + compClass;
            }
        }
    }
}
