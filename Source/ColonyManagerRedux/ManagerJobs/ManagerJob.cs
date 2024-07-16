// ManagerJob.cs
// Copyright Karel Kroeze, 2018-2020

using System.Text;
using Verse.AI;

namespace ColonyManagerRedux;

internal interface IManagerJob
{
    bool TryDoJob();
}

public abstract class ManagerJob : IManagerJob, IExposable
{
    public static float SuspendStampWidth = Constants.MediumIconSize,
                        LastUpdateRectWidth = 50f,
                        ProgressRectWidth = 10f,
                        StatusRectWidth = SuspendStampWidth + LastUpdateRectWidth + ProgressRectWidth;

    public bool ShouldCheckReachable = true;

    public int LastActionTick;

    public Manager Manager;
    public bool UsePathBasedDistance;

    public int Priority;

    private bool _isSuspended;

    private UpdateInterval? _updateInterval;
    private int _updateIntervalScribe;

    public ManagerJob(Manager manager)
    {
        Manager = manager;
        Touch(); // set last updated to current time.
    }

    public abstract bool IsCompleted { get; }
    public virtual bool IsValid => Manager != null;
    public abstract string Label { get; }
    public virtual bool IsManaged { get; set; }


    public virtual bool ShouldDoNow => IsManaged && !IsSuspended && !IsCompleted && ShouldUpdate;

    private bool ShouldUpdate => LastActionTick + UpdateInterval.ticks < Find.TickManager.TicksGame;

    public virtual bool IsSuspended
    {
        get => _isSuspended;
        set => _isSuspended = value;
    }

    public abstract ManagerTab? Tab { get; }
    public abstract string[] Targets { get; }

    public virtual UpdateInterval UpdateInterval
    {
        get => _updateInterval ?? Settings.DefaultUpdateInterval;
        set => _updateInterval = value;
    }

    public abstract WorkTypeDef? WorkTypeDef { get; }

    public virtual void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            _updateIntervalScribe = UpdateInterval.ticks;
        }

        Scribe_References.Look(ref Manager, "manager");
        Scribe_Values.Look(ref _updateIntervalScribe, "updateInterval");
        Scribe_Values.Look(ref LastActionTick, "lastActionTick");
        Scribe_Values.Look(ref Priority, "priority");
        Scribe_Values.Look(ref ShouldCheckReachable, "shouldCheckReachable", true);
        Scribe_Values.Look(ref UsePathBasedDistance, "usePathBasedDistance");
        Scribe_Values.Look(ref _isSuspended, "isSuspended");

        if (Scribe.mode == LoadSaveMode.PostLoadInit || Manager.LoadSaveMode == Manager.Modes.ImportExport)
        {
            // must be true if it was saved.
            IsManaged = true;

            try
            {
                _updateInterval = Utilities.UpdateIntervalOptions.Find(ui => ui.ticks == _updateIntervalScribe) ??
                                  Settings.DefaultUpdateInterval;
            }
            catch
            {
                _updateInterval = Settings.DefaultUpdateInterval;
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

        Manager.For(Manager).JobStack.Delete(this, false);
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

    public abstract void DrawListEntry(Rect rect, bool overview = true, bool active = true);

    public abstract void DrawOverviewDetails(Rect rect);

    public virtual bool IsReachable(Thing target)
    {
        return !target.Position.Fogged(Manager.map)
            && (!ShouldCheckReachable ||
                 Manager.map.mapPawns.FreeColonistsSpawned.Any(
                     p => p.CanReach(target, PathEndMode.Touch, Danger.Some)));
    }

    public virtual void Tick()
    {
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
}
