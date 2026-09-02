// Dialog_NewManualGroup.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux.Managers;

internal sealed class RenamableJobGroup(string initialName) : IRenameable
{
    public string RenamableLabel { get; set; } = initialName;
    public string BaseLabel => RenamableLabel;
    public string InspectLabel => RenamableLabel;
}

internal sealed class Dialog_NewManualGroup(
    IReadOnlyCollection<string> existingGroups,
    Action<string> onNamed
) : Dialog_Rename<RenamableJobGroup>(new RenamableJobGroup(""))
{
    protected override AcceptanceReport NameIsValid(string name)
    {
        var baseReport = base.NameIsValid(name);
        return !baseReport.Accepted ? baseReport
            : existingGroups.Contains(name, StringComparer.OrdinalIgnoreCase)
                ? "ColonyManagerRedux.Overview.ManualGroup.NameInUse".Translate(name)
            : true;
    }

    protected override void OnRenamed(string name) => onNamed(name);
}
