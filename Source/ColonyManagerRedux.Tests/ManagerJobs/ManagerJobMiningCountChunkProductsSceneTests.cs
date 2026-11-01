// ManagerJobMiningCountChunkProductsSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;
using static ColonyManagerRedux.Managers.ManagerJob_Mining;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// <see cref="ManagerJob_Mining.CountChunkProducts"/> turned off makes chunks worth nothing to
/// the job and stops it from designating chunks for hauling.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobMiningCountChunkProductsSceneTests
{
    private static ThingDef GraniteRock => DefDatabase<ThingDef>.GetNamed("Granite");
    private static ThingDef SlagChunk => ThingDef.Named("ChunkSlagSteel");

    private static IntVec3 Slot(ManagerScene scene, int i) =>
        scene.Cells(1, 1, -60 + (i * 3), -50).First();

    private static ManagerJob_Mining NewJob(ManagerScene scene, bool countChunkProducts)
    {
        var job = scene.AddJob<ManagerJob_Mining>("CM_MiningManager");
        job.CheckRoofSupport = false;
        job.CheckRoomDivision = false;
        job.ShouldCheckReachable = false;
        job.HaulMapChunks = true;
        job.HaulMinedChunks = true;
        job.CountChunkProducts = countChunkProducts;
        job.TriggerThreshold.ThresholdFilter.SetDisallowAll();
        job.TriggerThreshold.ThresholdFilter.SetAllow(ThingDefOf.Steel, true);
        job.TriggerThreshold.ThresholdFilter.SetAllow(ThingDefOf.BlocksGranite, true);
        job.TriggerThreshold.Op = Trigger_Threshold.Ops.LowerThan;
        job.TriggerThreshold.TargetCount = 100000;
        return job;
    }

    private static int CountInChunks(ManagerJob_Mining job)
    {
        var count = new AnyBoxed<int>(0);
        ManagerScene.RunToCompletion(job.GetCountInChunksCoroutine(count));
        return count.Value;
    }

    private static int CountInDesignations(ManagerJob_Mining job)
    {
        var count = new AnyBoxed<int>(0);
        ManagerScene.RunToCompletion(job.GetCountInDesignationsCoroutine(count));
        return count.Value;
    }

    [Test]
    public static void ChunkYieldsNothingWhenProductsAreNotCounted()
    {
        using var scene = new ManagerScene();
        var on = NewJob(scene, countChunkProducts: true);
        var off = NewJob(scene, countChunkProducts: false);

        Expect.IsTrue(on.GetCountInChunk(SlagChunk) > 0, "The chunk yields nothing.");
        Expect.AreEqual(0, off.GetCountInChunk(SlagChunk));
    }

    [Test]
    public static void StockpiledChunkIsNotCountedWhenProductsAreNotCounted()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene, countChunkProducts: false);
        var cell = Slot(scene, 0);
        _ = scene.AddStockpile([cell]);
        _ = scene.SpawnThing(SlagChunk, cell);

        Expect.AreEqual(0, CountInChunks(job));
    }

    [Test]
    public static void HaulDesignatedChunkIsNotCountedWhenProductsAreNotCounted()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene, countChunkProducts: false);
        var chunk = scene.SpawnThing(SlagChunk, Slot(scene, 0));
        job.AddDesignation(new Designation(chunk, DesignationDefOf.Haul));

        Expect.AreEqual(0, CountInDesignations(job));
    }

    [Test]
    public static void ChunkYieldingRockIsNotRelevantWhenProductsAreNotCounted()
    {
        using var scene = new ManagerScene();
        var on = NewJob(scene, countChunkProducts: true);
        var off = NewJob(scene, countChunkProducts: false);

        Expect.IsTrue(on.GetCountInMineral(GraniteRock) > 0, "Granite yields nothing.");
        Expect.AreEqual(0, off.GetCountInMineral(GraniteRock));
    }

    [Test]
    public static void HaulingSettingsAreIneffectiveWhenProductsAreNotCounted()
    {
        using var scene = new ManagerScene();
        var on = NewJob(scene, countChunkProducts: true);
        var off = NewJob(scene, countChunkProducts: false);

        Expect.IsTrue(on.ShouldHaulMapChunks);
        Expect.IsTrue(on.ShouldHaulMinedChunks);
        Expect.IsFalse(off.ShouldHaulMapChunks);
        Expect.IsFalse(off.ShouldHaulMinedChunks);
    }

    [Test]
    public static void HaulPlansNoChunksWhenProductsAreNotCounted()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene, countChunkProducts: false);
        _ = scene.SpawnThing(SlagChunk, Slot(scene, 0));
        var data = new MiningWorkData();

        ManagerScene.RunToCompletion(job.PlanHaulChunks(new Boxed<int>(0), data));

        Expect.IsEmpty(data.DesignationsToAdd);
    }

    [Test]
    public static void PlayerHaulDesignationOnAChunkIsNotAdoptedWhenProductsAreNotCounted()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene, countChunkProducts: false);
        var chunk = scene.SpawnThing(SlagChunk, Slot(scene, 0));
        scene.Map.designationManager.AddDesignation(new Designation(chunk, DesignationDefOf.Haul));
        try
        {
            ManagerScene.RunToCompletion(job.AddRelevantGameDesignations());

            Expect.IsFalse(job._designations.Any(d => d.target.Thing == chunk));
        }
        finally
        {
            scene.Map.designationManager.RemoveAllDesignationsOn(chunk);
        }
    }

    [Test]
    public static void PlayerHaulDesignationOnAChunkIsAdoptedWhenProductsAreCounted()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene, countChunkProducts: true);
        var chunk = scene.SpawnThing(SlagChunk, Slot(scene, 0));
        scene.Map.designationManager.AddDesignation(new Designation(chunk, DesignationDefOf.Haul));
        try
        {
            ManagerScene.RunToCompletion(job.AddRelevantGameDesignations());

            Expect.IsTrue(job._designations.Any(d => d.target.Thing == chunk));
        }
        finally
        {
            scene.Map.designationManager.RemoveAllDesignationsOn(chunk);
        }
    }

    [Test]
    public static void NewJobTakesTheDefaultFromTheManagerSettings()
    {
        var def = DefDatabase<ManagerDef>.GetNamed("CM_MiningManager");
        var defaults =
            ColonyManagerReduxMod.Settings.ManagerDefaultSettingsFor<ManagerDefaultSettings_Mining>(
                def
            )!;
        var saved = defaults.DefaultCountChunkProducts;
        try
        {
            defaults.DefaultCountChunkProducts = false;

            using var scene = new ManagerScene();
            var job = scene.AddJob<ManagerJob_Mining>("CM_MiningManager");
            job.PostMake();

            Expect.IsFalse(job.CountChunkProducts);
        }
        finally
        {
            defaults.DefaultCountChunkProducts = saved;
        }
    }
}
