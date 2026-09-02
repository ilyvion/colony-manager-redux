// ManagerTabOverviewTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;
using static ColonyManagerRedux.Managers.ManagerTab_Overview;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class ManagerTabOverviewTests
{
    private readonly struct TestJob(
        ManagerDef def,
        bool hasException,
        bool isSuspended,
        bool isCompleted,
        string? group
    )
    {
        public ManagerDef Def { get; } = def;
        public bool HasException { get; } = hasException;
        public bool IsSuspended { get; } = isSuspended;
        public bool IsCompleted { get; } = isCompleted;
        public string? Group { get; } = group;
    }

    private static List<OverviewJobGroup<TestJob>> RunGetGroups(
        OverviewGroupMode mode,
        List<TestJob> jobs
    ) =>
        GetGroups(
            mode,
            jobs,
            job => job.Def,
            job => job.HasException,
            job => job.IsSuspended,
            job => job.IsCompleted,
            job => job.Group,
            "Needs attention",
            "Active",
            "Suspended",
            "Completed",
            "Ungrouped",
            null
        );

    [Test]
    public static void NoneModeReturnsAllJobsInOneUnheadedGroup()
    {
        var def = new ManagerDef
        {
            defName = "CMR_TestDef",
            label = "test",
            order = 0,
        };
        var jobs = new List<TestJob>
        {
            new(def, false, false, false, null),
            new(def, false, false, false, null),
        };

        var groups = RunGetGroups(OverviewGroupMode.None, jobs);

        Assert.ThatCollection(groups).Has.Count(1);
        Assert.That(groups[0].Header).Is.Null();
        Assert.ThatCollection(groups[0].Jobs).Has.Count(2);
    }

    [Test]
    public static void JobTypeModeGroupsByDefAndOrdersByDefOrder()
    {
        // Second def is declared with a higher `order` than the first, but jobs are added to the
        // input list in the opposite order - output must still follow def.order, not input order.
        var huntingDef = new ManagerDef
        {
            defName = "CMR_TestHunting",
            label = "hunting",
            order = 1,
        };
        var forestryDef = new ManagerDef
        {
            defName = "CMR_TestForestry",
            label = "forestry",
            order = 0,
        };
        var jobs = new List<TestJob>
        {
            new(huntingDef, false, false, false, null),
            new(forestryDef, false, false, false, null),
            new(huntingDef, false, false, false, null),
        };

        var groups = RunGetGroups(OverviewGroupMode.JobType, jobs);

        Assert.ThatCollection(groups).Has.Count(2);
        Assert.That(groups[0].Header).Is.EqualTo("Forestry");
        Assert.ThatCollection(groups[0].Jobs).Has.Count(1);
        Assert.That(groups[1].Header).Is.EqualTo("Hunting");
        Assert.ThatCollection(groups[1].Jobs).Has.Count(2);
    }

    [Test]
    public static void StatusModePrioritizesExceptionOverSuspended()
    {
        // A job with a caused exception must land in "needs attention" even if it is also
        // suspended (e.g. auto-suspended because of the exception) - exception status takes
        // priority over the suspended bucket.
        var def = new ManagerDef
        {
            defName = "CMR_TestStatusDef",
            label = "test",
            order = 0,
        };
        var jobs = new List<TestJob>
        {
            new(def, hasException: true, isSuspended: true, isCompleted: false, null),
        };

        var groups = RunGetGroups(OverviewGroupMode.Status, jobs);

        Assert.ThatCollection(groups).Has.Count(1);
        Assert.That(groups[0].Header).Is.EqualTo("Needs attention");
    }

    [Test]
    public static void StatusModePrioritizesSuspendedOverCompleted()
    {
        // A suspended job that also happens to be completed must land in "suspended", not
        // "completed" - suspended takes priority, mirroring Utilities.DrawStampButton.
        var def = new ManagerDef
        {
            defName = "CMR_TestStatusDef3",
            label = "test",
            order = 0,
        };
        var jobs = new List<TestJob>
        {
            new(def, hasException: false, isSuspended: true, isCompleted: true, null),
        };

        var groups = RunGetGroups(OverviewGroupMode.Status, jobs);

        Assert.ThatCollection(groups).Has.Count(1);
        Assert.That(groups[0].Header).Is.EqualTo("Suspended");
    }

    [Test]
    public static void StatusModeSeparatesCompletedFromActive()
    {
        var def = new ManagerDef
        {
            defName = "CMR_TestStatusDef4",
            label = "test",
            order = 0,
        };
        var jobs = new List<TestJob>
        {
            new(def, hasException: false, isSuspended: false, isCompleted: true, null),
            new(def, hasException: false, isSuspended: false, isCompleted: false, null),
        };

        var groups = RunGetGroups(OverviewGroupMode.Status, jobs);

        Assert.ThatCollection(groups).Has.Count(2);
        Assert.That(groups[0].Header).Is.EqualTo("Active");
        Assert.ThatCollection(groups[0].Jobs).Has.Count(1);
        Assert.That(groups[1].Header).Is.EqualTo("Completed");
        Assert.ThatCollection(groups[1].Jobs).Has.Count(1);
    }

    [Test]
    public static void StatusModeOmitsEmptyBuckets()
    {
        var def = new ManagerDef
        {
            defName = "CMR_TestStatusDef2",
            label = "test",
            order = 0,
        };
        var jobs = new List<TestJob> { new(def, false, false, false, null) };

        var groups = RunGetGroups(OverviewGroupMode.Status, jobs);

        Assert.ThatCollection(groups).Has.Count(1);
        Assert.That(groups[0].Header).Is.EqualTo("Active");
    }

    [Test]
    public static void ManualModeCollectsUngroupedJobsIntoTrailingBucket()
    {
        var def = new ManagerDef
        {
            defName = "CMR_TestManualDef",
            label = "test",
            order = 0,
        };
        var jobs = new List<TestJob>
        {
            new(def, false, false, false, "Alpha"),
            new(def, false, false, false, null),
            new(def, false, false, false, "Beta"),
        };

        var groups = RunGetGroups(OverviewGroupMode.Manual, jobs);

        Assert.ThatCollection(groups).Has.Count(3);
        Assert.That(groups[0].Header).Is.EqualTo("Alpha");
        Assert.That(groups[1].Header).Is.EqualTo("Beta");
        Assert.That(groups[2].Header).Is.EqualTo("Ungrouped");
        Assert.ThatCollection(groups[2].Jobs).Has.Count(1);
    }

    [Test]
    public static void ManualModeOmitsUngroupedBucketWhenEveryJobIsAssigned()
    {
        var def = new ManagerDef
        {
            defName = "CMR_TestManualDef2",
            label = "test",
            order = 0,
        };
        var jobs = new List<TestJob> { new(def, false, false, false, "Alpha") };

        var groups = RunGetGroups(OverviewGroupMode.Manual, jobs);

        Assert.ThatCollection(groups).Has.Count(1);
        Assert.That(groups[0].Header).Is.EqualTo("Alpha");
    }
}
