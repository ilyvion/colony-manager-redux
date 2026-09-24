// AreaRemovalTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// Deleting an area must make every job clear its references to it, via the
/// <c>AreaManager.NotifyEveryoneAreaRemoved</c> postfix.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class AreaRemovalTests
{
    private static void ExpectSingleAreaCleared<T>(
        string defName,
        Func<T, Area?> get,
        Action<T, Area?> set
    )
        where T : ManagerJob
    {
        using var scene = new ManagerScene();
        var doomed = scene.AddArea("Doomed", scene.Cells(2, 2));
        var survivor = scene.AddArea("Survivor", scene.Cells(2, 2, offsetX: 5));
        var withDoomed = scene.AddJob<T>(defName);
        var withSurvivor = scene.AddJob<T>(defName);
        set(withDoomed, doomed);
        set(withSurvivor, survivor);

        doomed.Delete();

        Expect.IsNull(get(withDoomed), "Reference to the deleted area is cleared");
        Expect.ReferencesAreEqual(survivor, get(withSurvivor));
    }

    [Test]
    public static void HuntingGroundsCleared() =>
        ExpectSingleAreaCleared<ManagerJob_Hunting>(
            "CM_HuntingManager",
            j => j.HuntingGrounds,
            (j, a) => j.HuntingGrounds = a
        );

    [Test]
    public static void ForagingAreaCleared() =>
        ExpectSingleAreaCleared<ManagerJob_Foraging>(
            "CM_ForagingManager",
            j => j.ForagingArea,
            (j, a) => j.ForagingArea = a
        );

    [Test]
    public static void MiningAreaCleared() =>
        ExpectSingleAreaCleared<ManagerJob_Mining>(
            "CM_MiningManager",
            j => j.MiningArea,
            (j, a) => j.MiningArea = a
        );

    [Test]
    public static void WorkbenchAreaCleared() =>
        ExpectSingleAreaCleared<ManagerJob_Production>(
            "CM_ProductionManager",
            j => j.WorkbenchArea,
            (j, a) => j.WorkbenchArea = a
        );

    [Test]
    public static void ForestryLoggingAreaCleared() =>
        ExpectSingleAreaCleared<ManagerJob_Forestry>(
            "CM_ForestryManager",
            j => j.LoggingArea,
            (j, a) => j.LoggingArea = a
        );

    [Test]
    public static void ForestryClearAreasDropsOnlyTheDeletedArea()
    {
        using var scene = new ManagerScene();
        var doomed = scene.AddArea("Doomed", scene.Cells(2, 2));
        var survivor = scene.AddArea("Survivor", scene.Cells(2, 2, offsetX: 5));
        var job = scene.AddJob<ManagerJob_Forestry>("CM_ForestryManager");
        _ = job.ClearAreas.Add(doomed);
        _ = job.ClearAreas.Add(survivor);

        doomed.Delete();

        Expect.AreEqual(1, job.ClearAreas.Count);
        Expect.IsTrue(job.ClearAreas.Contains(survivor));
    }

    [Test]
    public static void LivestockClearsEveryFieldReferencingTheArea()
    {
        using var scene = new ManagerScene();
        var doomed = scene.AddArea("Doomed", scene.Cells(2, 2));
        var survivor = scene.AddArea("Survivor", scene.Cells(2, 2, offsetX: 5));
        var job = scene.AddJob<ManagerJob_Livestock>("CM_LivestockManager");
        job.MilkArea = doomed;
        job.ShearArea = doomed;
        job.CullingArea = survivor;
        job.TameArea = doomed;
        job.TrainingArea = doomed;
        job.TrainedArea = survivor;
        job.RestrictArea[0] = doomed;
        job.RestrictArea[1] = survivor;
        job.RestrictArea[2] = doomed;

        doomed.Delete();

        Expect.IsNull(job.MilkArea);
        Expect.IsNull(job.ShearArea);
        Expect.IsNull(job.TameArea);
        Expect.IsNull(job.TrainingArea);
        Expect.ReferencesAreEqual(survivor, job.CullingArea);
        Expect.ReferencesAreEqual(survivor, job.TrainedArea);
        Expect.IsNull(job.RestrictArea[0]);
        Expect.ReferencesAreEqual(survivor, job.RestrictArea[1]);
        Expect.IsNull(job.RestrictArea[2]);
    }

    [Test]
    public static void JobsWithNoReferenceAreUntouched()
    {
        using var scene = new ManagerScene();
        var doomed = scene.AddArea("Doomed", scene.Cells(2, 2));
        var job = scene.AddJob<ManagerJob_Hunting>("CM_HuntingManager");
        job.HuntingGrounds = null;

        doomed.Delete();

        Expect.IsNull(job.HuntingGrounds);
    }
}
