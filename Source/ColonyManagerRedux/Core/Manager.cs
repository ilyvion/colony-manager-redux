// Manager.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ColonyManagerRedux;

public class Manager : MapComponent, ILoadReferenceable
{
    public enum Modes
    {
        ImportExport,
        Normal
    }

    public static bool helpShown;

    public static Modes LoadSaveMode = Modes.Normal;

    public List<ManagerTab> tabs;

    private List<ManagerTab>? _managerTabsLeft;
    private List<ManagerTab>? _managerTabsMiddle;
    private List<ManagerTab>? _managerTabsRight;
    private JobStack _stack;
    private int id = -1;

    public Manager(Map map) : base(map)
    {
        _stack = new JobStack(this);

        tabs = DefDatabase<ManagerTabDef>.AllDefs
            .OrderBy(m => m.order)
            .Select(m => ManagerTabMaker.MakeManagerTab(m, this))
            .ToList();

        // if not created in SavingLoading, give yourself the ID of the map you were constructed on.
        if (Scribe.mode == Verse.LoadSaveMode.Inactive)
        {
            id = map.uniqueID;
        }
    }

    public JobStack JobStack => _stack ??= new JobStack(this);

    public List<ManagerTab> ManagerTabsLeft
    {
        get
        {
            _managerTabsLeft ??= tabs.Where(tab => tab.def.iconArea == IconArea.Left).ToList();
            return _managerTabsLeft;
        }
    }

    public List<ManagerTab> ManagerTabsMiddle
    {
        get
        {
            _managerTabsMiddle ??=
                tabs.Where(tab => tab.def.iconArea == IconArea.Middle).ToList();
            return _managerTabsMiddle;
        }
    }

    public List<ManagerTab> ManagerTabsRight
    {
        get
        {
            _managerTabsRight ??=
                tabs.Where(tab => tab.def.iconArea == IconArea.Right).ToList();
            return _managerTabsRight;
        }
    }

    public string GetUniqueLoadID()
    {
        return $"ColonyManagerRedux_{id}";
    }

    public static Manager For(Map map)
    {
        var instance = map.GetComponent<Manager>();
        if (instance != null)
        {
            return instance;
        }

        instance = new Manager(map);
        map.components.Add(instance);
        return instance;
    }

    public static implicit operator Map(Manager manager)
    {
        return manager.map;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref id, "id", -1, true);
        Scribe_Values.Look(ref helpShown, "helpShown");
        Scribe_Deep.Look(ref _stack, "jobStack", this);

        foreach (var tab in tabs)
        {
            if (tab is IExposable exposableTab)
            {
                Scribe_Deep.Look(ref exposableTab, tab.def.defName, this);
            }
        }

        _stack ??= new JobStack(this);
    }

    public override void MapComponentTick()
    {
        base.MapComponentTick();

        // tick jobs
        foreach (var job in JobStack.FullStack())
        {
            if (!job.Suspended)
            {
                try
                {
                    job.Tick();
                }
                catch (Exception err)
                {
                    Log.Error($"Suspending manager job because it error-ed on tick: \n{err}");
                }
            }
        }

        // tick tabs
        foreach (var tab in tabs)
        {
            tab.Tick();
        }
    }

    public bool TryDoWork()
    {
        return JobStack.TryDoNextJob();
    }

    internal void NewJobStack(JobStack jobstack)
    {
        // clean up old jobs
        foreach (var job in _stack.FullStack())
        {
            job.CleanUp();
        }

        // replace stack
        _stack = jobstack;

        // touch new jobs in inappropriate places (reset timing so they are properly performed)
        foreach (var job in _stack.FullStack())
        {
            job.manager = this;
            job.Touch();
        }
    }
}
