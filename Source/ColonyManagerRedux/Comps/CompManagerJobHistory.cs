// CompManagerJobHistory.cs
// Copyright (c) 2024–2025 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Component that manages the history tracking for manager jobs in Colony Manager Redux.
/// </summary>
[CoroutineSettingsType]
public class CompManagerJobHistory : ManagerJobComp
{
    /// <summary>
    /// Gets the component properties specific to ManagerJobHistory.
    /// </summary>
    public new CompProperties_ManagerJobHistory Props =>
        (CompProperties_ManagerJobHistory)base.Props;

#pragma warning disable CS8618 // Set in Initialize
    private History history;
#pragma warning restore CS8618
    /// <summary>
    /// Gets the <see cref="History"/> instance that tracks historical data for this manager job.
    /// </summary>
    public History History => history;

    /// <inheritdoc/>
    protected internal override void Initialize() =>
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

    private int? _currentUpdateTick;
    private bool _reportedSkippedUpdateTick;

    /// <inheritdoc/>
    protected internal override void CompTick()
    {
        if (!ColonyManagerReduxMod.Settings.RecordHistoricalData || !History.IsUpdateTick)
        {
            return;
        }

        var ticksGame = Find.TickManager.TicksGame;

        if (!_reportedSkippedUpdateTick && _queuedToRecord > 0 && _currentUpdateTick != ticksGame)
        {
            ColonyManagerReduxMod.Instance.LogWarning(
                "It was time for a history update, but the previous update hasn't finished yet. "
                    + "This means that your history updates are taking longer than "
                    + History.PeriodTickInterval(Period.Day)
                    + " ticks, which either means you have a "
                    + "very large number of jobs, jobs that have very slow history updates or that "
                    + "there is a bug. To avoid potentially adding to an ever increasing queue of "
                    + " history update tasks, we're going to skip this update cycle."
            );
            _reportedSkippedUpdateTick = true;
            return;
        }

        _currentUpdateTick = ticksGame;

        var worker = Props.Worker;
        worker.HistoryUpdateTick(Parent, ticksGame);

        _ = MultiTickCoroutineManager.StartCoroutine(
            DoHistoryUpdateCoroutine(worker, ticksGame),
            debugHandle: "DoHistoryUpdateCoroutine"
        );
    }

    private static bool _isRecordingHistory;
    private static int _queuedToRecord;

    [CoroutineSettingsMethod]
    private Coroutine DoHistoryUpdateCoroutine(HistoryWorker worker, int tick)
    {
        if (_isRecordingHistory)
        {
            // we only want to run one history update coroutine at any one time, even if many
            // get scheduled to run at once
            _queuedToRecord++;
            // ColonyManagerReduxMod.Instance.LogDebug($"Queueing @ {_queuedToRecord}");
            yield return new ResumeWhenTrue(() => !_isRecordingHistory);
            _queuedToRecord--;
            // ColonyManagerReduxMod.Instance.LogDebug($"Done queueing @ {_queuedToRecord}");
        }
        else
        {
            // ColonyManagerReduxMod.Instance.LogDebug("No queueing");
        }
        _isRecordingHistory = true;
        using var _ = new DoOnDispose(() =>
        {
            _isRecordingHistory = false;
            if (_queuedToRecord == 0)
            {
                _reportedSkippedUpdateTick = false;
                _currentUpdateTick = null;
                // ColonyManagerReduxMod.Instance.LogDebug(
                //     $"Reset _reportedSkippedUpdateTick and _currentUpdateTick"
                // );
            }
        });

        var ticksBetweenOperations =
            ColonyManagerReduxMod.Settings.GetTicksBetweenOperationsForCoroutine(
                DoHistoryUpdateCoroutine
            );

        //ColonyManagerReduxMod.Instance.LogDebug($"Doing history for {Parent.Label}");

        var coroutineStartTick = Find.TickManager.TicksGame;

        if (Props.Worker.HistoryUpdateCoroutine(Parent, tick) is { } coroutine)
        {
            yield return coroutine.ResumeWhenOtherCoroutineIsCompleted();
            yield return new ResumeAfterTicks(ticksBetweenOperations);
        }

        var chapterCount = Props.chapters.Count;
        var chapterCounts = new int[chapterCount];

        Boxed<int> count = new();
        if (worker.UpdatesMax)
        {
            foreach (var (chapterDef, i) in Props.chapters.Select((c, i) => (c, i)))
            {
                yield return Props
                    .Worker.GetMaxForHistoryChapterCoroutine(Parent, tick, chapterDef, count)
                    .ResumeWhenOtherCoroutineIsCompleted();
                yield return new ResumeAfterTicks(ticksBetweenOperations);
                chapterCounts[i] = count.Value;
            }

            History.UpdateMax(chapterCounts);
        }

        var chapterTargets = new int[chapterCount];
        foreach (var (chapterDef, i) in Props.chapters.Select((c, i) => (c, i)))
        {
            var preChapterTick = Find.TickManager.TicksGame;
            yield return Props
                .Worker.GetCountForHistoryChapterCoroutine(Parent, tick, chapterDef, count)
                .ResumeWhenOtherCoroutineIsCompleted();
            yield return new ResumeAfterTicks(ticksBetweenOperations);
            // ColonyManagerReduxMod.Instance.LogDebug(
            //     $"{nameof(HistoryWorker.GetCountForHistoryChapterCoroutine)} for chapter "
            //         + $"{chapterDef.defName} took "
            //         + $"{Find.TickManager.TicksGame - preChapterTick} ticks to complete"
            // );
            chapterCounts[i] = count.Value;

            preChapterTick = Find.TickManager.TicksGame;
            yield return Props
                .Worker.GetTargetForHistoryChapterCoroutine(Parent, tick, chapterDef, count)
                .ResumeWhenOtherCoroutineIsCompleted();
            yield return new ResumeAfterTicks(ticksBetweenOperations);
            // ColonyManagerReduxMod.Instance.LogDebug(
            //     $"{nameof(HistoryWorker.GetTargetForHistoryChapterCoroutine)} for chapter "
            //         + $"{chapterDef.defName} took "
            //         + $"{Find.TickManager.TicksGame - preChapterTick} ticks to complete"
            // );
            chapterTargets[i] = count.Value;
        }

        History.Update(tick, chapterCounts, chapterTargets);

        var coroutineEndTick = Find.TickManager.TicksGame;
        var tickCount = coroutineEndTick - coroutineStartTick;
        // ColonyManagerReduxMod.Instance.LogDebug(
        //     $"{nameof(DoHistoryUpdateCoroutine)} took {tickCount} ticks to complete"
        // );
    }

    /// <inheritdoc/>
    protected internal override void PostExposeData()
    {
        base.PostExposeData();
        if (Parent.Manager.ScribeSameGameData)
        {
            Scribe_Deep.Look(ref history, "history");
        }
    }
}

/// <summary>
/// Abstract base class for implementing history tracking logic for manager jobs.
/// </summary>
public abstract class HistoryWorker
{
    /// <summary>
    /// Gets a value indicating whether this worker updates the maximum value for history chapters.
    /// </summary>
    public virtual bool UpdatesMax { get; }

    /// <summary>
    /// Gets the count for a specific history chapter at a given tick.
    /// </summary>
    /// <param name="managerJob">The manager job instance.</param>
    /// <param name="tick">The game tick for which to get the count.</param>
    /// <param name="chapterDef">The definition of the history chapter.</param>
    /// <returns>The count for the specified chapter at the given tick.</returns>
    [Obsolete(
        "Implement GetCountForHistoryChapterCoroutine; this is only here for backwards compatibility; "
            + "this method will be removed in a future version"
    )]
    public virtual int GetCountForHistoryChapter(
        ManagerJob managerJob,
        int tick,
        ManagerJobHistoryChapterDef chapterDef
    ) => throw new NotImplementedException();

    /// <summary>
    /// Gets the target value for a specific history chapter at a given tick.
    /// </summary>
    /// <param name="managerJob">The manager job instance.</param>
    /// <param name="tick">The game tick for which to get the target value.</param>
    /// <param name="chapterDef">The definition of the history chapter.</param>
    /// <returns>The target value for the specified chapter at the given tick.</returns>
    [Obsolete(
        "Implement GetTargetForHistoryChapterCoroutine; this is only here for backwards compatibility; "
            + "this method will be removed in a future version"
    )]
    public virtual int GetTargetForHistoryChapter(
        ManagerJob managerJob,
        int tick,
        ManagerJobHistoryChapterDef chapterDef
    ) => throw new NotImplementedException();

    /// <summary>
    /// Gets the maximum value for a specific history chapter at a given tick.
    /// </summary>
    /// <param name="managerJob">The manager job instance.</param>
    /// <param name="tick">The game tick for which to get the maximum value.</param>
    /// <param name="chapterDef">The definition of the history chapter.</param>
    /// <returns>The maximum value for the specified chapter at the given tick.</returns>
    [Obsolete(
        "Implement GetMaxForHistoryChapterCoroutine; this is only here for backwards compatibility; "
            + "this method will be removed in a future version"
    )]
    public virtual int GetMaxForHistoryChapter(
        ManagerJob managerJob,
        int tick,
        ManagerJobHistoryChapterDef chapterDef
    ) => throw new NotImplementedException();

    /// <summary>
    /// Gets the count for a specific history chapter at a given tick as a coroutine.
    /// </summary>
    /// <param name="managerJob">The manager job instance.</param>
    /// <param name="tick">The game tick for which to get the count.</param>
    /// <param name="chapterDef">The definition of the history chapter.</param>
    /// <param name="count">A boxed integer to store the result.</param>
    /// <returns>A coroutine that yields the count for the specified chapter at the given tick.</returns>
    public virtual Coroutine GetCountForHistoryChapterCoroutine(
        ManagerJob managerJob,
        int tick,
        ManagerJobHistoryChapterDef chapterDef,
        Boxed<int> count
    )
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
            ColonyManagerReduxMod.Instance.LogWarning(
                $"Neither "
                    + $"{nameof(GetCountForHistoryChapter)} nor "
                    + $"{nameof(GetCountForHistoryChapterCoroutine)} have been overridden, so we're "
                    + $"returning a count of 0 for {chapterDef.defName} in {GetType().FullName}."
            );
            count.Value = 0;
        }
#pragma warning restore CS0618
        yield break;
    }

    /// <summary>
    /// Gets the target value for a specific history chapter at a given tick as a coroutine.
    /// </summary>
    /// <param name="managerJob">The manager job instance.</param>
    /// <param name="tick">The game tick for which to get the target value.</param>
    /// <param name="chapterDef">The definition of the history chapter.</param>
    /// <param name="target">A boxed integer to store the result.</param>
    /// <returns>A coroutine that yields the target value for the specified chapter at the given tick.</returns>
    public virtual Coroutine GetTargetForHistoryChapterCoroutine(
        ManagerJob managerJob,
        int tick,
        ManagerJobHistoryChapterDef chapterDef,
        Boxed<int> target
    )
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
            ColonyManagerReduxMod.Instance.LogWarning(
                $"Neither "
                    + $"{nameof(GetTargetForHistoryChapter)} nor "
                    + $"{nameof(GetTargetForHistoryChapterCoroutine)} have been overridden, so we're "
                    + $"returning a target count of 0 for {chapterDef.defName} in {GetType().FullName}."
            );
            target.Value = 0;
        }
#pragma warning restore CS0618
        yield break;
    }

    /// <summary>
    /// Gets the maximum value for a specific history chapter at a given tick as a coroutine.
    /// </summary>
    /// <param name="managerJob">The manager job instance.</param>
    /// <param name="tick">The game tick for which to get the maximum value.</param>
    /// <param name="chapterDef">The definition of the history chapter.</param>
    /// <param name="max">A boxed integer to store the result.</param>
    /// <returns>A coroutine that yields the maximum value for the specified chapter at the given tick.</returns>
    public virtual Coroutine GetMaxForHistoryChapterCoroutine(
        ManagerJob managerJob,
        int tick,
        ManagerJobHistoryChapterDef chapterDef,
        Boxed<int> max
    )
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
                $"Neither {nameof(GetMaxForHistoryChapter)} "
                    + $"nor {nameof(GetMaxForHistoryChapterCoroutine)} have been overridden, so we're "
                    + $"returning a max count of 0 for {chapterDef.defName} in {GetType().FullName}."
            );
            max.Value = 0;
        }
#pragma warning restore CS0618
        yield break;
    }

    /// <summary>
    /// Performs a history update tick for the specified manager job at the given tick.
    /// </summary>
    /// <param name="managerJob">The manager job instance.</param>
    /// <param name="tick">The game tick for which to perform the update.</param>
    public virtual void HistoryUpdateTick(ManagerJob managerJob, int tick) { }

    /// <summary>
    /// Performs a history update for the specified manager job at the given tick as a coroutine.
    /// </summary>
    /// <param name="managerJob">The manager job instance.</param>
    /// <param name="tick">The game tick for which to perform the update.</param>
    /// <returns>A coroutine that performs the history update, or null if not implemented.</returns>
    public virtual Coroutine? HistoryUpdateCoroutine(ManagerJob managerJob, int tick) => null;
}

/// <summary>
/// Generic abstract base class for implementing history tracking logic for manager jobs of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of ManagerJob this worker operates on.</typeparam>
public abstract class HistoryWorker<T> : HistoryWorker
    where T : ManagerJob
{
    /// <inheritdoc/>
    public sealed override void HistoryUpdateTick(ManagerJob managerJob, int tick) =>
        HistoryUpdateTick((T)managerJob, tick);

    /// <inheritdoc/>
    public sealed override Coroutine? HistoryUpdateCoroutine(ManagerJob managerJob, int tick) =>
        HistoryUpdateCoroutine((T)managerJob, tick);

    /// <inheritdoc/>
    public sealed override Coroutine GetCountForHistoryChapterCoroutine(
        ManagerJob managerJob,
        int tick,
        ManagerJobHistoryChapterDef chapterDef,
        Boxed<int> count
    ) => GetCountForHistoryChapterCoroutine((T)managerJob, tick, chapterDef, count);

    /// <inheritdoc/>
    public sealed override Coroutine GetTargetForHistoryChapterCoroutine(
        ManagerJob managerJob,
        int tick,
        ManagerJobHistoryChapterDef chapterDef,
        Boxed<int> target
    ) => GetTargetForHistoryChapterCoroutine((T)managerJob, tick, chapterDef, target);

    /// <inheritdoc/>
    public sealed override Coroutine GetMaxForHistoryChapterCoroutine(
        ManagerJob managerJob,
        int tick,
        ManagerJobHistoryChapterDef chapterDef,
        Boxed<int> max
    ) => GetMaxForHistoryChapterCoroutine((T)managerJob, tick, chapterDef, max);

    /// <summary>
    /// Gets the count for a specific history chapter at a given tick as a coroutine for the specified manager job type.
    /// </summary>
    /// <param name="managerJob">The manager job instance of type <typeparamref name="T"/>.</param>
    /// <param name="tick">The game tick for which to get the count.</param>
    /// <param name="chapterDef">The definition of the history chapter.</param>
    /// <param name="count">A boxed integer to store the result.</param>
    /// <returns>A coroutine that yields the count for the specified chapter at the given tick.</returns>
    public virtual Coroutine GetCountForHistoryChapterCoroutine(
        T managerJob,
        int tick,
        ManagerJobHistoryChapterDef chapterDef,
        Boxed<int> count
    )
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
            ColonyManagerReduxMod.Instance.LogWarning(
                $"Neither "
                    + $"{nameof(GetCountForHistoryChapter)} nor "
                    + $"{nameof(GetCountForHistoryChapterCoroutine)} have been overridden, so we're "
                    + $"returning a count of 0 for {chapterDef.defName} in {GetType().FullName}."
            );
            count.Value = 0;
        }
#pragma warning restore CS0618
        yield break;
    }

    /// <summary>
    /// Gets the target value for a specific history chapter at a given tick as a coroutine for the specified manager job type.
    /// </summary>
    /// <param name="managerJob">The manager job instance of type <typeparamref name="T"/>.</param>
    /// <param name="tick">The game tick for which to get the target value.</param>
    /// <param name="chapterDef">The definition of the history chapter.</param>
    /// <param name="target">A boxed integer to store the result.</param>
    /// <returns>A coroutine that yields the target value for the specified chapter at the given tick.</returns>
    public virtual Coroutine GetTargetForHistoryChapterCoroutine(
        T managerJob,
        int tick,
        ManagerJobHistoryChapterDef chapterDef,
        Boxed<int> target
    )
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
            ColonyManagerReduxMod.Instance.LogWarning(
                $"Neither "
                    + $"{nameof(GetTargetForHistoryChapter)} nor "
                    + $"{nameof(GetTargetForHistoryChapterCoroutine)} have been overridden, so we're "
                    + $"returning a target count of 0 for {chapterDef.defName} in {GetType().FullName}."
            );
            target.Value = 0;
        }
#pragma warning restore CS0618
        yield break;
    }

    /// <summary>
    /// Gets the maximum value for a specific history chapter at a given tick as a coroutine for the specified manager job type.
    /// </summary>
    /// <param name="managerJob">The manager job instance of type <typeparamref name="T"/>.</param>
    /// <param name="tick">The game tick for which to get the maximum value.</param>
    /// <param name="chapterDef">The definition of the history chapter.</param>
    /// <param name="max">A boxed integer to store the result.</param>
    /// <returns>A coroutine that yields the maximum value for the specified chapter at the given tick.</returns>
    public virtual Coroutine GetMaxForHistoryChapterCoroutine(
        T managerJob,
        int tick,
        ManagerJobHistoryChapterDef chapterDef,
        Boxed<int> max
    )
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

    /// <summary>
    /// Performs a history update tick for the specified manager job of type <typeparamref name="T"/> at the given tick.
    /// </summary>
    /// <param name="managerJob">The manager job instance of type <typeparamref name="T"/>.</param>
    /// <param name="tick">The game tick for which to perform the update.</param>
    public virtual void HistoryUpdateTick(T managerJob, int tick) { }

    /// <summary>
    /// Performs a history update for the specified manager job of type <typeparamref name="T"/> at the given tick as a coroutine.
    /// </summary>
    /// <param name="managerJob">The manager job instance of type <typeparamref name="T"/>.</param>
    /// <param name="tick">The game tick for which to perform the update.</param>
    /// <returns>A coroutine that performs the history update, or null if not implemented.</returns>
    public virtual Coroutine? HistoryUpdateCoroutine(T managerJob, int tick) => null;
}
