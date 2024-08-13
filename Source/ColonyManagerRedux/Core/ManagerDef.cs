// ManagerDef.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public class ManagerDef : Def
{
    public int order;
    public Type? managerJobClass;
    public Type managerTabClass = typeof(ManagerTab);
    public Type? managerSettingsClass;

    public List<ManagerJobCompProperties> jobComps = [];
    public List<ManagerCompProperties> managerComps = [];

    public IconArea iconArea = IconArea.Middle;
    [Unsaved(false)]
    public Texture2D icon = BaseContent.BadTex;
    [NoTranslate]
    public string iconPath = "UI/Icons/CMR_Hammer";

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

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string item in base.ConfigErrors())
        {
            yield return item;
        }

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

        if (managerSettingsClass != null && !typeof(ManagerSettings).IsAssignableFrom(managerSettingsClass))
        {
            yield return $"{nameof(managerSettingsClass)} is not a subclass of {nameof(ManagerSettings)}";
        }

        foreach (ManagerJobCompProperties comp in jobComps)
        {
            foreach (string item in comp.ConfigErrors(this))
            {
                yield return item;
            }
        }
    }
}

public enum IconArea
{
    Left = 0,
    Middle = 1,
    Right = 2
}
