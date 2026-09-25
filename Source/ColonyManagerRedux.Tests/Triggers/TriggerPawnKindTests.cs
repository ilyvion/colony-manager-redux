// TriggerPawnKindTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class TriggerPawnKindTests
{
    [Test]
    public static void ComputeRemainingCountsSubtractsCulledFromTame() =>
        Expect.IsTrue(
            Trigger_PawnKind.ComputeRemainingCounts([5, 5, 5, 5], [1, 2, 3, 4]).Contains(4)
        );

    [Test]
    public static void ComputeRemainingCountsMatchesElementwise()
    {
        var result = Trigger_PawnKind.ComputeRemainingCounts([5, 5, 5, 5], [1, 2, 3, 4]);

        Expect.AreEqual(4, result.Length);
        Expect.AreEqual(4, result[0]);
        Expect.AreEqual(3, result[1]);
        Expect.AreEqual(2, result[2]);
        Expect.AreEqual(1, result[3]);
    }

    [Test]
    public static void ComputeRemainingCountsCanGoNegativeWhenCulledExceedsTame() =>
        // No clamping in the original code - pin down that this is intentional (used for progress
        // bars, which are expected to tolerate a temporary over-culled state).
        Expect.AreEqual(-3, Trigger_PawnKind.ComputeRemainingCounts([2], [5])[0]);

    [Test]
    public static void HasWorkToDoWhenATargetIsNotMet()
    {
        var trainingChecked = false;
        Expect.IsTrue(
            Trigger_PawnKind.HasWorkToDo(
                [5, 5, 5, 5],
                [5, 5, 4, 5],
                () =>
                {
                    trainingChecked = true;
                    return true;
                }
            )
        );
        Expect.IsFalse(trainingChecked);
    }

    [Test]
    public static void HasWorkToDoWhenTrainingIsStillToBeSet() =>
        Expect.IsTrue(Trigger_PawnKind.HasWorkToDo([5, 5, 5, 5], [5, 5, 5, 5], () => false));

    [Test]
    public static void HasNoWorkToDoWhenTargetsAreMetAndTrainingIsSet() =>
        Expect.IsFalse(Trigger_PawnKind.HasWorkToDo([5, 5, 5, 5], [5, 5, 5, 5], () => true));

    [Test]
    public static void AllTargetsMetTrueForEmptyArrays() =>
        Expect.IsTrue(Trigger_PawnKind.AllTargetsMet([], []));

    [Test]
    public static void AllTargetsMetTrueWhenEveryTargetMatches() =>
        Expect.IsTrue(Trigger_PawnKind.AllTargetsMet([5, 5, 5, 5], [5, 5, 5, 5]));

    [Test]
    public static void AllTargetsMetFalseOnSingleMismatch() =>
        Expect.IsFalse(Trigger_PawnKind.AllTargetsMet([5, 5, 5, 5], [5, 5, 4, 5]));

    [Test]
    public static void AllTargetsMetFalseWhenAllMismatched() =>
        Expect.IsFalse(Trigger_PawnKind.AllTargetsMet([5, 5, 5, 5], [1, 2, 3, 4]));
}
