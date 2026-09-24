// ManagerJobScribeTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// Scribing of <see cref="ManagerJob"/> in the same-game mode (normal saves) and the template
/// mode (import/export), and the update-interval reconciliation on load.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobScribeTests
{
    private const string LoadingVarsWarning = "during LoadingVars";

    private static TestManagerJobA MakeJob(ManagerScene scene) =>
        new(scene.Manager)
        {
            IsManaged = true,
            Priority = 5,
            IsSuspended = true,
            State = ManagerJobState.Completed,
        };

    private static List<ManagerJob> RoundTripInTemplateMode(
        ManagerScene scene,
        List<ManagerJob> jobs
    )
    {
        var manager = scene.Manager;
        var previousMap = manager.ScribeSameMapData;
        var previousGame = manager.ScribeSameGameData;
        manager.ScribeSameMapData = false;
        manager.ScribeSameGameData = false;
        try
        {
            return ScribeRoundTrip.RoundTripJobs(manager, jobs);
        }
        finally
        {
            manager.ScribeSameMapData = previousMap;
            manager.ScribeSameGameData = previousGame;
        }
    }

    [Test]
    public static void SameGameRoundTripKeepsStateIdentityAndManager()
    {
        using var scene = new ManagerScene();
        var job = MakeJob(scene);
        job.Touch();

        var loaded = ScribeRoundTrip.RoundTripJobs(scene.Manager, [job]);

        Expect.AreEqual(1, loaded.Count);
        var copy = loaded[0];
        Expect.AreEqual(5, copy.Priority);
        Expect.IsTrue(copy.IsSuspended);
        Expect.AreEqual(ManagerJobState.Completed, copy.JobState);
        Expect.AreEqual(job.LoadID, copy.LoadID);
        Expect.IsTrue(copy.HasBeenUpdated);
        Expect.IsTrue(copy.IsManaged);
        Expect.ReferencesAreEqual(scene.Manager, copy.Manager);
    }

    [Test]
    [WarningsAllowed(LoadingVarsWarning)]
    public static void TemplateModeDropsGameStateAndAssignsANewLoadId()
    {
        using var scene = new ManagerScene();
        var job = MakeJob(scene);

        var loaded = RoundTripInTemplateMode(scene, [job]);

        Expect.AreEqual(1, loaded.Count);
        var copy = loaded[0];
        Expect.AreEqual(0, copy.Priority);
        Expect.IsFalse(copy.IsSuspended);
        Expect.AreEqual(ManagerJobState.Active, copy.JobState);
        Expect.AreNotEqual(job.LoadID, copy.LoadID);
        Expect.IsFalse(copy.IsManaged, "Import marks it managed, not the load");
        Expect.IsTrue(scene.Manager.ScribeSameGameData);
        Expect.IsTrue(scene.Manager.ScribeSameMapData);
    }

    [Test]
    public static void SavedIntervalIsRestoredFromTheOptions()
    {
        using var scene = new ManagerScene();
        var job = MakeJob(scene);
        var longest = Utilities.UpdateIntervalOptions.OrderBy(i => i.Ticks).Last();
        job.UpdateInterval = longest;

        var loaded = ScribeRoundTrip.RoundTripJobs(scene.Manager, [job]);

        Expect.AreEqual(longest.Ticks, loaded[0].UpdateInterval.Ticks);
    }

    [Test]
    public static void UnknownSavedIntervalFallsBackToTheDefault()
    {
        using var scene = new ManagerScene();
        var job = MakeJob(scene);
        job.UpdateInterval = new UpdateInterval(12345, "Not an option");

        var loaded = ScribeRoundTrip.RoundTripJobs(scene.Manager, [job]);

        Expect.AreEqual(
            ColonyManagerReduxMod.Settings.DefaultUpdateInterval.Ticks,
            loaded[0].UpdateInterval.Ticks
        );
    }

    [Test]
    public static void CustomIntervalRemovedFromSettingsFallsBackToTheDefault()
    {
        using var scene = new ManagerScene();
        var settings = ColonyManagerReduxMod.Settings;
        var previous = settings.CustomUpdateIntervalTickList;
        const int customTicks = 7777;
        try
        {
            settings.CustomUpdateIntervalTickList = [customTicks];
            var job = MakeJob(scene);
            job.UpdateInterval = Utilities.UpdateIntervalOptions.First(i => i.Ticks == customTicks);

            var xml = ScribeRoundTrip.Save(
                new ScribeRoundTrip.ManagerHarness(
                    scene.Manager,
                    new ScribeRoundTrip.JobListHarness(scene.Manager, [job])
                )
            );
            var withCustom = new ScribeRoundTrip.JobListHarness(scene.Manager);
            ScribeRoundTrip.Load(
                xml,
                new ScribeRoundTrip.ManagerHarness(scene.Manager, withCustom)
            );
            Expect.AreEqual(customTicks, withCustom.Jobs[0].UpdateInterval.Ticks);

            settings.CustomUpdateIntervalTickList = [];
            var withoutCustom = new ScribeRoundTrip.JobListHarness(scene.Manager);
            ScribeRoundTrip.Load(
                xml,
                new ScribeRoundTrip.ManagerHarness(scene.Manager, withoutCustom)
            );
            Expect.AreEqual(
                settings.DefaultUpdateInterval.Ticks,
                withoutCustom.Jobs[0].UpdateInterval.Ticks
            );
        }
        finally
        {
            settings.CustomUpdateIntervalTickList = previous;
        }
    }
}
