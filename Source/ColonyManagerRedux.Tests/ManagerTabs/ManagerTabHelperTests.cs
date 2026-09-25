// ManagerTabHelperTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The list and threshold logic the Livestock and Logs tabs run before drawing.
/// </summary>
[TestFixture(TestType.MainMenu)]
internal sealed class ManagerTabHelperTests
{
    // Livestock: available pawn kinds

    private static string Kinds(string[] wild, string[] colony, string?[] managed) =>
        string.Join(",", ManagerTab_Livestock.AvailableKinds(wild, colony, managed, s => s));

    [Test]
    public static void KindInBothWildAndColonyAppearsOnce() =>
        Expect.AreEqual("a,b,c", Kinds(["a", "b"], ["b", "c"], []));

    [Test]
    public static void ManagedKindsAreRemoved() =>
        Expect.AreEqual("a,c", Kinds(["a", "b"], ["c"], ["b"]));

    [Test]
    public static void ManagedEntryWithoutAKindDoesNotBreakTheList() =>
        Expect.AreEqual("a,b", Kinds(["a"], ["b"], [null]));

    [Test]
    public static void KindsAreOrderedByLabelIgnoringCase() =>
        Expect.AreEqual("Alpaca,bear,Cow,dog", Kinds(["dog", "Alpaca"], ["Cow", "bear"], []));

    [Test]
    public static void NoKindsGivesAnEmptyList() =>
        Expect.IsEmpty(Kinds([], [], []).Split(',', StringSplitOptions.RemoveEmptyEntries));

    // Logs

    private static string Logs(
        int[] oldestFirst,
        Func<int, bool>? forSelectedJob = null,
        bool showNoWork = true
    ) =>
        string.Join(
            ",",
            ManagerTab_Logs.VisibleLogs(oldestFirst, forSelectedJob, showNoWork, l => l > 2)
        );

    [Test]
    public static void LogsAreListedNewestFirst() => Expect.AreEqual("4,3,2,1", Logs([1, 2, 3, 4]));

    [Test]
    public static void OnlyTheSelectedJobsLogsAreListed() =>
        Expect.AreEqual("3,1", Logs([1, 2, 3, 4], forSelectedJob: l => l % 2 == 1));

    [Test]
    public static void LogsThatDidNoWorkAreHiddenUnlessAsked() =>
        Expect.AreEqual("4,3", Logs([1, 2, 3, 4], showNoWork: false));

    [Test]
    public static void JobFilterAndNoWorkFilterCombine() =>
        Expect.AreEqual("3", Logs([1, 2, 3, 4], l => l % 2 == 1, showNoWork: false));

    [Test]
    public static void NoLogsGivesAnEmptyList() => Expect.AreEqual("", Logs([]));

    // Livestock: milk and wool fullness ordering

    [Test]
    public static void FullnessDifferencesBelowOnePercentStillOrder() =>
        Expect.IsTrue(
            ManagerTab_Livestock.PawnColumnWorker_Livestock.CompareFullness(0.994f, 0.991f) > 0
        );

    [Test]
    public static void EqualFullnessComparesEqual() =>
        Expect.AreEqual(
            0,
            ManagerTab_Livestock.PawnColumnWorker_Livestock.CompareFullness(0.5f, 0.5f)
        );

    [Test]
    public static void FullnessComparisonIsSymmetric()
    {
        var forwards = ManagerTab_Livestock.PawnColumnWorker_Livestock.CompareFullness(0.2f, 0.7f);
        var backwards = ManagerTab_Livestock.PawnColumnWorker_Livestock.CompareFullness(0.7f, 0.2f);

        Expect.IsTrue(forwards < 0, "lower sorts first");
        Expect.AreEqual(-forwards, backwards);
    }

    [Test]
    public static void NaNFullnessDoesNotThrow() =>
        Expect.AreEqual(
            0,
            ManagerTab_Livestock.PawnColumnWorker_Livestock.CompareFullness(float.NaN, float.NaN)
        );
}
