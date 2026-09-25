// UtilitiesPlanningTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class UtilitiesPlanningTests
{
    private static readonly string[] Abcd = ["a", "b", "c", "d"];
    private static readonly string[] Abc = ["a", "b", "c"];
    private static readonly string[] Ab = ["a", "b"];
    private static readonly int[] OneToFour = [1, 2, 3, 4];

    // ---- WalkRunningCount ----

    [Test]
    public static void RemovalsRecordTheCountAfterEachOne()
    {
        var result = Utilities_Planning.WalkRunningCount(10, -1, 3, Abcd);

        Expect.AreEqual(3, result.Count());
        Expect.That(() => result.SequenceEqual([("a", 9), ("b", 8), ("c", 7)]));
    }

    [Test]
    public static void AdditionsCountUpwards() =>
        Expect.That(() =>
            Utilities_Planning.WalkRunningCount(2, 1, 2, Abc).SequenceEqual([("a", 3), ("b", 4)])
        );

    [Test]
    public static void LimitLargerThanCandidatesStopsAtTheEndOfTheCandidates() =>
        // Picking 5 out of 2 candidates only counts the 2 actually picked.
        Expect.That(() =>
            Utilities_Planning.WalkRunningCount(10, -1, 5, Ab).SequenceEqual([("a", 9), ("b", 8)])
        );

    [Test]
    public static void ZeroOrNegativeLimitPlansNothingAndNeverEnumerates()
    {
        var enumerations = 0;
        IEnumerable<int> Candidates()
        {
            enumerations++;
            yield return 1;
        }

        Expect.IsEmpty(Utilities_Planning.WalkRunningCount(10, -1, 0, Candidates()));
        Expect.IsEmpty(Utilities_Planning.WalkRunningCount(10, -1, -3, Candidates()));
        Expect.AreEqual(0, enumerations);
    }

    [Test]
    public static void NullLimitTakesEveryCandidate() =>
        // Used by TamePastTargets and by stopping already-picked designations.
        Expect.AreEqual(4, Utilities_Planning.WalkRunningCount(0, 1, null, OneToFour).Count());

    [Test]
    public static void CandidatesBeyondTheLimitAreNotPulled()
    {
        var pulled = 0;
        IEnumerable<int> Candidates()
        {
            for (var i = 0; i < 10; i++)
            {
                pulled++;
                yield return i;
            }
        }

        _ = Utilities_Planning.WalkRunningCount(0, 1, 2, Candidates()).ToList();

        Expect.AreEqual(2, pulled);
    }

    // ---- WalkAdditions ----

    private static List<Utilities_Planning.AdditionStep<string>> Additions(
        int start,
        string[] candidates,
        Func<string, int> yieldOf,
        int target,
        Func<bool>? canAddMore = null,
        Func<string, bool>? accept = null
    ) =>
        [
            .. Utilities_Planning.WalkAdditions(
                start,
                candidates,
                yieldOf,
                count => count >= target,
                canAddMore ?? (() => true),
                accept
            ),
        ];

    [Test]
    public static void AdditionsStopOnceTheTargetIsMet()
    {
        var steps = Additions(0, ["a", "b", "c", "d"], _ => 40, target: 100);

        // 40, 80, 120: the target check happens before each candidate, so the one that crosses
        // the target is still taken, but nothing after it.
        Expect.AreEqual(3, steps.Count);
        Expect.AreEqual(120, steps[2].CountAfter);
    }

    [Test]
    public static void AdditionsPlanNothingWhenTheTargetIsAlreadyMet() =>
        Expect.IsEmpty(Additions(100, ["a"], _ => 10, target: 100));

    [Test]
    public static void AdditionsStopWhenNoMoreCanBeAdded()
    {
        // The caller records what it accepts as it iterates, so the lazily read limit sees it.
        var planned = 0;
        var seen = new List<string>();
        foreach (
            var step in Utilities_Planning.WalkAdditions(
                0,
                Abc,
                _ => 1,
                _ => false,
                () => planned < 2
            )
        )
        {
            seen.Add(step.Item);
            planned++;
        }

        Expect.That(() => seen.SequenceEqual(["a", "b"]));
    }

    [Test]
    public static void RejectedCandidatesAreReportedWithoutChangingTheCount()
    {
        var steps = Additions(
            0,
            ["a", "skip", "b"],
            _ => 10,
            target: 100,
            accept: c => c != "skip"
        );

        Expect.AreEqual(3, steps.Count);
        Expect.IsTrue(steps[0].Accepted);
        Expect.AreEqual(10, steps[0].CountAfter);
        Expect.IsFalse(steps[1].Accepted);
        Expect.AreEqual(10, steps[1].CountAfter);
        Expect.AreEqual(20, steps[2].CountAfter);
    }

    [Test]
    public static void AdditionStepsCarryTheCandidatesYield()
    {
        var steps = Additions(0, ["aa", "bbbb"], c => c.Length, target: 100);

        Expect.AreEqual(2, steps[0].Yield);
        Expect.AreEqual(4, steps[1].Yield);
        Expect.AreEqual(6, steps[1].CountAfter);
    }

    [Test]
    public static void RejectedCandidatesDoNotCountTowardsTheTarget()
    {
        // Every candidate is rejected, so the target is never met and all are visited.
        var steps = Additions(0, ["a", "b", "c"], _ => 100, target: 50, accept: _ => false);

        Expect.AreEqual(3, steps.Count);
        Expect.All(steps, s => !s.Accepted);
    }
}
