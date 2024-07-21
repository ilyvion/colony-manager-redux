// ManagerDefMaker.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public static class ManagerDefMaker
{
    public static ManagerJob? MakeManagerJob(ManagerDef def, Manager manager, params object[] args)
    {
        if (def.managerJobClass == null)
        {
            return null;
        }

        ManagerJob job = (ManagerJob)Activator.CreateInstance(def.managerJobClass, [manager, .. args]);
        job.def = def;
        job.Initialize();
        job.PostMake();
        return job;
    }

    public static ManagerTab MakeManagerTab(ManagerDef def, Manager manager)
    {
        ManagerTab tab = (ManagerTab)Activator.CreateInstance(def.managerTabClass, manager);
        tab.def = def;
        tab.PostMake();
        return tab;
    }

    public static ManagerJobSettings? MakeManagerJobSettings(ManagerDef def)
    {
        if (def.managerJobSettingsClass == null)
        {
            return null;
        }

        ManagerJobSettings settings = (ManagerJobSettings)Activator.CreateInstance(def.managerJobSettingsClass);
        settings.def = def;
        settings.PostMake();
        return settings;
    }
}
