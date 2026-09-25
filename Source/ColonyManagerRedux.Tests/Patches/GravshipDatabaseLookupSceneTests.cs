// GravshipDatabaseLookupSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

#if !v1_5
using DevTools.Testing;
using RimWorld.Planet;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// Finding the manager database on a grav engine's connected substructure and among a
/// gravship's things.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class GravshipDatabaseLookupSceneTests
{
    private static ThingDef DatabaseDef => DefDatabase<ThingDef>.GetNamed("CM_ManagerDatabase");

    private static IntVec3 Cell(ManagerScene scene, int i) =>
        scene.Cells(1, 1, -70 + (i * 4), -70).First();

    private static Building_GravEngine SpawnEngine(
        ManagerScene scene,
        params IntVec3[] substructure
    )
    {
        var def = DefDatabase<ThingDef>.GetNamedSilentFail("GravEngine");
        Expect.IsNotNull(def, "Odyssey's grav engine def must be present.");
        var engine = (Building_GravEngine)scene.SpawnBuilding(def, Cell(scene, 5));
        engine.substructureDirty = false;
        engine.allConnectedSubstructure.Clear();
        foreach (var cell in substructure)
        {
            _ = engine.allConnectedSubstructure.Add(cell);
        }
        return engine;
    }

    [Test]
    public static void EngineFindsTheDatabaseOnConnectedSubstructure()
    {
        using var scene = new ManagerScene();
        var database = scene.SpawnBuilding(DatabaseDef, Cell(scene, 0));
        var engine = SpawnEngine(scene, Cell(scene, 1), Cell(scene, 0));

        Expect.ReferencesAreEqual(database, engine.ManagerDatabase());
    }

    [Test]
    public static void EngineIgnoresADatabaseOnUnconnectedSubstructure()
    {
        using var scene = new ManagerScene();
        _ = scene.SpawnBuilding(DatabaseDef, Cell(scene, 0));
        var engine = SpawnEngine(scene, Cell(scene, 1));

        Expect.IsNull(engine.ManagerDatabase());
    }

    [Test]
    public static void EngineWithoutADatabaseFindsNone()
    {
        using var scene = new ManagerScene();
        var engine = SpawnEngine(scene, Cell(scene, 0), Cell(scene, 1));

        Expect.IsNull(engine.ManagerDatabase());
    }

    private static Gravship NewGravship() =>
        (Gravship)Activator.CreateInstance(typeof(Gravship), nonPublic: true);

    [Test]
    public static void GravshipFindsTheDatabaseAmongItsThings()
    {
        using var scene = new ManagerScene();
        var database = scene.SpawnBuilding(DatabaseDef, Cell(scene, 0));
        var other = scene.SpawnThing(ThingDefOf.Steel, Cell(scene, 1));
        var gravship = NewGravship();
        gravship.things[other] = new PositionData(IntVec3.Zero, Rot4.North);
        gravship.things[database] = new PositionData(IntVec3.Zero, Rot4.North);

        Expect.ReferencesAreEqual(database, gravship.ManagerDatabase());
    }

    [Test]
    public static void GravshipWithoutADatabaseFindsNone()
    {
        using var scene = new ManagerScene();
        var other = scene.SpawnThing(ThingDefOf.Steel, Cell(scene, 1));
        var gravship = NewGravship();
        gravship.things[other] = new PositionData(IntVec3.Zero, Rot4.North);

        Expect.IsNull(gravship.ManagerDatabase());
    }
}
#endif // !v1_5
