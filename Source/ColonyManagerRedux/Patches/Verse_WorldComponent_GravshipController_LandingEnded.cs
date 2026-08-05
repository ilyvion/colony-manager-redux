// Verse_WorldComponent_GravshipController_LandingEnded.cs
// Copyright (c) 2025–2026 Alexander Krivács Schrøder

#if !v1_5
using RimWorld.Planet;

namespace ColonyManagerRedux;

[HarmonyPatch(typeof(WorldComponent_GravshipController), "LandingEnded")]
internal static class Verse_WorldComponent_GravshipController_LandingEnded
{
    internal static void Prefix(out Building_GravEngine __state, Gravship ___gravship) =>
        // Store the gravship's grav engine for use in the postfix
        __state = ___gravship.Engine;

    internal static void Postfix(Building_GravEngine __state)
    {
        var managerDatabase = __state.ManagerDatabase();
        if (managerDatabase == null)
        {
            return;
        }
        var compManagerDatabase = managerDatabase.TryGetComp<CompManagerDatabase>();
        if (compManagerDatabase == null)
        {
            ColonyManagerReduxMod.Instance.LogError(
                "GravshipController.InitiateLanding: CompManagerDatabase not found on grav ship's manager database."
            );
            return;
        }
        if (compManagerDatabase.JobTransferData == null)
        {
            ColonyManagerReduxMod.Instance.LogError(
                "GravshipController.InitiateLanding: JobTransferData is null, cannot transfer jobs."
            );
            return;
        }

        var manager = Manager.For(__state.Map);

        using var jobTransferDataStream = new MemoryStream(compManagerDatabase.JobTransferData);
        using var loadStreamReader = new StreamReader(jobTransferDataStream);

        List<ManagerJob> jobList = [];
        CustomStreamReaderScribeLoader.InitLoading(loadStreamReader);
        try
        {
            manager.ScribeSameMapData = false;
            ScribeMetaHeaderUtility.LoadGameDataHeader(
                ScribeMetaHeaderUtility.ScribeHeaderMode.None,
                logVersionConflictWarning: true
            );
            Scribe_Collections.Look(ref jobList, "jobList", LookMode.Deep, manager);
            Scribe.loader.FinalizeLoading();
        }
        catch (Exception ex)
        {
            Scribe.ForceStop();
            ColonyManagerReduxMod.Instance.LogException(
                "GravshipController.InitiateLanding: Failed to load job list from grav ship's manager database.",
                ex
            );
        }
        finally
        {
            manager.ScribeSameMapData = true;
            compManagerDatabase.JobTransferData = null; // Clear the data after use
        }

        var localJobs = manager.JobTracker.JobList.ToList();

        var action = DetermineLandingAction(
            localJobs.Count,
            jobList.Count,
            ColonyManagerReduxMod.Settings.GravshipJobConflictResolution
        );
        switch (action)
        {
            case GravshipLandingAction.Import:
                ImportJobs(manager, jobList);
                break;
            case GravshipLandingAction.KeepLocalOnly:
                Messages.Message(
                    "ColonyManagerRedux.Gravship.KeptLocalJobsMessage".Translate(),
                    MessageTypeDefOf.TaskCompletion
                );
                break;
            case GravshipLandingAction.KeepGravshipOnly:
                Messages.Message(
                    "ColonyManagerRedux.Gravship.KeptGravshipJobsMessage".Translate(),
                    MessageTypeDefOf.TaskCompletion
                );
                DeleteJobs(manager, localJobs);
                ImportJobs(manager, jobList);
                break;
            case GravshipLandingAction.Ask:
            default:
                Find.WindowStack.Add(
                    new Dialog_GravshipJobConflict(
                        localJobs.Count,
                        jobList.Count,
                        keepLocalJobs: () => { },
                        keepGravshipJobs: () =>
                        {
                            DeleteJobs(manager, localJobs);
                            ImportJobs(manager, jobList);
                        },
                        keepBothJobs: () => ImportJobs(manager, jobList),
                        chooseIndividually: () =>
                            Find.WindowStack.Add(
                                new Dialog_GravshipJobPicker(
                                    localJobs,
                                    jobList,
                                    onConfirm: (keptLocalJobs, keptGravshipJobs) =>
                                    {
                                        DeleteJobs(manager, [.. localJobs.Except(keptLocalJobs)]);
                                        ImportJobs(manager, keptGravshipJobs);
                                    }
                                )
                            )
                    )
                );
                break;
        }
    }

    /// <summary>
    /// Pure decision behind the landing postfix: what to do with a landing gravship's manager
    /// jobs given how many jobs already exist locally, how many the gravship carries, and the
    /// configured conflict-resolution setting. Kept separate so it's unit-testable without a
    /// live <see cref="Manager"/>.
    /// </summary>
    internal static GravshipLandingAction DetermineLandingAction(
        int localJobCount,
        int gravshipJobCount,
        GravshipJobConflictResolution resolution
    )
    {
        if (localJobCount == 0 || gravshipJobCount == 0)
        {
            // Nothing to reconcile: either the ship brought no jobs, or the map had none yet.
            return GravshipLandingAction.Import;
        }

        return resolution switch
        {
            GravshipJobConflictResolution.KeepLocalJobs => GravshipLandingAction.KeepLocalOnly,
            GravshipJobConflictResolution.KeepGravshipJobs =>
                GravshipLandingAction.KeepGravshipOnly,
            GravshipJobConflictResolution.MergeJobs => GravshipLandingAction.Import,
            GravshipJobConflictResolution.AlwaysAsk => GravshipLandingAction.Ask,
            _ => GravshipLandingAction.Ask,
        };
    }

    private static void ImportJobs(Manager manager, List<ManagerJob> jobList)
    {
        foreach (var job in jobList)
        {
            try
            {
                job.PreImport();
                manager.JobTracker.Add(job);
                job.PostImportInt();
            }
            catch (Exception err)
            {
                ColonyManagerReduxMod.Instance.LogException(
                    "ManagerJob caused exception while being imported after gravship landing.",
                    err
                );
            }
        }
    }

    private static void DeleteJobs(Manager manager, List<ManagerJob> jobList)
    {
        foreach (var job in jobList)
        {
            manager.JobTracker.Delete(job);
        }
    }
}

/// <summary>
/// What to do with a landing gravship's manager jobs relative to the ones already on the map it's
/// landing on. See <see cref="Verse_WorldComponent_GravshipController_LandingEnded.DetermineLandingAction"/>.
/// </summary>
internal enum GravshipLandingAction
{
    /// <summary>
    /// Import the gravship's jobs as-is; there's no conflict to resolve.
    /// </summary>
    Import,

    /// <summary>
    /// Keep only the jobs already on the map; discard the gravship's jobs.
    /// </summary>
    KeepLocalOnly,

    /// <summary>
    /// Delete the map's existing jobs and import the gravship's jobs in their place.
    /// </summary>
    KeepGravshipOnly,

    /// <summary>
    /// Prompt the player to choose which jobs to keep.
    /// </summary>
    Ask,
}
#endif // !v1_5
