// ManagerJob_Production.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerJob_Production(Manager manager)
    : ManagerJob<ManagerSettings_Production>(manager)
{
    public override IEnumerable<string> Targets => [];

    public override WorkTypeDef? WorkTypeDef => null;

    public override void CleanUp(ManagerLog? jobLog = null) { }
}
