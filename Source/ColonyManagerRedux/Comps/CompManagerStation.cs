// CompManagerStation.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024–2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// A component for manager stations, providing UI gizmos and debug actions for the Colony Manager mod.
/// </summary>
public class CompManagerStation : ThingComp
{
    /// <summary>
    /// Gets the properties for this manager station component.
    /// </summary>
    public CompProperties_ManagerStation Props => (CompProperties_ManagerStation)props;

    private CoroutineHandle? _handle;

    /// <inheritdoc/>
    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);

        if (!respawningAfterLoad)
        {
            Manager.For(parent.Map).TryApplyDefaultTemplateOnFirstManagerStation();
        }
    }

    /// <summary>
    /// Returns extra gizmos for the manager station, including the main manager tab and debug actions.
    /// </summary>
    /// <returns>An enumerable of additional gizmos.</returns>
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        yield return new Command_Action
        {
            action = () =>
                Find.MainTabsRoot.SetCurrentTab(ManagerMainButtonDefOf.ColonyManagerRedux_Manager),
            defaultLabel = "ColonyManagerRedux.ManagerStation.OpenManagerTab".Translate(),
            defaultDesc = "ColonyManagerRedux.ManagerStation.OpenManagerTab.Tip".Translate(),
            icon = Resources.ManagerTab_Gizmo,
        };

        if (DebugSettings.ShowDevGizmos)
        {
            yield return new Command_Action
            {
                defaultLabel =
                    _handle == null || _handle.IsCompleted
                        ? "DEV: Run manager job"
                        : "DEV: Running manager job...",
                action = () =>
                {
                    if (_handle == null || _handle.IsCompleted)
                    {
                        ColonyManagerReduxMod.Instance.LogVerboseMessage(
                            $"Manually running a job due to 'DEV: Manage Jobs' command."
                        );
                        var manager = Manager.For(parent.Map);
                        var coroutine = manager.TryDoWork();
                        if (coroutine != null)
                        {
                            _handle = MultiTickCoroutineManager.StartCoroutine(coroutine);
                        }
                        else
                        {
                            Messages.Message(
                                "No manager jobs currently need to run.",
                                MessageTypeDefOf.RejectInput,
                                false
                            );
                        }
                    }
                },
                Disabled = _handle != null && !_handle.IsCompleted,
            };
        }
    }
}
