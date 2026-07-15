// JobTrackerTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class JobTrackerTests
{
    [Test]
    public static void ComputeCleanedPrioritiesRenumbersDenselyFromZero()
    {
        var result = JobTracker.ComputeCleanedPriorities([5, 2, 9]);
        // 2 (index 1) is smallest -> 0, 5 (index 0) is middle -> 1, 9 (index 2) is largest -> 2.
        Assert.That(result[0]).Is.EqualTo(1);
        Assert.That(result[1]).Is.EqualTo(0);
        Assert.That(result[2]).Is.EqualTo(2);
    }

    [Test]
    public static void ComputeCleanedPrioritiesPreservesRelativeOrderOnTies()
    {
        // Equal priorities must keep their original relative (list) order, matching the stable
        // sort this replaced.
        var result = JobTracker.ComputeCleanedPriorities([3, 3, 3]);
        Assert.That(result[0]).Is.EqualTo(0);
        Assert.That(result[1]).Is.EqualTo(1);
        Assert.That(result[2]).Is.EqualTo(2);
    }

    [Test]
    public static void ComputeCleanedPrioritiesOnAlreadyDenseListIsNoOp()
    {
        var result = JobTracker.ComputeCleanedPriorities([0, 1, 2]);
        Assert.That(result[0]).Is.EqualTo(0);
        Assert.That(result[1]).Is.EqualTo(1);
        Assert.That(result[2]).Is.EqualTo(2);
    }

    [Test]
    public static void ComputeReprioritizedPrioritiesMovesJobToTop()
    {
        // Three jobs with ascending priorities 0, 1, 2; move the last one (index 2) to the top
        // via a very-negative sentinel, as TopPriority does.
        var result = JobTracker.ComputeReprioritizedPriorities([0, 1, 2], 2, -1);

        // The moved job (originally at index 2) now sorts first, so it receives the smallest
        // old priority value (0); the untouched jobs shift down but keep the remaining old
        // values in their original relative order.
        Assert.That(result[2]).Is.EqualTo(0);
        Assert.That(result[0]).Is.EqualTo(1);
        Assert.That(result[1]).Is.EqualTo(2);
    }

    [Test]
    public static void ComputeReprioritizedPrioritiesMovesJobToBottom()
    {
        // Move the first job (index 0) to the bottom via a very-large sentinel, as
        // BottomPriority does.
        var result = JobTracker.ComputeReprioritizedPriorities([0, 1, 2], 0, 100);

        Assert.That(result[0]).Is.EqualTo(2);
        Assert.That(result[1]).Is.EqualTo(0);
        Assert.That(result[2]).Is.EqualTo(1);
    }

    [Test]
    public static void ComputeReprioritizedPrioritiesLeavesOthersUntouchedWhenMovedStaysInPlace()
    {
        // Moving a job to a priority that keeps it in the same relative position should not
        // disturb the others.
        var result = JobTracker.ComputeReprioritizedPriorities([0, 1, 2], 1, 1);
        Assert.That(result[0]).Is.EqualTo(0);
        Assert.That(result[1]).Is.EqualTo(1);
        Assert.That(result[2]).Is.EqualTo(2);
    }

    [Test]
    public static void ShouldLogJobRunSuppressesWhenCompletedBeforeAndAfter() =>
        Assert.That(JobTracker.ShouldLogJobRun(true, ManagerJobState.Completed)).Is.False();

    [Test]
    public static void ShouldLogJobRunLogsWhenNotCompletedBeforeButCompletedAfter() =>
        Assert.That(JobTracker.ShouldLogJobRun(false, ManagerJobState.Completed)).Is.True();

    [Test]
    public static void ShouldLogJobRunLogsWhenCompletedBeforeButNotAfter() =>
        Assert.That(JobTracker.ShouldLogJobRun(true, ManagerJobState.Active)).Is.True();

    [Test]
    public static void ShouldLogJobRunLogsWhenNotCompletedBeforeOrAfter() =>
        Assert.That(JobTracker.ShouldLogJobRun(false, ManagerJobState.Active)).Is.True();

    [Test]
    public static void FindAdjacentPriorityFindsClosestLowerValue() =>
        Assert.That(JobTracker.FindAdjacentPriority([0, 2, 5, 8], 5, lower: true)).Is.EqualTo(2);

    [Test]
    public static void FindAdjacentPriorityFindsClosestHigherValue() =>
        Assert.That(JobTracker.FindAdjacentPriority([0, 2, 5, 8], 5, lower: false)).Is.EqualTo(8);

    [Test]
    [ShouldThrow(typeof(InvalidOperationException))]
    public static void FindAdjacentPriorityThrowsWhenAlreadyAtTop() =>
        JobTracker.FindAdjacentPriority([0, 2, 5], 0, lower: true);

    [Test]
    [ShouldThrow(typeof(InvalidOperationException))]
    public static void FindAdjacentPriorityThrowsWhenAlreadyAtBottom() =>
        JobTracker.FindAdjacentPriority([0, 2, 5], 5, lower: false);
}
