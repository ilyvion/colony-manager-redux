// ManagerTabProductionSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.Playing)]
internal sealed class ManagerTabProductionSceneTests
{
    // Regression guard: the grouped recipe list used to be rebuilt on every GUI event, which
    // dropped the frame rate to around 10 FPS while it was shown.
    [Test]
    public static void WorkbenchGroupsAreReusedUntilTheTabRefreshes()
    {
        using var scene = new ManagerScene();
        var tab = new ManagerTab_Production(scene.Manager);
        tab.PreOpen();

        var first = tab.WorkbenchGroups;
        Expect.IsTrue(ReferenceEquals(first, tab.WorkbenchGroups));

        tab.PreOpen();
        Expect.IsFalse(ReferenceEquals(first, tab.WorkbenchGroups));
    }
}
