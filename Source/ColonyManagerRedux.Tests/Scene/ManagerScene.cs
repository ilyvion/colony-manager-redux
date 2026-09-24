// ManagerScene.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// Shared, disposable scene for in-game manager tests. Everything a test adds through it is
/// removed again in <see cref="Dispose"/>, since all <c>Playing</c> fixtures share one map.
/// </summary>
internal sealed class ManagerScene : IDisposable
{
    private readonly List<ManagerJob> _jobs = [];
    private readonly List<Thing> _things = [];
    private readonly List<Zone> _zones = [];
    private readonly List<Area> _areas = [];
    private readonly List<string> _templates = [];
    private readonly HashSet<ManagerLog> _logsBefore;

    public Map Map { get; }
    public Manager Manager { get; }

    public ManagerScene(Map? map = null)
    {
        Map = map ?? Find.CurrentMap;
        Manager = Manager.For(Map);

        Expect.IsTrue(
            Manager.JobTracker.HasNoJobs,
            "The previous test left manager jobs behind on the shared map."
        );
        _logsBefore = [.. Logs()];
    }

    private IEnumerable<ManagerLog> Logs() =>
        Manager.CompsOfType<IJobLogger>().SelectMany(l => l.Logs);

    /// <summary>Logs added to the manager's job loggers since the scene was created.</summary>
    public IEnumerable<ManagerLog> NewLogs => Logs().Where(l => !_logsBefore.Contains(l));

    /// <summary>
    /// Creates a job of <typeparamref name="T"/> through the tracker, so it is deleted again on
    /// dispose.
    /// </summary>
    public T AddJob<T>(bool managed = true)
        where T : ManagerJob
    {
        var job = (T)Activator.CreateInstance(typeof(T), Manager);
        return Register(job, managed);
    }

    /// <summary>
    /// Creates a real job through <see cref="ManagerDefMaker"/>.
    /// </summary>
    public T AddJob<T>(string managerDefName, bool managed = true)
        where T : ManagerJob
    {
        var def = DefDatabase<ManagerDef>.GetNamed(managerDefName);
        var job = (T)ManagerDefMaker.MakeManagerJob(def, Manager)!;
        return Register(job, managed);
    }

    /// <summary>
    /// Adds an already-made job to the tracker and this scene's clean-up list.
    /// </summary>
    public T Register<T>(T job, bool managed = true)
        where T : ManagerJob
    {
        job.IsManaged = managed;
        Manager.JobTracker.Add(job);
        _jobs.Add(job);
        return job;
    }

    /// <summary>Remembers a template name so <see cref="Dispose"/> deletes its file.</summary>
    public string NewTemplateName(string suffix = "")
    {
        var name = "CMRTest-" + suffix + Guid.NewGuid().ToString("N");
        _templates.Add(name);
        return name;
    }

    /// <summary>Spawns a thing and removes it again on dispose.</summary>
    public Thing SpawnThing(ThingDef def, IntVec3 cell, int count = 1, ThingDef? stuff = null)
    {
        var thing = ThingMaker.MakeThing(def, stuff ?? GenStuff.DefaultStuffFor(def));
        thing.stackCount = count;
        _ = GenSpawn.Spawn(thing, cell, Map);
        _things.Add(thing);
        return thing;
    }

    /// <summary>Spawns a colony-owned building and removes it again on dispose.</summary>
    public Building SpawnBuilding(ThingDef def, IntVec3 cell)
    {
        var building = (Building)ThingMaker.MakeThing(def, GenStuff.DefaultStuffFor(def));
        building.SetFactionDirect(Faction.OfPlayer);
        _ = GenSpawn.Spawn(building, cell, Map);
        _things.Add(building);
        return building;
    }

    /// <summary>Creates a stockpile zone over <paramref name="cells"/>.</summary>
    public Zone_Stockpile AddStockpile(IEnumerable<IntVec3> cells)
    {
        var stockpile = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, Map.zoneManager);
        Map.zoneManager.RegisterZone(stockpile);
        foreach (var cell in cells)
        {
            stockpile.AddCell(cell);
        }
        stockpile.settings.filter.SetAllowAll(null);
        _zones.Add(stockpile);
        return stockpile;
    }

    /// <summary>Creates an allowed area with the given label over <paramref name="cells"/>.</summary>
    public Area_Allowed AddArea(string label, IEnumerable<IntVec3> cells)
    {
        _ = Map.areaManager.TryMakeNewAllowed(out var area);
        area.SetLabel(label);
        foreach (var cell in cells)
        {
            area[cell] = true;
        }
        _areas.Add(area);
        return area;
    }

    /// <summary>A block of cells near the map centre, for placing things away from map edges.</summary>
    public IEnumerable<IntVec3> Cells(int width, int height, int offsetX = 0, int offsetZ = 0)
    {
        var origin = Map.Center + new IntVec3(offsetX, 0, offsetZ);
        for (var x = 0; x < width; x++)
        {
            for (var z = 0; z < height; z++)
            {
                yield return origin + new IntVec3(x, 0, z);
            }
        }
    }

    /// <summary>Runs a coroutine to completion without needing game ticks.</summary>
    public static void RunToCompletion(Coroutine? coroutine) =>
        coroutine?.RunImmediatelyToCompletion();

    public void Dispose()
    {
        foreach (var job in Manager.JobTracker.Jobs.ToList())
        {
            // The Power job is a map-wide singleton whose CleanUp deliberately throws.
            Manager.JobTracker.Delete(job, cleanup: job is not ManagerJob_Power);
        }
        _jobs.Clear();

        foreach (var thing in _things.Where(t => !t.Destroyed))
        {
            thing.Destroy();
        }
        _things.Clear();

        foreach (var zone in _zones.Where(Map.zoneManager.AllZones.Contains))
        {
            zone.Delete();
        }
        _zones.Clear();

        foreach (var area in _areas.Where(Map.areaManager.AllAreas.Contains))
        {
            area.Delete();
        }
        _areas.Clear();

        foreach (var name in _templates)
        {
            ManagerJobTemplates.DeleteTemplate(name);
        }
        _templates.Clear();
    }
}
