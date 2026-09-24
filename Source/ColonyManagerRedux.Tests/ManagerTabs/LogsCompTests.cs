// LogsCompTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class LogsCompTests
{
    private static List<int> Resize(int count, int capacity) =>
        ManagerTab_Logs.LogsComp.ResizeKeepingNewest(Enumerable.Range(1, count).ToList(), capacity);

    [Test]
    public static void ShrinkingKeepsTheNewestInOriginalOrder() =>
        Expect.That(() => Resize(5, 3).SequenceEqual([3, 4, 5]));

    [Test]
    public static void GrowingKeepsEverything() =>
        Expect.That(() => Resize(3, 10).SequenceEqual([1, 2, 3]));

    [Test]
    public static void SameCapacityKeepsEverything() =>
        Expect.That(() => Resize(3, 3).SequenceEqual([1, 2, 3]));

    [Test]
    public static void EmptyStaysEmpty() => Expect.IsEmpty(Resize(0, 3));

    // Regression guard (0.14.3): a KeepLogCount of 0 must not produce a zero-capacity buffer.
    [Test]
    public static void ZeroKeepLogCountIsDisabledWithCapacityOne()
    {
        Expect.AreEqual((1, false), ManagerTab_Logs.LogsComp.ComputeCapacity(0));
        Expect.AreEqual((1, false), ManagerTab_Logs.LogsComp.ComputeCapacity(-4));
    }

    [Test]
    public static void PositiveKeepLogCountIsEnabledWithThatCapacity() =>
        Expect.AreEqual((25, true), ManagerTab_Logs.LogsComp.ComputeCapacity(25));
}
