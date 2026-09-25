// UtilitiesIncapableWorkTypeTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// <see cref="Utilities.IsIncapableOfWholeWorkType"/> is a static copy of vanilla's check; these
/// tests pin its behaviour and compare it with vanilla's so drift is caught.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class UtilitiesIncapableWorkTypeTests
{
    private static Pawn Unconscious(ManagerScene scene, IntVec3 cell)
    {
        var pawn = scene.SpawnColonist(cell);
        _ = pawn.health.AddHediff(HediffDefOf.Anesthetic);
        return pawn;
    }

    private static bool IsCapacityFree(WorkGiverDef giver) => giver.requiredCapacities.Count == 0;

    [Test]
    public static void NullArgumentsThrow()
    {
        using var scene = new ManagerScene();
        var pawn = scene.SpawnColonist(scene.Cells(1, 1, 30, 30).First());

        _ = Expect.Throws<ArgumentNullException>(() =>
            Utilities.IsIncapableOfWholeWorkType(null!, WorkTypeDefOf.Hauling)
        );
        _ = Expect.Throws<ArgumentNullException>(() =>
            Utilities.IsIncapableOfWholeWorkType(pawn, null!)
        );
    }

    [Test]
    public static void UnconsciousPawnIsIncapableOfWorkTypesWhoseGiversAllNeedCapacities()
    {
        using var scene = new ManagerScene();
        var pawn = Unconscious(scene, scene.Cells(1, 1, 30, 32).First());
        var needy = DefDatabase<WorkTypeDef>
            .AllDefsListForReading.Where(w =>
                w.workGiversByPriority.Count > 0 && !w.workGiversByPriority.Any(IsCapacityFree)
            )
            .ToList();

        Expect.IsNotEmpty(needy);
        Expect.All(needy, pawn.IsIncapableOfWholeWorkType);
    }

    [Test]
    public static void UnconsciousPawnIsNotIncapableWhenAGiverNeedsNoCapacity()
    {
        using var scene = new ManagerScene();
        var pawn = Unconscious(scene, scene.Cells(1, 1, 30, 34).First());
        var free = DefDatabase<WorkTypeDef>
            .AllDefsListForReading.Where(w => w.workGiversByPriority.Any(IsCapacityFree))
            .ToList();

        // Not every game setup has such a work type; the assertion only means something if one exists.
        if (free.Count == 0)
        {
            Test.Skip("No work type has a capacity-free work giver.");
            return;
        }

        Expect.All(free, w => !pawn.IsIncapableOfWholeWorkType(w));
    }

    [Test]
    public static void WorkTypeWithoutWorkGiversIsIncapable()
    {
        using var scene = new ManagerScene();
        var pawn = scene.SpawnColonist(scene.Cells(1, 1, 30, 36).First());

        Expect.IsTrue(
            pawn.IsIncapableOfWholeWorkType(new WorkTypeDef { defName = "CMR_NoGivers" })
        );
    }

    [Test]
    public static void MatchesVanillaForEveryWorkTypeAndPawnState()
    {
        using var scene = new ManagerScene();
        var healthy = scene.SpawnColonist(scene.Cells(1, 1, 30, 38).First());
        var unconscious = Unconscious(scene, scene.Cells(1, 1, 30, 40).First());
        var vanilla = new PawnColumnWorker_WorkPriority();

        foreach (var pawn in new[] { healthy, unconscious })
        {
            foreach (var work in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                Expect.AreEqual(
                    vanilla.IsIncapableOfWholeWorkType(pawn, work),
                    pawn.IsIncapableOfWholeWorkType(work),
                    $"{work.defName} for {pawn.LabelShort}"
                );
            }
        }
    }
}
