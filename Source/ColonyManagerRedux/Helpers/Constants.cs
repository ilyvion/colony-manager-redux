// Constants.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public static class Constants
{
    public const float LargeIconSize = 32f;
    public const float LargeListEntryHeight = 50f;
    public const float ListEntryHeight = 30f;
    public const float Margin = 6f;
    public const float SectionHeaderHeight = 25f;
    public const float SliderHeight = 20f;
    public const float SmallIconSize = 16f;
    public static readonly Vector2 ButtonSize = new(200f, 40f);
    public const int DefaultMaxUpperThreshold = 3000;
    public const int MaxStackallocSize = 256;

    [Obsolete("This constant is no longer used and will be removed in a future version. "
        + "Third party jobs currently using this should switch to using the performance attributes "
        + "CoroutineSettingsType and CoroutineSettingsMethod, and use the `ColonyManagerReduxMod." +
        "Settings.GetTicksBetweenOperationsForCoroutine` method for getting the value instead.", true)]
    public const int CoroutineBreakAfter = 10;

    public const string SurvivalistsAdditionsModId = "mlie.survivalistsadditions";
}
