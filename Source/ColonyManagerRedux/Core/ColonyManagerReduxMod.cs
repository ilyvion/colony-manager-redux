// ColonyManagerReduxMod.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024–2025 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// The main mod class for Colony Manager Redux.
/// </summary>
public partial class ColonyManagerReduxMod
{
    partial void Construct()
    {
        // apply fixes
        var harmony = new Harmony(PackageId);
        //Harmony.DEBUG = true;
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        //Harmony.DEBUG = false;

        LongEventHandler.ExecuteWhenFinished(() =>
        {
            // We need to load settings here at the latest because if we end up waiting until during
            //  a game load, it leads to the ScribeLoader exception
            // "Called InitLoading() but current mode is LoadingVars"
            // because you can't Scribe multiple things at once.
            _ = Settings;
        });
    }

    /// <summary>
    /// Logs a verbose message if verbose logging is enabled in the settings.
    /// </summary>
    /// <param name="message">The message to log.</param>
    public void LogVerboseMessage(string message)
    {
        if (Settings.DoVerboseLogging)
        {
            LogDevMessage($"[Verbose] {message}");
        }
    }

    /// <inheritdoc/>
    public override void LogDebug(string message) => base.LogDebug($"[Debug] {message}");
}

/// <summary>
/// Indicates that a class or struct supports hot swapping at runtime.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class HotSwappableAttribute : Attribute { }
