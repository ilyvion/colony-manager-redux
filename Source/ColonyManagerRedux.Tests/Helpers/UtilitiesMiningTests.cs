// UtilitiesMiningTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using RimTestRedux;
using Task = ColonyManagerRedux.Managers.ManagerJob_Mining.Task;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class UtilitiesMiningTests
{
    private static readonly List<Task> AllTasks =
    [
        Task.HaulChunks,
        Task.DeconstructBuildings,
        Task.Mine,
    ];

    [Test]
    public static void CompleteListIsUnchanged()
    {
        var current = new List<Task> { Task.Mine, Task.HaulChunks, Task.DeconstructBuildings };

        var result = Utilities_Mining.EnsureAllEnumValuesPresent(current, AllTasks);

        Assert.ThatCollection(result).Has.Count(3);
        Assert.ThatCollection(result).Does.Contain(Task.Mine);
        Assert.ThatCollection(result).Does.Contain(Task.HaulChunks);
        Assert.ThatCollection(result).Does.Contain(Task.DeconstructBuildings);
    }

    [Test]
    public static void MissingValueIsAppendedAtEndPreservingOrder()
    {
        var current = new List<Task> { Task.Mine, Task.HaulChunks };

        var result = Utilities_Mining.EnsureAllEnumValuesPresent(current, AllTasks);

        Assert.ThatCollection(result).Has.Count(3);
        Assert.That(result[0]).Is.EqualTo(Task.Mine);
        Assert.That(result[1]).Is.EqualTo(Task.HaulChunks);
        Assert.That(result[2]).Is.EqualTo(Task.DeconstructBuildings);
    }

    [Test]
    public static void EmptyListGetsAllValuesInEnumOrder()
    {
        var result = Utilities_Mining.EnsureAllEnumValuesPresent([], AllTasks);

        Assert.ThatCollection(result).Has.Count(3);
        Assert.That(result[0]).Is.EqualTo(Task.HaulChunks);
        Assert.That(result[1]).Is.EqualTo(Task.DeconstructBuildings);
        Assert.That(result[2]).Is.EqualTo(Task.Mine);
    }

    [Test]
    public static void NullListGetsAllValuesInEnumOrder()
    {
        var result = Utilities_Mining.EnsureAllEnumValuesPresent(null, AllTasks);

        Assert.ThatCollection(result).Has.Count(3);
        Assert.That(result[0]).Is.EqualTo(Task.HaulChunks);
        Assert.That(result[1]).Is.EqualTo(Task.DeconstructBuildings);
        Assert.That(result[2]).Is.EqualTo(Task.Mine);
    }
}
