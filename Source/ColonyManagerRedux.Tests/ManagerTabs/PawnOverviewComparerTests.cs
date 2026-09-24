// PawnOverviewComparerTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class PawnOverviewComparerTests
{
    private static int Compare((int, float) a, (int, float) b) =>
        ManagerTab_Overview.PawnColumnWorker_WorkPriorities.ComparePawnSuitability(a, b);

    [Test]
    public static void LowerPriorityNumberSortsFirst() =>
        Expect.LessThan(Compare((1, 0f), (2, 20f)), 0);

    [Test]
    public static void HigherSkillSortsFirstWithinSamePriority()
    {
        Expect.LessThan(Compare((1, 10f), (1, 5f)), 0);
        Expect.GreaterThan(Compare((1, 5f), (1, 10f)), 0);
    }

    [Test]
    public static void DisabledSortsAfterEnabledAndIncapableAfterDisabled()
    {
        Expect.LessThan(Compare((4, 0f), (short.MaxValue, 20f)), 0);
        Expect.LessThan(Compare((short.MaxValue, 0f), (short.MaxValue + 1, -1f)), 0);
    }

    // Pins the current behavior: the skill difference is truncated to an int, so skills less
    // than one whole point apart compare as equal.
    [Test]
    public static void SkillsLessThanOnePointApartCompareEqual()
    {
        Expect.AreEqual(0, Compare((1, 5.4f), (1, 5.0f)));
        Expect.AreEqual(0, Compare((1, 5.0f), (1, 4.2f)));
        Expect.AreEqual(-1, Compare((1, 5.0f), (1, 3.9f)));
    }
}
