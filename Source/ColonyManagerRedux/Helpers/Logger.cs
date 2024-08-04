// Logger.cs
// Copyright Karel Kroeze, 2017-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Diagnostics;

namespace ColonyManagerRedux;

internal static class Logger
{
    [Conditional("DEBUG_FOLLOW")]
    public static void Follow(string message)
    {
        ColonyManagerReduxMod.Instance.LogDevMessage(message);
    }
}
