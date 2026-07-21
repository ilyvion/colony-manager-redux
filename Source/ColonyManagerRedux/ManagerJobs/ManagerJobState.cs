// ManagerJobState.cs
// Copyright (c) 2024–2025 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Represents the state of a manager job.
/// </summary>
public enum ManagerJobState
{
    /// <summary>
    /// The job is currently active.
    /// </summary>
    Active = 0,

    /// <summary>
    /// The job has been completed.
    /// </summary>
    Completed = 1,
}
