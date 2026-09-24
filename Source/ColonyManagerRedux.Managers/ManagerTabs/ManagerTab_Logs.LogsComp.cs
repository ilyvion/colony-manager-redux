// ManagerTab_Logs.LogsComp.cs
// Copyright (c) 2024–2025 Alexander Krivács Schrøder

using ilyvion.Laboratory.Collections;

namespace ColonyManagerRedux.Managers;

internal partial class ManagerTab_Logs
{
    [HotSwappable]
    public sealed class LogsComp : ManagerComp, IJobLogger
    {
        // This is set by Initialize immediately after the comp is instantiated
        private CircularBuffer<ManagerLog> _logs = null!;
        public IEnumerable<ManagerLog> Logs => _logs;
        private bool _enabled = true;

        public override void Initialize()
        {
            var logSettings =
                ColonyManagerReduxMod.Settings.ManagerSettingsFor<ManagerSettings_Logs>(
                    ManagerDefOf.CM_LogsManager
                )!;
            var (capacity, enabled) = ComputeCapacity(logSettings.KeepLogCount);
            _logs = new(capacity);
            _enabled = enabled;
        }

        internal static (int Capacity, bool Enabled) ComputeCapacity(int keepLogCount) =>
            (keepLogCount > 0 ? keepLogCount : 1, keepLogCount > 0);

        internal static List<T> ResizeKeepingNewest<T>(
            IReadOnlyList<T> oldestToNewest,
            int newCapacity
        ) => [.. oldestToNewest.Skip(Math.Max(0, oldestToNewest.Count - newCapacity))];

        public void AddLog(ManagerLog log)
        {
            if (_enabled)
            {
                _logs.PushBack(log);
            }
        }

        public override void Notify_JobDeleted(ManagerJob job)
        {
            foreach (var log in _logs)
            {
                log.ForgetJob(job);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();

            Scribe_CircularBuffer.Look(ref _logs!, "logs");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                var logSettings =
                    ColonyManagerReduxMod.Settings.ManagerSettingsFor<ManagerSettings_Logs>(
                        ManagerDefOf.CM_LogsManager
                    )!;
                var (capacity, enabled) = ComputeCapacity(logSettings.KeepLogCount);
                _enabled = enabled;
                if (_logs == null)
                {
                    _logs = new(capacity);
                }
                else if (!_enabled)
                {
                    _logs.Clear();
                }
                else if (_logs.Capacity != logSettings.KeepLogCount)
                {
                    // The KeepLogCount setting has changed; we need to resize the logs buffer.
                    var oldLogs = _logs;
                    _logs = new(capacity);
                    foreach (var log in ResizeKeepingNewest([.. oldLogs], capacity))
                    {
                        _logs.PushBack(log);
                    }
                }
            }
        }
    }
}

internal static class LogsComp_ManagerLogsExtensions
{
    public static void AddLog(this Manager manager, ManagerLog log) =>
        manager.CompOfType<ManagerTab_Logs.LogsComp>()!.AddLog(log);

    public static IEnumerable<ManagerLog> Logs(this Manager manager) =>
        manager.CompOfType<ManagerTab_Logs.LogsComp>()!.Logs;
}
