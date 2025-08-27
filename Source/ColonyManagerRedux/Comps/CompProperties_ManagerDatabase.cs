// CompProperties_ManagerDatabase.cs
// Copyright (c) 2025 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Properties for the manager database component.
/// </summary>
public class CompProperties_ManagerDatabase : CompProperties
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CompProperties_ManagerDatabase"/> class.
    /// </summary>
    public CompProperties_ManagerDatabase()
    {
        compClass = typeof(CompManagerDatabase);
    }
}
