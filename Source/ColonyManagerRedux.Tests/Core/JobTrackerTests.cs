// JobTrackerTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class JobTrackerTests
{
    [Test]
    public static void ComputeCleanedPrioritiesRenumbersDenselyFromZero()
    {
        var result = JobTracker.ComputeCleanedPriorities([5, 2, 9]);
        // 2 (index 1) is smallest -> 0, 5 (index 0) is middle -> 1, 9 (index 2) is largest -> 2.
        Expect.AreEqual(1, result[0]);
        Expect.AreEqual(0, result[1]);
        Expect.AreEqual(2, result[2]);
    }

    [Test]
    public static void ComputeCleanedPrioritiesPreservesRelativeOrderOnTies()
    {
        // Equal priorities must keep their original relative (list) order, matching the stable
        // sort this replaced.
        var result = JobTracker.ComputeCleanedPriorities([3, 3, 3]);
        Expect.AreEqual(0, result[0]);
        Expect.AreEqual(1, result[1]);
        Expect.AreEqual(2, result[2]);
    }

    [Test]
    public static void ComputeCleanedPrioritiesOnAlreadyDenseListIsNoOp()
    {
        var result = JobTracker.ComputeCleanedPriorities([0, 1, 2]);
        Expect.AreEqual(0, result[0]);
        Expect.AreEqual(1, result[1]);
        Expect.AreEqual(2, result[2]);
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
        Expect.AreEqual(0, result[2]);
        Expect.AreEqual(1, result[0]);
        Expect.AreEqual(2, result[1]);
    }

    [Test]
    public static void ComputeReprioritizedPrioritiesMovesJobToBottom()
    {
        // Move the first job (index 0) to the bottom via a very-large sentinel, as
        // BottomPriority does.
        var result = JobTracker.ComputeReprioritizedPriorities([0, 1, 2], 0, 100);

        Expect.AreEqual(2, result[0]);
        Expect.AreEqual(0, result[1]);
        Expect.AreEqual(1, result[2]);
    }

    [Test]
    public static void ComputeReprioritizedPrioritiesLeavesOthersUntouchedWhenMovedStaysInPlace()
    {
        // Moving a job to a priority that keeps it in the same relative position should not
        // disturb the others.
        var result = JobTracker.ComputeReprioritizedPriorities([0, 1, 2], 1, 1);
        Expect.AreEqual(0, result[0]);
        Expect.AreEqual(1, result[1]);
        Expect.AreEqual(2, result[2]);
    }

    [Test]
    public static void ComputeSortOrderReturnsAscendingIndices()
    {
        // Physical positions 1 (value 2), 0 (value 5), 2 (value 9) is the ascending-by-value
        // visiting order.
        var order = JobTracker.ComputeSortOrder([5, 2, 9]);
        Expect.AreEqual(1, order[0]);
        Expect.AreEqual(0, order[1]);
        Expect.AreEqual(2, order[2]);
    }

    [Test]
    public static void ComputeSortOrderPreservesOriginalPositionOnTies()
    {
        var order = JobTracker.ComputeSortOrder([3, 3, 3]);
        Expect.AreEqual(0, order[0]);
        Expect.AreEqual(1, order[1]);
        Expect.AreEqual(2, order[2]);
    }

    [Test]
    public static void ComputeSortOrderOnAlreadySortedListIsIdentity()
    {
        var order = JobTracker.ComputeSortOrder([0, 1, 2]);
        Expect.AreEqual(0, order[0]);
        Expect.AreEqual(1, order[1]);
        Expect.AreEqual(2, order[2]);
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
        Expect.AreEqual(4, order[0]); // A2, priority 0
        Expect.AreEqual(1, order[1]); // B0, priority 1
        Expect.AreEqual(0, order[2]); // A0, priority 2
        Expect.AreEqual(3, order[3]); // B1, priority 3
        Expect.AreEqual(2, order[4]); // A1, priority 4
    }

    [Test]
    public static void ShouldLogJobRunSuppressesWhenCompletedBeforeAndAfter() =>
        Expect.IsFalse(JobTracker.ShouldLogJobRun(true, ManagerJobState.Completed));

    [Test]
    public static void ShouldLogJobRunLogsWhenNotCompletedBeforeButCompletedAfter() =>
        Expect.IsTrue(JobTracker.ShouldLogJobRun(false, ManagerJobState.Completed));

    [Test]
    public static void ShouldLogJobRunLogsWhenCompletedBeforeButNotAfter() =>
        Expect.IsTrue(JobTracker.ShouldLogJobRun(true, ManagerJobState.Active));

    [Test]
    public static void ShouldLogJobRunLogsWhenNotCompletedBeforeOrAfter() =>
        Expect.IsTrue(JobTracker.ShouldLogJobRun(false, ManagerJobState.Active));

    [Test]
    public static void FindAdjacentPriorityFindsClosestLowerValue() =>
        Expect.AreEqual(2, JobTracker.FindAdjacentPriority([0, 2, 5, 8], 5, lower: true));

    [Test]
    public static void FindAdjacentPriorityFindsClosestHigherValue() =>
        Expect.AreEqual(8, JobTracker.FindAdjacentPriority([0, 2, 5, 8], 5, lower: false));

    [Test]
    public static void FindAdjacentPriorityThrowsWhenAlreadyAtTop() =>
        Expect.Throws<InvalidOperationException>(() =>
            JobTracker.FindAdjacentPriority([0, 2, 5], 0, lower: true)
        );

    [Test]
    public static void FindAdjacentPriorityThrowsWhenAlreadyAtBottom() =>
        Expect.Throws<InvalidOperationException>(() =>
            JobTracker.FindAdjacentPriority([0, 2, 5], 5, lower: false)
        );
}
