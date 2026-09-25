// ManagerJobLivestockTrainingTrackerTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;
using static ColonyManagerRedux.Managers.ManagerJob_Livestock;

namespace ColonyManagerRedux.Tests;

// Uses vanilla's real trainable chain: Tameness <- Obedience <- (Release, Rescue, Haul).
// Toggling a trainable plays a camera sound, which needs a running game.
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobLivestockTrainingTrackerTests
{
    private static TrainableDef Def(string name) => DefDatabase<TrainableDef>.GetNamed(name);

    private static TrainingTracker Tracker() => new();

    [Test]
    public static void NothingIsEnabledInitially()
    {
        var tracker = Tracker();

        Expect.IsFalse(tracker.AnyEnabled);
        Expect.IsFalse(tracker[Def("Obedience")]);
    }

    [Test]
    public static void EnablingATrainableEnablesItsPrerequisitesRecursively()
    {
        var tracker = Tracker();

        tracker[Def("Release")] = true;

        Expect.IsTrue(tracker[Def("Release")]);
        Expect.IsTrue(tracker[Def("Obedience")]);
        Expect.IsTrue(tracker[Def("Tameness")]);
        Expect.IsFalse(tracker[Def("Haul")]);
        Expect.IsTrue(tracker.AnyEnabled);
    }

    [Test]
    public static void DisablingAPrerequisiteDisablesEverythingDownstream()
    {
        var tracker = Tracker();
        tracker[Def("Release")] = true;
        tracker[Def("Haul")] = true;

        tracker[Def("Tameness")] = false;

        Expect.IsFalse(tracker[Def("Tameness")]);
        Expect.IsFalse(tracker[Def("Obedience")]);
        Expect.IsFalse(tracker[Def("Release")]);
        Expect.IsFalse(tracker[Def("Haul")]);
        Expect.IsFalse(tracker.AnyEnabled);
    }

    [Test]
    public static void DisablingALeafKeepsItsPrerequisites()
    {
        var tracker = Tracker();
        tracker[Def("Haul")] = true;

        tracker[Def("Haul")] = false;

        Expect.IsFalse(tracker[Def("Haul")]);
        Expect.IsTrue(tracker[Def("Obedience")]);
        Expect.IsTrue(tracker[Def("Tameness")]);
    }

    [Test]
    public static void DisablingObedienceKeepsTamenessButDisablesSiblingsOfItsDependents()
    {
        var tracker = Tracker();
        tracker[Def("Release")] = true;
        tracker[Def("Rescue")] = true;

        tracker[Def("Obedience")] = false;

        Expect.IsTrue(tracker[Def("Tameness")]);
        Expect.IsFalse(tracker[Def("Release")]);
        Expect.IsFalse(tracker[Def("Rescue")]);
    }

    [Test]
    public static void SettingTheCurrentValueChangesNothing()
    {
        var tracker = Tracker();
        tracker[Def("Obedience")] = true;

        tracker[Def("Obedience")] = true;
        tracker[Def("Release")] = false;

        Expect.IsTrue(tracker[Def("Obedience")]);
        Expect.IsTrue(tracker[Def("Tameness")]);
        Expect.IsFalse(tracker[Def("Release")]);
    }

    [Test]
    public static void SettingsDefaultToDocumentedValues()
    {
        var tracker = Tracker();

        Expect.AreEqual(3, tracker.AnimalLimit);
        Expect.AreApproximatelyEqual(0.8f, tracker.EndOfLifeThreshold);
        Expect.IsFalse(tracker.TrainYoung);
        Expect.IsFalse(tracker.UnassignTraining);
        Expect.IsFalse(tracker.LimitAnimals);
    }

    [Test]
    public static void CountCoversEveryTrainableDef() =>
        Expect.AreEqual(TrainingTracker.TrainableDefs.Count, Tracker().Count);

    [Test]
    public static void SettingsAndTargetsRoundTripThroughScribe()
    {
        var saved = Tracker();
        saved.TrainYoung = true;
        saved.UnassignTraining = true;
        saved.LimitAnimals = true;
        saved.AnimalLimit = 7;
        saved.PrioritizeNamed = true;
        saved.PrioritizeBonded = true;
        saved.IgnoreNearingEndOfLife = true;
        saved.EndOfLifeThreshold = 0.6f;
        saved[Def("Release")] = true;

        var loaded = Tracker();
        ScribeRoundTrip.Run(saved, loaded);

        Expect.IsTrue(loaded.TrainYoung);
        Expect.IsTrue(loaded.UnassignTraining);
        Expect.IsTrue(loaded.LimitAnimals);
        Expect.AreEqual(7, loaded.AnimalLimit);
        Expect.IsTrue(loaded.PrioritizeNamed);
        Expect.IsTrue(loaded.PrioritizeBonded);
        Expect.IsTrue(loaded.IgnoreNearingEndOfLife);
        Expect.AreApproximatelyEqual(0.6f, loaded.EndOfLifeThreshold);
        Expect.IsTrue(loaded[Def("Release")]);
        Expect.IsTrue(loaded[Def("Obedience")]);
        Expect.IsFalse(loaded[Def("Haul")]);
    }
}
