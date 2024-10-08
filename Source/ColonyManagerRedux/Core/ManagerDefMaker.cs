// ManagerDefMaker.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

internal static class ManagerDefMaker
{
    public static ManagerJob? MakeManagerJob(ManagerDef def, Manager manager, params object[] args)
    {
        if (def.managerJobClass == null)
        {
            return null;
        }

        ManagerJob job = (ManagerJob)Activator.CreateInstance(def.managerJobClass, [manager, .. args]);
        job._def = def;
        job.Initialize();
        job.PostMake();
        return job;
    }

    public static ManagerTab MakeManagerTab(ManagerDef def, Manager manager)
    {
        ManagerTab tab = (ManagerTab)Activator.CreateInstance(def.managerTabClass, manager);
        tab.Def = def;
        tab.PostMake();
        return tab;
    }

    public static ManagerSettings? MakeManagerSettings(ManagerDef def)
    {
        if (def.managerSettingsClass == null)
        {
            return null;
        }

        ManagerSettings settings = (ManagerSettings)Activator.CreateInstance(def.managerSettingsClass);
        settings.Def = def;
        settings.PostMake();
        return settings;
    }
}

public static class ManagerDefMakerManagerExtensions
{
    public static T NewJob<T>(this Manager manager, ManagerDef def, params object[] args)
        where T : ManagerJob
    {
        if (def == null)
        {
            throw new ArgumentNullException(nameof(def));
        }

        if (ManagerDefMaker.MakeManagerJob(def, manager, args) is not T managerJob)
        {
            throw new ArgumentException($"ManagerDef provided ({def}) does not produce a {typeof(T).Name}");
        }

        return managerJob;
    }
}
