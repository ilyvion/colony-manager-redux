// ManagerJobDesignationSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The designation bookkeeping shared by every designating job: pruning designations that no
/// longer exist in the game, deleting them all, and saving them.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobDesignationSceneTests
{
    private sealed class DesignationsProbe(Map map, List<Thing> things) : IExposable
    {
        public List<Designation> Designations = [];

        public void ExposeData()
        {
            // The designated things are saved with the map in a real save; make them resolvable
            // as the same live instances here.
            foreach (var thing in things)
            {
                if (Scribe.mode == LoadSaveMode.Saving)
                {
                    Scribe.saver.loadIDsErrorsChecker.RegisterDeepSaved(thing, "thing");
                }
                else if (Scribe.mode == LoadSaveMode.LoadingVars)
                {
                    Scribe.loader.crossRefs.loadedObjectDirectory.RegisterLoaded(thing);
                }
            }
            Utilities.Scribe_Designations(ref Designations, map);
        }
    }

    private static Designation Designate(ManagerScene scene, Thing thing, DesignationDef def)
    {
        var designation = new Designation(thing, def);
        scene.Map.designationManager.AddDesignation(designation);
        return designation;
    }

    private static Thing SpawnTarget(ManagerScene scene, int offset) =>
        scene.SpawnThing(ThingDefOf.Steel, scene.Cells(1, 1, offsetX: offset).First(), 10);

    private static int GameDesignationCount(ManagerScene scene, DesignationDef def) =>
        scene.Map.designationManager.SpawnedDesignationsOfDef(def).Count();

    private static ManagerLog NewLog(ManagerJob job) => new(job) { _workDone = true };

    // CleanDeadDesignations

    [Test]
    public static void CleanDeadDesignationsKeepsOnlyThoseStillInTheGame()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        var log = NewLog(job);
        var kept1 = Designate(scene, SpawnTarget(scene, 0), DesignationDefOf.Haul);
        var dead = Designate(scene, SpawnTarget(scene, 2), DesignationDefOf.Haul);
        var kept2 = Designate(scene, SpawnTarget(scene, 4), DesignationDefOf.Haul);
        var designations = new List<Designation> { kept1, dead, kept2 };
        scene.Map.designationManager.RemoveDesignation(dead);

        job.CleanDead(designations, DesignationDefOf.Haul, log);

        Expect.AreEqual(2, designations.Count);
        Expect.IsTrue(designations.Contains(kept1) && designations.Contains(kept2));
        Expect.AreEqual(1, log.Details.Count());
    }

    [Test]
    public static void CleanDeadDesignationsFiltersByDef()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        var haul = Designate(scene, SpawnTarget(scene, 0), DesignationDefOf.Haul);
        var uninstall = Designate(scene, SpawnTarget(scene, 2), DesignationDefOf.Uninstall);
        var designations = new List<Designation> { haul, uninstall };

        job.CleanDead(designations, DesignationDefOf.Haul, null);

        Expect.AreEqual(1, designations.Count);
        Expect.ReferencesAreEqual(haul, designations[0]);
    }

    [Test]
    public static void CleanDeadDesignationsWithoutADefKeepsEveryLiveDesignation()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        var haul = Designate(scene, SpawnTarget(scene, 0), DesignationDefOf.Haul);
        var uninstall = Designate(scene, SpawnTarget(scene, 2), DesignationDefOf.Uninstall);
        var designations = new List<Designation> { haul, uninstall };

        job.CleanDead(designations, null, null);

        Expect.AreEqual(2, designations.Count);
    }

    [Test]
    public static void CleanDeadDesignationsLogsNothingWhenNothingWasRemoved()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        var log = NewLog(job);
        var designations = new List<Designation>
        {
            Designate(scene, SpawnTarget(scene, 0), DesignationDefOf.Haul),
        };

        job.CleanDead(designations, DesignationDefOf.Haul, log);

        Expect.AreEqual(1, designations.Count);
        Expect.IsEmpty(log.Details);
    }

    [Test]
    public static void CleanDeadDesignationsOnAnEmptyListIsANoOp()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        var log = NewLog(job);
        var designations = new List<Designation>();

        job.CleanDead(designations, DesignationDefOf.Haul, log);

        Expect.IsEmpty(designations);
        Expect.IsEmpty(log.Details);
    }

    [Test]
    public static void CleanDeadDesignationsRejectsANullList()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();

        _ = Expect.Throws<ArgumentNullException>(() =>
            job.CleanDead(null!, DesignationDefOf.Haul, null)
        );
    }

    // CleanUpDesignations

    [Test]
    public static void CleanUpDesignationsRemovesThemFromTheGameAndEmptiesTheList()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        var log = NewLog(job);
        var baseline = GameDesignationCount(scene, DesignationDefOf.Haul);
        var designations = new List<Designation>
        {
            Designate(scene, SpawnTarget(scene, 0), DesignationDefOf.Haul),
            Designate(scene, SpawnTarget(scene, 2), DesignationDefOf.Haul),
        };
        Expect.AreEqual(baseline + 2, GameDesignationCount(scene, DesignationDefOf.Haul));

        TestManagerJob.CleanUpAll(designations, log);

        Expect.IsEmpty(designations);
        Expect.AreEqual(baseline, GameDesignationCount(scene, DesignationDefOf.Haul));
        Expect.AreEqual(1, log.Details.Count());
    }

    [Test]
    public static void CleanUpDesignationsRejectsANullList() =>
        _ = Expect.Throws<ArgumentNullException>(() => TestManagerJob.CleanUpAll(null!, null));

    // PlanDisallowedDesignations / ExecuteDisallowedDesignations

    [Test]
    public static void PlanDisallowedDesignationsSkipsAllowedAndAlreadyRemovedOnes()
    {
        using var scene = new ManagerScene();
        var allowed = Designate(scene, SpawnTarget(scene, 0), DesignationDefOf.Haul);
        var disallowed = Designate(scene, SpawnTarget(scene, 2), DesignationDefOf.Haul);
        var alreadyRemoving = Designate(scene, SpawnTarget(scene, 4), DesignationDefOf.Haul);

        var planned = TestManagerJob.PlanDisallowed(
            [allowed, disallowed, alreadyRemoving],
            [alreadyRemoving],
            d => d != allowed
        );

        Expect.AreEqual(1, planned.Count);
        Expect.ReferencesAreEqual(disallowed, planned[0]);
    }

    [Test]
    public static void ExecuteDisallowedDesignationsRemovesThemFromTheGameAndTheList()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        var log = NewLog(job);
        var kept = Designate(scene, SpawnTarget(scene, 0), DesignationDefOf.Haul);
        var removed = Designate(scene, SpawnTarget(scene, 2), DesignationDefOf.Haul);
        var designations = new List<Designation> { kept, removed };

        TestManagerJob.ExecuteDisallowed(designations, [removed], _ => true, log);

        Expect.AreEqual(1, designations.Count);
        Expect.ReferencesAreEqual(kept, designations[0]);
        Expect.IsNull(
            scene.Map.designationManager.DesignationOn(removed.target.Thing, DesignationDefOf.Haul)
        );
        Expect.AreEqual(1, log.Details.Count());
    }

    [Test]
    public static void ExecuteDisallowedDesignationsKeepsOnesAllowedAgainSincePlanning()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        var log = NewLog(job);
        var reallowed = Designate(scene, SpawnTarget(scene, 0), DesignationDefOf.Haul);
        var designations = new List<Designation> { reallowed };

        TestManagerJob.ExecuteDisallowed(designations, [reallowed], _ => false, log);

        Expect.AreEqual(1, designations.Count);
        Expect.IsNotNull(
            scene.Map.designationManager.DesignationOn(
                reallowed.target.Thing,
                DesignationDefOf.Haul
            )
        );
        Expect.IsEmpty(log.Details);
    }

    // Scribe_Designations

    private static DesignationsProbe RoundTrip(
        ManagerScene scene,
        DesignationsProbe probe,
        List<Thing> things
    ) =>
        ScribeRoundTrip.RoundTripDeep(scene.Manager, probe, scene.Map, things)
        ?? throw new InvalidOperationException("The probe did not load");

    [Test]
    public static void StaleDesignationsAreDroppedWhenSaving()
    {
        using var scene = new ManagerScene();
        var target1 = SpawnTarget(scene, 0);
        var target2 = SpawnTarget(scene, 2);
        var live = Designate(scene, target1, DesignationDefOf.Haul);
        var stale = Designate(scene, target2, DesignationDefOf.Haul);
        var things = new List<Thing> { target1, target2 };
        var probe = new DesignationsProbe(scene.Map, things) { Designations = [live, stale] };
        scene.Map.designationManager.RemoveDesignation(stale);

        var loaded = RoundTrip(scene, probe, things);

        Expect.AreEqual(1, probe.Designations.Count);
        Expect.AreEqual(1, loaded.Designations.Count);
    }

    [Test]
    public static void LoadedDesignationsAreTheGamesOwnInstances()
    {
        using var scene = new ManagerScene();
        var target = SpawnTarget(scene, 0);
        var live = Designate(scene, target, DesignationDefOf.Haul);
        var things = new List<Thing> { target };
        var probe = new DesignationsProbe(scene.Map, things) { Designations = [live] };

        var loaded = RoundTrip(scene, probe, things);

        Expect.AreEqual(1, loaded.Designations.Count);
        Expect.ReferencesAreEqual(live, loaded.Designations[0]);
    }

    [Test]
    public static void CellDesignationsAreLeftAsLoaded()
    {
        using var scene = new ManagerScene();
        var cell = scene.Cells(1, 1, offsetX: 6).First();
        var live = new Designation(cell, DesignationDefOf.Mine);
        scene.Map.designationManager.AddDesignation(live);
        var probe = new DesignationsProbe(scene.Map, []) { Designations = [live] };

        var loaded = RoundTrip(scene, probe, []);

        Expect.AreEqual(1, loaded.Designations.Count);
        Expect.AreEqual(cell, loaded.Designations[0].target.Cell);
        scene.Map.designationManager.RemoveDesignation(live);
    }

    [Test]
    public static void ScribingDesignationsRejectsNullArguments()
    {
        using var scene = new ManagerScene();
        var designations = new List<Designation>();
        List<Designation> nullList = null!;

        _ = Expect.Throws<ArgumentNullException>(() =>
            Utilities.Scribe_Designations(ref nullList, scene.Map)
        );
        _ = Expect.Throws<ArgumentNullException>(() =>
            Utilities.Scribe_Designations(ref designations, null!)
        );
    }
}
