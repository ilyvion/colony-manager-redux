// ManagerTabLabelSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The label building and truncation cache of <see cref="ManagerTab.GetFullLabel"/>.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerTabLabelSceneTests
{
    private const float Width = 100f;

    private sealed class LabelTab(Manager manager) : ManagerTab(manager)
    {
        public override string GetMainLabel(ManagerJob job) => "Main";

        public override string GetSubLabel(ManagerJob job) => "Sub";

        public Dictionary<float, Dictionary<string, string>> Cache => _truncateCache;
    }

    private static string Long(int i) => new string('W', 80) + i;

    [Test]
    public static void LabelWithoutSubLabelIsTheMainLabelOnly()
    {
        using var scene = new ManagerScene();
        var tab = new LabelTab(scene.Manager);
        var job = scene.AddJob<TestManagerJobA>();

        var (label, _) = tab.GetFullLabel(job, Width, drawSubLabel: false);

        Expect.AreEqual("Main", label);
    }

    [Test]
    public static void ShortLabelsAreJoinedUnchanged()
    {
        using var scene = new ManagerScene();
        var tab = new LabelTab(scene.Manager);
        var job = scene.AddJob<TestManagerJobA>();

        var (label, size) = tab.GetFullLabel(job, 1000f);

        Expect.AreEqual("Main\nSub", label);
        Expect.IsTrue(size.x > 0f && size.y > 0f, "Size measured");
    }

    [Test]
    public static void GivenSubLabelReplacesTheJobsOwn()
    {
        using var scene = new ManagerScene();
        var tab = new LabelTab(scene.Manager);
        var job = scene.AddJob<TestManagerJobA>();

        var (label, _) = tab.GetFullLabel(job, 1000f, "Given");

        Expect.AreEqual("Main\nGiven", label);
    }

    [Test]
    public static void LongSubLabelIsTruncatedToFit()
    {
        using var scene = new ManagerScene();
        var tab = new LabelTab(scene.Manager);
        var job = scene.AddJob<TestManagerJobA>();
        var text = Long(0);

        var (label, _) = tab.GetFullLabel(job, Width, text);

        var sub = label.Split('\n')[1];
        Expect.IsTrue(sub.EndsWith("...", StringComparison.Ordinal), "Truncated");
        Expect.IsTrue(sub.Length < text.Length, "Shorter than the original");
        Expect.IsTrue(Text.CalcSize(sub).x <= Width, "Fits");
    }

    [Test]
    public static void RepeatedTruncationServesTheCachedString()
    {
        using var scene = new ManagerScene();
        var tab = new LabelTab(scene.Manager);
        var job = scene.AddJob<TestManagerJobA>();
        var text = Long(0);

        var first = tab.GetFullLabel(job, Width, text).label;
        var second = tab.GetFullLabel(job, Width, text).label;

        Expect.AreEqual(first, second);
        Expect.AreEqual(1, tab.Cache[Width].Count);
    }

    [Test]
    public static void TruncationCacheIsClearedAfterTwoHundredStringsAtOneWidth()
    {
        using var scene = new ManagerScene();
        var tab = new LabelTab(scene.Manager);
        var job = scene.AddJob<TestManagerJobA>();

        for (var i = 0; i < 250; i++)
        {
            _ = tab.GetFullLabel(job, Width, Long(i));
        }

        Expect.AreEqual(50, tab.Cache[Width].Count);
    }

    [Test]
    public static void TruncationCacheDoesNotGrowWithEveryDistinctWidth()
    {
        using var scene = new ManagerScene();
        var tab = new LabelTab(scene.Manager);
        var job = scene.AddJob<TestManagerJobA>();
        var text = Long(0);

        for (var i = 0; i < 100; i++)
        {
            _ = tab.GetFullLabel(job, Width + i, text);
        }

        Expect.IsTrue(tab.Cache.Count <= 32, $"Cached widths: {tab.Cache.Count}");
        Expect.IsTrue(tab.Cache.ContainsKey(Width + 99), "Latest width cached");
    }
}
