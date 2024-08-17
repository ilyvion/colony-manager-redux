// CompManagerJobHistory.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public class CompManagerJobHistory : ManagerJobComp
{
    public new CompProperties_ManagerJobHistory Props => (CompProperties_ManagerJobHistory)base.Props;

#pragma warning disable CS8618 // Set in Initialize
    private History history;
#pragma warning restore CS8618
    public History History { get => history; }

    public override void Initialize()
    {
        // create History tracker
        history = new History(Props.chapters)
        {
            AllowTogglingLegend = Props.allowTogglingLegend,
            DrawInlineLegend = Props.drawInlineLegend,
            DrawOptions = Props.drawOptions,
            DrawTargetLine = Props.drawTargetLine,

            PeriodShown = Props.periodShown,
            YAxisSuffix = Props.yAxisSuffix,
        };
    }

    public override void CompTick()
    {
        if (!ColonyManagerReduxMod.Settings.RecordHistoricalData ||
            !History.IsUpdateTick)
        {
            return;
        }

        int ticksGame = Find.TickManager.TicksGame;
        DoHistoryUpdate(ticksGame);
    }

    public static bool IsRecordingHistory { get; private set; }
    private void DoHistoryUpdate(int tick)
    {
        HistoryWorker worker = Props.Worker;
        worker.HistoryUpdateTick(Parent, tick);

        MultiTickCoroutineManager.StartCoroutine(DoHistoryUpdateCoroutine(),
            debugHandle: "DoHistoryUpdateCoroutine");

        Coroutine DoHistoryUpdateCoroutine()
        {
            if (IsRecordingHistory)
            {
                // we only want to run one history update coroutine at any one time, even if many
                // get scheduled to run at once
                yield return new ResumeWhenTrue(() => IsRecordingHistory == false);
            }
            IsRecordingHistory = true;

            int coroutineStartTick = Find.TickManager.TicksGame;

            int chapterCount = Props.chapters.Count;
            int[] chapterCounts = new int[chapterCount];

            Boxed<int> count = new();
            if (worker.UpdatesMax)
            {
                foreach (var (chapterDef, i) in Props.chapters.Select((c, i) => (c, i)))
                {
                    yield return Props.Worker.GetMaxForHistoryChapterCoroutine(
                        Parent, tick, chapterDef, count)
                        .ResumeWhenOtherCoroutineIsCompleted();
                    chapterCounts[i] = count.Value;
                }

                History.UpdateMax(chapterCounts);
            }

            int[] chapterTargets = new int[chapterCount];
            foreach (var (chapterDef, i) in Props.chapters.Select((c, i) => (c, i)))
            {
                yield return Props.Worker.GetCountForHistoryChapterCoroutine(
                    Parent, tick, chapterDef, count)
                    .ResumeWhenOtherCoroutineIsCompleted();
                chapterCounts[i] = count.Value;
                yield return Props.Worker.GetTargetForHistoryChapterCoroutine(
                    Parent, tick, chapterDef, count)
                    .ResumeWhenOtherCoroutineIsCompleted();
                chapterTargets[i] = count.Value;
            }

            History.Update(tick, chapterCounts, chapterTargets);

            int coroutineEndTick = Find.TickManager.TicksGame;
            var tickCount = coroutineEndTick - coroutineStartTick;
            ColonyManagerReduxMod.Instance.LogDevMessage(
                $"DoHistoryUpdateCoroutine took {tickCount} ticks to complete");

            IsRecordingHistory = false;
        }
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        if (Parent.Manager.ScribeGameSpecificData)
        {
            Scribe_Deep.Look(ref history, "history");
        }
    }
}

public abstract class HistoryWorker
{
    public virtual bool UpdatesMax { get; }

    [Obsolete("Implement GetCountForHistoryChapterCoroutine; this is only here for backwards compatibility")]
    public virtual int GetCountForHistoryChapter(ManagerJob managerJob, int tick, ManagerJobHistoryChapterDef chapterDef)
    {
        throw new NotImplementedException();
    }
    [Obsolete("Implement GetTargetForHistoryChapterCoroutine; this is only here for backwards compatibility")]
    public virtual int GetTargetForHistoryChapter(ManagerJob managerJob, int tick, ManagerJobHistoryChapterDef chapterDef)
    {
        throw new NotImplementedException();
    }
    [Obsolete("Implement GetMaxForHistoryChapterCoroutine; this is only here for backwards compatibility")]
    public virtual int GetMaxForHistoryChapter(ManagerJob managerJob, int tick, ManagerJobHistoryChapterDef chapterDef)
    {
        throw new NotImplementedException();
    }

    public virtual Coroutine GetCountForHistoryChapterCoroutine(
        ManagerJob managerJob,
        int tick,
        ManagerJobHistoryChapterDef chapterDef,
        Boxed<int> count)
    {
        if (count == null)
        {
            throw new ArgumentNullException(nameof(count));
        }
        if (chapterDef == null)
        {
            throw new ArgumentNullException(nameof(chapterDef));
        }

#pragma warning disable CS0618
        try
        {
            count.Value = GetCountForHistoryChapter(managerJob, tick, chapterDef);
        }
        catch (NotImplementedException)
        {
            ColonyManagerReduxMod.Instance.LogWarning($"Neither " +
                $"{nameof(GetCountForHistoryChapter)} nor " +
                $"{nameof(GetCountForHistoryChapterCoroutine)} have been overridden, so we're " +
                $"returning a count of 0 for {chapterDef.defName} in {GetType().FullName}.");
            count.Value = 0;
        }
#pragma warning restore CS0618
        yield break;
    }

    public virtual Coroutine GetTargetForHistoryChapterCoroutine(
        ManagerJob managerJob,
        int tick,
        ManagerJobHistoryChapterDef chapterDef,
        Boxed<int> target)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }
        if (chapterDef == null)
        {
            throw new ArgumentNullException(nameof(chapterDef));
        }

#pragma warning disable CS0618
        try
        {
            target.Value = GetTargetForHistoryChapter(managerJob, tick, chapterDef);
        }
        catch (NotImplementedException)
        {
            ColonyManagerReduxMod.Instance.LogWarning($"Neither " +
                $"{nameof(GetTargetForHistoryChapter)} nor " +
                $"{nameof(GetTargetForHistoryChapterCoroutine)} have been overridden, so we're " +
                $"returning a target count of 0 for {chapterDef.defName} in {GetType().FullName}.");
            target.Value = 0;
        }
#pragma warning restore CS0618
        yield break;
    }

    public virtual Coroutine GetMaxForHistoryChapterCoroutine(
        ManagerJob managerJob,
        int tick,
        ManagerJobHistoryChapterDef chapterDef,
        Boxed<int> max)
    {
        if (max == null)
        {
            throw new ArgumentNullException(nameof(max));
        }
        if (chapterDef == null)
        {
            throw new ArgumentNullException(nameof(chapterDef));
        }

#pragma warning disable CS0618
        try
        {
            max.Value = GetMaxForHistoryChapter(managerJob, tick, chapterDef);
        }
        catch (NotImplementedException)
        {
            ColonyManagerReduxMod.Instance.LogWarning(
                $"Neither {nameof(GetMaxForHistoryChapter)} " +
                $"nor {nameof(GetMaxForHistoryChapterCoroutine)} have been overridden, so we're " +
                $"returning a max count of 0 for {chapterDef.defName} in {GetType().FullName}.");
            max.Value = 0;
        }
#pragma warning restore CS0618
        yield break;
    }

    public virtual void HistoryUpdateTick(ManagerJob managerJob, int tick)
    {
    }
}

public abstract class HistoryWorker<T> : HistoryWorker where T : ManagerJob
{
    [Obsolete("Implement GetCountForHistoryChapterCoroutine; this is only here for backwards compatibility")]
    public override sealed int GetCountForHistoryChapter(ManagerJob managerJob, int tick, ManagerJobHistoryChapterDef chapterDef)
    {
        return GetCountForHistoryChapter((T)managerJob, tick, chapterDef);
    }
    [Obsolete("Implement GetTargetForHistoryChapterCoroutine; this is only here for backwards compatibility")]
    public override sealed int GetTargetForHistoryChapter(ManagerJob managerJob, int tick, ManagerJobHistoryChapterDef chapterDef)
    {
        return GetTargetForHistoryChapter((T)managerJob, tick, chapterDef);
    }
    [Obsolete("Implement GetMaxForHistoryChapterCoroutine; this is only here for backwards compatibility")]
    public override sealed int GetMaxForHistoryChapter(ManagerJob managerJob, int tick, ManagerJobHistoryChapterDef chapterDef)
    {
        return GetMaxForHistoryChapter((T)managerJob, tick, chapterDef);
    }
    public override sealed void HistoryUpdateTick(ManagerJob managerJob, int tick)
    {
        HistoryUpdateTick((T)managerJob, tick);
    }

    [Obsolete("Implement GetCountForHistoryChapterCoroutine; this is only here for backwards compatibility")]
    public virtual int GetCountForHistoryChapter(T managerJob, int tick, ManagerJobHistoryChapterDef chapterDef)
    {
        throw new NotImplementedException();
    }
    [Obsolete("Implement GetTargetForHistoryChapterCoroutine; this is only here for backwards compatibility")]
    public virtual int GetTargetForHistoryChapter(T managerJob, int tick, ManagerJobHistoryChapterDef chapterDef)
    {
        throw new NotImplementedException();
    }
    [Obsolete("Implement GetMaxForHistoryChapterCoroutine; this is only here for backwards compatibility")]
    public virtual int GetMaxForHistoryChapter(T managerJob, int tick, ManagerJobHistoryChapterDef chapterDef)
    {
        throw new NotImplementedException();
    }

    public override sealed Coroutine GetCountForHistoryChapterCoroutine(ManagerJob managerJob, int tick, ManagerJobHistoryChapterDef chapterDef, Boxed<int> count)
    {
        return GetCountForHistoryChapterCoroutine((T)managerJob, tick, chapterDef, count);
    }

    public override sealed Coroutine GetTargetForHistoryChapterCoroutine(ManagerJob managerJob, int tick, ManagerJobHistoryChapterDef chapterDef, Boxed<int> target)
    {
        return GetTargetForHistoryChapterCoroutine((T)managerJob, tick, chapterDef, target);
    }

    public override sealed Coroutine GetMaxForHistoryChapterCoroutine(ManagerJob managerJob, int tick, ManagerJobHistoryChapterDef chapterDef, Boxed<int> max)
    {
        return GetMaxForHistoryChapterCoroutine((T)managerJob, tick, chapterDef, max);
    }

    public virtual Coroutine GetCountForHistoryChapterCoroutine(
        T managerJob,
        int tick,
        ManagerJobHistoryChapterDef chapterDef,
        Boxed<int> count)
    {
        if (count == null)
        {
            throw new ArgumentNullException(nameof(count));
        }
        if (chapterDef == null)
        {
            throw new ArgumentNullException(nameof(chapterDef));
        }

#pragma warning disable CS0618
        try
        {
            count.Value = GetCountForHistoryChapter(managerJob, tick, chapterDef);
        }
        catch (NotImplementedException)
        {
            ColonyManagerReduxMod.Instance.LogWarning($"Neither " +
                $"{nameof(GetCountForHistoryChapter)} nor " +
                $"{nameof(GetCountForHistoryChapterCoroutine)} have been overridden, so we're " +
                $"returning a count of 0 for {chapterDef.defName} in {GetType().FullName}.");
            count.Value = 0;
        }
#pragma warning restore CS0618
        yield break;
    }

    public virtual Coroutine GetTargetForHistoryChapterCoroutine(
        T managerJob,
        int tick,
        ManagerJobHistoryChapterDef chapterDef,
        Boxed<int> target)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }
        if (chapterDef == null)
        {
            throw new ArgumentNullException(nameof(chapterDef));
        }

#pragma warning disable CS0618
        try
        {
            target.Value = GetTargetForHistoryChapter(managerJob, tick, chapterDef);
        }
        catch (NotImplementedException)
        {
            ColonyManagerReduxMod.Instance.LogWarning($"Neither " +
                $"{nameof(GetTargetForHistoryChapter)} nor " +
                $"{nameof(GetTargetForHistoryChapterCoroutine)} have been overridden, so we're " +
                $"returning a target count of 0 for {chapterDef.defName} in {GetType().FullName}.");
            target.Value = 0;
        }
#pragma warning restore CS0618
        yield break;
    }

    public virtual Coroutine GetMaxForHistoryChapterCoroutine(
        T managerJob,
        int tick,
        ManagerJobHistoryChapterDef chapterDef,
        Boxed<int> max)
    {
        if (max == null)
        {
            throw new ArgumentNullException(nameof(max));
        }
        if (chapterDef == null)
        {
            throw new ArgumentNullException(nameof(chapterDef));
        }

#pragma warning disable CS0618
        try
        {
            max.Value = GetMaxForHistoryChapter(managerJob, tick, chapterDef);
        }
        catch (NotImplementedException)
        {
            max.Value = 0;
        }
#pragma warning restore CS0618
        yield break;
    }

    public virtual void HistoryUpdateTick(T managerJob, int tick)
    {
    }
}
