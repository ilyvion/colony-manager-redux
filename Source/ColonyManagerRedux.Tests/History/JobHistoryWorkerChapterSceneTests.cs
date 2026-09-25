// JobHistoryWorkerChapterSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The history worker of every manager job type, run over each chapter its job def lists and a
/// chapter that belongs to another job type.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class JobHistoryWorkerChapterSceneTests
{
    private static readonly ThingDef SlagChunk = ThingDef.Named("ChunkSlagSteel");

    private static CompProperties_ManagerJobHistory HistoryProps(ManagerJob job) =>
        job.Def.jobComps.OfType<CompProperties_ManagerJobHistory>().Single();

    private static int Count(ManagerJob job, ManagerJobHistoryChapterDef chapter)
    {
        var box = new Boxed<int> { Value = 0 };
        ManagerScene.RunToCompletion(
            HistoryProps(job).Worker.GetCountForHistoryChapterCoroutine(job, 0, chapter, box)
        );
        return box.Value;
    }

    private static int Target(ManagerJob job, ManagerJobHistoryChapterDef chapter)
    {
        var box = new Boxed<int> { Value = 0 };
        ManagerScene.RunToCompletion(
            HistoryProps(job).Worker.GetTargetForHistoryChapterCoroutine(job, 0, chapter, box)
        );
        return box.Value;
    }

    private static void ExpectCountThrowsForForeignChapter(ManagerJob job)
    {
        var box = new Boxed<int>();
        var worker = HistoryProps(job).Worker;
        _ = Expect.Throws<ArgumentException>(() =>
        {
            foreach (
                var _ in worker.GetCountForHistoryChapterCoroutine(
                    job,
                    0,
                    ManagerJobHistoryChapterDefOf.CM_HistoryProduction,
                    box
                )
            ) { }
        });
    }

    /// <summary>
    /// Every chapter the job's def lists must be countable, and only the stock chapter has a
    /// target.
    /// </summary>
    private static void ExpectChaptersResolve(ManagerJob job, int stockTarget)
    {
        foreach (var chapter in HistoryProps(job).chapters)
        {
            var count = Count(job, chapter);
            Expect.IsTrue(count >= 0, $"{chapter.defName} has count {count}.");
            var expectedTarget =
                chapter == ManagerJobHistoryChapterDefOf.CM_HistoryStock ? stockTarget : 0;
            Expect.AreEqual(expectedTarget, Target(job, chapter), chapter.defName);
        }
    }

    [Test]
    public static void HuntingChaptersResolveAndOnlyStockHasATarget()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<ManagerJob_Hunting>("CM_HuntingManager");
        job.TriggerThreshold.TargetCount = 123;

        ExpectChaptersResolve(job, 123);
        ExpectCountThrowsForForeignChapter(job);
    }

    [Test]
    public static void MiningChaptersResolveAndOnlyStockHasATarget()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<ManagerJob_Mining>("CM_MiningManager");
        job.TriggerThreshold.TargetCount = 123;

        ExpectChaptersResolve(job, 123);
        ExpectCountThrowsForForeignChapter(job);
    }

    [Test]
    public static void MiningChunksChapterIsTheYieldOfStockpiledChunks()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<ManagerJob_Mining>("CM_MiningManager");
        job.TriggerThreshold.ThresholdFilter.SetDisallowAll();
        job.TriggerThreshold.ThresholdFilter.SetAllow(ThingDefOf.Steel, true);
        var cell = scene.Cells(1, 1, -60, -40).First();
        _ = scene.AddStockpile([cell]);
        _ = scene.SpawnThing(SlagChunk, cell);
        var yield = job.GetCountInChunk(SlagChunk);
        Expect.IsTrue(yield > 0, "The chunk yields nothing.");

        Expect.AreEqual(yield, Count(job, ManagerJobHistoryChapterDefOf.CM_HistoryChunks));
        Expect.AreEqual(job.ChunksCachedValue.Value, yield);
    }

    [Test]
    public static void ForestryChaptersResolveAndOnlyStockHasATarget()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<ManagerJob_Forestry>("CM_ForestryManager");
        job.TriggerThreshold.TargetCount = 123;

        ExpectChaptersResolve(job, 123);
        ExpectCountThrowsForForeignChapter(job);
    }

    [Test]
    public static void ForagingChaptersResolveAndOnlyStockHasATarget()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<ManagerJob_Foraging>("CM_ForagingManager");
        job.TriggerThreshold.TargetCount = 123;

        ExpectChaptersResolve(job, 123);
        ExpectCountThrowsForForeignChapter(job);
    }

    [Test]
    public static void LivestockChaptersReportTheTriggersCountsAndTargets()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<ManagerJob_Livestock>("CM_LivestockManager");
        job.TriggerPawnKind.pawnKind = PawnKindDefOf.Muffalo;
        for (var i = 0; i < job.TriggerPawnKind.CountTargets.Length; i++)
        {
            job.TriggerPawnKind.CountTargets[i] = 10 + i;
        }
        _ = scene.SpawnAnimal(
            PawnKindDefOf.Muffalo,
            scene.Cells(1, 1, -50, -20).First(),
            Faction.OfPlayer
        );

        var total = 0;
        foreach (var chapter in HistoryProps(job).chapters)
        {
            var ageAndSex = chapter.defName switch
            {
                "CM_HistoryAdultFemale" => AgeAndSex.AdultFemale,
                "CM_HistoryAdultMale" => AgeAndSex.AdultMale,
                "CM_HistoryJuvenileFemale" => AgeAndSex.JuvenileFemale,
                "CM_HistoryJuvenileMale" => AgeAndSex.JuvenileMale,
                _ => throw new InvalidOperationException(chapter.defName),
            };
            var count = Count(job, chapter);
            Expect.AreEqual(job.TriggerPawnKind.GetCountFor(ageAndSex, cached: false), count);
            Expect.AreEqual(10 + (int)ageAndSex, Target(job, chapter));
            total += count;
        }
        Expect.AreEqual(1, total, "The tame muffalo is counted in exactly one chapter.");
        ExpectCountThrowsForForeignChapter(job);
    }

    [Test]
    public static void LivestockTargetIsZeroForAChapterOfAnotherJobType()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<ManagerJob_Livestock>("CM_LivestockManager");

        Expect.AreEqual(0, Target(job, ManagerJobHistoryChapterDefOf.CM_HistoryStock));
    }
}
