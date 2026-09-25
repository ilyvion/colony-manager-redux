// ManagerJobMiningSafetySceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The room-divider and roof-support safety checks, their TTL caches, and the deconstruction
/// target ladder of <see cref="ManagerJob_Mining"/> against real walls, rooms and roofs.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobMiningSafetySceneTests
{
    private static ThingDef SteelVein => DefDatabase<ThingDef>.GetNamed("MineableSteel");

    private static IntVec3 At(ManagerScene scene, int x, int z) => scene.Cells(1, 1, x, z).First();

    private static Thing SpawnWall(ManagerScene scene, IntVec3 cell) =>
        scene.SpawnThing(ThingDefOf.Wall, cell, stuff: ThingDefOf.Steel);

    /// <summary>
    /// Builds two closed 3x3 rooms side by side, separated by a wall column, and returns the one
    /// gap in that column, which the caller fills.
    /// </summary>
    private static IntVec3 BuildTwoRooms(ManagerScene scene)
    {
        var origin = At(scene, -40, -40);
        for (var x = 0; x <= 8; x++)
        {
            for (var z = 0; z <= 4; z++)
            {
                var border = x is 0 or 8 || z is 0 or 4;
                var gap = x == 4 && z == 2;
                if ((border || x == 4) && !gap)
                {
                    _ = SpawnWall(scene, origin + new IntVec3(x, 0, z));
                }
            }
        }
        scene.Map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
        return origin + new IntVec3(4, 0, 2);
    }

    private static Thing FillGap(ManagerScene scene, IntVec3 gap)
    {
        var wall = SpawnWall(scene, gap);
        scene.Map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
        return wall;
    }

    // A position-only stand-in: the checks only read where the target is.
    private static Thing MarkerAt(IntVec3 cell)
    {
        var marker = ThingMaker.MakeThing(ThingDefOf.Steel);
        marker.Position = cell;
        return marker;
    }

    private static ManagerJob_Mining NewJob(ManagerScene scene)
    {
        var job = scene.AddJob<ManagerJob_Mining>("CM_MiningManager");
        job.CheckRoofSupport = false;
        job.CheckRoomDivision = false;
        job.ShouldCheckReachable = false;
        job.MineThickRoofs = true;
        job.MiningArea = null;
        job.InvertMiningArea = false;
        job.AllowedMinerals.Clear();
        _ = job.AllowedMinerals.Add(SteelVein);
        job.AllowedBuildings.Clear();
        _ = job.AllowedBuildings.Add(ThingDefOf.Wall);
        _ = job.AllowedBuildings.Add(ThingDefOf.Door);
        job.TriggerThreshold.ThresholdFilter.SetAllow(ThingDefOf.Steel, true);
        return job;
    }

    private static void WithFog(Map map, IntVec3 cell, Action action)
    {
        var fogIndex = map.cellIndices.CellToIndex(cell);
        var fogGrid = map.fogGrid.fogGrid;
        var wasFogged = fogGrid.IsSet(fogIndex);
        fogGrid.Set(fogIndex, true);
        try
        {
            action();
        }
        finally
        {
            fogGrid.Set(fogIndex, wasFogged);
        }
    }

    // Room dividers

    [Test]
    public static void WallBetweenTwoRoomsIsARoomDivider()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job.CheckRoomDivision = true;
        var gap = BuildTwoRooms(scene);
        _ = FillGap(scene, gap);

        Expect.IsTrue(job.IsARoomDivider(MarkerAt(gap)));
    }

    [Test]
    public static void RoomDivisionIsNotCheckedWhenTheSettingIsOff()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job.CheckRoomDivision = false;
        var gap = BuildTwoRooms(scene);
        _ = FillGap(scene, gap);

        Expect.IsFalse(job.IsARoomDivider(MarkerAt(gap)));
    }

    [Test]
    public static void PillarInsideASingleRoomIsNotARoomDivider()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job.CheckRoomDivision = true;
        var origin = At(scene, -40, -30);
        for (var x = 0; x <= 8; x++)
        {
            for (var z = 0; z <= 4; z++)
            {
                if (x is 0 or 8 || z is 0 or 4)
                {
                    _ = SpawnWall(scene, origin + new IntVec3(x, 0, z));
                }
            }
        }
        var pillar = origin + new IntVec3(4, 0, 2);
        _ = SpawnWall(scene, pillar);
        scene.Map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();

        Expect.IsFalse(job.IsARoomDivider(MarkerAt(pillar)));
    }

    [Test]
    public static void EndOfAShortWallIsNotADivider()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job.CheckRoomDivision = true;
        for (var x = 0; x <= 2; x++)
        {
            _ = SpawnWall(scene, At(scene, x, 15));
        }

        Expect.IsFalse(job.IsARoomDivider(MarkerAt(At(scene, 1, 15))));
    }

    [Test]
    public static void RoomDividerAnswerIsCachedForTheCell()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job.CheckRoomDivision = true;
        var otherJob = NewJob(scene);
        otherJob.CheckRoomDivision = true;
        var gap = BuildTwoRooms(scene);
        var gapWall = FillGap(scene, gap);
        var marker = MarkerAt(gap);
        Expect.IsTrue(job.IsARoomDivider(marker));

        gapWall.Destroy();
        scene.Map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();

        Expect.IsTrue(job.IsARoomDivider(marker), "The cached answer should be served.");
        Expect.IsFalse(otherJob.IsARoomDivider(marker), "A job without a cache sees the change.");
    }

    [Test]
    public static void VeinInADividerPositionIsRejectedOnlyWhenRoomDivisionIsChecked()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var gap = BuildTwoRooms(scene);
        var vein = (Mineable)scene.SpawnThing(SteelVein, gap);
        scene.Map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();

        job.CheckRoomDivision = true;
        Expect.IsFalse(job.IsValidMiningTarget(vein), "Check on");

        var freshJob = NewJob(scene);
        freshJob.CheckRoomDivision = false;
        Expect.IsTrue(freshJob.IsValidMiningTarget(vein), "Check off");
    }

    // Roof support cache

    [Test]
    public static void RoofSupportAnswerIsCachedForThePositionAndSupport()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var otherJob = NewJob(scene);
        var position = At(scene, 40, 40);
        var support = position + new IntVec3(1, 0, 0);
        var roofGrid = scene.Map.roofGrid;
        var previousRoof = roofGrid.RoofAt(position);
        roofGrid.SetRoof(position, RoofDefOf.RoofConstructed);
        try
        {
            Expect.IsTrue(
                job.WouldCollapseIfSupportDestroyed(position, support, scene.Map),
                "No other holder"
            );

            _ = SpawnWall(scene, position + new IntVec3(0, 0, 1));

            Expect.IsTrue(
                job.WouldCollapseIfSupportDestroyed(position, support, scene.Map),
                "Cached"
            );
            Expect.IsFalse(
                otherJob.WouldCollapseIfSupportDestroyed(position, support, scene.Map),
                "A job without a cache sees the new holder."
            );
        }
        finally
        {
            roofGrid.SetRoof(position, previousRoof);
        }
    }

    // Deconstruction targets

    private static Building SpawnTarget(ManagerScene scene, IntVec3? cell = null) =>
        (Building)SpawnWall(
            scene,
            cell
                ?? scene
                    .Cells(20, 20, 60, 20)
                    .First(c => !ManagerJob_Mining.IsARoofSupport_Basic(c))
        );

    [Test]
    public static void ForeignRelevantAllowedBuildingIsAValidDeconstructionTarget()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var wall = SpawnTarget(scene);

        Expect.IsTrue(job.IsValidDeconstructionTarget(wall));
        Expect.IsTrue(job.IsValidDeconstructionTarget(new LocalTargetInfo(wall)));
    }

    [Test]
    public static void NullAndCellOnlyDeconstructionTargetsAreInvalid()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var wall = SpawnTarget(scene);

        Expect.IsFalse(job.IsValidDeconstructionTarget(null!));
        Expect.IsFalse(job.IsValidDeconstructionTarget(new LocalTargetInfo(wall.Position)));
        Expect.IsFalse(job.IsValidDeconstructionTarget(LocalTargetInfo.Invalid));
    }

    [Test]
    public static void BuildingOwnedByTheColonyIsRejected()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var wall = SpawnTarget(scene);

        wall.SetFactionDirect(Faction.OfPlayer);

        Expect.IsFalse(job.IsValidDeconstructionTarget(wall));
    }

    [Test]
    public static void DeconstructionTargetWithADesignationIsOnlyValidWhenIncludingDeconstructDesignations()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var wall = SpawnTarget(scene);
        var designation = new Designation(wall, DesignationDefOf.Deconstruct);
        scene.Map.designationManager.AddDesignation(designation);
        try
        {
            Expect.IsFalse(job.IsValidDeconstructionTarget(wall));
            Expect.IsTrue(job.IsValidDeconstructionTarget(wall, includeDesignated: true));
        }
        finally
        {
            scene.Map.designationManager.RemoveDesignation(designation);
        }
    }

    [Test]
    public static void DeconstructionTargetWithAnotherKindOfDesignationIsRejectedEvenWhenIncludingDesignated()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var wall = SpawnTarget(scene);
        var designation = new Designation(wall, DesignationDefOf.Uninstall);
        scene.Map.designationManager.AddDesignation(designation);
        try
        {
            Expect.IsFalse(job.IsValidDeconstructionTarget(wall, includeDesignated: true));
        }
        finally
        {
            scene.Map.designationManager.RemoveDesignation(designation);
        }
    }

    [Test]
    public static void ForbiddenBuildingIsRejected()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var door = (Building)
            scene.SpawnThing(
                ThingDefOf.Door,
                scene.Cells(20, 20, 60, 20).First(c => !ManagerJob_Mining.IsARoofSupport_Basic(c)),
                stuff: ThingDefOf.Steel
            );
        Expect.IsTrue(job.IsValidDeconstructionTarget(door), "Precondition");

        door.SetForbidden(true, warnOnFail: false);

        Expect.IsFalse(job.IsValidDeconstructionTarget(door));
    }

    [Test]
    public static void BuildingTheJobDoesNotAllowIsRejected()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var wall = SpawnTarget(scene);

        _ = job.AllowedBuildings.Remove(ThingDefOf.Wall);

        Expect.IsFalse(job.IsValidDeconstructionTarget(wall));
    }

    [Test]
    public static void BuildingWhoseMaterialsAreNotCountedIsRejected()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var wall = SpawnTarget(scene);

        job.TriggerThreshold.ThresholdFilter.SetAllow(ThingDefOf.Steel, false);

        Expect.IsFalse(job.IsRelevantDeconstructionTarget(wall));
        Expect.IsFalse(job.IsValidDeconstructionTarget(wall));
    }

    [Test]
    public static void BuildingThatYieldsNothingWhenDeconstructedIsNotRelevant()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var wall = SpawnTarget(scene);
        var previous = wall.def.resourcesFractionWhenDeconstructed;
        wall.def.resourcesFractionWhenDeconstructed = 0f;
        try
        {
            Expect.IsFalse(job.IsRelevantDeconstructionTarget(wall));
        }
        finally
        {
            wall.def.resourcesFractionWhenDeconstructed = previous;
        }
    }

    [Test]
    public static void NaturalRockIsNotADeconstructionTarget()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var vein = (Mineable)scene.SpawnThing(SteelVein, At(scene, 60, 10));

        Expect.IsFalse(job.IsRelevantDeconstructionTarget(vein));
    }

    [Test]
    public static void DeconstructionTargetOutsideTheMiningAreaIsRejected()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var wall = SpawnTarget(scene);

        job.MiningArea = scene.AddArea(
            "CMRTest-decon-away",
            [wall.Position + new IntVec3(10, 0, 0)]
        );
        Expect.IsFalse(job.IsValidDeconstructionTarget(wall), "Outside the area");

        job.MiningArea = scene.AddArea("CMRTest-decon-here", [wall.Position]);
        Expect.IsTrue(job.IsValidDeconstructionTarget(wall), "Inside the area");
    }

    [Test]
    public static void FoggedDeconstructionTargetIsRejected()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var wall = SpawnTarget(scene);

        WithFog(
            scene.Map,
            wall.Position,
            () => Expect.IsFalse(job.IsValidDeconstructionTarget(wall))
        );
    }

    [Test]
    public static void DeconstructionTargetOnTheRoofSupportGridIsRejectedOnlyWhenTheCheckIsOn()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var wall = SpawnTarget(
            scene,
            scene.Cells(20, 20, 60, 20).First(ManagerJob_Mining.IsARoofSupport_Basic)
        );

        job.CheckRoofSupport = true;
        Expect.IsFalse(job.IsValidDeconstructionTarget(wall), "Check on");

        job.CheckRoofSupport = false;
        Expect.IsTrue(job.IsValidDeconstructionTarget(wall), "Check off");
    }

    [Test]
    public static void DeconstructionTargetThatIsARoomDividerIsRejectedOnlyWhenTheCheckIsOn()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var gap = BuildTwoRooms(scene);
        var wall = (Building)FillGap(scene, gap);

        job.CheckRoomDivision = true;
        Expect.IsFalse(job.IsValidDeconstructionTarget(wall), "Check on");

        var freshJob = NewJob(scene);
        freshJob.CheckRoomDivision = false;
        Expect.IsTrue(freshJob.IsValidDeconstructionTarget(wall), "Check off");
    }
}
