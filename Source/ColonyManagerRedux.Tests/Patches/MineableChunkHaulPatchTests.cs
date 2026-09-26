// MineableChunkHaulPatchTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The patch on <c>Mineable</c>'s placed-yield callback installs a postfix.
/// </summary>
[TestFixture(TestType.MainMenu)]
internal sealed class MineableChunkHaulPatchTests
{
    [Test]
    public static void TargetMethodResolves() =>
        Expect.IsNotNull(
            RimWorld_Mineable_TrySpawnYield_ForbidIfNecessary.TargetMethod(),
            "ForbidIfNecessary"
        );

    [Test]
    public static void PostfixIsInstalledByTheMod()
    {
        var target = RimWorld_Mineable_TrySpawnYield_ForbidIfNecessary.TargetMethod();

        var info = Harmony.GetPatchInfo(target);

        Expect.IsNotNull(info, "patch info");
        Expect.IsTrue(
            info.Postfixes.Any(p =>
                p.owner == ColonyManagerReduxMod.PackageId
                && p.PatchMethod.DeclaringType
                    == typeof(RimWorld_Mineable_TrySpawnYield_ForbidIfNecessary)
            ),
            "postfix owned by the mod"
        );
    }
}

/// <summary>
/// Mining a rock as a colonist makes the mining job designate the resulting chunk for hauling,
/// and <see cref="ManagerJob_Mining.Notify_StoneChunkMined"/>'s individual conditions.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class MineableChunkHaulSceneTests
{
    private static ThingDef GraniteRock => DefDatabase<ThingDef>.GetNamed("Granite");
    private static ThingDef SteelVein => DefDatabase<ThingDef>.GetNamed("MineableSteel");
    private static ThingDef GraniteChunk => ThingDef.Named("ChunkGranite");

    private static readonly MethodInfo _addCellDesignation = AccessTools.Method(
        typeof(ManagerJob_Mining),
        "AddDesignation",
        [typeof(Designation)]
    );

    private static ManagerJob_Mining NewJob(ManagerScene scene, bool haul = true)
    {
        var job = scene.AddJob<ManagerJob_Mining>("CM_MiningManager");
        job.HaulMinedChunks = haul;
        job.TriggerThreshold.ThresholdFilter.SetAllow(ThingDefOf.BlocksGranite, true);
        return job;
    }

    private static IntVec3 FreeCell(ManagerScene scene) =>
        scene.Cells(20, 20, 20, 20).First(c => c.Standable(scene.Map) && !c.Roofed(scene.Map));

    private static void DesignateCell(ManagerJob_Mining job, IntVec3 cell) =>
        _ = _addCellDesignation.Invoke(
            job,
            [new Designation(new LocalTargetInfo(cell), DesignationDefOf.Mine)]
        );

    private static bool IsHaulDesignated(Map map, Thing thing) =>
        map.designationManager.DesignationOn(thing, DesignationDefOf.Haul) != null;

    /// <summary>Mines a granite rock on a free cell, returning the chunks that dropped.</summary>
    private static List<Thing> Mine(ManagerScene scene, ManagerJob_Mining job, Pawn? miner)
    {
        var cell = FreeCell(scene);
        var rock = (Mineable)scene.SpawnThing(GraniteRock, cell);
        DesignateCell(job, cell);
        return MineRock(scene, rock, miner);
    }

    private static List<Thing> MineRock(ManagerScene scene, Mineable rock, Pawn? miner)
    {
        var before = scene.Map.listerThings.AllThings.ToHashSet();
        var originalDropChance = rock.def.building.mineableDropChance;
        rock.def.building.mineableDropChance = 1f;
        try
        {
            rock.DestroyMined(miner);
        }
        finally
        {
            rock.def.building.mineableDropChance = originalDropChance;
        }
        var dropped = scene.Map.listerThings.AllThings.Where(t => !before.Contains(t)).ToList();
        return dropped;
    }

    private static void Cleanup(ManagerScene scene, IEnumerable<Thing> things)
    {
        foreach (var thing in things.Where(t => !t.Destroyed))
        {
            scene.Map.designationManager.RemoveAllDesignationsOn(thing);
            thing.Destroy();
        }
    }

    private static void CleanupCell(ManagerScene scene, IntVec3 cell) =>
        scene.Map.designationManager.TryRemoveDesignation(cell, DesignationDefOf.Mine);

    [Test]
    public static void ChunkMinedByAColonistIsDesignatedForHauling()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var colonist = scene.SpawnColonist(FreeCell(scene));

        var chunks = Mine(scene, job, colonist);
        try
        {
            var chunk = chunks.Single(t => t.def == GraniteChunk);
            Expect.IsTrue(IsHaulDesignated(scene.Map, chunk), "haul designation");
        }
        finally
        {
            Cleanup(scene, chunks);
        }
    }

    [Test]
    public static void ChunkIsNotDesignatedWhenHaulingIsOff()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene, haul: false);
        var colonist = scene.SpawnColonist(FreeCell(scene));

        var chunks = Mine(scene, job, colonist);
        try
        {
            Expect.IsFalse(IsHaulDesignated(scene.Map, chunks.Single(t => t.def == GraniteChunk)));
        }
        finally
        {
            Cleanup(scene, chunks);
        }
    }

    [Test]
    public static void ChunkMinedByANonColonistIsNotDesignated()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var animal = scene.SpawnAnimal(PawnKindDefOf.Muffalo, FreeCell(scene));

        var chunks = Mine(scene, job, animal);
        try
        {
            Expect.IsFalse(IsHaulDesignated(scene.Map, chunks.Single(t => t.def == GraniteChunk)));
        }
        finally
        {
            Cleanup(scene, chunks);
        }
    }

    [Test]
    public static void ChunkWithoutAMinerIsNotDesignated()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);

        var chunks = Mine(scene, job, null);
        try
        {
            Expect.IsFalse(IsHaulDesignated(scene.Map, chunks.Single(t => t.def == GraniteChunk)));
        }
        finally
        {
            Cleanup(scene, chunks);
        }
    }

    [Test]
    public static void OreMinedByAColonistIsNotDesignated()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var colonist = scene.SpawnColonist(FreeCell(scene));
        var cell = FreeCell(scene);
        var vein = (Mineable)scene.SpawnThing(SteelVein, cell);
        DesignateCell(job, cell);

        var dropped = MineRock(scene, vein, colonist);
        try
        {
            Expect.IsNotEmpty(dropped, "steel dropped");
            Expect.IsFalse(dropped.Any(t => IsHaulDesignated(scene.Map, t)));
        }
        finally
        {
            Cleanup(scene, dropped);
        }
    }

    private static Thing SpawnChunk(ManagerScene scene, IntVec3 cell) =>
        scene.SpawnThing(GraniteChunk, cell);

    [Test]
    public static void NotifyDesignatesACountedChunkOnADesignatedCell()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var colonist = scene.SpawnColonist(FreeCell(scene));
        var cell = FreeCell(scene);
        var chunk = SpawnChunk(scene, cell);
        DesignateCell(job, cell);
        try
        {
            job.Notify_StoneChunkMined(colonist, chunk);

            Expect.IsTrue(IsHaulDesignated(scene.Map, chunk));
        }
        finally
        {
            Cleanup(scene, [chunk]);
            CleanupCell(scene, cell);
        }
    }

    [Test]
    public static void NotifyDoesNothingWhenHaulingIsOff()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene, haul: false);
        var colonist = scene.SpawnColonist(FreeCell(scene));
        var cell = FreeCell(scene);
        var chunk = SpawnChunk(scene, cell);
        DesignateCell(job, cell);
        try
        {
            job.Notify_StoneChunkMined(colonist, chunk);

            Expect.IsFalse(IsHaulDesignated(scene.Map, chunk));
        }
        finally
        {
            Cleanup(scene, [chunk]);
            CleanupCell(scene, cell);
        }
    }

    [Test]
    public static void NotifyDoesNothingForAChunkThatIsNotHaulDesignatable()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var colonist = scene.SpawnColonist(FreeCell(scene));
        var cell = FreeCell(scene);
        var chunk = SpawnChunk(scene, cell);
        DesignateCell(job, cell);
        var original = GraniteChunk.designateHaulable;
        GraniteChunk.designateHaulable = false;
        try
        {
            job.Notify_StoneChunkMined(colonist, chunk);

            Expect.IsFalse(IsHaulDesignated(scene.Map, chunk));
        }
        finally
        {
            GraniteChunk.designateHaulable = original;
            Cleanup(scene, [chunk]);
            CleanupCell(scene, cell);
        }
    }

    [Test]
    public static void NotifyDoesNothingWhenNoChunkProductIsCounted()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job.TriggerThreshold.ThresholdFilter.SetAllow(ThingDefOf.BlocksGranite, false);
        var colonist = scene.SpawnColonist(FreeCell(scene));
        var cell = FreeCell(scene);
        var chunk = SpawnChunk(scene, cell);
        DesignateCell(job, cell);
        try
        {
            job.Notify_StoneChunkMined(colonist, chunk);

            Expect.IsFalse(IsHaulDesignated(scene.Map, chunk));
        }
        finally
        {
            Cleanup(scene, [chunk]);
            CleanupCell(scene, cell);
        }
    }

    [Test]
    public static void NotifyDoesNothingWithoutADesignationAtTheChunksCell()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var colonist = scene.SpawnColonist(FreeCell(scene));
        var chunk = SpawnChunk(scene, FreeCell(scene));
        try
        {
            job.Notify_StoneChunkMined(colonist, chunk);

            Expect.IsFalse(IsHaulDesignated(scene.Map, chunk));
        }
        finally
        {
            Cleanup(scene, [chunk]);
        }
    }
}
