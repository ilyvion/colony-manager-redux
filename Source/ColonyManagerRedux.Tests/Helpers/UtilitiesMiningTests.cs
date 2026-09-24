// UtilitiesMiningTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;
using Task = ColonyManagerRedux.Managers.ManagerJob_Mining.Task;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class UtilitiesMiningTests
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

        Expect.AreEqual(3, result.Count);
        Expect.IsTrue(result.Contains(Task.Mine));
        Expect.IsTrue(result.Contains(Task.HaulChunks));
        Expect.IsTrue(result.Contains(Task.DeconstructBuildings));
    }

    [Test]
    public static void MissingValueIsAppendedAtEndPreservingOrder()
    {
        var current = new List<Task> { Task.Mine, Task.HaulChunks };

        var result = Utilities_Mining.EnsureAllEnumValuesPresent(current, AllTasks);

        Expect.AreEqual(3, result.Count);
        Expect.AreEqual(Task.Mine, result[0]);
        Expect.AreEqual(Task.HaulChunks, result[1]);
        Expect.AreEqual(Task.DeconstructBuildings, result[2]);
    }

    [Test]
    public static void EmptyListGetsAllValuesInEnumOrder()
    {
        var result = Utilities_Mining.EnsureAllEnumValuesPresent([], AllTasks);

        Expect.AreEqual(3, result.Count);
        Expect.AreEqual(Task.HaulChunks, result[0]);
        Expect.AreEqual(Task.DeconstructBuildings, result[1]);
        Expect.AreEqual(Task.Mine, result[2]);
    }

    [Test]
    public static void NullListGetsAllValuesInEnumOrder()
    {
        var result = Utilities_Mining.EnsureAllEnumValuesPresent(null, AllTasks);

        Expect.AreEqual(3, result.Count);
        Expect.AreEqual(Task.HaulChunks, result[0]);
        Expect.AreEqual(Task.DeconstructBuildings, result[1]);
        Expect.AreEqual(Task.Mine, result[2]);
    }
}
