// BuildingAIManagerTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;
using static ColonyManagerRedux.Building_AIManager;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class BuildingAIManagerTests
{
    [Test]
    public static void OffTicksDoNothingAndDrawIdlePower()
    {
        var step = DecideAIManagerStep(false, true, false);

        Expect.IsFalse(step.CheckForWork);
        Expect.IsFalse(step.ClearHandle);
        Expect.IsFalse(step.FullPower);
        Expect.IsFalse(step.PrimaryColor.HasValue);
    }

    [Test]
    public static void NoHandleLooksForWorkAtFullPowerWithoutPickingAColorYet()
    {
        var step = DecideAIManagerStep(true, false, false);

        Expect.IsTrue(step.CheckForWork);
        Expect.IsFalse(step.ClearHandle);
        Expect.IsTrue(step.FullPower);
        Expect.IsFalse(step.PrimaryColor.HasValue);
    }

    [Test]
    public static void RunningJobKeepsFullPowerAndShowsGreen()
    {
        var step = DecideAIManagerStep(true, true, false);

        Expect.IsFalse(step.CheckForWork);
        Expect.IsFalse(step.ClearHandle);
        Expect.IsTrue(step.FullPower);
        Expect.AreEqual(Color.green, step.PrimaryColor!.Value);
    }

    [Test]
    public static void CompletedJobClearsTheHandleAndDropsToIdleRed()
    {
        var step = DecideAIManagerStep(true, true, true);

        Expect.IsFalse(step.CheckForWork);
        Expect.IsTrue(step.ClearHandle);
        Expect.IsFalse(step.FullPower);
        Expect.AreEqual(Color.red, step.PrimaryColor!.Value);
    }

    [Test]
    public static void CompletedHandleIsIgnoredBetweenWorkChecks()
    {
        var step = DecideAIManagerStep(false, true, true);

        Expect.IsFalse(step.ClearHandle);
        Expect.IsFalse(step.PrimaryColor.HasValue);
    }

    [TestCase(0, ExpectedResult = BlinkPhase.On)]
    [TestCase(30, ExpectedResult = BlinkPhase.On)]
    [TestCase(250, ExpectedResult = BlinkPhase.None)]
    [TestCase(25, ExpectedResult = BlinkPhase.Off)]
    [TestCase(55, ExpectedResult = BlinkPhase.Off)]
    [TestCase(1, ExpectedResult = BlinkPhase.None)]
    [TestCase(24, ExpectedResult = BlinkPhase.None)]
    [TestCase(26, ExpectedResult = BlinkPhase.None)]
    [TestCase(29, ExpectedResult = BlinkPhase.None)]
    public static BlinkPhase BlinkPhaseFollowsTheThirtyTickCycle(int tick) => BlinkPhaseAt(tick);
}
