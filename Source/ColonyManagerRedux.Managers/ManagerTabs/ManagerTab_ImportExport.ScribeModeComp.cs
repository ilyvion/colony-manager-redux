// ManagerTab_ImportExport.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

partial class ManagerTab_ImportExport
{
    public sealed class ScribeModeComp : ManagerComp
    {
        private ScribingMode mode = ScribingMode.Normal;

        public ScribingMode Mode
        {
            get => mode; internal set
            {
                mode = value;
                Manager.ScribeGameSpecificData = Mode == ScribingMode.Normal;
            }
        }
    }
}

public static class ScribeModeCompManagerExtensions
{
    internal static ScribingMode SetScribingMode(this Manager manager, ScribingMode mode)
    {
        if (manager == null)
        {
            throw new ArgumentNullException(nameof(manager));
        }
        return manager.CompOfType<ManagerTab_ImportExport.ScribeModeComp>()!.Mode = mode;
    }
}
