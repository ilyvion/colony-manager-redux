// CompProperties_ManagerStation.cs
// Copyright Karel Kroeze, 2017-2020
// Copyright (c) 2024–2025 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Component properties for a manager station, including work speed.
/// </summary>
public class CompProperties_ManagerStation : CompProperties
{
    /// <summary>
    /// The work speed provided by the manager station. Lower is faster; it's the amount of work required to finish the manager job.
    /// </summary>
    public int speed = 250;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompProperties_ManagerStation"/> class.
    /// </summary>
    public CompProperties_ManagerStation()
    {
        compClass = typeof(CompManagerStation);
    }
}
