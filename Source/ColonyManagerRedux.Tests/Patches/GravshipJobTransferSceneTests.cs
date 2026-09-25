// GravshipJobTransferSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

#if !v1_5
using System.Xml.Linq;
using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// Manager job that records the transfer hooks it receives and can be told to fail in them.
/// </summary>
internal sealed class TestTransferJob(Manager manager) : TestManagerJob(manager)
{
    public List<string> Calls { get; } = [];
    public bool NonTransferable { get; set; }
    public bool ThrowOnPreExport { get; set; }
    public bool ThrowOnExposeData { get; set; }
    public bool ThrowOnPreImport { get; set; }
    public bool InTrackerDuringPreImport { get; private set; }
    public bool InTrackerDuringPostImport { get; private set; }

    public override bool IsTransferable => !NonTransferable;

    public override void PreExport()
    {
        Calls.Add(nameof(PreExport));
        if (ThrowOnPreExport)
        {
            throw new InvalidOperationException("PreExport failed on purpose");
        }
    }

    public override void PostExport() => Calls.Add(nameof(PostExport));

    public override void ExposeData()
    {
        if (ThrowOnExposeData && Scribe.mode == LoadSaveMode.Saving)
        {
            throw new InvalidOperationException("ExposeData failed on purpose");
        }
        base.ExposeData();
    }

    public override void PreImport()
    {
        Calls.Add(nameof(PreImport));
        InTrackerDuringPreImport = Manager.JobTracker.HasJob(this);
        if (ThrowOnPreImport)
        {
            throw new InvalidOperationException("PreImport failed on purpose");
        }
    }

    public override void PostImport()
    {
        Calls.Add(nameof(PostImport));
        InTrackerDuringPostImport = Manager.JobTracker.HasJob(this);
    }
}

/// <summary>
/// The job export at gravship takeoff, the load and apply steps at landing, and how the manager
/// database component carries the bytes through a save.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class GravshipJobTransferSceneTests
{
    private const string PreExportError = "during PreExport";
    private const string SerializeError = "Failed to serialize job list";
    private const string LoadError = "Failed to load job list";
    private const string ImportError = "while being imported";

    private static CompManagerDatabase? MakeDatabaseComp(out Thing? thing)
    {
        var def = DefDatabase<ThingDef>.GetNamedSilentFail("CM_ManagerDatabase");
        if (def == null)
        {
            thing = null;
            return null;
        }
        thing = ThingMaker.MakeThing(def);
        return thing.TryGetComp<CompManagerDatabase>();
    }

    private static CompManagerDatabase RequireComp()
    {
        var comp = MakeDatabaseComp(out _);
        Expect.IsNotNull(comp, "Odyssey's manager database def must be present.");
        return comp!;
    }

    private static TestTransferJob AddJob(ManagerScene scene) => scene.Register(NewJob(scene));

    private static TestTransferJob NewJob(ManagerScene scene) => new(scene.Manager);

    private static List<ManagerJob> Load(byte[] bytes, Manager manager)
    {
        var comp = RequireComp();
        comp.JobTransferData = bytes;
        return Verse_WorldComponent_GravshipController_LandingEnded.LoadJobs(comp, manager);
    }

    private static List<ManagerJob> ExportAndLoad(ManagerScene scene) =>
        Load(
            Verse_WorldComponent_GravshipController_InitiateTakeoff.SerializeJobs(scene.Manager),
            scene.Manager
        );

    private static void Apply(
        GravshipLandingAction action,
        ManagerScene scene,
        List<ManagerJob> local,
        List<ManagerJob> gravship,
        Action? ask = null
    ) =>
        Verse_WorldComponent_GravshipController_LandingEnded.ApplyLandingAction(
            action,
            scene.Manager,
            local,
            gravship,
            ask ?? (() => { })
        );

    // GRAV-1

    [Test]
    public static void OnlyTransferableJobsAreExported()
    {
        using var scene = new ManagerScene();
        _ = AddJob(scene);
        var skipped = AddJob(scene);
        skipped.NonTransferable = true;

        var loaded = ExportAndLoad(scene);

        Expect.AreEqual(1, loaded.Count);
    }

    [Test]
    public static void PowerJobIsExportedDuringTakeoff()
    {
        using var scene = new ManagerScene();
        _ = scene.AddJob<ManagerJob_Power>("CM_PowerManager");

        var loaded = ExportAndLoad(scene);

        Expect.AreEqual(1, loaded.Count);
        Expect.IsTrue(loaded[0] is ManagerJob_Power);
    }

    [Test]
    public static void PreAndPostExportRunOncePerExportedJob()
    {
        using var scene = new ManagerScene();
        var exported = AddJob(scene);
        var skipped = AddJob(scene);
        skipped.NonTransferable = true;

        _ = Verse_WorldComponent_GravshipController_InitiateTakeoff.SerializeJobs(scene.Manager);

        Expect.AreEqual(2, exported.Calls.Count);
        Expect.AreEqual(nameof(ManagerJob.PreExport), exported.Calls[0]);
        Expect.AreEqual(nameof(ManagerJob.PostExport), exported.Calls[1]);
        Expect.AreEqual(0, skipped.Calls.Count);
    }

    [Test]
    [ErrorsAllowed(PreExportError)]
    public static void ThrowingPreExportIsLoggedAndTheJobIsStillWritten()
    {
        using var scene = new ManagerScene();
        var job = AddJob(scene);
        job.ThrowOnPreExport = true;

        var loaded = ExportAndLoad(scene);

        Expect.AreEqual(1, loaded.Count);
        Expect.IsTrue(job.Calls.Contains(nameof(ManagerJob.PostExport)));
    }

    [Test]
    [ErrorsAllowed(SerializeError, "ExposeData failed on purpose")]
    public static void ScribeStateIsRestoredWhenAJobThrowsWhileScribing()
    {
        using var scene = new ManagerScene();
        AddJob(scene).ThrowOnExposeData = true;

        _ = Verse_WorldComponent_GravshipController_InitiateTakeoff.SerializeJobs(scene.Manager);

        Expect.IsTrue(scene.Manager.ScribeSameMapData);
        Expect.AreEqual(LoadSaveMode.Inactive, Scribe.mode);
    }

    [Test]
    public static void ScribeStateIsRestoredAfterAnExport()
    {
        using var scene = new ManagerScene();
        _ = AddJob(scene);

        _ = Verse_WorldComponent_GravshipController_InitiateTakeoff.SerializeJobs(scene.Manager);

        Expect.IsTrue(scene.Manager.ScribeSameMapData);
        Expect.AreEqual(LoadSaveMode.Inactive, Scribe.mode);
    }

    [Test]
    public static void ExportedBytesAreExactlyTheXmlDocument()
    {
        using var scene = new ManagerScene();
        _ = AddJob(scene);

        var bytes = Verse_WorldComponent_GravshipController_InitiateTakeoff.SerializeJobs(
            scene.Manager
        );

        Expect.IsTrue(bytes.Length > 0);
        Expect.IsTrue(bytes[^1] != 0, "Exported data ends in a NUL byte.");
        Expect.IsFalse(Array.IndexOf(bytes, (byte)0) >= 0, "Exported data contains NUL bytes.");
        using var stream = new MemoryStream(bytes);
        var document = XDocument.Load(stream);
        Expect.IsNotNull(document.Root);
    }

    // GRAV-2

    [Test]
    public static void ExportedJobsLoadBackWithTheirManager()
    {
        using var scene = new ManagerScene();
        _ = AddJob(scene);
        _ = AddJob(scene);

        var loaded = ExportAndLoad(scene);

        Expect.AreEqual(2, loaded.Count);
        Expect.IsTrue(loaded.All(j => j is TestTransferJob));
        Expect.IsTrue(loaded.All(j => ReferenceEquals(j.Manager, scene.Manager)));
    }

    [Test]
    public static void ExportingNoJobsLoadsBackAnEmptyList()
    {
        using var scene = new ManagerScene();

        var loaded = ExportAndLoad(scene);

        Expect.IsNotNull(loaded);
        Expect.AreEqual(0, loaded.Count);
    }

    [Test]
    public static void LoadingClearsTheTransferDataAndRestoresScribeState()
    {
        using var scene = new ManagerScene();
        _ = AddJob(scene);
        var comp = RequireComp();
        comp.JobTransferData =
            Verse_WorldComponent_GravshipController_InitiateTakeoff.SerializeJobs(scene.Manager);

        _ = Verse_WorldComponent_GravshipController_LandingEnded.LoadJobs(comp, scene.Manager);

        Expect.IsNull(comp.JobTransferData);
        Expect.IsTrue(scene.Manager.ScribeSameMapData);
        Expect.AreEqual(LoadSaveMode.Inactive, Scribe.mode);
    }

    [Test]
    [ErrorsAllowed(LoadError, "XmlException", "root level", "Exception")]
    public static void LoadingGarbageImportsNothingAndCleansUp()
    {
        using var scene = new ManagerScene();
        var comp = RequireComp();
        comp.JobTransferData = System.Text.Encoding.UTF8.GetBytes("this is not xml");

        var loaded = Verse_WorldComponent_GravshipController_LandingEnded.LoadJobs(
            comp,
            scene.Manager
        );

        Expect.AreEqual(0, loaded.Count);
        Expect.IsNull(comp.JobTransferData);
        Expect.IsTrue(scene.Manager.ScribeSameMapData);
        Expect.AreEqual(LoadSaveMode.Inactive, Scribe.mode);
    }

    [Test]
    public static void ImportAddsJobsBetweenPreAndPostImport()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);

        Apply(GravshipLandingAction.Import, scene, [], [job]);

        Expect.AreEqual(2, job.Calls.Count);
        Expect.AreEqual(nameof(ManagerJob.PreImport), job.Calls[0]);
        Expect.AreEqual(nameof(ManagerJob.PostImport), job.Calls[1]);
        Expect.IsFalse(job.InTrackerDuringPreImport);
        Expect.IsTrue(job.InTrackerDuringPostImport);
    }

    [Test]
    [ErrorsAllowed(ImportError, "PreImport failed on purpose")]
    public static void ImportContinuesWithTheNextJobWhenOneThrows()
    {
        using var scene = new ManagerScene();
        var broken = NewJob(scene);
        broken.ThrowOnPreImport = true;
        var fine = NewJob(scene);

        Apply(GravshipLandingAction.Import, scene, [], [broken, fine]);

        Expect.IsFalse(scene.Manager.JobTracker.HasJob(broken));
        Expect.IsTrue(scene.Manager.JobTracker.HasJob(fine));
    }

    [Test]
    public static void KeepLocalOnlyLeavesTheTrackerUntouched()
    {
        using var scene = new ManagerScene();
        var local = AddJob(scene);
        var gravship = NewJob(scene);

        Apply(GravshipLandingAction.KeepLocalOnly, scene, [local], [gravship]);

        Expect.AreEqual(1, scene.Manager.JobTracker.Jobs.Count());
        Expect.IsTrue(scene.Manager.JobTracker.HasJob(local));
        Expect.AreEqual(0, local.CleanUpCount);
        Expect.AreEqual(0, gravship.Calls.Count);
    }

    [Test]
    public static void KeepGravshipOnlyDeletesLocalJobsWithCleanUpAndImportsTheRest()
    {
        using var scene = new ManagerScene();
        var local = AddJob(scene);
        var gravship = NewJob(scene);

        Apply(GravshipLandingAction.KeepGravshipOnly, scene, [local], [gravship]);

        Expect.IsFalse(scene.Manager.JobTracker.HasJob(local));
        Expect.AreEqual(1, local.CleanUpCount);
        Expect.IsTrue(scene.Manager.JobTracker.HasJob(gravship));
    }

    [Test]
    public static void AskLeavesTheTrackerUntouchedAndAsksOnce()
    {
        using var scene = new ManagerScene();
        var local = AddJob(scene);
        var gravship = NewJob(scene);
        var asked = 0;

        Apply(GravshipLandingAction.Ask, scene, [local], [gravship], () => asked++);

        Expect.AreEqual(1, asked);
        Expect.AreEqual(1, scene.Manager.JobTracker.Jobs.Count());
        Expect.AreEqual(0, gravship.Calls.Count);
    }

    [Test]
    public static void ImportingAPowerJobOverALocalOneLeavesOnlyTheImportedJob()
    {
        using var scene = new ManagerScene();
        var local = scene.AddJob<ManagerJob_Power>("CM_PowerManager");
        var imported = (ManagerJob_Power)
            ManagerDefMaker.MakeManagerJob(
                DefDatabase<ManagerDef>.GetNamed("CM_PowerManager"),
                scene.Manager
            )!;

        Apply(GravshipLandingAction.Import, scene, [local], [imported]);

        Expect.AreEqual(1, scene.Manager.JobTracker.JobsOfType<ManagerJob_Power>().Count());
        Expect.IsTrue(scene.Manager.JobTracker.HasJob(imported));
        Expect.IsFalse(scene.Manager.JobTracker.HasJob(local));
    }

    // GRAV-6

    private sealed class CompHarness(CompManagerDatabase comp) : IExposable
    {
        public void ExposeData() => comp.PostExposeData();
    }

    private static byte[]? RoundTripComp(byte[]? data)
    {
        var source = RequireComp();
        source.JobTransferData = data;
        var target = RequireComp();
        ScribeRoundTrip.Run(new CompHarness(source), new CompHarness(target));
        return target.JobTransferData;
    }

    [Test]
    public static void NullTransferDataStaysNull() => Expect.IsNull(RoundTripComp(null));

    [Test]
    public static void SmallTransferDataRoundTrips()
    {
        byte[] data = [0, 1, 2, 3, 255, 128];

        var loaded = RoundTripComp(data);

        Expect.IsNotNull(loaded);
        Expect.IsTrue(data.AsSpan().SequenceEqual(loaded));
    }

    [Test]
    public static void LargeTransferDataRoundTripsWithoutAliasing()
    {
        var data = new byte[100 * 1024];
        for (var i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i * 31);
        }
        var expected = (byte[])data.Clone();

        var loaded = RoundTripComp(data);
        data[0]++;

        Expect.IsNotNull(loaded);
        Expect.IsTrue(expected.AsSpan().SequenceEqual(loaded));
    }
}
#endif // !v1_5
