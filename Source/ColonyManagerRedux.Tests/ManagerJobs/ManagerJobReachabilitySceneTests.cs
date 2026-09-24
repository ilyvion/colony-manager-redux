// ManagerJobReachabilitySceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;
using Verse.AI;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// <see cref="ManagerJob.IsReachable(Thing, PathEndMode, Danger)"/> and
/// <see cref="ManagerJob.Distance"/> against real things, one of which is walled in.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobReachabilitySceneTests
{
    private static bool IsReachable(ManagerJob job, Thing target) =>
        job.IsReachable(target, PathEndMode.Touch, Danger.Some);

    private static IntVec3 Origin(ManagerScene scene) => scene.Cells(1, 1, 30, 30).First();

    private static (Thing open, Thing walledIn, List<Building> walls) SpawnTargets(
        ManagerScene scene
    )
    {
        var origin = Origin(scene);
        var open = scene.SpawnThing(ThingDefOf.MeleeWeapon_Knife, origin);
        var walledCell = origin + new IntVec3(10, 0, 0);
        var walledIn = scene.SpawnThing(ThingDefOf.MeleeWeapon_Knife, walledCell);
        var walls = new List<Building>();
        foreach (var offset in GenAdj.AdjacentCells)
        {
            walls.Add(scene.SpawnBuilding(ThingDefOf.Wall, walledCell + offset));
        }
        return (open, walledIn, walls);
    }

    [Test]
    public static void FoggedTargetIsUnreachableEvenWithCheckingOff()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        job.ShouldCheckReachable = false;
        var target = scene.SpawnThing(ThingDefOf.MeleeWeapon_Knife, Origin(scene));
        var fogIndex = scene.Map.cellIndices.CellToIndex(target.Position);
        var fogGrid = scene.Map.fogGrid.fogGrid;
        var wasFogged = fogGrid.IsSet(fogIndex);
        fogGrid.Set(fogIndex, true);
        try
        {
            Expect.IsFalse(IsReachable(job, target));
        }
        finally
        {
            fogGrid.Set(fogIndex, wasFogged);
        }
    }

    [Test]
    public static void CheckingOffTreatsWalledInTargetAsReachable()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        job.ShouldCheckReachable = false;
        var (_, walledIn, _) = SpawnTargets(scene);

        Expect.IsTrue(IsReachable(job, walledIn));
    }

    [Test]
    public static void CheckingOnSeparatesOpenFromWalledInTargets()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        job.ShouldCheckReachable = true;
        var (open, walledIn, _) = SpawnTargets(scene);
        _ = scene.SpawnColonist(open.Position + new IntVec3(0, 0, 3));

        Expect.IsTrue(IsReachable(job, open), "Open target is reachable");
        Expect.IsFalse(IsReachable(job, walledIn), "Walled-in target is unreachable");
    }

    [Test]
    public static void ReachabilityResultIsCachedWithinItsLifetime()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        job.ShouldCheckReachable = true;
        var (open, walledIn, walls) = SpawnTargets(scene);
        _ = scene.SpawnColonist(open.Position + new IntVec3(0, 0, 3));
        Expect.IsFalse(IsReachable(job, walledIn), "Unreachable to begin with");

        foreach (var wall in walls)
        {
            wall.Destroy();
        }

        Expect.IsFalse(IsReachable(job, walledIn), "Cached result is returned");
    }

    [Test]
    public static void StraightLineDistanceIsTwiceTheCellDistance()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        job.UsePathBasedDistance = false;
        var origin = Origin(scene);
        var target = scene.SpawnThing(ThingDefOf.MeleeWeapon_Knife, origin + new IntVec3(3, 0, 0));

        Expect.AreEqual(6f, job.Distance(target, origin));
    }

    [Test]
    public static void UnreachableTargetIsFartherThanAnyReachableOneByPath()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        job.UsePathBasedDistance = true;
        var (open, walledIn, _) = SpawnTargets(scene);
        var source = open.Position + new IntVec3(0, 0, 3);

        var reachableDistance = job.Distance(open, source);
        var unreachableDistance = job.Distance(walledIn, source);

        Expect.IsTrue(reachableDistance > 0f, "Reachable distance is positive");
        Expect.IsTrue(
            unreachableDistance > reachableDistance,
            "Unreachable distance is larger, not overflowed"
        );
    }

    [Test]
    public static void UnreachableTargetSortsLast()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        job.UsePathBasedDistance = true;
        var (open, walledIn, _) = SpawnTargets(scene);
        var source = open.Position + new IntVec3(0, 0, 3);
        var sorted = new List<Thing>();

        ManagerScene.RunToCompletion(
            job.GetTargetsSorted<Thing, float>(
                sorted,
                t => t == open || t == walledIn,
                (_, distance) => -distance,
                source
            )
        );

        Expect.That(
            () => sorted.SequenceEqual([open, walledIn]),
            "Nearest first, unreachable last"
        );
    }
}
