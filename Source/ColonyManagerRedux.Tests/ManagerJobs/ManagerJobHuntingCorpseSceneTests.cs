// ManagerJobHuntingCorpseSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// How <see cref="ManagerJob_Hunting"/> counts the meat or leather it expects from corpses and
/// from designated animals, against real corpses, graves, stockpiles and hunting grounds.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobHuntingCorpseSceneTests
{
    private static readonly PawnKindDef Prey = PawnKindDefOf.Muffalo;
    private static readonly PawnKindDef OtherPrey = PawnKindDef.Named("Alpaca");

    private static IntVec3 Slot(ManagerScene scene, int i, int row = 0) =>
        scene.Cells(1, 1, -60 + (i * 4), -40 + (row * 4)).First();

    private static ManagerJob_Hunting NewJob(
        ManagerScene scene,
        ManagerJob_Hunting.HuntingTargetResource resource =
            ManagerJob_Hunting.HuntingTargetResource.Meat
    )
    {
        var job = scene.AddJob<ManagerJob_Hunting>("CM_HuntingManager");
        job.ShouldCheckReachable = false;
        job.HuntingGrounds = null;
        job.InvertHuntingGrounds = false;
        job.UnforbidAllCorpses = true;
        job.UnforbidHumanCorpses = false;
        job.TargetResource = resource;
        job.AllowedAnimals.Clear();
        _ = job.AllowedAnimals.Add(Prey);
        job.TriggerThreshold.ThresholdFilter.SetDisallowAll();
        job.TriggerThreshold.ThresholdFilter.SetAllow(ResourceOf(Prey, resource), true);
        job.TriggerThreshold.Op = Trigger_Threshold.Ops.LowerThan;
        job.TriggerThreshold.TargetCount = 100000;
        // Reachability is judged from the colony, which needs a colonist.
        _ = scene.SpawnColonist(Slot(scene, 30));
        return job;
    }

    private static ThingDef ResourceOf(
        PawnKindDef kind,
        ManagerJob_Hunting.HuntingTargetResource resource
    ) =>
        resource == ManagerJob_Hunting.HuntingTargetResource.Meat
            ? kind.race.race.meatDef
            : kind.race.race.leatherDef;

    private static int CountCorpses(ManagerJob_Hunting job)
    {
        var count = new AnyBoxed<int>(0);
        ManagerScene.RunToCompletion(
            job.TargetResource == ManagerJob_Hunting.HuntingTargetResource.Meat
                ? job.GetMeatInCorpsesCoroutine(count)
                : job.GetLeatherInCorpsesCoroutine(count)
        );
        return count.Value;
    }

    private static int CountDesignated(ManagerJob_Hunting job)
    {
        var count = new AnyBoxed<int>(0);
        ManagerScene.RunToCompletion(
            job.TargetResource == ManagerJob_Hunting.HuntingTargetResource.Meat
                ? job.GetMeatInDesignationsCoroutine(count)
                : job.GetLeatherInDesignationsCoroutine(count)
        );
        return count.Value;
    }

    // Corpses of pawns that die outside the home area start out forbidden.
    private static Corpse SpawnCorpse(ManagerScene scene, PawnKindDef kind, IntVec3 cell)
    {
        var corpse = scene.SpawnCorpse(kind, cell);
        corpse.SetForbidden(false, warnOnFail: false);
        return corpse;
    }

    private static Corpse FreshCorpse(ManagerScene scene, int slot, PawnKindDef? kind = null)
    {
        var corpse = SpawnCorpse(scene, kind ?? Prey, Slot(scene, slot));
        Expect.IsFalse(corpse.IsNotFresh(), "The corpse should start out fresh.");
        return corpse;
    }

    // Corpses

    [Test]
    public static void FreshAllowedCorpseIsCountedAtItsMeatYield()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var corpse = FreshCorpse(scene, 0);
        var meat = corpse.EstimatedMeatCount();
        Expect.IsTrue(meat > 0, "The corpse yields no meat.");

        Expect.AreEqual(meat, CountCorpses(job));
    }

    [Test]
    public static void CorpsesAreSummed()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var first = FreshCorpse(scene, 0);
        var second = FreshCorpse(scene, 1);

        Expect.AreEqual(
            first.EstimatedMeatCount() + second.EstimatedMeatCount(),
            CountCorpses(job)
        );
    }

    [Test]
    public static void LeatherTargetCountsLeatherInsteadOfMeat()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene, ManagerJob_Hunting.HuntingTargetResource.Leather);
        var corpse = FreshCorpse(scene, 0);
        var leather = corpse.EstimatedLeatherCount();
        Expect.IsTrue(leather > 0, "The corpse yields no leather.");
        Expect.AreNotEqual(leather, corpse.EstimatedMeatCount(), "Meat and leather are equal.");

        Expect.AreEqual(leather, CountCorpses(job));
    }

    [Test]
    public static void ForbiddenCorpseIsNotCounted()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var corpse = FreshCorpse(scene, 0);
        corpse.SetForbidden(true, warnOnFail: false);

        Expect.AreEqual(0, CountCorpses(job));
    }

    [Test]
    public static void UnreachableCorpseIsNotCounted()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var corpse = FreshCorpse(scene, 0);
        Expect.AreEqual(corpse.EstimatedMeatCount(), CountCorpses(job), "Reachable to begin with");

        foreach (var offset in GenAdj.AdjacentCellsAndInside)
        {
            if (offset != IntVec3.Zero)
            {
                _ = scene.SpawnThing(
                    ThingDefOf.Wall,
                    corpse.Position + offset,
                    stuff: ThingDefOf.Steel
                );
            }
        }
        scene.Map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();

        Expect.AreEqual(0, CountCorpses(job));
    }

    [Test]
    public static void CorpseInAGraveIsNotCounted()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var cell = Slot(scene, 0);
        var grave = (Building_Grave)scene.SpawnBuilding(ThingDefOf.Grave, cell);
        grave.GetStoreSettings().filter.SetAllowAll(null);
        var corpse = SpawnCorpse(scene, Prey, cell);
        Expect.AreEqual(corpse.EstimatedMeatCount(), CountCorpses(job), "Not yet buried");

        // A hauler carries the corpse, so it is unspawned when the grave takes it.
        corpse.DeSpawn();
        Expect.IsTrue(grave.TryAcceptThing(corpse, allowSpecialEffects: false), "Not buried.");
        Expect.IsTrue(grave.HasCorpse, "The grave should hold the corpse.");

        Expect.AreEqual(0, CountCorpses(job));
    }

    [Test]
    public static void CorpseInAnOrdinaryStockpileIsCounted()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var cell = Slot(scene, 0);
        _ = scene.AddStockpile([cell]);
        var corpse = SpawnCorpse(scene, Prey, cell);

        Expect.AreEqual(corpse.EstimatedMeatCount(), CountCorpses(job));
    }

    [Test]
    public static void RottenCorpseIsNotCounted()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var corpse = FreshCorpse(scene, 0);
        var rot = corpse.GetComp<CompRottable>();
        rot.RotProgress = rot.PropsRot.TicksToRotStart + 1;
        Expect.IsTrue(corpse.IsNotFresh(), "The corpse should have started rotting.");

        Expect.AreEqual(0, CountCorpses(job));
    }

    [Test]
    public static void CorpseWhoseMeatTheThresholdFilterRejectsIsNotCounted()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        _ = job.AllowedAnimals.Add(OtherPrey);
        Expect.AreNotEqual(
            Prey.race.race.meatDef,
            OtherPrey.race.race.meatDef,
            "The two kinds share a meat def."
        );
        var wanted = FreshCorpse(scene, 0);
        _ = FreshCorpse(scene, 1, OtherPrey);

        Expect.AreEqual(wanted.EstimatedMeatCount(), CountCorpses(job));
    }

    // Regression guard: the 'unforbid' policy decides which corpses get unforbidden, not which
    // corpses count towards the threshold. The threshold filter alone decides that.

    [Test]
    public static void CorpseOfADisallowedAnimalIsCountedWhenTheFilterAllowsItsMeat()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job.UnforbidAllCorpses = false;
        job.TriggerThreshold.ThresholdFilter.SetAllow(OtherPrey.race.race.meatDef, true);
        var corpse = FreshCorpse(scene, 0, OtherPrey);
        Expect.IsFalse(job.AllowedAnimals.Contains(OtherPrey), "The kind should be disallowed.");

        Expect.AreEqual(corpse.EstimatedMeatCount(), CountCorpses(job));
    }

    [Test]
    public static void HumanCorpseIsCountedWhenTheFilterAllowsHumanMeat()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job.UnforbidHumanCorpses = false;
        job.TriggerThreshold.ThresholdFilter.SetAllow(ThingDefOf.Meat_Human, true);
        var corpse = SpawnCorpse(scene, PawnKindDefOf.Drifter, Slot(scene, 0));
        var meat = corpse.EstimatedMeatCount();
        Expect.IsTrue(meat > 0, "The corpse yields no meat.");

        Expect.AreEqual(meat, CountCorpses(job));
    }

    // Hunting grounds

    [Test]
    public static void CorpsesOutsideTheHuntingGroundsAreNotCounted()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var inside = FreshCorpse(scene, 0);
        _ = FreshCorpse(scene, 1);
        job.HuntingGrounds = scene.AddArea("CMRTest-corpse-grounds", [inside.Position]);

        Expect.AreEqual(inside.EstimatedMeatCount(), CountCorpses(job));
    }

    [Test]
    public static void CorpsesInsideAnInvertedHuntingGroundAreNotCounted()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var inside = FreshCorpse(scene, 0);
        var outside = FreshCorpse(scene, 1);
        job.HuntingGrounds = scene.AddArea("CMRTest-corpse-grounds-inverted", [inside.Position]);
        job.InvertHuntingGrounds = true;

        Expect.AreEqual(outside.EstimatedMeatCount(), CountCorpses(job));
    }

    // Designated animals

    [Test]
    public static void DesignatedAnimalsAreSummedByTheirEstimatedMeat()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var first = scene.SpawnAnimal(Prey, Slot(scene, 0));
        var second = scene.SpawnAnimal(Prey, Slot(scene, 1));
        job.AddDesignation(new Designation(first, DesignationDefOf.Hunt));
        job.AddDesignation(new Designation(second, DesignationDefOf.Hunt));

        Expect.AreEqual(
            first.EstimatedMeatCount() + second.EstimatedMeatCount(),
            CountDesignated(job)
        );
    }

    [Test]
    public static void DesignatedAnimalsAreSummedByTheirEstimatedLeatherWhenTargetingLeather()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene, ManagerJob_Hunting.HuntingTargetResource.Leather);
        var animal = scene.SpawnAnimal(Prey, Slot(scene, 0));
        job.AddDesignation(new Designation(animal, DesignationDefOf.Hunt));

        Expect.AreEqual(animal.EstimatedLeatherCount(), CountDesignated(job));
    }

    [Test]
    public static void DesignationsOnNonPawnsAreIgnored()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var animal = scene.SpawnAnimal(Prey, Slot(scene, 0));
        var item = scene.SpawnThing(ThingDefOf.Steel, Slot(scene, 1), 5);
        job.AddDesignation(new Designation(animal, DesignationDefOf.Hunt), addToGame: false);
        job.AddDesignation(new Designation(item, DesignationDefOf.Hunt), addToGame: false);

        Expect.AreEqual(animal.EstimatedMeatCount(), CountDesignated(job));
    }

    // Planning which corpses to unforbid

    private static Corpse ForbiddenCorpse(
        ManagerScene scene,
        int slot,
        PawnKindDef kind,
        bool human = false
    )
    {
        var corpse = SpawnCorpse(scene, human ? PawnKindDefOf.Drifter : kind, Slot(scene, slot));
        corpse.SetForbidden(true, warnOnFail: false);
        return corpse;
    }

    private static ManagerJob_Hunting.HuntingWorkData PlanUnforbid(
        ManagerJob_Hunting job,
        int startCount = 0
    )
    {
        var data = new ManagerJob_Hunting.HuntingWorkData();
        ManagerScene.RunToCompletion(job.PlanUnforbidCorpses(new Boxed<int>(startCount), data));
        return data;
    }

    [Test]
    public static void UnforbidPlansAnAllowedAnimalsCorpseAtItsYield()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        var corpse = ForbiddenCorpse(scene, 0, Prey);

        var data = PlanUnforbid(job);

        Expect.AreEqual(1, data.CorpsesToUnforbid.Count);
        Expect.AreEqual(corpse.EstimatedMeatCount(), data.CorpsesToUnforbid[0].Yield);
    }

    [Test]
    public static void UnforbidLeavesADisallowedAnimalsCorpseAloneWhenUnforbidAllIsOff()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job.UnforbidAllCorpses = false;
        _ = ForbiddenCorpse(scene, 0, OtherPrey);

        Expect.IsEmpty(PlanUnforbid(job).CorpsesToUnforbid);
    }

    [Test]
    public static void UnforbidPlansHumanCorpsesEvenWhenTheirMeatIsNotInTheThresholdFilter()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job.UnforbidAllCorpses = true;
        job.UnforbidHumanCorpses = true;
        Expect.IsFalse(
            job.TriggerThreshold.ThresholdFilter.Allows(ThingDefOf.Meat_Human),
            "Human meat should not be in the filter."
        );
        var corpse = ForbiddenCorpse(scene, 0, Prey, human: true);

        var data = PlanUnforbid(job, startCount: 7);

        Expect.AreEqual(1, data.CorpsesToUnforbid.Count);
        Expect.ReferencesAreEqual(corpse, data.CorpsesToUnforbid[0].Corpse);
        Expect.AreEqual(0, data.CorpsesToUnforbid[0].Yield, "Uncounted meat adds to the count.");
        Expect.AreEqual(7, data.CorpsesToUnforbid[0].CountAfter);
    }

    [Test]
    public static void UnforbidLeavesHumanCorpsesAloneWithoutTheHumanSetting()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job.UnforbidAllCorpses = true;
        job.UnforbidHumanCorpses = false;
        _ = ForbiddenCorpse(scene, 0, Prey, human: true);

        Expect.IsEmpty(PlanUnforbid(job).CorpsesToUnforbid);
    }

    [Test]
    public static void UnforbidStillPlansUncountedCorpsesOnceTheTargetIsMet()
    {
        using var scene = new ManagerScene();
        var job = NewJob(scene);
        job.UnforbidAllCorpses = true;
        job.UnforbidHumanCorpses = true;
        job.TriggerThreshold.Op = Trigger_Threshold.Ops.LowerThan;
        job.TriggerThreshold.TargetCount = 5;
        _ = ForbiddenCorpse(scene, 0, Prey);
        var human = ForbiddenCorpse(scene, 1, Prey, human: true);

        var data = PlanUnforbid(job, startCount: 5);

        Expect.AreEqual(1, data.CorpsesToUnforbid.Count);
        Expect.ReferencesAreEqual(human, data.CorpsesToUnforbid[0].Corpse);
    }
}
