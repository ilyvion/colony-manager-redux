// ScribeRoundTrip.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Tests;

/// <summary>
/// Drives a real Scribe save/load cycle through in-memory streams (via ilyvion.Laboratory's
/// CustomStream Scribe helpers), so no on-disk save file is needed.
/// </summary>
internal static class ScribeRoundTrip
{
    /// <summary>
    /// A <see cref="Stream"/> wrapper whose dispose is a no-op, so the wrapped stream survives
    /// Scribe's own save/load lifecycle and can still be read from afterwards.
    /// </summary>
    private sealed class NonClosingStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => inner.CanSeek;
        public override bool CanWrite => inner.CanWrite;
        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override void Flush() => inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void SetLength(long value) => inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) =>
            inner.Write(buffer, offset, count);
    }

    /// <summary>
    /// Saves <paramref name="saveTarget"/> and loads the result into <paramref name="loadTarget"/>.
    /// </summary>
    public static void Run(IExposable saveTarget, IExposable loadTarget)
    {
        var xml = Save(saveTarget);
        Load(xml, loadTarget);
    }

    /// <summary>
    /// Saves <paramref name="saveTarget"/> and returns the raw XML.
    /// </summary>
    public static string Save(IExposable saveTarget)
    {
        using var memory = new MemoryStream();
        using (var nonClosing = new NonClosingStream(memory))
        {
            CustomStreamScribeSaver.InitSaving(nonClosing, "root");
            try
            {
                saveTarget.ExposeData();
                Scribe.saver.FinalizeSaving();
            }
            finally
            {
                if (Scribe.mode != LoadSaveMode.Inactive)
                {
                    Scribe.ForceStop();
                }
            }
        }
        return System.Text.Encoding.UTF8.GetString(memory.ToArray());
    }

    /// <summary>
    /// Loads <paramref name="xml"/> (a whole document, as produced by <see cref="Save"/> or
    /// written by hand) into <paramref name="loadTarget"/>.
    /// </summary>
    public static void Load(string xml, IExposable loadTarget)
    {
        using var memory = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
        using (var reader = new StreamReader(memory))
        {
            CustomStreamReaderScribeLoader.InitLoading(reader);
        }
        try
        {
            Scribe.loader.curParent = loadTarget;
            loadTarget.ExposeData();
            Scribe.loader.crossRefs.RegisterForCrossRefResolve(loadTarget);
            Scribe.loader.FinalizeLoading();
        }
        finally
        {
            if (Scribe.mode != LoadSaveMode.Inactive)
            {
                Scribe.ForceStop();
            }
        }
    }

    /// <summary>
    /// Wraps an <see cref="IExposable"/> that holds manager jobs, making the manager resolvable
    /// as a cross-reference the way it is inside a real save.
    /// </summary>
    public sealed class ManagerHarness(Manager manager, IExposable inner) : IExposable
    {
        public void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                Scribe.saver.loadIDsErrorsChecker.RegisterDeepSaved(manager, "manager");
            }
            else if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe.loader.crossRefs.loadedObjectDirectory.RegisterLoaded(manager);
            }
            inner.ExposeData();
        }
    }

    /// <summary>
    /// Scribes a single deep-saved object, so its <c>PostLoadInit</c> runs like in a real save.
    /// </summary>
    public sealed class DeepHarness<T>(params object[] ctorArgs) : IExposable
        where T : class, IExposable
    {
        public T? Value;

        public void ExposeData() => Scribe_Deep.Look(ref Value, "value", ctorArgs);
    }

    /// <summary>
    /// Scribes a list of manager jobs the way <c>JobTracker</c> does.
    /// </summary>
    public sealed class JobListHarness(Manager manager, List<ManagerJob>? jobs = null) : IExposable
    {
        public List<ManagerJob> Jobs = jobs ?? [];

        public void ExposeData() =>
            Scribe_Collections.Look(ref Jobs, "jobs", LookMode.Deep, manager);
    }

    /// <summary>
    /// Round-trips a deep-saved <paramref name="value"/> (constructed on load with
    /// <paramref name="ctorArgs"/>) inside a manager-aware harness.
    /// </summary>
    public static T? RoundTripDeep<T>(Manager manager, T value, params object[] ctorArgs)
        where T : class, IExposable
    {
        var loaded = new DeepHarness<T>(ctorArgs);
        Run(
            new ManagerHarness(manager, new DeepHarness<T>(ctorArgs) { Value = value }),
            new ManagerHarness(manager, loaded)
        );
        return loaded.Value;
    }

    /// <summary>
    /// Round-trips <paramref name="jobs"/> through Scribe using the manager's current scribing
    /// mode flags, returning the loaded jobs.
    /// </summary>
    public static List<ManagerJob> RoundTripJobs(Manager manager, List<ManagerJob> jobs)
    {
        var loaded = new JobListHarness(manager);
        Run(
            new ManagerHarness(manager, new JobListHarness(manager, jobs)),
            new ManagerHarness(manager, loaded)
        );
        return loaded.Jobs;
    }
}
