// ManagerJobLivestockDecisionTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;
using static ColonyManagerRedux.Managers.ManagerJob_Livestock;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ManagerJobLivestockDecisionTests
{
    private const int Interval = 1000;

    private sealed class FakeArea(string name)
    {
        public override string ToString() => name;
    }

    private static readonly FakeArea Cull = new("cull");
    private static readonly FakeArea Milk = new("milk");
    private static readonly FakeArea Shear = new("shear");
    private static readonly FakeArea Training = new("training");
    private static readonly FakeArea Trained = new("trained");
    private static readonly FakeArea Restrict = new("restrict");
    private static readonly FakeArea Other = new("other");

    private static (FakeArea? Area, AreaAssignReason Reason)? Decide(
        FakeArea? current = null,
        bool sendToCulling = false,
        bool alreadyCulling = false,
        bool sendToMilking = false,
        int? milkTicks = null,
        bool sendToShearing = false,
        int? shearTicks = null,
        bool sendToTraining = false,
        bool sendToTrained = false,
        bool hasTrainableToTrain = false,
        bool restrictToArea = false
    ) =>
        DecideArea(
            sendToCulling,
            alreadyCulling,
            sendToMilking,
            () => milkTicks,
            sendToShearing,
            () => shearTicks,
            Interval,
            sendToTraining,
            sendToTrained,
            hasTrainableToTrain,
            restrictToArea,
            current,
            Cull,
            Milk,
            Shear,
            Training,
            Trained,
            Restrict
        );

    [Test]
    public static void NothingEnabledPlansNothing() => Expect.IsTrue(Decide() == null);

    [Test]
    public static void CullingBeatsEveryOtherReason()
    {
        var decision = Decide(
            sendToCulling: true,
            alreadyCulling: true,
            sendToMilking: true,
            milkTicks: 0,
            sendToTraining: true,
            hasTrainableToTrain: true,
            restrictToArea: true
        );

        Expect.AreEqual(AreaAssignReason.Culling, decision!.Value.Reason);
        Expect.ReferencesAreEqual(Cull, decision.Value.Area);
    }

    [Test]
    public static void CullingNotPlannedWhenNotCulling() =>
        Expect.IsTrue(Decide(sendToCulling: true, alreadyCulling: false) == null);

    // Documented quirk: culling is planned even when already in the culling area.
    [Test]
    public static void CullingIsPlannedEvenWhenAlreadyInCullingArea() =>
        Expect.AreEqual(
            AreaAssignReason.Culling,
            Decide(current: Cull, sendToCulling: true, alreadyCulling: true)!.Value.Reason
        );

    [Test]
    public static void MilkingPlannedBelowIntervalBoundary() =>
        Expect.AreEqual(
            AreaAssignReason.Milking,
            Decide(sendToMilking: true, milkTicks: Interval - 1)!.Value.Reason
        );

    [Test]
    public static void MilkingNotPlannedAtExactlyTheInterval() =>
        Expect.IsTrue(Decide(sendToMilking: true, milkTicks: Interval) == null);

    [Test]
    public static void MilkingNotPlannedWithoutMilkComp() =>
        Expect.IsTrue(Decide(sendToMilking: true, milkTicks: null) == null);

    [Test]
    public static void AlreadyInMilkAreaFallsThroughToShearing()
    {
        var decision = Decide(
            current: Milk,
            sendToMilking: true,
            milkTicks: 0,
            sendToShearing: true,
            shearTicks: 0
        );

        Expect.AreEqual(AreaAssignReason.Shearing, decision!.Value.Reason);
        Expect.ReferencesAreEqual(Shear, decision.Value.Area);
    }

    [Test]
    public static void TrainingPlannedWhenSomethingLeftToTrain() =>
        Expect.AreEqual(
            AreaAssignReason.Training,
            Decide(
                sendToTraining: true,
                sendToTrained: true,
                hasTrainableToTrain: true
            )!.Value.Reason
        );

    [Test]
    public static void TrainedPlannedWhenNothingLeftToTrain() =>
        Expect.AreEqual(
            AreaAssignReason.Trained,
            Decide(
                sendToTraining: true,
                sendToTrained: true,
                hasTrainableToTrain: false
            )!.Value.Reason
        );

    [Test]
    public static void AlreadyInTrainingAreaFallsThroughToRestrict() =>
        Expect.AreEqual(
            AreaAssignReason.Restrict,
            Decide(
                current: Training,
                sendToTraining: true,
                hasTrainableToTrain: true,
                restrictToArea: true
            )!.Value.Reason
        );

    [Test]
    public static void RestrictPlannedOnlyWhenNotAlreadyThere()
    {
        Expect.AreEqual(
            AreaAssignReason.Restrict,
            Decide(current: Other, restrictToArea: true)!.Value.Reason
        );
        Expect.IsTrue(Decide(current: Restrict, restrictToArea: true) == null);
    }

    [Test]
    public static void RestrictOffPlansNothing() => Expect.IsTrue(Decide(current: Other) == null);

    [Test]
    public static void ThresholdsAreNotEvaluatedWhenReasonIsDisabled()
    {
        var evaluated = false;
        _ = DecideArea<FakeArea>(
            false,
            false,
            false,
            () =>
            {
                evaluated = true;
                return 0;
            },
            false,
            () =>
            {
                evaluated = true;
                return 0;
            },
            Interval,
            false,
            false,
            false,
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            null
        );

        Expect.IsFalse(evaluated);
    }

    [Test]
    public static void SetsTrainingWhenAllowedAndWithinLimit() =>
        Expect.IsTrue(ShouldSetTraining(true, false, true, false, true));

    [Test]
    public static void SetsTrainingWhenThereIsNoLimit() =>
        Expect.IsTrue(ShouldSetTraining(true, false, true, false, null));

    [Test]
    public static void DoesNotSetTrainingWhenOverLimit() =>
        Expect.IsFalse(ShouldSetTraining(true, false, true, false, false));

    [Test]
    public static void DoesNotSetTrainingWhenCannotAssign() =>
        Expect.IsFalse(ShouldSetTraining(false, false, true, false, true));

    [Test]
    public static void DoesNothingWhenWantedAlreadyMatches() =>
        Expect.IsFalse(ShouldSetTraining(true, true, true, true, true));

    [Test]
    public static void UntrainingRequiresUnassignTraining()
    {
        Expect.IsFalse(ShouldSetTraining(true, true, false, false, null));
        Expect.IsTrue(ShouldSetTraining(true, true, false, true, null));
    }

    [Test]
    public static void UntrainingIgnoresTheLimit() =>
        Expect.IsTrue(ShouldSetTraining(true, true, false, true, false));
}
