// Controller.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// The main mod class for Colony Manager Redux.
/// </summary>
public class ColonyManagerReduxMod : IlyvionMod
{
#pragma warning disable CS8618 // Set by constructor
    /// <summary>
    /// Gets the singleton instance of the <see cref="ColonyManagerReduxMod"/> class.
    /// </summary>
    public static ColonyManagerReduxMod Instance
    {
        get; private set;
    }

#pragma warning restore CS8618

    /// <inheritdoc/>
    protected override bool HasSettings => true;

    /// <summary>
    /// Gets the settings for the Colony Manager Redux mod.
    /// </summary>
    public static Settings Settings => Instance.GetSettings<Settings>();

    /// <summary>
    /// Initializes a new instance of the <see cref="ColonyManagerReduxMod"/> class.
    /// </summary>
    /// <param name="content">The mod content pack.</param>
    public ColonyManagerReduxMod(ModContentPack content) : base(content)
    {
        // This is kind of stupid, but also kind of correct. Correct wins.
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        Instance = this;

        // apply fixes
        var harmony = new Harmony(content.PackageId);
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

    /// <inheritdoc/>
    public override void DoSettingsWindowContents(Rect inRect) => Settings.DoSettingsWindowContents(inRect);

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
public sealed class HotSwappableAttribute : Attribute
{
}
