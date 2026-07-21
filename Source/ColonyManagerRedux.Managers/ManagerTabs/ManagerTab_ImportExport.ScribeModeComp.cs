// ManagerTab_ImportExport.ScribeModeComp.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024–2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

internal partial class ManagerTab_ImportExport
{
    public sealed class ScribeModeComp : ManagerComp
    {
        public ScribingMode Mode
        {
            get;
            internal set
            {
                field = value;
                Manager.ScribeSameMapData = Mode == ScribingMode.Normal;
                Manager.ScribeSameGameData = Mode == ScribingMode.Normal;
            }
        } = ScribingMode.Normal;
    }
}

internal static class ScribeModeCompManagerExtensions
{
    internal static ScribingMode SetScribingMode(this Manager manager, ScribingMode mode) =>
        manager == null
            ? throw new ArgumentNullException(nameof(manager))
            : (manager.CompOfType<ManagerTab_ImportExport.ScribeModeComp>()!.Mode = mode);
}
