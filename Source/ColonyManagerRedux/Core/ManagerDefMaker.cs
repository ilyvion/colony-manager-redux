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

        var job = (ManagerJob)Activator.CreateInstance(def.managerJobClass, [manager, .. args]);
        job._def = def;
        job.Initialize();
        job.PostMake();
        return job;
    }

    public static ManagerTab MakeManagerTab(ManagerDef def, Manager manager)
    {
        var tab = (ManagerTab)Activator.CreateInstance(def.managerTabClass, manager);
        tab.Def = def;
        tab.PostMakeInt();
        return tab;
    }

    public static ManagerSettings? MakeManagerSettings(ManagerDef def)
    {
        if (def.managerSettingsClass == null)
        {
            return null;
        }

        var settings = (ManagerSettings)Activator.CreateInstance(def.managerSettingsClass);
        settings.Def = def;
        settings.PostMake();
        return settings;
    }
}

/// <summary>
/// Extension methods for creating new manager jobs from a Manager instance and ManagerDef.
/// </summary>
public static class ManagerDefMakerManagerExtensions
{
    /// <summary>
    /// Creates a new manager job of type <typeparamref name="T"/> using the specified <see cref="ManagerDef"/> and arguments.
    /// </summary>
    /// <typeparam name="T">The type of <see cref="ManagerJob"/> to create.</typeparam>
    /// <param name="manager">The manager instance.</param>
    /// <param name="def">The manager definition to use for job creation.</param>
    /// <param name="args">Additional arguments for the job constructor.</param>
    /// <returns>A new instance of <typeparamref name="T"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="def"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown if the manager job created is not of type <typeparamref name="T"/>.</exception>
    public static T NewJob<T>(this Manager manager, ManagerDef def, params object[] args)
        where T : ManagerJob => def == null
            ? throw new ArgumentNullException(nameof(def))
            : ManagerDefMaker.MakeManagerJob(def, manager, args) is not T managerJob
            ? throw new ArgumentException($"ManagerDef provided ({def}) does not produce a {typeof(T).Name}")
            : managerJob;
}
