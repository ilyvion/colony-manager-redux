// ManagerJobTemplatesSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// Saving, loading and applying manager job templates through real files. Every template uses a
/// unique <c>CMRTest-</c> name so the player's own templates are never touched.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class ManagerJobTemplatesSceneTests
{
    private const string LoadingVarsWarning = "during LoadingVars";

    [Test]
    [WarningsAllowed(LoadingVarsWarning)]
    public static void SavedTemplateExistsAndIsListedInOrder()
    {
        using var scene = new ManagerScene();
        var second = scene.NewTemplateName("b");
        var first = scene.NewTemplateName("a");
        var job = scene.AddJob<TestManagerJobA>();

        ManagerJobTemplates.SaveTemplate(scene.Manager, second, [job]);
        ManagerJobTemplates.SaveTemplate(scene.Manager, first, [job]);

        Expect.IsTrue(ManagerJobTemplates.TemplateExists(first));
        var names = ManagerJobTemplates.GetTemplateNames();
        Expect.IsTrue(names.Contains(first) && names.Contains(second));
        Expect.IsTrue(names.IndexOf(first) < names.IndexOf(second));
    }

    [Test]
    [WarningsAllowed(LoadingVarsWarning)]
    public static void LoadedTemplateJobsAreResetToTemplateState()
    {
        using var scene = new ManagerScene();
        var name = scene.NewTemplateName();
        var a = scene.AddJob<TestManagerJobA>();
        var b = scene.AddJob<TestManagerJobB>();
        a.IsSuspended = true;
        a.State = ManagerJobState.Completed;

        ManagerJobTemplates.SaveTemplate(scene.Manager, name, scene.Manager.JobTracker.Jobs);
        var loaded = ManagerJobTemplates.TryLoadTemplateJobs(scene.Manager, name);

        Expect.IsNotNull(loaded);
        if (loaded == null)
        {
            return;
        }
        Expect.AreEqual(2, loaded.Count);
        Expect.IsTrue(loaded[0] is TestManagerJobA && loaded[1] is TestManagerJobB);
        Expect.All(loaded, j => !j.IsSuspended && j.JobState == ManagerJobState.Active);
        Expect.All(loaded, j => j.Priority == 0);
        Expect.IsFalse(scene.Manager.JobTracker.HasJob(loaded[0]), "Loading does not add jobs");
        Expect.IsTrue(a.IsSuspended, "The source job is untouched");
        _ = b;
    }

    [Test]
    [WarningsAllowed(LoadingVarsWarning)]
    public static void ApplyTemplateAddsEveryJobAsManaged()
    {
        using var scene = new ManagerScene();
        var name = scene.NewTemplateName();
        var a = scene.AddJob<TestManagerJobA>();
        var b = scene.AddJob<TestManagerJobB>();
        ManagerJobTemplates.SaveTemplate(scene.Manager, name, [a, b]);
        scene.Manager.JobTracker.Delete(a);
        scene.Manager.JobTracker.Delete(b);

        var count = ManagerJobTemplates.ApplyTemplate(scene.Manager, name);

        Expect.AreEqual(2, count);
        Expect.AreEqual(2, scene.Manager.JobTracker.Jobs.Count());
        Expect.All(scene.Manager.JobTracker.Jobs, j => j.IsManaged);
        Expect.AreEqual(2, scene.Manager.JobTracker.MaxPriority + 1);
    }

    [Test]
    public static void MissingTemplateLoadsAsNullWithoutError()
    {
        using var scene = new ManagerScene();
        var name = scene.NewTemplateName();

        Expect.IsFalse(ManagerJobTemplates.TemplateExists(name));
        Expect.IsNull(ManagerJobTemplates.TryLoadTemplateJobs(scene.Manager, name));
        Expect.IsTrue(ManagerJobTemplates.ApplyTemplate(scene.Manager, name) == null);
    }

    [Test]
    [WarningsAllowed(LoadingVarsWarning)]
    public static void SaveTemplateRestoresScribingFlags()
    {
        using var scene = new ManagerScene();
        var name = scene.NewTemplateName();
        var job = scene.AddJob<TestManagerJobA>();

        ManagerJobTemplates.SaveTemplate(scene.Manager, name, [job]);
        Expect.IsTrue(scene.Manager.ScribeSameGameData);
        Expect.IsTrue(scene.Manager.ScribeSameMapData);

        _ = ManagerJobTemplates.TryLoadTemplateJobs(scene.Manager, name);
        Expect.IsTrue(scene.Manager.ScribeSameGameData);
        Expect.IsTrue(scene.Manager.ScribeSameMapData);
    }

    [Test]
    [ErrorsAllowed(".*")]
    [WarningsAllowed(".*")]
    public static void CorruptTemplateLoadsAsNullAndRestoresScribingFlags()
    {
        using var scene = new ManagerScene();
        var name = scene.NewTemplateName();
        File.WriteAllText(ManagerJobTemplates.FilePath(name), "<this is not><valid xml");

        List<ManagerJob>? loaded = null;
        try
        {
            loaded = ManagerJobTemplates.TryLoadTemplateJobs(scene.Manager, name);
        }
        catch (Exception)
        {
            // Loading may throw before the guarded section; the flags must still be intact.
        }

        Expect.IsNull(loaded);
        Expect.IsTrue(scene.Manager.ScribeSameGameData);
        Expect.IsTrue(scene.Manager.ScribeSameMapData);
        Expect.AreEqual(LoadSaveMode.Inactive, Scribe.mode);
    }

    [Test]
    public static void SaveTemplateWithNullNameThrows()
    {
        using var scene = new ManagerScene();
        _ = Expect.Throws<ArgumentNullException>(() =>
            ManagerJobTemplates.SaveTemplate(scene.Manager, null!, [])
        );
    }

    [Test]
    [WarningsAllowed(LoadingVarsWarning)]
    public static void DeleteRemovesTheFileAndIsANoOpTheSecondTime()
    {
        using var scene = new ManagerScene();
        var name = scene.NewTemplateName();
        var job = scene.AddJob<TestManagerJobA>();
        ManagerJobTemplates.SaveTemplate(scene.Manager, name, [job]);

        ManagerJobTemplates.DeleteTemplate(name);
        Expect.IsFalse(ManagerJobTemplates.TemplateExists(name));

        ManagerJobTemplates.DeleteTemplate(name);
        Expect.IsFalse(ManagerJobTemplates.TemplateExists(name));
    }

    [Test]
    public static void SaveTemplateWithAnEmptyNameShowsAnErrorDialogAndSavesNothing()
    {
        using var scene = new ManagerScene();
        var job = scene.AddJob<TestManagerJobA>();
        var namesBefore = ManagerJobTemplates.GetTemplateNames();
        var windowsBefore = Find.WindowStack.Windows.ToHashSet();
        try
        {
            ManagerJobTemplates.SaveTemplate(scene.Manager, "", [job]);

            var opened = Find.WindowStack.Windows.Where(w => !windowsBefore.Contains(w)).ToList();
            Expect.AreEqual(1, opened.Count);
            Expect.IsTrue(opened.All(w => w is Dialog_MessageBox));
            Expect.That(() => ManagerJobTemplates.GetTemplateNames().SequenceEqual(namesBefore));
            Expect.IsTrue(scene.Manager.ScribeSameGameData);
            Expect.IsTrue(scene.Manager.ScribeSameMapData);
            Expect.AreEqual(LoadSaveMode.Inactive, Scribe.mode);
        }
        finally
        {
            foreach (
                var window in Find
                    .WindowStack.Windows.Where(w => !windowsBefore.Contains(w))
                    .ToList()
            )
            {
                _ = Find.WindowStack.TryRemove(window, doCloseSound: false);
            }
        }
    }
}
