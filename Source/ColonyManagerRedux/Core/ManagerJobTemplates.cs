// ManagerJobTemplates.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Handles saving and loading of named, reusable manager job templates: sets of manager jobs
/// that were exported once and can then be applied to any map, either manually or automatically
/// when the first manager station is built on a map (see <see cref="Settings.DefaultTemplateName"/>).
/// </summary>
public static class ManagerJobTemplates
{
    /// <summary>
    /// The file extension used for saved manager job templates.
    /// </summary>
    public const string TemplateExtension = ".cmt";

    /// <summary>
    /// Gets the folder templates are stored in.
    /// </summary>
    public static string GetTemplateSaveLocation() =>
        GenFilePaths.FolderUnderSaveData("ManagerJobTemplates");

    internal static string FilePath(string name) =>
        GetTemplateSaveLocation() + "/" + name + TemplateExtension;

    /// <summary>
    /// Gets the names of all currently saved templates, ordered by name.
    /// </summary>
    public static List<string> GetTemplateNames()
    {
        var directoryInfo = new DirectoryInfo(GetTemplateSaveLocation());
        if (!directoryInfo.Exists)
        {
            return [];
        }

        return
        [
            .. directoryInfo
                .GetFiles()
                .Where(f => f.Extension == TemplateExtension)
                .Select(f => Path.GetFileNameWithoutExtension(f.Name))
                .OrderBy(n => n),
        ];
    }

    /// <summary>
    /// Determines whether a template with the given name exists.
    /// </summary>
    public static bool TemplateExists(string name) => File.Exists(FilePath(name));

    /// <summary>
    /// Deletes the named template, if it exists.
    /// </summary>
    public static void DeleteTemplate(string name)
    {
        var filePath = FilePath(name);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
    }

    /// <summary>
    /// Saves the given jobs as a named template that can later be applied to any map.
    /// </summary>
    public static void SaveTemplate(Manager manager, string name, IEnumerable<ManagerJob> jobs)
    {
        if (manager == null)
        {
            throw new ArgumentNullException(nameof(manager));
        }
        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        var exportJobs = jobs.ToList();

        var previousSameMapData = manager.ScribeSameMapData;
        var previousSameGameData = manager.ScribeSameGameData;
        manager.ScribeSameMapData = false;
        manager.ScribeSameGameData = false;
        try
        {
            try
            {
                Scribe.saver.InitSaving(FilePath(name), "ManagerJobTemplate");
            }
            catch (Exception ex)
            {
                GenUI.ErrorDialog("ProblemSavingFile".Translate(ex.ToString()));
                return;
            }

            ScribeMetaHeaderUtility.WriteMetaHeader();

            foreach (var job in exportJobs)
            {
                job.PreExport();
            }
            Scribe_Collections.Look(ref exportJobs, "jobs", LookMode.Deep);
            foreach (var job in exportJobs)
            {
                job.PostExport();
            }
        }
        finally
        {
            manager.ScribeSameMapData = previousSameMapData;
            manager.ScribeSameGameData = previousSameGameData;
            Scribe.saver.FinalizeSaving();
        }
    }

    /// <summary>
    /// Loads (but does not apply) the jobs contained in the named template. Returns <see
    /// langword="null"/> if the template does not exist or fails to load.
    /// </summary>
    public static List<ManagerJob>? TryLoadTemplateJobs(Manager manager, string name)
    {
        if (manager == null)
        {
            throw new ArgumentNullException(nameof(manager));
        }

        var filePath = FilePath(name);
        if (!File.Exists(filePath))
        {
            return null;
        }

        List<ManagerJob>? loadedJobs = null;
        PreLoadUtility.CheckVersionAndLoad(
            filePath,
            ScribeMetaHeaderUtility.ScribeHeaderMode.None,
            () =>
            {
                Scribe.loader.InitLoading(filePath);

                var previousSameMapData = manager.ScribeSameMapData;
                var previousSameGameData = manager.ScribeSameGameData;
                manager.ScribeSameMapData = false;
                manager.ScribeSameGameData = false;
                try
                {
                    List<ManagerJob> jobs = [];
                    ScribeMetaHeaderUtility.LoadGameDataHeader(
                        ScribeMetaHeaderUtility.ScribeHeaderMode.None,
                        logVersionConflictWarning: true
                    );
                    Scribe_Collections.Look(ref jobs, "jobs", LookMode.Deep, manager);
                    Scribe.loader.FinalizeLoading();
                    loadedJobs = jobs;
                }
                catch (Exception ex)
                {
                    ColonyManagerReduxMod.Instance.LogError(
                        $"Exception while loading manager job template '{name}': {ex}"
                    );
                    Scribe.ForceStop();
                }
                finally
                {
                    manager.ScribeSameMapData = previousSameMapData;
                    manager.ScribeSameGameData = previousSameGameData;
                }
            }
        );

        return loadedJobs;
    }

    /// <summary>
    /// Loads and directly applies (adds to the map's job tracker) every valid job in the named
    /// template. Returns the number of jobs added, or <see langword="null"/> if the template
    /// could not be loaded.
    /// </summary>
    public static int? ApplyTemplate(Manager manager, string name)
    {
        var jobs = TryLoadTemplateJobs(manager, name);
        if (jobs == null)
        {
            return null;
        }

        var addedCount = 0;
        foreach (var job in jobs.Where(j => j.IsValid))
        {
            job.PreImport();
            manager.JobTracker.Add(job);
            job.PostImportInt();
            addedCount++;
        }

        return addedCount;
    }
}
