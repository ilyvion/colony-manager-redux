// HistoryLabel.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Base class for labels used in history graphs and chapters.
/// </summary>
public abstract class HistoryLabel : IExposable
{
    /// <summary>
    /// Gets the label string for this history label.
    /// </summary>
    public abstract string Label { get; }

    /// <summary>
    /// Exposes data for saving and loading the label.
    /// </summary>
    public abstract void ExposeData();

    /// <summary>
    /// Returns the label string.
    /// </summary>
    public override string ToString() => Label;
}

/// <summary>
/// A history label that uses a direct string value.
/// </summary>
public class DirectHistoryLabel : HistoryLabel
{
    private string direct;

    /// <summary>
    /// Gets the direct label string.
    /// </summary>
    public override string Label => direct;

    /// <summary>
    /// Creates a new direct history label with the specified string.
    /// </summary>
    /// <param name="direct">The label string.</param>
    public DirectHistoryLabel(string direct)
    {
        this.direct = direct;
    }

#pragma warning disable CS8618 // Used by scribe
    /// <summary>
    /// Initializes a new instance of the <see cref="DirectHistoryLabel"/> class for scribing.
    /// </summary>
    public DirectHistoryLabel()
#pragma warning restore CS8618
    { }

    /// <summary>
    /// Exposes data for saving and loading the direct label.
    /// </summary>
    public override void ExposeData() => Scribe_Values.Look(ref direct, "direct", string.Empty);

    /// <summary>
    /// Implicitly converts a string to a DirectHistoryLabel.
    /// </summary>
    /// <param name="direct">The label string.</param>
    public static implicit operator DirectHistoryLabel(string direct)
    {
        return new(direct);
    }

    /// <summary>
    /// Creates a DirectHistoryLabel from a string.
    /// </summary>
    /// <param name="direct">The label string.</param>
    /// <returns>A DirectHistoryLabel instance.</returns>
    public static DirectHistoryLabel FromString(string direct) => direct;
}

/// <summary>
/// A history label that uses a Def instance for its label.
/// </summary>
/// <typeparam name="T">The type of Def.</typeparam>
public class DefHistoryLabel<T> : HistoryLabel
    where T : Def, new()
{
    private T def;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefHistoryLabel{T}"/> class with the specified Def.
    /// </summary>
    /// <param name="def">The Def instance.</param>
    public DefHistoryLabel(T def)
    {
        this.def = def;
    }

#pragma warning disable CS8618 // Used by scribe
    /// <summary>
    /// Initializes a new instance of the <see cref="DefHistoryLabel{T}"/> class for scribing.
    /// </summary>
    public DefHistoryLabel()
#pragma warning restore CS8618
    { }

    /// <summary>
    /// Gets the label string from the Def instance.
    /// </summary>
    public override string Label =>
        ((string?)def?.LabelCap) ?? def?.defName.CapitalizeFirst() ?? "<null>";

    /// <summary>
    /// Exposes data for saving and loading the Def label.
    /// </summary>
    public override void ExposeData() => Scribe_Defs.Look(ref def, "def");

#pragma warning disable CA2225 // Operator overloads have named alternates
    /// <summary>
    /// Implicitly converts a Def to a DefHistoryLabel.
    /// </summary>
    /// <param name="def">The Def instance.</param>
    public static implicit operator DefHistoryLabel<T>(T def)
#pragma warning restore CA2225 // Operator overloads have named alternates
    {
        return new(def);
    }
}

/// <summary>
/// A history label that uses a ManagerJobHistoryChapterDef for its label.
/// </summary>
public class ManagerJobHistoryChapterDefLabel : HistoryLabel
{
    private ManagerJobHistoryChapterDef historyChapterDef;

    /// <summary>
    /// Initializes a new instance of the <see cref="ManagerJobHistoryChapterDefLabel"/> class with the specified chapter def.
    /// </summary>
    /// <param name="historyChapterDef">The manager job history chapter definition.</param>
    public ManagerJobHistoryChapterDefLabel(ManagerJobHistoryChapterDef historyChapterDef)
    {
        this.historyChapterDef = historyChapterDef;
    }

#pragma warning disable CS8618 // Used by scribe
    /// <summary>
    /// Initializes a new instance of the <see cref="ManagerJobHistoryChapterDefLabel"/> class for scribing.
    /// </summary>
    public ManagerJobHistoryChapterDefLabel()
#pragma warning restore CS8618
    { }

    /// <summary>
    /// Gets the label string from the ManagerJobHistoryChapterDef.
    /// </summary>
    public override string Label => historyChapterDef.historyLabel.Label;

    /// <summary>
    /// Exposes data for saving and loading the ManagerJobHistoryChapterDef label.
    /// </summary>
    public override void ExposeData() =>
        Scribe_Defs.Look(ref historyChapterDef, "historyChapterDef");
}

/// <summary>
/// (Obsolete) A history label that uses a translation key for its label.
/// </summary>
[Obsolete(
    "Use ManagerJobHistoryChapterDefs instead of this directly; this method will be removed in a future version"
)]
public class TranslationHistoryLabel : HistoryLabel
{
    private string translationKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="TranslationHistoryLabel"/> class with the specified translation key.
    /// </summary>
    /// <param name="translationKey">The translation key.</param>
    public TranslationHistoryLabel(string translationKey)
    {
        this.translationKey = translationKey;
    }

#pragma warning disable CS8618 // Used by scribe and defs
    /// <summary>
    /// Initializes a new instance of the <see cref="TranslationHistoryLabel"/> class for scribing.
    /// </summary>
    public TranslationHistoryLabel()
#pragma warning restore CS8618
    { }

    /// <summary>
    /// Gets the label string by translating the translation key.
    /// </summary>
    public override string Label => translationKey.Translate();

    /// <summary>
    /// Exposes data for saving and loading the translation key label.
    /// </summary>
    public override void ExposeData() =>
        Scribe_Values.Look(ref translationKey, "translationKey", string.Empty);

    /// <summary>
    /// Implicitly converts a string to a TranslationHistoryLabel.
    /// </summary>
    /// <param name="translationKey">The translation key.</param>
    public static implicit operator TranslationHistoryLabel(string translationKey)
    {
        return new(translationKey);
    }

    /// <summary>
    /// Creates a DirectHistoryLabel from a translation key string.
    /// </summary>
    /// <param name="translationKey">The translation key.</param>
    /// <returns>A DirectHistoryLabel instance.</returns>
    public static DirectHistoryLabel FromString(string translationKey) => translationKey;
}
