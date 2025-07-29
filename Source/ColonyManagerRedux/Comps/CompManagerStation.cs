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
                defaultLabel = _handle == null || _handle.IsCompleted ? "DEV: Run manager job" : "DEV: Running manager job...",
                action = () =>
                    {
                        if (_handle == null || _handle.IsCompleted)
                        {
                            ColonyManagerReduxMod.Instance.LogVerboseMessage($"Manually running a job due to 'DEV: Manage Jobs' command.");
                            Manager manager = Manager.For(parent.Map);
                            var coroutine = manager.TryDoWork();
                            if (coroutine != null)
                            {
                                _handle = MultiTickCoroutineManager.StartCoroutine(coroutine);
                            }
                            else
                            {
                                Messages.Message("No manager jobs currently need to run.", MessageTypeDefOf.RejectInput, false);
                            }
                        }
                    },
                Disabled = _handle != null && !_handle.IsCompleted
            };
        }
    }
}
