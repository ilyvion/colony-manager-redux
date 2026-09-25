// ManagerJobForestryClearAreaSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.Playing)]
internal sealed class ManagerJobForestryClearAreaSceneTests
{
    [Test]
    public static void UpdateClearAreasDropsAreasThatNoLongerExistOnTheMap()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<ManagerJob_Forestry>("CM_ForestryManager");
        var existing = scene.AddArea("CMRTest-clear", scene.Cells(2, 2));
        // Never registered with the area manager, so it is not one of the map's areas.
        var gone = new Area_Allowed(scene.Map.areaManager, "CMRTest-gone");
        _ = job.ClearAreas.Add(existing);
        _ = job.ClearAreas.Add(gone);

        job.UpdateClearAreas();

        Expect.AreEqual(1, job.ClearAreas.Count);
        Expect.IsTrue(job.ClearAreas.Contains(existing));
    }

    [Test]
    public static void UpdateClearAreasKeepsEveryExistingArea()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<ManagerJob_Forestry>("CM_ForestryManager");
        _ = job.ClearAreas.Add(scene.AddArea("CMRTest-a", scene.Cells(2, 2)));
        _ = job.ClearAreas.Add(scene.AddArea("CMRTest-b", scene.Cells(2, 2, offsetX: 5)));

        job.UpdateClearAreas();

        Expect.AreEqual(2, job.ClearAreas.Count);
    }
}
