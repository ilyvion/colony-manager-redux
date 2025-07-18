// CompManagerStation.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public class CompManagerStation : ThingComp
{
    public CompProperties_ManagerStation Props => (CompProperties_ManagerStation)props;

    private CoroutineHandle? _handle;
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        yield return new Command_Action
        {
            action = () => Find.MainTabsRoot.SetCurrentTab(
                ManagerMainButtonDefOf.ColonyManagerRedux_Manager),
            defaultLabel = "ColonyManagerRedux.ManagerStation.OpenManagerTab".Translate(),
            defaultDesc = "ColonyManagerRedux.ManagerStation.OpenManagerTab.Tip".Translate(),
            icon = Resources.ManagerTab_Gizmo,
        };

        if (DebugSettings.ShowDevGizmos)
        {
            yield return new Command_Action
            {
                defaultLabel = "DEV: Manage Jobs",
                action = () =>
                    {
                        if (_handle == null || _handle.IsCompleted)
                        {
                            Manager manager = Manager.For(parent.Map);
                            var coroutine = manager.TryDoWork();
                            if (coroutine != null)
                            {
                                _handle = MultiTickCoroutineManager.StartCoroutine(coroutine);
                            }
                        }
                    }
            };
        }
    }
}
