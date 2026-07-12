// ManagerDef.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Definition for a manager job, including job, tab, settings, and icon configuration.
/// </summary>
public class ManagerDef : Def
{
    /// <summary>
    /// The order in which this manager appears in the UI.
    /// </summary>
    public int order;

    /// <summary>
    /// The type of the manager job class.
    /// </summary>
    public Type? managerJobClass;

    /// <summary>
    /// The type of the manager tab class.
    /// </summary>
    public Type managerTabClass = typeof(ManagerTab);

    /// <summary>
    /// The type of the manager settings class.
    /// </summary>
    public Type? managerSettingsClass;

    /// <summary>
    /// The list of job component properties for this manager.
    /// </summary>
    public List<ManagerJobCompProperties> jobComps = [];

    /// <summary>
    /// The list of manager component properties for this manager.
    /// </summary>
    public List<ManagerCompProperties> managerComps = [];

    /// <summary>
    /// The icon area where this manager's tab appears.
    /// </summary>
    public IconArea iconArea = IconArea.Middle;

    /// <summary>
    /// The icon texture for this manager tab.
    /// </summary>
    [Unsaved(false)]
    public Texture2D icon = BaseContent.BadTex;

    /// <summary>
    /// The path to the icon texture for this manager tab.
    /// </summary>
    [NoTranslate]
    public string iconPath = "UI/Icons/CMR_Hammer";

    /// <inheritdoc/>
    public override void PostLoad()
    {
        if (!iconPath.NullOrEmpty())
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                icon = ContentFinder<Texture2D>.Get(iconPath);
            });
        }
    }

    /// <inheritdoc/>
    public override IEnumerable<string> ConfigErrors()
    {
        foreach (var item in base.ConfigErrors())
        {
            yield return item;
        }

        foreach (
            var item in ValidateManagerDefTypes(
                managerJobClass,
                managerTabClass,
                managerSettingsClass
            )
        )
        {
            yield return item;
        }

        foreach (var comp in jobComps)
        {
            foreach (var item in comp.ConfigErrors(this))
            {
                yield return item;
            }
        }

        foreach (var comp in managerComps)
        {
            foreach (var item in comp.ConfigErrors(this))
            {
                yield return item;
            }
        }
    }

    /// <summary>
    /// Validates that <paramref name="managerJobClass"/>, <paramref name="managerTabClass"/>, and
    /// <paramref name="managerSettingsClass"/> are null (where allowed) or assignable to the
    /// expected base types, yielding a config-error string for each violation. Kept separate from
    /// <see cref="ConfigErrors"/> so this is unit-testable without a live <see cref="Def"/>.
    /// </summary>
    internal static IEnumerable<string> ValidateManagerDefTypes(
        Type? managerJobClass,
        Type? managerTabClass,
        Type? managerSettingsClass
    )
    {
        if (managerJobClass != null && !typeof(ManagerJob).IsAssignableFrom(managerJobClass))
        {
            yield return $"{nameof(managerJobClass)} is not {nameof(ManagerJob)} or a subclass thereof";
        }

        if (managerTabClass == null)
        {
            yield return $"{nameof(managerTabClass)} is null";
        }
        else if (!typeof(ManagerTab).IsAssignableFrom(managerTabClass))
        {
            yield return $"{nameof(managerTabClass)} is not {nameof(ManagerTab)} or a subclass thereof";
        }

        if (
            managerSettingsClass != null
            && !typeof(ManagerSettings).IsAssignableFrom(managerSettingsClass)
        )
        {
            yield return $"{nameof(managerSettingsClass)} is not a subclass of {nameof(ManagerSettings)}";
        }
    }
}

/// <summary>
/// Specifies the area of the UI where a manager tab icon appears.
/// </summary>
public enum IconArea
{
    /// <summary>
    /// The left icon area.
    /// </summary>
    Left = 0,

    /// <summary>
    /// The middle icon area.
    /// </summary>
    Middle = 1,

    /// <summary>
    /// The right icon area.
    /// </summary>
    Right = 2,
}
