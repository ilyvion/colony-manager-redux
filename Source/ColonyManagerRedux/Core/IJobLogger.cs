// IJobLogger.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public interface IJobLogger
{
    void AddLog(ManagerLog log);
    IEnumerable<ManagerLog> Logs { get; }
}
