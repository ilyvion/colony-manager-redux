// Verse_WorldComponent_GravshipController_LandingEnded.cs
// Copyright (c) 2025 Alexander Krivács Schrøder

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
}
#endif // !v1_5
