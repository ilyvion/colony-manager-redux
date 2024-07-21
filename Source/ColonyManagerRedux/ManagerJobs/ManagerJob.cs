﻿// ManagerJob.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Text;
using Verse.AI;

namespace ColonyManagerRedux;

[HotSwappable]
public abstract class ManagerJob : ILoadReferenceable, IExposable
{
    public ManagerDef def;

    public List<ManagerJobComp>? comps;

    public bool ShouldCheckReachable;

    public int LastActionTick = -1;

    public Manager Manager;
    public bool UsePathBasedDistance;

    public int Priority;

    private bool _isSuspended;

    private UpdateInterval? _updateInterval;
    private int _updateIntervalScribe;

    public int loadID = -1;
    private bool isManaged;

#pragma warning disable CS8618 // Set by ManagerDefMaker.MakeManagerJob
    protected ManagerJob(Manager manager)
#pragma warning restore CS8618
    {
        Settings settings = ColonyManagerReduxMod.Instance.Settings;
        ShouldCheckReachable = settings.DefaultShouldCheckReachable;
        UsePathBasedDistance = settings.DefaultUsePathBasedDistance;
        if (!settings.NewJobsAreImmediatelyOutdated)
        {
            // set last updated to current time
            Touch();
        }

        Manager = manager;
    }

    public abstract bool IsCompleted { get; }
    public virtual bool IsValid => Manager != null;
    public abstract string Label { get; }
    public virtual bool IsManaged
    {
        get => isManaged;
        set
        {
            isManaged = value;
            if (isManaged && loadID == -1)
            {
                loadID = Manager.GetNextManagerJobID();
            }
        }
    }


    public virtual bool ShouldDoNow => IsManaged && !IsSuspended && !IsCompleted && ShouldUpdate;

    private bool ShouldUpdate => LastActionTick < 0 || ((LastActionTick + UpdateInterval.ticks) < Find.TickManager.TicksGame);

    public virtual bool IsSuspended
    {
        get => _isSuspended;
        set => _isSuspended = value;
    }

    public ManagerTab Tab => Manager.tabs.Find(tab => tab.GetType() == def.managerTabClass);
    public abstract string[] Targets { get; }

    public virtual UpdateInterval UpdateInterval
    {
        get => _updateInterval ?? ColonyManagerReduxMod.Instance.Settings.DefaultUpdateInterval;
        set => _updateInterval = value;
    }

    public abstract WorkTypeDef? WorkTypeDef { get; }

    public virtual int MaxUpperThreshold { get; } = Constants.DefaultMaxUpperThreshold;

    internal void Initialize()
    {
        if (def.comps.Any())
        {
            comps = [];
            foreach (var compProperties in def.comps)
            {
                ManagerJobComp? managerJobComp = null;
                try
                {
                    managerJobComp = (ManagerJobComp)Activator.CreateInstance(compProperties.compClass);
                    managerJobComp.parent = this;
                    comps.Add(managerJobComp);
                    managerJobComp.Initialize(compProperties);
                }
                catch (Exception ex)
                {
                    Log.Error("Could not instantiate or initialize a ManagerJobComp: " + ex);
                    if (managerJobComp != null)
                    {
                        comps.Remove(managerJobComp);
                    }
                }
            }
        }
    }

    public virtual void PostMake()
    {
    }

    public virtual void PreExport()
    {
    }

    public virtual void PostExport()
    {
    }

    public virtual void PreImport()
    {
    }

    public virtual void PostImport()
    {
        IsManaged = true;
        if (!ColonyManagerReduxMod.Instance.Settings.NewJobsAreImmediatelyOutdated)
        {
            // set last updated to current time
            Touch();
        }
    }

    public virtual void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            _updateIntervalScribe = UpdateInterval.ticks;
        }

        Scribe_Defs.Look(ref def, "def");
        if (def == null)
        {
            return;
        }

        Scribe_Values.Look(ref _updateIntervalScribe, "updateInterval");
        Scribe_Values.Look(ref ShouldCheckReachable, "shouldCheckReachable", true);
        Scribe_Values.Look(ref UsePathBasedDistance, "usePathBasedDistance");

        if (Manager.Mode == Manager.ScribingMode.Normal)
        {
            Scribe_Values.Look(ref loadID, "loadID", 0);
            Scribe_References.Look(ref Manager, "manager");
            Scribe_Values.Look(ref LastActionTick, "lastActionTick");
            Scribe_Values.Look(ref Priority, "priority");
            Scribe_Values.Look(ref _isSuspended, "isSuspended");

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // must be true if it was saved.
                IsManaged = true;

                try
                {
                    _updateInterval = Utilities.UpdateIntervalOptions.Find(ui => ui.ticks == _updateIntervalScribe) ??
                        ColonyManagerReduxMod.Instance.Settings.DefaultUpdateInterval;
                }
                catch
                {
                    _updateInterval = ColonyManagerReduxMod.Instance.Settings.DefaultUpdateInterval;
                }
            }
        }
        else if (Manager.Mode == Manager.ScribingMode.Transfer)
        {
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                loadID = Manager.GetNextManagerJobID();
            }
        }

        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            Initialize();
        }
        if (comps != null)
        {
            foreach (ManagerJobComp comp in comps)
            {
                comp.PostExposeData();
            }
        }
    }

    public abstract bool TryDoJob();

    public abstract void CleanUp();

    public virtual void Delete(bool cleanup = true)
    {
        if (cleanup)
        {
            CleanUp();
        }

        Manager.JobStack.Delete(this, false);
    }

    public virtual float Distance(Thing target, IntVec3 source)
    {
        if (UsePathBasedDistance)
        {
            var path = target.Map.pathFinder.FindPath(source, target,
                TraverseParms.For(TraverseMode.PassDoors, Danger.Some),
                PathEndMode.Touch);
            var cost = path.Found ? path.TotalCost : int.MaxValue;
            path.ReleaseToPool();
            return cost * 2;
        }

        return Mathf.Sqrt(source.DistanceToSquared(target.Position)) * 2;
    }

    public virtual bool IsReachable(Thing target)
    {
        return !target.Position.Fogged(Manager.map)
            && (!ShouldCheckReachable ||
                Manager.map.mapPawns.FreeColonistsSpawned.Any(
                    p => p.CanReach(target, PathEndMode.Touch, Danger.Some)));
    }

    public virtual void Tick()
    {
        if (!comps.NullOrEmpty())
        {
            foreach (ManagerJobComp c in comps!)
            {
                c.CompTick();
            }
        }
    }

    public override string ToString()
    {
        var s = new StringBuilder();
        s.AppendLine("Priority: " + Priority);
        s.AppendLine("Active: " + IsSuspended);
        s.AppendLine("LastAction: " + LastActionTick);
        s.AppendLine("Interval: " + UpdateInterval);
        s.AppendLine("GameTick: " + Find.TickManager.TicksGame);
        return s.ToString();
    }

    public void Touch()
    {
        LastActionTick = Find.TickManager.TicksGame;
    }

    public string GetUniqueLoadID()
    {
        return $"ColonyManagerRedux_ManagerJob_{Manager.id}_{loadID}";
    }

    public T? CompOfType<T>() where T : ManagerJobComp
    {
        return comps?.FirstOrDefault(c => c is T) as T;
    }

    public IEnumerable<T> CompsOfType<T>() where T : ManagerJobComp
    {
        return comps?.Where(c => c is T).Cast<T>() ?? [];
    }
}
