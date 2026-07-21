// Verse_WorldComponent_GravshipController_InitiateTakeoff.cs
// Copyright (c) 2025–2026 Alexander Krivács Schrøder

#if !v1_5
namespace ColonyManagerRedux;

[HarmonyPatch(
    typeof(WorldComponent_GravshipController),
    nameof(WorldComponent_GravshipController.InitiateTakeoff)
)]
internal static class Verse_WorldComponent_GravshipController_InitiateTakeoff
{
    private static void Prefix(Building_GravEngine engine)
    {
        var managerDatabase = engine.ManagerDatabase();
        if (managerDatabase == null)
        {
            return;
        }
        var compManagerDatabase = managerDatabase.TryGetComp<CompManagerDatabase>();
        if (compManagerDatabase == null)
        {
            ColonyManagerReduxMod.Instance.LogError(
                "GravshipController.InitiateTakeoff: CompManagerDatabase not found on grav engine's manager database."
            );
            return;
        }

        var map = engine.Map;
        var manager = Manager.For(map);

        byte[] bytes;
        using (var m = new MemoryStream())
        {
            try
            {
                manager.ScribeSameMapData = false;
                CustomStreamScribeSaver.InitSaving(m, "JobList", false);
                ScribeMetaHeaderUtility.WriteMetaHeader();
                var jobList = manager
                    .JobTracker.JobsOfType<ManagerJob>()
                    .Where(j => j.IsTransferable)
                    .ToList();
                foreach (var job in jobList)
                {
                    try
                    {
                        job.PreExport();
                    }
                    catch (Exception err)
                    {
                        ColonyManagerReduxMod.Instance.LogException(
                            $"ManagerJob caused exception during {nameof(ManagerJob.PreExport)}",
                            err
                        );
                    }
                }
                Scribe_Collections.Look(ref jobList, "jobList", LookMode.Deep);
                foreach (var job in jobList)
                {
                    try
                    {
                        job.PostExport();
                    }
                    catch (Exception err)
                    {
                        ColonyManagerReduxMod.Instance.LogException(
                            $"ManagerJob caused exception during {nameof(ManagerJob.PostExport)}",
                            err
                        );
                    }
                }
            }
            catch (Exception err)
            {
                ColonyManagerReduxMod.Instance.LogException(
                    "Failed to serialize job list for gravship takeoff.",
                    err
                );
            }
            finally
            {
                Scribe.saver.FinalizeSaving();
                manager.ScribeSameMapData = true;
            }

            bytes = m.GetBuffer();
            ColonyManagerReduxMod.Instance.LogDevMessage(
                "Serialized job data:\n\n" + System.Text.Encoding.UTF8.GetString(bytes)
            );
        }

        compManagerDatabase.JobTransferData = bytes;
    }
}
#endif // !v1_5
