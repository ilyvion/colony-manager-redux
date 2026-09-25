// DefaultTemplateSceneTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// Applying the configured default template the first time a manager station is built. Each
/// test restores the settings and the manager's checked flag it changes.
/// </summary>
[TestFixture(TestType.Playing)]
internal sealed class DefaultTemplateSceneTests
{
    private const string LoadingVarsWarning = "during LoadingVars";

    private static void WithDefaultTemplate(
        ManagerScene scene,
        string? templateName,
        bool autoApply,
        Action action
    )
    {
        var settings = ColonyManagerReduxMod.Settings;
        var previousName = settings.DefaultTemplateName;
        var previousAutoApply = settings.AutoApplyDefaultTemplateOnFirstStation;
        var previousChecked = scene.Manager._hasCheckedDefaultTemplate;
        settings.DefaultTemplateName = templateName;
        settings.AutoApplyDefaultTemplateOnFirstStation = autoApply;
        scene.Manager._hasCheckedDefaultTemplate = false;
        try
        {
            action();
        }
        finally
        {
            settings.DefaultTemplateName = previousName;
            settings.AutoApplyDefaultTemplateOnFirstStation = previousAutoApply;
            scene.Manager._hasCheckedDefaultTemplate = previousChecked;
        }
    }

    private static string SaveTemplateWithTwoJobs(ManagerScene scene)
    {
        var name = scene.NewTemplateName();
        var a = scene.AddJob<TestManagerJobA>();
        var b = scene.AddJob<TestManagerJobB>();
        ManagerJobTemplates.SaveTemplate(scene.Manager, name, [a, b]);
        scene.Manager.JobTracker.Delete(a);
        scene.Manager.JobTracker.Delete(b);
        return name;
    }

    [Test]
    [WarningsAllowed(LoadingVarsWarning)]
    public static void FirstCallAppliesTheTemplateAndLaterCallsDoNot()
    {
        using var scene = new ManagerScene();
        var name = SaveTemplateWithTwoJobs(scene);

        WithDefaultTemplate(
            scene,
            name,
            true,
            () =>
            {
                scene.Manager.TryApplyDefaultTemplateOnFirstManagerStation();
                Expect.AreEqual(2, scene.Manager.JobTracker.Jobs.Count());

                scene.Manager.TryApplyDefaultTemplateOnFirstManagerStation();
                Expect.AreEqual(2, scene.Manager.JobTracker.Jobs.Count());
            }
        );
    }

    // The map counts as checked as soon as the first station is built, so enabling the setting
    // afterwards does not apply the template retroactively.
    [Test]
    [WarningsAllowed(LoadingVarsWarning)]
    public static void DisabledSettingAppliesNothingButStillMarksTheMapAsChecked()
    {
        using var scene = new ManagerScene();
        var name = SaveTemplateWithTwoJobs(scene);

        WithDefaultTemplate(
            scene,
            name,
            false,
            () =>
            {
                scene.Manager.TryApplyDefaultTemplateOnFirstManagerStation();

                Expect.IsTrue(scene.Manager.JobTracker.HasNoJobs);
                Expect.IsTrue(scene.Manager._hasCheckedDefaultTemplate);

                ColonyManagerReduxMod.Settings.AutoApplyDefaultTemplateOnFirstStation = true;
                scene.Manager.TryApplyDefaultTemplateOnFirstManagerStation();
                Expect.IsTrue(scene.Manager.JobTracker.HasNoJobs);
            }
        );
    }

    [Test]
    public static void NoConfiguredTemplateAppliesNothing()
    {
        using var scene = new ManagerScene();

        WithDefaultTemplate(
            scene,
            null,
            true,
            () =>
            {
                scene.Manager.TryApplyDefaultTemplateOnFirstManagerStation();

                Expect.IsTrue(scene.Manager.JobTracker.HasNoJobs);
            }
        );
    }

    [Test]
    public static void ConfiguredTemplateThatNoLongerExistsAppliesNothing()
    {
        using var scene = new ManagerScene();
        var missing = scene.NewTemplateName("missing");

        WithDefaultTemplate(
            scene,
            missing,
            true,
            () =>
            {
                scene.Manager.TryApplyDefaultTemplateOnFirstManagerStation();

                Expect.IsTrue(scene.Manager.JobTracker.HasNoJobs);
            }
        );
    }
}
