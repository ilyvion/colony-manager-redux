// HistoryLabelTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class HistoryLabelTests
{
    [Test]
    public static void DirectLabelIsItsString() =>
        Expect.AreEqual("Steel", new DirectHistoryLabel("Steel").Label);

    [Test]
    public static void DirectLabelConvertsImplicitlyFromString()
    {
        DirectHistoryLabel label = "Steel";

        Expect.AreEqual("Steel", label.Label);
        Expect.AreEqual("Steel", DirectHistoryLabel.FromString("Steel").Label);
    }

    [Test]
    public static void ToStringIsTheLabel() =>
        Expect.AreEqual("Steel", new DirectHistoryLabel("Steel").ToString());

    [Test]
    public static void DefLabelUsesTheCapitalizedDefLabel()
    {
        var def = new ThingDef { defName = "TestThing", label = "test thing" };

        Expect.AreEqual("Test thing", new DefHistoryLabel<ThingDef>(def).Label);
    }

    [Test]
    public static void DefLabelConvertsImplicitlyFromDef()
    {
        DefHistoryLabel<ThingDef> label = new ThingDef { defName = "X", label = "x" };

        Expect.AreEqual("X", label.Label);
    }

    [Test]
    public static void NullDefLabelIsPlaceholder() =>
        Expect.AreEqual("<null>", new DefHistoryLabel<ThingDef>(null!).Label);

    [Test]
    public static void DefWithoutALabelFallsBackToItsDefName()
    {
        var def = new ThingDef { defName = "testThing" };

        Expect.AreEqual("TestThing", new DefHistoryLabel<ThingDef>(def).Label);
    }
}
