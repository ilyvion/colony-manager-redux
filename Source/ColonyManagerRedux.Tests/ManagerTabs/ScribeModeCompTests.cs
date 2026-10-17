// ScribeModeCompTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.Playing)]
internal sealed class ScribeModeCompTests
{
    private static ScribingMode[] AllModes => Enum.GetValues<ScribingMode>();

    private static void WithRestoredMode(Manager manager, Action<Manager> test)
    {
        var previous = manager.CompOfType<ManagerTab_ImportExport.ScribeModeComp>()!.Mode;
        try
        {
            test(manager);
        }
        finally
        {
            _ = manager.SetScribingMode(previous);
        }
    }

    [Test]
    public static void NormalModeScribesBothSameMapAndSameGameData() =>
        WithRestoredMode(
            Manager.For(Find.CurrentMap),
            manager =>
            {
                _ = manager.SetScribingMode(ScribingMode.Normal);

                Expect.IsTrue(manager.ScribeSameMapData);
                Expect.IsTrue(manager.ScribeSameGameData);
            }
        );

    [Test]
    public static void EveryOtherModeScribesNeitherSameMapNorSameGameData() =>
        WithRestoredMode(
            Manager.For(Find.CurrentMap),
            manager =>
            {
                foreach (var mode in AllModes.Where(m => m != ScribingMode.Normal))
                {
                    _ = manager.SetScribingMode(mode);

                    Expect.IsFalse(manager.ScribeSameMapData, $"{mode} same map data");
                    Expect.IsFalse(manager.ScribeSameGameData, $"{mode} same game data");
                }
            }
        );

    [Test]
    public static void SwitchingBackToNormalRestoresBothFlags() =>
        WithRestoredMode(
            Manager.For(Find.CurrentMap),
            manager =>
            {
                foreach (var mode in AllModes.Where(m => m != ScribingMode.Normal))
                {
                    _ = manager.SetScribingMode(mode);
                    _ = manager.SetScribingMode(ScribingMode.Normal);

                    Expect.IsTrue(manager.ScribeSameMapData, $"after {mode}");
                    Expect.IsTrue(manager.ScribeSameGameData, $"after {mode}");
                }
            }
        );

    [Test]
    public static void SetScribingModeReturnsTheModeItSet()
    {
        var manager = Manager.For(Find.CurrentMap);

        WithRestoredMode(
            manager,
            _ => Expect.AreEqual(ScribingMode.Normal, manager.SetScribingMode(ScribingMode.Normal))
        );
    }

    [Test]
    public static void NullManagerThrows() =>
        _ = Expect.Throws<ArgumentNullException>(() =>
            ScribeModeCompManagerExtensions.SetScribingMode(null!, ScribingMode.Normal)
        );
}
