// Constants.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Contains UI and configuration constants for Colony Manager Redux.
/// </summary>
public static class Constants
{
    /// <summary>
    /// The size of large icons in the UI.
    /// </summary>
    public const float LargeIconSize = 32f;

    /// <summary>
    /// The height of large list entries in the UI.
    /// </summary>
    public const float LargeListEntryHeight = 50f;

    /// <summary>
    /// The height of standard list entries in the UI.
    /// </summary>
    public const float ListEntryHeight = 30f;

    /// <summary>
    /// The standard margin used in the UI.
    /// </summary>
    public const float Margin = 6f;

    /// <summary>
    /// The height of section headers in the UI.
    /// </summary>
    public const float SectionHeaderHeight = 25f;

    /// <summary>
    /// The height of sliders in the UI.
    /// </summary>
    public const float SliderHeight = 20f;

    /// <summary>
    /// The size of small icons in the UI.
    /// </summary>
    public const float SmallIconSize = 16f;

    /// <summary>
    /// The standard button size in the UI.
    /// </summary>
    public static readonly Vector2 ButtonSize = new(200f, 40f);

    /// <summary>
    /// The default maximum upper threshold for triggers.
    /// </summary>
    public const int DefaultMaxUpperThreshold = 3000;

    /// <summary>
    /// The maximum size for stackalloc operations.
    /// </summary>
    public const int MaxStackallocSize = 256;

    /// <summary>
    /// (Obsolete) The number of coroutine operations before a break. No longer used.
    /// </summary>
    [Obsolete(
        "This constant is no longer used and will be removed in a future version. "
            + "Third party jobs currently using this should switch to using the performance attributes "
            + "CoroutineSettingsType and CoroutineSettingsMethod, and use the `ColonyManagerReduxMod."
            + "Settings.GetTicksBetweenOperationsForCoroutine` method for getting the value instead.",
        true
    )]
    public const int CoroutineBreakAfter = 10;

    /// <summary>
    /// The mod ID for Survivalist's Additions.
    /// </summary>
    public const string SurvivalistsAdditionsModId = "mlie.survivalistsadditions";
}
