// ResourceSyncSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// The allowed-list / threshold-filter sync and the lock-to-map refresh of the Hunting, Foraging
/// and Forestry jobs, on real jobs and the live def database.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ResourceSyncSceneTests
{
    private static List<T> DistinctByResource<T>(
        IEnumerable<T> defs,
        Func<T, ThingDef?> resource
    ) =>
        [
            .. defs.Where(d => resource(d) != null)
                .GroupBy(d => resource(d))
                .Where(g => g.Count() == 1)
                .Select(g => g.Single())
                .Take(3),
        ];

    private static List<T> NotInList<T>(IEnumerable<T> candidates, IEnumerable<T> lockedList) =>
        [.. candidates.Except(lockedList)];

    // ---- Hunting ----

    private static ThingDef? Meat(PawnKindDef kind) => kind.RaceProps.meatDef;

    private static ManagerJob_Hunting AddHuntingJob(ManagerScene scene)
    {
        var job = scene.AddJob<ManagerJob_Hunting>("CM_HuntingManager");
        job.AnimalsLockedToMap = false;
        job.TargetResource = ManagerJob_Hunting.HuntingTargetResource.Meat;
        return job;
    }

    [Test]
    public static void HuntingSetAnimalAllowedSyncsTheThresholdFilter()
    {
        using var scene = new ManagerScene();
        var job = AddHuntingJob(scene);
        var animals = DistinctByResource(job.AllAnimals, Meat);
        Expect.IsTrue(animals.Count >= 2, "There are animals with distinct meat");
        var (a, b) = (animals[0], animals[1]);

        job.SetAnimalAllowed(a, true);
        job.SetAnimalAllowed(b, true);
        Expect.IsTrue(job.TriggerThreshold.ThresholdFilter.Allows(a.RaceProps.meatDef));
        Expect.IsTrue(job.TriggerThreshold.ThresholdFilter.Allows(b.RaceProps.meatDef));

        job.SetAnimalAllowed(a, false);
        Expect.IsFalse(job.TriggerThreshold.ThresholdFilter.Allows(a.RaceProps.meatDef));
        Expect.IsTrue(job.TriggerThreshold.ThresholdFilter.Allows(b.RaceProps.meatDef));
        Expect.IsFalse(job.AllowedAnimals.Contains(a));
        Expect.IsTrue(job.AllowedAnimals.Contains(b));
    }

    [Test]
    public static void HuntingSetAnimalAllowedWithoutSyncLeavesTheThresholdFilterAlone()
    {
        using var scene = new ManagerScene();
        var job = AddHuntingJob(scene);
        var animal = DistinctByResource(job.AllAnimals, Meat)[0];
        job.TriggerThreshold.ThresholdFilter.SetAllow(animal.RaceProps.meatDef, false);

        job.SetAnimalAllowed(animal, true, sync: false);

        Expect.IsTrue(job.AllowedAnimals.Contains(animal));
        Expect.IsFalse(job.TriggerThreshold.ThresholdFilter.Allows(animal.RaceProps.meatDef));
    }

    [Test]
    public static void HuntingLockToMapRefreshesTheListAndParentFilterImmediately()
    {
        using var scene = new ManagerScene();
        var job = AddHuntingJob(scene);
        var unlocked = job.AllAnimals.ToList();
        job.AnimalsLockedToMap = true;
        var locked = job.AllAnimals.ToList();
        job.AnimalsLockedToMap = false;
        var notOnMap = DistinctByResource(NotInList(unlocked, locked), Meat)[0];
        Expect.IsTrue(job.TriggerThreshold.ParentFilter.Allows(notOnMap.RaceProps.meatDef));

        job.AnimalsLockedToMap = true;

        Expect.IsTrue(job.AllAnimals.Count < unlocked.Count, "Locking narrows the list");
        Expect.IsFalse(job.AllAnimals.Contains(notOnMap));
        Expect.AreEqual(
            job.AllAnimals.Any(k => k.RaceProps.meatDef == notOnMap.RaceProps.meatDef),
            job.TriggerThreshold.ParentFilter.Allows(notOnMap.RaceProps.meatDef)
        );

        job.AnimalsLockedToMap = false;

        Expect.AreEqual(unlocked.Count, job.AllAnimals.Count);
        Expect.IsTrue(job.TriggerThreshold.ParentFilter.Allows(notOnMap.RaceProps.meatDef));
    }

    [Test]
    public static void HuntingPostImportDropsAnimalsNoLongerInTheList()
    {
        using var scene = new ManagerScene();
        var job = AddHuntingJob(scene);
        var unlocked = job.AllAnimals.ToList();
        job.AnimalsLockedToMap = true;
        var locked = job.AllAnimals.ToList();
        job.AnimalsLockedToMap = false;
        var notOnMap = NotInList(unlocked, locked)[0];
        job.SetAnimalAllowed(notOnMap, true);

        job.AnimalsLockedToMap = true;
        job.PostImport();

        Expect.IsFalse(job.AllowedAnimals.Contains(notOnMap));
    }

    [Test]
    public static void HuntingFilterChangeUpdatesAllowedAnimalsOnlyWhenSyncingFromTheFilter()
    {
        using var scene = new ManagerScene();
        var job = AddHuntingJob(scene);
        var animal = DistinctByResource(job.AllAnimals, Meat)[0];
        var meat = animal.RaceProps.meatDef;
        job.TriggerThreshold.ThresholdFilter.SetAllow(meat, true);

        job.Sync = Utilities.SyncDirection.AllowedToFilter;
        job.Notify_ThresholdFilterChanged();
        Expect.IsFalse(job.AllowedAnimals.Contains(animal), "Nothing changes in this direction");

        job.Sync = Utilities.SyncDirection.FilterToAllowed;
        job.Notify_ThresholdFilterChanged();
        Expect.IsTrue(job.AllowedAnimals.Contains(animal));

        job.TriggerThreshold.ThresholdFilter.SetAllow(meat, false);
        job.Notify_ThresholdFilterChanged();
        Expect.IsFalse(job.AllowedAnimals.Contains(animal));
    }

    [Test]
    public static void HuntingDesignationLabelDescribesTheTarget()
    {
        using var scene = new ManagerScene();
        var job = AddHuntingJob(scene);
        var kind = PawnKindDef.Named("Muffalo");
        var animal = PawnGenerator.GeneratePawn(kind);
        _ = GenSpawn.Spawn(animal, scene.Cells(1, 1, offsetX: 5).First(), scene.Map);
        try
        {
            var label = job.DesignationLabel(new Designation(animal, DesignationDefOf.Hunt));

            Expect.IsNotNull(label);
            Expect.IsTrue(
                label != null && label.Contains(animal.LabelCap, StringComparison.Ordinal)
            );
            Expect.IsNull(
                job.DesignationLabel(
                    new Designation(
                        new LocalTargetInfo(scene.Cells(1, 1).First()),
                        DesignationDefOf.Hunt
                    )
                )
            );
        }
        finally
        {
            animal.Destroy();
        }
    }

    // ---- Foraging ----

    private static ThingDef? Harvested(ThingDef plant) => plant.plant?.harvestedThingDef;

    private static ManagerJob_Foraging AddForagingJob(ManagerScene scene)
    {
        var job = scene.AddJob<ManagerJob_Foraging>("CM_ForagingManager");
        job.PlantsLockedToMap = false;
        return job;
    }

    [Test]
    public static void ForagingSetPlantAllowedSyncsTheThresholdFilter()
    {
        using var scene = new ManagerScene();
        var job = AddForagingJob(scene);
        var plants = DistinctByResource(job.AllPlants, Harvested);
        Expect.IsTrue(plants.Count >= 2, "There are plants with distinct harvests");
        var (a, b) = (plants[0], plants[1]);

        job.SetPlantAllowed(a, true);
        job.SetPlantAllowed(b, true);
        Expect.IsTrue(job.TriggerThreshold.ThresholdFilter.Allows(Harvested(a)));

        job.SetPlantAllowed(a, false);
        Expect.IsFalse(job.TriggerThreshold.ThresholdFilter.Allows(Harvested(a)));
        Expect.IsTrue(job.TriggerThreshold.ThresholdFilter.Allows(Harvested(b)));
        Expect.IsFalse(job.AllowedPlants.Contains(a));
    }

    [Test]
    public static void ForagingLockToMapRefreshesListPrunesAllowedAndFixesParentFilter()
    {
        using var scene = new ManagerScene();
        var job = AddForagingJob(scene);
        var unlocked = job.AllPlants.ToList();
        job.PlantsLockedToMap = true;
        var locked = job.AllPlants.ToList();
        job.PlantsLockedToMap = false;
        var plant = DistinctByResource(NotInList(unlocked, locked), Harvested)[0];
        job.SetPlantAllowed(plant, true);
        Expect.IsTrue(job.TriggerThreshold.ParentFilter.Allows(Harvested(plant)));

        job.PlantsLockedToMap = true;

        Expect.IsTrue(job.AllPlants.Count < unlocked.Count, "Locking narrows the list");
        Expect.IsFalse(job.AllowedPlants.Contains(plant), "Allowed plants outside the list go");
        Expect.AreEqual(
            job.AllPlants.Any(p => Harvested(p) == Harvested(plant)),
            job.TriggerThreshold.ParentFilter.Allows(Harvested(plant))
        );

        job.PlantsLockedToMap = false;

        Expect.AreEqual(unlocked.Count, job.AllPlants.Count);
        Expect.IsTrue(job.TriggerThreshold.ParentFilter.Allows(Harvested(plant)));
    }

    // ---- Forestry ----

    private static ManagerJob_Forestry AddForestryJob(ManagerScene scene)
    {
        var job = scene.AddJob<ManagerJob_Forestry>("CM_ForestryManager");
        job.PlantsLockedToMap = false;
        return job;
    }

    [Test]
    public static void ForestrySetTreeAllowedKeepsTheHarvestAllowedWhileAnyTreeSharingItIsAllowed()
    {
        using var scene = new ManagerScene();
        var job = AddForestryJob(scene);
        var wood = job
            .AllPlants.Where(t => Harvested(t) != null)
            .GroupBy(Harvested)
            .First(g => g.Count() >= 2)
            .ToList();
        var (a, b) = (wood[0], wood[1]);
        var harvest = Harvested(a);

        job.SetTreeAllowed(a, true);
        job.SetTreeAllowed(b, true);
        Expect.IsTrue(job.TriggerThreshold.ThresholdFilter.Allows(harvest));

        job.SetTreeAllowed(a, false);
        Expect.IsTrue(job.TriggerThreshold.ThresholdFilter.Allows(harvest), "b still uses it");
        Expect.IsFalse(job.AllowedTrees.Contains(a));

        job.SetTreeAllowed(b, false);
        Expect.IsFalse(job.TriggerThreshold.ThresholdFilter.Allows(harvest));
    }

    [Test]
    public static void ForestryLockToMapRefreshesListPrunesAllowedAndFixesParentFilter()
    {
        using var scene = new ManagerScene();
        var job = AddForestryJob(scene);
        var unlocked = job.AllPlants.ToList();
        job.PlantsLockedToMap = true;
        var locked = job.AllPlants.ToList();
        job.PlantsLockedToMap = false;
        var tree = NotInList(unlocked, locked).First(t => Harvested(t) != null);
        job.SetTreeAllowed(tree, true);
        Expect.IsTrue(job.TriggerThreshold.ParentFilter.Allows(Harvested(tree)));

        job.PlantsLockedToMap = true;

        Expect.IsTrue(job.AllPlants.Count < unlocked.Count, "Locking narrows the list");
        Expect.IsFalse(job.AllowedTrees.Contains(tree));
        Expect.AreEqual(
            job.AllPlants.Any(t => Harvested(t) == Harvested(tree)),
            job.TriggerThreshold.ParentFilter.Allows(Harvested(tree))
        );

        job.PlantsLockedToMap = false;

        Expect.AreEqual(unlocked.Count, job.AllPlants.Count);
        Expect.IsTrue(job.TriggerThreshold.ParentFilter.Allows(Harvested(tree)));
    }

    [Test]
    public static void ForestryFilterChangeUpdatesAllowedTreesOnlyWhenSyncingFromTheFilter()
    {
        using var scene = new ManagerScene();
        var job = AddForestryJob(scene);
        var tree = DistinctByResource(job.AllPlants, Harvested)[0];
        job.TriggerThreshold.ThresholdFilter.SetAllow(Harvested(tree), true);

        job.Sync = Utilities.SyncDirection.AllowedToFilter;
        job.Notify_ThresholdFilterChanged();
        Expect.IsFalse(job.AllowedTrees.Contains(tree));

        job.Sync = Utilities.SyncDirection.FilterToAllowed;
        job.Notify_ThresholdFilterChanged();
        Expect.IsTrue(job.AllowedTrees.Contains(tree));
    }
}
