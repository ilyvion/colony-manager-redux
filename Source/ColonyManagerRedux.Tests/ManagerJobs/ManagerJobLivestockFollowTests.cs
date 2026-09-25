// ManagerJobLivestockFollowTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;
using static ColonyManagerRedux.Managers.ManagerJob_Livestock;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ManagerJobLivestockFollowTests
{
    private static FollowDecision Decide(
        bool learnedObedience = true,
        bool followTraining = false,
        bool hasTrainableLeft = false,
        MasterMode trainers = MasterMode.Specific,
        MasterMode masters = MasterMode.Specific,
        bool setFollow = false,
        bool followDrafted = false,
        bool followFieldwork = false
    ) =>
        DecideFollow(
            learnedObedience,
            followTraining,
            () => hasTrainableLeft,
            trainers,
            masters,
            setFollow,
            followDrafted,
            followFieldwork
        );

    [Test]
    public static void AnimalThatHasNotLearnedObedienceIsLeftAlone() =>
        Expect.AreEqual(
            new FollowDecision(),
            Decide(
                learnedObedience: false,
                followTraining: true,
                hasTrainableLeft: true,
                setFollow: true,
                followDrafted: true
            )
        );

    [Test]
    public static void AnimalWithTrainingLeftUsesTrainerSettings()
    {
        var decision = Decide(
            followTraining: true,
            hasTrainableLeft: true,
            setFollow: false,
            followDrafted: true,
            followFieldwork: false
        );

        // Trainer settings ignore the default follow flags: follows for fieldwork, not drafted.
        Expect.AreEqual(MasterSource.Trainer, decision.Master);
        Expect.IsTrue(decision.SetFollow);
        Expect.IsFalse(decision.Drafted);
        Expect.IsTrue(decision.Fieldwork);
    }

    [Test]
    public static void ManualTrainersPlanNeitherMasterNorFollowForAnimalWithTrainingLeft() =>
        Expect.AreEqual(
            new FollowDecision(),
            Decide(
                followTraining: true,
                hasTrainableLeft: true,
                trainers: MasterMode.Manual,
                masters: MasterMode.Specific,
                setFollow: true
            )
        );

    [Test]
    public static void AnimalWithNothingLeftToTrainUsesDefaultSettings()
    {
        var decision = Decide(
            followTraining: true,
            hasTrainableLeft: false,
            setFollow: true,
            followDrafted: true,
            followFieldwork: false
        );

        Expect.AreEqual(MasterSource.Default, decision.Master);
        Expect.IsTrue(decision.SetFollow);
        Expect.IsTrue(decision.Drafted);
        Expect.IsFalse(decision.Fieldwork);
    }

    [Test]
    public static void FollowTrainingOffUsesDefaultSettingsEvenWithTrainingLeft() =>
        Expect.AreEqual(
            MasterSource.Default,
            Decide(followTraining: false, hasTrainableLeft: true).Master
        );

    [Test]
    public static void ManualMastersStillPlanFollowWhenSetFollowIsOn()
    {
        var decision = Decide(masters: MasterMode.Manual, setFollow: true, followFieldwork: true);

        Expect.AreEqual(MasterSource.None, decision.Master);
        Expect.IsTrue(decision.SetFollow);
        Expect.IsTrue(decision.Fieldwork);
    }

    [Test]
    public static void SetFollowOffPlansNoFollow() =>
        Expect.IsFalse(Decide(masters: MasterMode.Hunters, setFollow: false).SetFollow);

    [Test]
    public static void TrainingLeftIsOnlyCheckedWhenFollowingTrainers()
    {
        var checkedTraining = false;
        _ = DecideFollow(
            true,
            false,
            () =>
            {
                checkedTraining = true;
                return true;
            },
            MasterMode.Specific,
            MasterMode.Specific,
            false,
            false,
            false
        );
        var checkedForDisobedient = false;
        _ = DecideFollow(
            false,
            true,
            () =>
            {
                checkedForDisobedient = true;
                return true;
            },
            MasterMode.Specific,
            MasterMode.Specific,
            false,
            false,
            false
        );

        Expect.IsFalse(checkedTraining);
        Expect.IsFalse(checkedForDisobedient);
    }

    [TestCase(false, false, false, false, ExpectedResult = false)]
    [TestCase(true, true, true, true, ExpectedResult = false)]
    [TestCase(true, false, false, false, ExpectedResult = true)]
    [TestCase(false, true, false, false, ExpectedResult = true)]
    [TestCase(false, false, true, false, ExpectedResult = true)]
    [TestCase(false, false, false, true, ExpectedResult = true)]
    public static bool FollowSettingsDifferWhenEitherFlagDiffers(
        bool currentDrafted,
        bool currentFieldwork,
        bool drafted,
        bool fieldwork
    ) => FollowSettingsDiffer(currentDrafted, currentFieldwork, drafted, fieldwork);
}
