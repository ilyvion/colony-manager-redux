// ManagerTabOverviewTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;
using static ColonyManagerRedux.Managers.ManagerTab_Overview;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ManagerTabOverviewTests
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

    private readonly struct TestGroupActionJob(bool isSuspended, bool shouldDoNow)
    {
        public bool IsSuspended { get; } = isSuspended;
        public bool ShouldDoNow { get; } = shouldDoNow;
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

        Expect.AreEqual(1, groups.Count);
        Expect.IsNull(groups[0].Header);
        Expect.AreEqual(2, groups[0].Jobs.Count);
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

        Expect.AreEqual(2, groups.Count);
        Expect.AreEqual("Forestry", groups[0].Header);
        Expect.AreEqual(1, groups[0].Jobs.Count);
        Expect.AreEqual("Hunting", groups[1].Header);
        Expect.AreEqual(2, groups[1].Jobs.Count);
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

        Expect.AreEqual(1, groups.Count);
        Expect.AreEqual("Needs attention", groups[0].Header);
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

        Expect.AreEqual(1, groups.Count);
        Expect.AreEqual("Suspended", groups[0].Header);
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

        Expect.AreEqual(2, groups.Count);
        Expect.AreEqual("Active", groups[0].Header);
        Expect.AreEqual(1, groups[0].Jobs.Count);
        Expect.AreEqual("Completed", groups[1].Header);
        Expect.AreEqual(1, groups[1].Jobs.Count);
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

        Expect.AreEqual(1, groups.Count);
        Expect.AreEqual("Active", groups[0].Header);
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

        Expect.AreEqual(3, groups.Count);
        Expect.AreEqual("Alpha", groups[0].Header);
        Expect.AreEqual("Beta", groups[1].Header);
        Expect.AreEqual("Ungrouped", groups[2].Header);
        Expect.AreEqual(1, groups[2].Jobs.Count);
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

        Expect.AreEqual(1, groups.Count);
        Expect.AreEqual("Alpha", groups[0].Header);
    }

    [Test]
    public static void AllSuspendedIsTrueOnlyWhenEveryJobIsSuspended()
    {
        var allSuspended = new List<TestGroupActionJob>
        {
            new(isSuspended: true, shouldDoNow: false),
            new(isSuspended: true, shouldDoNow: false),
        };
        var mixed = new List<TestGroupActionJob>
        {
            new(isSuspended: true, shouldDoNow: false),
            new(isSuspended: false, shouldDoNow: false),
        };
        var noneSuspended = new List<TestGroupActionJob>
        {
            new(isSuspended: false, shouldDoNow: false),
        };

        Expect.IsTrue(AllSuspended(allSuspended, job => job.IsSuspended));
        Expect.IsFalse(AllSuspended(mixed, job => job.IsSuspended));
        Expect.IsFalse(AllSuspended(noneSuspended, job => job.IsSuspended));
    }

    [Test]
    public static void JobsEligibleForForceUpdateExcludesSuspendedAndAlreadyDueJobs()
    {
        var jobs = new List<TestGroupActionJob>
        {
            new(isSuspended: false, shouldDoNow: false), // eligible
            new(isSuspended: true, shouldDoNow: false), // suspended - excluded
            new(isSuspended: false, shouldDoNow: true), // already due/pending - excluded
            new(isSuspended: true, shouldDoNow: true), // both - excluded
        };

        var eligible = JobsEligibleForForceUpdate(
            jobs,
            job => job.IsSuspended,
            job => job.ShouldDoNow
        );

        Expect.AreEqual(1, eligible.Count);
        Expect.IsFalse(eligible[0].IsSuspended);
        Expect.IsFalse(eligible[0].ShouldDoNow);
    }

    // ---- FilterJobs ----

    private sealed class FilterJob(ManagerDef def, string label, string targets, string tab)
    {
        public ManagerDef Def { get; } = def;
        public string Label { get; } = label;
        public string Targets { get; } = targets;
        public string Tab { get; } = tab;
    }

    private static readonly ManagerDef HuntingDef = new() { defName = "TestHunting" };
    private static readonly ManagerDef ForestryDef = new() { defName = "TestForestry" };

    private static readonly FilterJob Boars = new(HuntingDef, "Hunt boars", "Boar", "Hunting");
    private static readonly FilterJob Deer = new(HuntingDef, "Hunt deer", "Deer", "Hunting");
    private static readonly FilterJob Oaks = new(ForestryDef, "Cut trees", "Oak", "Forestry");

    private static List<FilterJob> Filter(
        HashSet<ManagerDef> hidden,
        string? search,
        params FilterJob[] jobs
    ) =>
        FilterJobs(
            jobs,
            hidden,
            job => job.Def,
            search != null,
            text => text.Contains(search!, StringComparison.OrdinalIgnoreCase),
            job => [job.Label, job.Targets, job.Tab]
        );

    [Test]
    public static void NoFiltersKeepEveryJob() =>
        Expect.AreEqual(3, Filter([], null, Boars, Deer, Oaks).Count);

    [Test]
    public static void HiddenTypesAreDropped()
    {
        var result = Filter([HuntingDef], null, Boars, Deer, Oaks);

        Expect.AreEqual(1, result.Count);
        Expect.ReferencesAreEqual(Oaks, result[0]);
    }

    [Test]
    public static void HidingEveryTypeLeavesNothing() =>
        Expect.IsEmpty(Filter([HuntingDef, ForestryDef], null, Boars, Deer, Oaks));

    [Test]
    public static void SearchMatchesOnTheLabel() =>
        Expect.AreEqual(1, Filter([], "boars", Boars, Deer, Oaks).Count);

    [Test]
    public static void SearchMatchesOnTheTargetsAloneEvenWhenTheLabelDoesNot() =>
        Expect.ReferencesAreEqual(Oaks, Filter([], "oak", Boars, Deer, Oaks).Single());

    [Test]
    public static void SearchMatchesOnTheTabName() =>
        Expect.AreEqual(2, Filter([], "hunting", Boars, Deer, Oaks).Count);

    [Test]
    public static void HiddenAndSearchCombineWithAnd() =>
        // The search matches all three via 'o' but the hunting jobs are hidden.
        Expect.ReferencesAreEqual(Oaks, Filter([HuntingDef], "o", Boars, Deer, Oaks).Single());

    [Test]
    public static void SearchWithNoMatchLeavesNothing() =>
        Expect.IsEmpty(Filter([], "zebra", Boars, Deer, Oaks));

    [Test]
    public static void InactiveSearchIgnoresTheMatcher()
    {
        var matcherCalls = 0;

        var result = FilterJobs(
            [Boars, Deer],
            new HashSet<ManagerDef>(),
            job => job.Def,
            false,
            _ =>
            {
                matcherCalls++;
                return false;
            },
            job => [job.Label]
        );

        Expect.AreEqual(2, result.Count);
        Expect.AreEqual(0, matcherCalls);
    }
}
