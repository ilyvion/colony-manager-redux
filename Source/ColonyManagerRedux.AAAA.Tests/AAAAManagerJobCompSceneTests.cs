// AAAAManagerJobCompSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using System.Text.RegularExpressions;
using ColonyManagerRedux.AAAA.Core;
using ColonyManagerRedux.AAAA.Patches;
using ColonyManagerRedux.Managers;
using DevTools.Testing;
using seekiworks_AllowedAreaAutomaticAdapter;

namespace ColonyManagerRedux.AAAA.Tests;

/// <summary>
/// The evacuation area swap and the AAAA watcher postfixes against real manager jobs and areas.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class AAAAManagerJobCompSceneTests
{
    private sealed class Scene : IDisposable
    {
        private readonly List<Area> _areas = [];

        public Map Map { get; } = Find.CurrentMap;
        public Manager Manager { get; }

        public Scene()
        {
            Manager = Manager.For(Map);
            Expect.IsTrue(
                Manager.JobTracker.HasNoJobs,
                "The previous test left manager jobs behind on the shared map."
            );
        }

        public T AddJob<T>(string managerDefName)
            where T : ManagerJob
        {
            var def = DefDatabase<ManagerDef>.GetNamed(managerDefName);
            var job = (T)ManagerDefMaker.MakeManagerJob(def, Manager)!;
            job.IsManaged = true;
            Manager.JobTracker.Add(job);
            return job;
        }

        public Area_Allowed AddArea(string label)
        {
            _ = Map.areaManager.TryMakeNewAllowed(out var area);
            area.SetLabel(label);
            _areas.Add(area);
            return area;
        }

        // AAAA's suffix is a regex ending in '$'; the label it matches is the unescaped rest.
        public Area_Allowed AddSafeArea(string label) =>
            AddArea(label + Regex.Unescape(Utility.GetSuffix.TrimEnd('$')));

        public void Dispose()
        {
            foreach (var job in Manager.JobTracker.Jobs.ToList())
            {
                Manager.JobTracker.Delete(job);
            }
            foreach (var area in _areas.Where(Map.areaManager.AllAreas.Contains))
            {
                area.Delete();
            }
        }
    }

    private static AAAAManagerJobCompField Field(
        ManagerJob job,
        string jobAreaFieldName,
        bool enabled = true,
        string? hideIfFieldWithNameIsFalse = null
    )
    {
        var field = new AAAAManagerJobCompField(
            job,
            new AAAAManagerJobCompFieldProperties
            {
                sectionColumn = "CMRTest.Column",
                section = "CMRTest.Section",
                scribeLabelPrefix = "cmrTest",
                jobAreaFieldName = jobAreaFieldName,
                hideIfFieldWithNameIsFalse = hideIfFieldWithNameIsFalse!,
                defaultValue = enabled,
            }
        );
        field.Initialize();
        return field;
    }

    // Single Area field

    [Test]
    public static void DangerModeSwapsAreaToTheSafeAreaAndNormalModeRestoresIt()
    {
        using var scene = new Scene();
        var job = scene.AddJob<ManagerJob_Hunting>("CM_HuntingManager");
        var home = scene.AddArea("CMRTest-Home");
        var safe = scene.AddSafeArea("CMRTest-Home");
        job.HuntingGrounds = home;
        var field = Field(job, nameof(ManagerJob_Hunting.HuntingGrounds));

        field.AllowedAreaChangeDangerMode();
        Expect.ReferencesAreEqual(safe, job.HuntingGrounds);

        field.AllowedAreaChangeNormalMode();
        Expect.ReferencesAreEqual(home, job.HuntingGrounds);
    }

    [Test]
    public static void MissingSafeAreaLeavesTheAreaUnchangedAndRestoreStillWorks()
    {
        using var scene = new Scene();
        var job = scene.AddJob<ManagerJob_Hunting>("CM_HuntingManager");
        var home = scene.AddArea("CMRTest-NoSafe");
        job.HuntingGrounds = home;
        var field = Field(job, nameof(ManagerJob_Hunting.HuntingGrounds));

        field.AllowedAreaChangeDangerMode();
        Expect.ReferencesAreEqual(home, job.HuntingGrounds);

        field.AllowedAreaChangeNormalMode();
        Expect.ReferencesAreEqual(home, job.HuntingGrounds);
    }

    [Test]
    public static void AreaLabelWithRegexMetacharactersIsMatchedLiterally()
    {
        using var scene = new Scene();
        var job = scene.AddJob<ManagerJob_Hunting>("CM_HuntingManager");
        var home = scene.AddArea("CMRTest a.b*c (1)");
        _ = scene.AddSafeArea("CMRTest aXbXXXc X1X");
        var safe = scene.AddSafeArea("CMRTest a.b*c (1)");
        job.HuntingGrounds = home;
        var field = Field(job, nameof(ManagerJob_Hunting.HuntingGrounds));

        field.AllowedAreaChangeDangerMode();

        Expect.ReferencesAreEqual(safe, job.HuntingGrounds);
    }

    [Test]
    public static void DisabledFieldDoesNothing()
    {
        using var scene = new Scene();
        var job = scene.AddJob<ManagerJob_Hunting>("CM_HuntingManager");
        var home = scene.AddArea("CMRTest-Disabled");
        _ = scene.AddSafeArea("CMRTest-Disabled");
        job.HuntingGrounds = home;
        var field = Field(job, nameof(ManagerJob_Hunting.HuntingGrounds), enabled: false);

        field.AllowedAreaChangeDangerMode();
        field.AllowedAreaChangeNormalMode();

        Expect.ReferencesAreEqual(home, job.HuntingGrounds);
    }

    // Collection and array fields

    [Test]
    public static void DangerModeSwapsEveryAreaInACollectionAndNormalModeRestoresThem()
    {
        using var scene = new Scene();
        var job = scene.AddJob<ManagerJob_Forestry>("CM_ForestryManager");
        var homeA = scene.AddArea("CMRTest-A");
        var homeB = scene.AddArea("CMRTest-B");
        var safeA = scene.AddSafeArea("CMRTest-A");
        _ = job.ClearAreas.Add(homeA);
        _ = job.ClearAreas.Add(homeB);
        var field = Field(job, nameof(ManagerJob_Forestry.ClearAreas));

        field.AllowedAreaChangeDangerMode();

        // An area without a safe counterpart stays as it is.
        Expect.AreEqual(2, job.ClearAreas.Count);
        Expect.IsTrue(job.ClearAreas.Contains(safeA));
        Expect.IsTrue(job.ClearAreas.Contains(homeB));
        Expect.IsFalse(job.ClearAreas.Contains(homeA));

        field.AllowedAreaChangeNormalMode();

        Expect.AreEqual(2, job.ClearAreas.Count);
        Expect.IsTrue(job.ClearAreas.Contains(homeA));
        Expect.IsTrue(job.ClearAreas.Contains(homeB));
    }

    [Test]
    public static void DangerModeSwapsAnAreaArrayInPlace()
    {
        using var scene = new Scene();
        var job = scene.AddJob<ManagerJob_Livestock>("CM_LivestockManager");
        var home = scene.AddArea("CMRTest-Pen");
        var safe = scene.AddSafeArea("CMRTest-Pen");
        var array = job.RestrictArea;
        array[0] = home;
        var field = Field(job, nameof(ManagerJob_Livestock.RestrictArea));

        field.AllowedAreaChangeDangerMode();
        Expect.ReferencesAreEqual(array, job.RestrictArea);
        Expect.ReferencesAreEqual(safe, array[0]);

        field.AllowedAreaChangeNormalMode();
        Expect.ReferencesAreEqual(home, array[0]);
    }

    // Misconfigured fields

    private static string CaptureErrors(Action action)
    {
        var errors = new List<string>();
        void Handler(string message, string stackTrace, LogType type)
        {
            if (type is LogType.Error or LogType.Exception)
            {
                errors.Add(message);
            }
        }

        Application.logMessageReceived += Handler;
        try
        {
            action();
        }
        finally
        {
            Application.logMessageReceived -= Handler;
        }
        return string.Join("\n", errors);
    }

    [Test]
    [ErrorsAllowed("Could not find field")]
    public static void UnknownAreaFieldIsReportedByNameAndLeavesTheJobAlone()
    {
        using var scene = new Scene();
        var job = scene.AddJob<ManagerJob_Hunting>("CM_HuntingManager");
        var home = scene.AddArea("CMRTest-Unknown");
        job.HuntingGrounds = home;
        var field = Field(job, "CMRTestNoSuchField");

        var errors = CaptureErrors(field.AllowedAreaChangeDangerMode);

        Expect.IsTrue(errors.Contains("'CMRTestNoSuchField'", StringComparison.Ordinal));
        Expect.ReferencesAreEqual(home, job.HuntingGrounds);
    }

    [Test]
    [ErrorsAllowed("is of type")]
    public static void WrongTypedBooleanFieldIsReportedUnderItsOwnName()
    {
        using var scene = new Scene();
        var job = scene.AddJob<ManagerJob_Forestry>("CM_ForestryManager");
        var field = Field(
            job,
            nameof(ManagerJob_Forestry.ClearAreas),
            hideIfFieldWithNameIsFalse: nameof(ManagerJob_Forestry.LoggingArea)
        );
        var pos = Vector2.zero;

        var errors = CaptureErrors(() =>
            field.RenderSectionPostfix("other", "other", ref pos, 100f)
        );

        Expect.IsTrue(
            errors.Contains(
                $"'{nameof(ManagerJob_Forestry.LoggingArea)}'",
                StringComparison.Ordinal
            )
        );
        Expect.IsFalse(
            errors.Contains($"'{nameof(ManagerJob_Forestry.ClearAreas)}'", StringComparison.Ordinal)
        );
    }

    // Watcher postfixes

    [Test]
    public static void WatcherMethodsArePatchedByThisIntegration()
    {
        var owner = $"{ColonyManagerReduxMod.PackageId}.AAAA";

        foreach (
            var method in new[]
            {
                AccessTools.Method(typeof(Watcher), nameof(Watcher.AllowedAreaChangeDangerMode)),
                AccessTools.Method(typeof(Watcher), nameof(Watcher.AllowedAreaChangeNormalMode)),
            }
        )
        {
            var patches = Harmony.GetPatchInfo(method);
            Expect.IsNotNull(patches);
            Expect.IsTrue(patches?.Postfixes.Any(p => p.owner == owner) ?? false, method.Name);
        }
    }

    [Test]
    public static void PostfixesNotifyEveryJobCompOnTheMap()
    {
        using var scene = new Scene();
        var hunting = scene.AddJob<ManagerJob_Hunting>("CM_HuntingManager");
        var forestry = scene.AddJob<ManagerJob_Forestry>("CM_ForestryManager");
        var home = scene.AddArea("CMRTest-Watched");
        var safe = scene.AddSafeArea("CMRTest-Watched");
        hunting.HuntingGrounds = home;
        forestry.LoggingArea = home;
        var comps = new ManagerJob[] { hunting, forestry }
            .SelectMany(j => j.CompsOfType<AAAAManagerJobComp>())
            .ToList();
        Expect.IsNotEmpty(comps, "The integration adds no AAAA comp to the jobs.");
        foreach (var field in comps.SelectMany(c => c.Fields))
        {
            field._useAAAAEvacuation = true;
        }

        AllowedAreaAutomaticAdapter_Watcher_AllowedAreaChangeDangerMode_Patches.Postfix(scene.Map);
        Expect.ReferencesAreEqual(safe, hunting.HuntingGrounds);
        Expect.ReferencesAreEqual(safe, forestry.LoggingArea);

        AllowedAreaAutomaticAdapter_Watcher_AllowedAreaChangeNormalMode_Patches.Postfix(scene.Map);
        Expect.ReferencesAreEqual(home, hunting.HuntingGrounds);
        Expect.ReferencesAreEqual(home, forestry.LoggingArea);
    }
}
