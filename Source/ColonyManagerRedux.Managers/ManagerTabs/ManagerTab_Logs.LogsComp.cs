// ManagerTab_Logs.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.Collections;

namespace ColonyManagerRedux.Managers;

partial class ManagerTab_Logs
{
    [HotSwappable]
    public sealed class LogsComp : ManagerComp, IJobLogger
    {
        // This is set by Initialize immediately after the comp is instantiated
        private CircularBuffer<ManagerLog> _logs = null!;
        public IEnumerable<ManagerLog> Logs => _logs;

        public override void Initialize()
        {
            var logSettings = ColonyManagerReduxMod.Settings
                .ManagerSettingsFor<ManagerSettings_Logs>(ManagerDefOf.CM_LogsManager)!;
            _logs = new(logSettings.KeepLogCount);
        }

        public void AddLog(ManagerLog log)
        {
            _logs.PushBack(log);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_CircularBuffer.Look(ref _logs!, "logs");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                var logSettings = ColonyManagerReduxMod.Settings
                    .ManagerSettingsFor<ManagerSettings_Logs>(ManagerDefOf.CM_LogsManager)!;
                if (_logs == null)
                {
                    _logs = new(logSettings.KeepLogCount);
                }
                else if (_logs.Capacity != logSettings.KeepLogCount)
                {
                    // The KeepLogCount setting has changed; we need to resize the logs buffer.
                    var oldLogs = _logs;
                    _logs = new(logSettings.KeepLogCount);
                    foreach (var log in oldLogs.Reverse().Take(logSettings.KeepLogCount))
                    {
                        _logs.PushFront(log);
                    }
                }
            }
        }
    }
}

internal static class LogsComp_ManagerLogsExtensions
{
    public static void AddLog(this Manager manager, ManagerLog log)
    {
        manager.CompOfType<ManagerTab_Logs.LogsComp>()!.AddLog(log);
    }
    public static IEnumerable<ManagerLog> Logs(this Manager manager)
    {
        return manager.CompOfType<ManagerTab_Logs.LogsComp>()!.Logs;
    }
}
