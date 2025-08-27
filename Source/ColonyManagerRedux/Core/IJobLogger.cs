// IJobLogger.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Interface for logging job-related events and retrieving job logs.
/// </summary>
public interface IJobLogger
{
    /// <summary>
    /// Adds a log entry to the job logger.
    /// </summary>
    /// <param name="log">The log entry to add.</param>
    void AddLog(ManagerLog log);

    /// <summary>
    /// Gets an enumerable collection of all job logs.
    /// </summary>
    IEnumerable<ManagerLog> Logs
    {
        get;
    }
}
