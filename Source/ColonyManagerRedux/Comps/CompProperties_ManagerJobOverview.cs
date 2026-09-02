// CompProperties_ManagerJobOverview.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Properties for <see cref="CompManagerJobOverview"/>. Unlike most job comps this isn't
/// declared per-<see cref="ManagerDef"/> in XML — it carries no per-job-type configuration, so
/// it's injected onto every job-producing <c>ManagerDef</c> by an XML patch instead (see
/// Common/Patches).
/// </summary>
public class CompProperties_ManagerJobOverview : ManagerJobCompProperties
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompProperties_ManagerJobOverview"/> class.
    /// </summary>
    public CompProperties_ManagerJobOverview()
    {
        compClass = typeof(CompManagerJobOverview);
    }
}
