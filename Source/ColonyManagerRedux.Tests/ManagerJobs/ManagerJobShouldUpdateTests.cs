// ManagerJobShouldUpdateTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ManagerJobShouldUpdateTests
{
    [Test]
    public static void NotDueWhenLastActionPlusIntervalEqualsNow() =>
        Expect.IsFalse(ManagerJob.ComputeShouldUpdate(false, 100, 50, 150));

    [Test]
    public static void DueOneTickAfterLastActionPlusInterval() =>
        Expect.IsTrue(ManagerJob.ComputeShouldUpdate(false, 100, 50, 151));

    [Test]
    public static void NotDueBeforeInterval() =>
        Expect.IsFalse(ManagerJob.ComputeShouldUpdate(false, 100, 50, 120));

    [Test]
    public static void DueWhenNeverRun() =>
        Expect.IsTrue(ManagerJob.ComputeShouldUpdate(false, -1, 50, 0));

    [Test]
    public static void ForceRequestOverridesInterval() =>
        Expect.IsTrue(ManagerJob.ComputeShouldUpdate(true, 100, 50, 101));
}
