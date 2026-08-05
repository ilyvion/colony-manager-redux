// GravshipLandingActionTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

#if !v1_5
using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class GravshipLandingActionTests
{
    [Test]
    public static void NoLocalJobsAlwaysImports()
    {
        // No local jobs to conflict with, regardless of the configured resolution: just import
        // whatever the gravship brought.
        foreach (
            var resolution in (GravshipJobConflictResolution[])
                Enum.GetValues(typeof(GravshipJobConflictResolution))
        )
        {
            var action =
                Verse_WorldComponent_GravshipController_LandingEnded.DetermineLandingAction(
                    localJobCount: 0,
                    gravshipJobCount: 3,
                    resolution
                );
            Assert.That(action).Is.EqualTo(GravshipLandingAction.Import);
        }
    }

    [Test]
    public static void NoGravshipJobsAlwaysImports()
    {
        // Nothing incoming to conflict with either: same as above, but from the other side.
        foreach (
            var resolution in (GravshipJobConflictResolution[])
                Enum.GetValues(typeof(GravshipJobConflictResolution))
        )
        {
            var action =
                Verse_WorldComponent_GravshipController_LandingEnded.DetermineLandingAction(
                    localJobCount: 2,
                    gravshipJobCount: 0,
                    resolution
                );
            Assert.That(action).Is.EqualTo(GravshipLandingAction.Import);
        }
    }

    [Test]
    public static void BothEmptyImports()
    {
        var action = Verse_WorldComponent_GravshipController_LandingEnded.DetermineLandingAction(
            localJobCount: 0,
            gravshipJobCount: 0,
            GravshipJobConflictResolution.AlwaysAsk
        );
        Assert.That(action).Is.EqualTo(GravshipLandingAction.Import);
    }

    [Test]
    public static void ConflictWithAlwaysAskPromptsThePlayer()
    {
        var action = Verse_WorldComponent_GravshipController_LandingEnded.DetermineLandingAction(
            localJobCount: 1,
            gravshipJobCount: 1,
            GravshipJobConflictResolution.AlwaysAsk
        );
        Assert.That(action).Is.EqualTo(GravshipLandingAction.Ask);
    }

    [Test]
    public static void ConflictWithKeepLocalJobsSettingKeepsLocalOnly()
    {
        var action = Verse_WorldComponent_GravshipController_LandingEnded.DetermineLandingAction(
            localJobCount: 1,
            gravshipJobCount: 1,
            GravshipJobConflictResolution.KeepLocalJobs
        );
        Assert.That(action).Is.EqualTo(GravshipLandingAction.KeepLocalOnly);
    }

    [Test]
    public static void ConflictWithKeepGravshipJobsSettingKeepsGravshipOnly()
    {
        var action = Verse_WorldComponent_GravshipController_LandingEnded.DetermineLandingAction(
            localJobCount: 1,
            gravshipJobCount: 1,
            GravshipJobConflictResolution.KeepGravshipJobs
        );
        Assert.That(action).Is.EqualTo(GravshipLandingAction.KeepGravshipOnly);
    }

    [Test]
    public static void ConflictWithMergeJobsSettingImportsOnTopOfLocal()
    {
        var action = Verse_WorldComponent_GravshipController_LandingEnded.DetermineLandingAction(
            localJobCount: 1,
            gravshipJobCount: 1,
            GravshipJobConflictResolution.MergeJobs
        );
        Assert.That(action).Is.EqualTo(GravshipLandingAction.Import);
    }
}
#endif // !v1_5
