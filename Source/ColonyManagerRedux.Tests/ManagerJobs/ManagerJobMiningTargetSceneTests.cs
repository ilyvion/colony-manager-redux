// ManagerJobMiningTargetSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// <see cref="ManagerJob_Mining.IsValidMiningTarget(Mineable?, bool)"/>: one rejection reason at
/// a time against a real steel vein, so a dropped or reordered condition is caught.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobMiningTargetSceneTests
{
    private static ThingDef SteelVein => DefDatabase<ThingDef>.GetNamed("MineableSteel");

    private static IntVec3 CellWhere(ManagerScene scene, bool roofSupport) =>
        scene
            .Cells(20, 20, 20, 20)
            .First(c => ManagerJob_Mining.IsARoofSupport_Basic(c) == roofSupport);

    /// <summary>A job that accepts steel veins, with every safety check switched off.</summary>
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
        job.TriggerThreshold.ThresholdFilter.SetAllow(ThingDefOf.Steel, true);
        return job;
    }

    private static Mineable SpawnVein(ManagerScene scene, IntVec3? cell = null) =>
        (Mineable)scene.SpawnThing(SteelVein, cell ?? CellWhere(scene, roofSupport: false));

    [Test]
    public static void AllowedVeinWithYieldOnOpenGroundIsValid()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var vein = SpawnVein(scene);

        Expect.IsTrue(job.IsValidMiningTarget(vein));
    }

    [Test]
    public static void NullTargetIsInvalid()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);

        Expect.IsFalse(job.IsValidMiningTarget(null));
    }

    [Test]
    public static void MineralNotAllowedByTheJobIsRejected()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var vein = SpawnVein(scene);

        _ = job.AllowedMinerals.Remove(SteelVein);

        Expect.IsFalse(job.IsValidMiningTarget(vein));
    }

    [Test]
    public static void VeinWhoseYieldIsNotCountedIsRejected()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var vein = SpawnVein(scene);

        job.TriggerThreshold.ThresholdFilter.SetAllow(ThingDefOf.Steel, false);

        Expect.IsFalse(job.IsValidMiningTarget(vein));
    }

    [Test]
    public static void DesignatedVeinIsOnlyValidWhenDesignatedTargetsAreIncluded()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var vein = SpawnVein(scene);
        var designation = new Designation(vein, DesignationDefOf.Mine);
        scene.Map.designationManager.AddDesignation(designation);
        try
        {
            Expect.IsFalse(job.IsValidMiningTarget(vein));
            Expect.IsTrue(job.IsValidMiningTarget(vein, includeDesignated: true));
        }
        finally
        {
            scene.Map.designationManager.RemoveDesignation(designation);
        }
    }

    [Test]
    public static void VeinWithAnotherKindOfDesignationIsRejectedEvenWhenIncludingDesignated()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var vein = SpawnVein(scene);
        var designation = new Designation(vein, DesignationDefOf.Haul);
        scene.Map.designationManager.AddDesignation(designation);
        try
        {
            Expect.IsFalse(job.IsValidMiningTarget(vein, includeDesignated: true));
        }
        finally
        {
            scene.Map.designationManager.RemoveDesignation(designation);
        }
    }

    [Test]
    public static void VeinOutsideTheMiningAreaIsRejectedAndInsideIsValid()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var vein = SpawnVein(scene);
        job.MiningArea = scene.AddArea("CMRTest-mining", [vein.Position + new IntVec3(10, 0, 0)]);

        Expect.IsFalse(job.IsValidMiningTarget(vein), "Outside the area");

        job.MiningArea = scene.AddArea("CMRTest-mining-around", [vein.Position]);

        Expect.IsTrue(job.IsValidMiningTarget(vein), "Inside the area");
    }

    [Test]
    public static void InvertedMiningAreaExcludesItsOwnCells()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var vein = SpawnVein(scene);
        job.MiningArea = scene.AddArea("CMRTest-mining-inverted", [vein.Position]);
        job.InvertMiningArea = true;

        Expect.IsFalse(job.IsValidMiningTarget(vein));
    }

    [Test]
    public static void VeinUnderAThickRoofIsRejectedUnlessThickRoofsAreAllowed()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var vein = SpawnVein(scene);
        var roofGrid = scene.Map.roofGrid;
        var previousRoof = roofGrid.RoofAt(vein.Position);
        roofGrid.SetRoof(vein.Position, RoofDefOf.RoofRockThick);
        try
        {
            job.MineThickRoofs = false;
            Expect.IsFalse(job.IsValidMiningTarget(vein), "Thick roofs not allowed");

            job.MineThickRoofs = true;
            Expect.IsTrue(job.IsValidMiningTarget(vein), "Thick roofs allowed");
        }
        finally
        {
            roofGrid.SetRoof(vein.Position, previousRoof);
        }
    }

    [Test]
    public static void VeinUnderAThinRoofIsValidEvenWhenThickRoofsAreNotAllowed()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var vein = SpawnVein(scene);
        var roofGrid = scene.Map.roofGrid;
        var previousRoof = roofGrid.RoofAt(vein.Position);
        roofGrid.SetRoof(vein.Position, RoofDefOf.RoofConstructed);
        try
        {
            job.MineThickRoofs = false;

            Expect.IsTrue(job.IsValidMiningTarget(vein));
        }
        finally
        {
            roofGrid.SetRoof(vein.Position, previousRoof);
        }
    }

    [Test]
    public static void VeinOnTheRoofSupportGridIsRejectedOnlyWhenTheCheckIsOn()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var vein = SpawnVein(scene, CellWhere(scene, roofSupport: true));

        job.CheckRoofSupport = true;
        Expect.IsFalse(job.IsValidMiningTarget(vein), "Check on");

        job.CheckRoofSupport = false;
        Expect.IsTrue(job.IsValidMiningTarget(vein), "Check off");
    }

    [Test]
    public static void FoggedVeinIsRejected()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var vein = SpawnVein(scene);
        var fogIndex = scene.Map.cellIndices.CellToIndex(vein.Position);
        var fogGrid = scene.Map.fogGrid.fogGrid;
        var wasFogged = fogGrid.IsSet(fogIndex);
        fogGrid.Set(fogIndex, true);
        try
        {
            Expect.IsFalse(job.IsValidMiningTarget(vein));
        }
        finally
        {
            fogGrid.Set(fogIndex, wasFogged);
        }
    }
}
