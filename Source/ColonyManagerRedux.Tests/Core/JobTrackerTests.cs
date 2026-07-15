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
    public static void ComputeSortOrderReturnsAscendingIndices()
    {
        // Physical positions 1 (value 2), 0 (value 5), 2 (value 9) is the ascending-by-value
        // visiting order.
        var order = JobTracker.ComputeSortOrder([5, 2, 9]);
        Assert.That(order[0]).Is.EqualTo(1);
        Assert.That(order[1]).Is.EqualTo(0);
        Assert.That(order[2]).Is.EqualTo(2);
    }

    [Test]
    public static void ComputeSortOrderPreservesOriginalPositionOnTies()
    {
        var order = JobTracker.ComputeSortOrder([3, 3, 3]);
        Assert.That(order[0]).Is.EqualTo(0);
        Assert.That(order[1]).Is.EqualTo(1);
        Assert.That(order[2]).Is.EqualTo(2);
    }

    [Test]
    public static void ComputeSortOrderOnAlreadySortedListIsIdentity()
    {
        var order = JobTracker.ComputeSortOrder([0, 1, 2]);
        Assert.That(order[0]).Is.EqualTo(0);
        Assert.That(order[1]).Is.EqualTo(1);
        Assert.That(order[2]).Is.EqualTo(2);
    }

    [Test]
    public static void ComputeSortOrderRecoversPriorityOrderAfterReprioritizeScramblesPhysicalOrder()
    {
        // Regression guard for the JobTracker physical-sort invariant: Reprioritize only mutates
        // Priority fields on same-type jobs without moving list entries, so the physical list can
        // end up out of order relative to priority (e.g. moving the physically-last job of a type
        // to top priority). CleanPriorities uses ComputeSortOrder on the resulting priorities to
        // physically re-sort the backing list back into priority order; this reproduces that
        // scenario directly: physical order [A0, B0, A1, B1, A2] with priorities
        // [2, 1, 4, 3, 0] (A2 was just moved to top) should sort back to
        // [A2, B0, A0, B1, A1].
        var order = JobTracker.ComputeSortOrder([2, 1, 4, 3, 0]);
        Assert.That(order[0]).Is.EqualTo(4); // A2, priority 0
        Assert.That(order[1]).Is.EqualTo(1); // B0, priority 1
        Assert.That(order[2]).Is.EqualTo(0); // A0, priority 2
        Assert.That(order[3]).Is.EqualTo(3); // B1, priority 3
        Assert.That(order[4]).Is.EqualTo(2); // A1, priority 4
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
