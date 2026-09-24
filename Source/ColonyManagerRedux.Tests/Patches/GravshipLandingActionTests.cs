// GravshipLandingActionTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

#if !v1_5
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class GravshipLandingActionTests
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
            Expect.AreEqual(GravshipLandingAction.Import, action);
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
            Expect.AreEqual(GravshipLandingAction.Import, action);
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
        Expect.AreEqual(GravshipLandingAction.Import, action);
    }

    [Test]
    public static void ConflictWithAlwaysAskPromptsThePlayer()
    {
        var action = Verse_WorldComponent_GravshipController_LandingEnded.DetermineLandingAction(
            localJobCount: 1,
            gravshipJobCount: 1,
            GravshipJobConflictResolution.AlwaysAsk
        );
        Expect.AreEqual(GravshipLandingAction.Ask, action);
    }

    [Test]
    public static void ConflictWithKeepLocalJobsSettingKeepsLocalOnly()
    {
        var action = Verse_WorldComponent_GravshipController_LandingEnded.DetermineLandingAction(
            localJobCount: 1,
            gravshipJobCount: 1,
            GravshipJobConflictResolution.KeepLocalJobs
        );
        Expect.AreEqual(GravshipLandingAction.KeepLocalOnly, action);
    }

    [Test]
    public static void ConflictWithKeepGravshipJobsSettingKeepsGravshipOnly()
    {
        var action = Verse_WorldComponent_GravshipController_LandingEnded.DetermineLandingAction(
            localJobCount: 1,
            gravshipJobCount: 1,
            GravshipJobConflictResolution.KeepGravshipJobs
        );
        Expect.AreEqual(GravshipLandingAction.KeepGravshipOnly, action);
    }

    [Test]
    public static void ConflictWithMergeJobsSettingImportsOnTopOfLocal()
    {
        var action = Verse_WorldComponent_GravshipController_LandingEnded.DetermineLandingAction(
            localJobCount: 1,
            gravshipJobCount: 1,
            GravshipJobConflictResolution.MergeJobs
        );
        Expect.AreEqual(GravshipLandingAction.Import, action);
    }
}
#endif // !v1_5
