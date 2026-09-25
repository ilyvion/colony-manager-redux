// SettingsExposeDataTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// <see cref="Settings.ExposeData"/> through in-memory Scribe streams, so the player's real
/// settings file is never touched.
/// </summary>
[TestFixture(TestType.MainMenu)]
internal sealed class SettingsExposeDataTests
{
    private const string EmptyDocument = "<?xml version=\"1.0\" encoding=\"utf-8\"?><root />";

    private static Settings LoadFrom(string xml)
    {
        var settings = new Settings();
        ScribeRoundTrip.Load(xml, settings);
        return settings;
    }

    private static string Document(string body) =>
        $"<?xml version=\"1.0\" encoding=\"utf-8\"?><root>{body}</root>";

    [Test]
    public static void EmptyDocumentLoadsEveryDefault()
    {
        var settings = LoadFrom(EmptyDocument);

        Expect.AreEqual(false, settings.DoVerboseLogging);
        Expect.AreEqual(GenDate.TicksPerDay, settings.DefaultUpdateIntervalTicks);
        Expect.AreEqual(UpdateInterval.Daily.Ticks, settings.DefaultUpdateInterval.Ticks);
        Expect.AreEqual(500, settings.DefaultTargetCount);
        Expect.AreEqual(true, settings.DefaultShouldCheckReachable);
        Expect.AreEqual(false, settings.DefaultUsePathBasedDistance);
        Expect.AreEqual(false, settings.DefaultCountAllOnMap);
        Expect.AreEqual(true, settings.NewJobsAreImmediatelyOutdated);
        Expect.AreEqual(true, settings.RecordHistoricalData);
        Expect.AreEqual(true, settings.NewJobsShouldBeResourceLocked);
        Expect.AreEqual(true, settings.ShowInfoCardButtonsWherePossible);
        Expect.AreEqual(0, settings.MaxDesignationsPerJob);
        Expect.AreEqual(true, settings.AutoApplyDefaultTemplateOnFirstStation);
        Expect.IsNull(settings.DefaultTemplateName);
        Expect.AreEqual(true, settings.ShowNoManagerAlert);
        Expect.AreEqual(true, settings.ShowNoTableAlert);
        Expect.AreEqual(true, settings.ShowJobsNotUpdatingAlert);
        Expect.AreEqual(0.5f, settings.DaysBeforeShowingAlert);
        Expect.AreEqual(1f, settings.DaysBeforeShowingHighAlert);
        Expect.AreEqual(2f, settings.DaysBeforeShowingCriticalAlert);
        Expect.AreEqual(true, settings.ShowNoTableNeededAlert);
        Expect.AreEqual(
            GravshipJobConflictResolution.AlwaysAsk,
            settings.GravshipJobConflictResolution
        );
        Expect.AreEqual(10, settings.OperationsPerTick);
        Expect.AreEqual(0, settings.TicksBetweenOperations);
        Expect.AreEqual(false, settings.ShowAdvancedPerformanceSettings);
    }

    [Test]
    public static void MissingCollectionsLoadAsEmptyNotNull()
    {
        var settings = LoadFrom(EmptyDocument);

        Expect.IsNotNull(settings.DisabledManagers);
        Expect.IsEmpty(settings.DisabledManagers);
        Expect.IsNotNull(settings.CustomUpdateIntervalTickList);
        Expect.IsEmpty(settings.CustomUpdateIntervalTickList);
    }

    [Test]
    public static void ChangedValuesRoundTrip()
    {
        var saved = new Settings
        {
            DoVerboseLogging = true,
            DefaultTargetCount = 123,
            DefaultShouldCheckReachable = false,
            DefaultUsePathBasedDistance = true,
            DefaultCountAllOnMap = true,
            NewJobsAreImmediatelyOutdated = false,
            RecordHistoricalData = false,
            NewJobsShouldBeResourceLocked = false,
            ShowInfoCardButtonsWherePossible = false,
            MaxDesignationsPerJob = 40,
            AutoApplyDefaultTemplateOnFirstStation = false,
            DefaultTemplateName = "My template",
            ShowNoManagerAlert = false,
            ShowNoTableAlert = false,
            ShowJobsNotUpdatingAlert = false,
            DaysBeforeShowingAlert = 1.5f,
            DaysBeforeShowingHighAlert = 2.5f,
            DaysBeforeShowingCriticalAlert = 3.5f,
            ShowNoTableNeededAlert = false,
            GravshipJobConflictResolution = GravshipJobConflictResolution.MergeJobs,
            OperationsPerTick = 25,
            TicksBetweenOperations = 3,
            ShowAdvancedPerformanceSettings = true,
            CustomUpdateIntervalTickList = [600, 1800],
        };
        _ = saved.DisabledManagers.Add(DefDatabase<ManagerDef>.AllDefsListForReading[0]);

        var loaded = new Settings();
        ScribeRoundTrip.Run(saved, loaded);

        Expect.AreEqual(true, loaded.DoVerboseLogging);
        Expect.AreEqual(123, loaded.DefaultTargetCount);
        Expect.AreEqual(false, loaded.DefaultShouldCheckReachable);
        Expect.AreEqual(true, loaded.DefaultUsePathBasedDistance);
        Expect.AreEqual(true, loaded.DefaultCountAllOnMap);
        Expect.AreEqual(false, loaded.NewJobsAreImmediatelyOutdated);
        Expect.AreEqual(false, loaded.RecordHistoricalData);
        Expect.AreEqual(false, loaded.NewJobsShouldBeResourceLocked);
        Expect.AreEqual(false, loaded.ShowInfoCardButtonsWherePossible);
        Expect.AreEqual(40, loaded.MaxDesignationsPerJob);
        Expect.AreEqual(false, loaded.AutoApplyDefaultTemplateOnFirstStation);
        Expect.AreEqual("My template", loaded.DefaultTemplateName);
        Expect.AreEqual(false, loaded.ShowNoManagerAlert);
        Expect.AreEqual(false, loaded.ShowNoTableAlert);
        Expect.AreEqual(false, loaded.ShowJobsNotUpdatingAlert);
        Expect.AreEqual(1.5f, loaded.DaysBeforeShowingAlert);
        Expect.AreEqual(2.5f, loaded.DaysBeforeShowingHighAlert);
        Expect.AreEqual(3.5f, loaded.DaysBeforeShowingCriticalAlert);
        Expect.AreEqual(false, loaded.ShowNoTableNeededAlert);
        Expect.AreEqual(
            GravshipJobConflictResolution.MergeJobs,
            loaded.GravshipJobConflictResolution
        );
        Expect.AreEqual(25, loaded.OperationsPerTick);
        Expect.AreEqual(3, loaded.TicksBetweenOperations);
        Expect.AreEqual(true, loaded.ShowAdvancedPerformanceSettings);
        Expect.That(() => loaded.CustomUpdateIntervalTickList.SequenceEqual([600, 1800]));
        Expect.AreEqual(1, loaded.DisabledManagers.Count);
        Expect.IsTrue(
            loaded.DisabledManagers.Contains(DefDatabase<ManagerDef>.AllDefsListForReading[0])
        );
    }

    [Test]
    public static void DefaultUpdateIntervalMatchingAnOptionIsKept()
    {
        var option = Utilities.UpdateIntervalOptions.First(i => i.Ticks != GenDate.TicksPerDay);

        var settings = LoadFrom(
            Document($"<defaultUpdateInterval>{option.Ticks}</defaultUpdateInterval>")
        );

        Expect.AreEqual(option.Ticks, settings.DefaultUpdateInterval.Ticks);
    }

    [Test]
    public static void DefaultUpdateIntervalWithoutAMatchingOptionFallsBackToDaily()
    {
        var unmatched = Utilities.UpdateIntervalOptions.Max(i => i.Ticks) + 1;

        var settings = LoadFrom(
            Document($"<defaultUpdateInterval>{unmatched}</defaultUpdateInterval>")
        );

        Expect.AreEqual(UpdateInterval.Daily.Ticks, settings.DefaultUpdateInterval.Ticks);
    }

    [Test]
    public static void MaxDesignationsPerJobIsStoredInUnitsOfTen()
    {
        var settings = LoadFrom(Document("<maxDesignationsPerJob>5</maxDesignationsPerJob>"));

        Expect.AreEqual(50, settings.MaxDesignationsPerJob);
    }
}
