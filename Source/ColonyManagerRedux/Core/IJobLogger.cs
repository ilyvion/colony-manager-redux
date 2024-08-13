// IJobLogger.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.Collections;

namespace ColonyManagerRedux;

public interface IJobLogger
{
    void AddLog(ManagerLog log);
    IEnumerable<ManagerLog> Logs { get; }
}
