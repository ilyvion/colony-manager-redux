// TriggerPawnKindSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// <see cref="Trigger_PawnKind"/> state and how it saves and loads its pawn kind.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class TriggerPawnKindSceneTests
{
    private const string MissingPawnKindName = "CMRTest_MissingPawnKind";

    private static Trigger_PawnKind LivestockTrigger(ManagerScene scene, int targets)
    {
        var job = scene.AddJob<ManagerJob_Livestock>("CM_LivestockManager");
        var trigger = job.TriggerPawnKind;
        trigger.pawnKind = PawnKindDefOf.Muffalo;
        Array.Fill(trigger.CountTargets, targets);
        return trigger;
    }

    [Test]
    public static void StateIsFalseWithoutAPawnKind()
    {
        using var scene = new ManagerScene();
        var trigger = LivestockTrigger(scene, 3);
        trigger.pawnKind = null;

        Expect.IsFalse(trigger.State);
    }

    [Test]
    public static void StateIsTrueWhenATargetIsNotMet()
    {
        using var scene = new ManagerScene();
        var trigger = LivestockTrigger(scene, 3);

        Expect.IsTrue(trigger.State);
    }

    [Test]
    public static void StateIsNotRecomputedWhileItsCacheIsValid()
    {
        using var scene = new ManagerScene();
        var trigger = LivestockTrigger(scene, 0);
        var first = trigger.State;

        Array.Fill(trigger.CountTargets, 3);

        Expect.AreEqual(first, trigger.State);
    }

    private static string SavePawnKindTrigger(
        ManagerScene scene,
        PawnKindDef? pawnKind,
        Action<Trigger_PawnKind>? configure = null
    )
    {
        var job = scene.AddJob<TestManagerJobA>();
        var trigger = new Trigger_PawnKind(job) { pawnKind = pawnKind };
        configure?.Invoke(trigger);
        job.SetTrigger(trigger);
        return ScribeRoundTrip.Save(
            new ScribeRoundTrip.ManagerHarness(
                scene.Manager,
                new ScribeRoundTrip.JobListHarness(scene.Manager, [job])
            )
        );
    }

    private static Trigger_PawnKind LoadPawnKindTrigger(ManagerScene scene, string xml)
    {
        var loaded = new ScribeRoundTrip.JobListHarness(scene.Manager);
        ScribeRoundTrip.Load(xml, new ScribeRoundTrip.ManagerHarness(scene.Manager, loaded));
        return (Trigger_PawnKind)loaded.Jobs.Single().Trigger!;
    }

    [Test]
    public static void PawnKindAndTargetsRoundTrip()
    {
        using var scene = new ManagerScene();
        var xml = SavePawnKindTrigger(scene, PawnKindDefOf.Muffalo, t => t.CountTargets[1] = 9);

        var loaded = LoadPawnKindTrigger(scene, xml);

        Expect.AreEqual(PawnKindDefOf.Muffalo, loaded.pawnKind);
        Expect.AreEqual(9, loaded.CountTargets[1]);
        Expect.IsTrue(
            loaded.ExpectedPawnKindName.Contains(
                PawnKindDefOf.Muffalo.defName,
                StringComparison.Ordinal
            )
        );
    }

    [Test]
    [WarningsAllowed("Could not load reference")]
    [ErrorsAllowed("Could not load reference")]
    public static void MissingPawnKindLoadsAsNullAndRemembersItsName()
    {
        using var scene = new ManagerScene();
        var xml = SavePawnKindTrigger(scene, PawnKindDefOf.Muffalo);
        Expect.IsTrue(
            xml.Contains(
                $"<pawnKind>{PawnKindDefOf.Muffalo.defName}</pawnKind>",
                StringComparison.Ordinal
            )
        );
        xml = xml.Replace(
            $"<pawnKind>{PawnKindDefOf.Muffalo.defName}</pawnKind>",
            $"<pawnKind>{MissingPawnKindName}</pawnKind>",
            StringComparison.Ordinal
        );

        var loaded = LoadPawnKindTrigger(scene, xml);

        Expect.IsNull(loaded.pawnKind);
        Expect.AreEqual(
            $"[PawnKindDef was saved as '{MissingPawnKindName}']",
            loaded.ExpectedPawnKindName
        );
    }

    [Test]
    public static void NoPawnKindLoadsWithoutAnExpectedName()
    {
        using var scene = new ManagerScene();
        var xml = SavePawnKindTrigger(scene, null);

        var loaded = LoadPawnKindTrigger(scene, xml);

        Expect.IsNull(loaded.pawnKind);
        Expect.IsNull(loaded.ExpectedPawnKindNameRaw);
    }
}
