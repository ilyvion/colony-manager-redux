// HistoryLabel.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public abstract class HistoryLabel : IExposable
{
    public abstract string Label { get; }

    public abstract void ExposeData();

    public override string ToString()
    {
        return Label;
    }
}

public class DirectHistoryLabel : HistoryLabel
{
    private string direct;

    public override string Label => direct;

    public DirectHistoryLabel(string direct)
    {
        this.direct = direct;
    }

#pragma warning disable CS8618 // Used by scribe
    public DirectHistoryLabel()
#pragma warning restore CS8618
    {
    }

    public override void ExposeData()
    {
        Scribe_Values.Look(ref direct, "direct", string.Empty);
    }

    public static implicit operator DirectHistoryLabel(string direct)
    {
        return new(direct);
    }

    public static DirectHistoryLabel FromString(string direct)
    {
        return direct;
    }
}

public class DefHistoryLabel<T> : HistoryLabel where T : Def, new()
{
    private T def;

    public DefHistoryLabel(T def)
    {
        this.def = def;
    }

#pragma warning disable CS8618 // Used by scribe
    public DefHistoryLabel()
#pragma warning restore CS8618
    {
    }

    public override string Label => ((string?)def?.LabelCap)
        ?? def?.defName.CapitalizeFirst()
        ?? "<null>";

    public override void ExposeData()
    {
        Scribe_Defs.Look(ref def, "def");
    }

#pragma warning disable CA2225 // Operator overloads have named alternates
    public static implicit operator DefHistoryLabel<T>(T def)
#pragma warning restore CA2225 // Operator overloads have named alternates
    {
        return new(def);
    }
}

public class ManagerJobHistoryChapterDefLabel : HistoryLabel
{
    private ManagerJobHistoryChapterDef historyChapterDef;

    public ManagerJobHistoryChapterDefLabel(ManagerJobHistoryChapterDef historyChapterDef)
    {
        this.historyChapterDef = historyChapterDef;
    }

#pragma warning disable CS8618 // Used by scribe
    public ManagerJobHistoryChapterDefLabel()
#pragma warning restore CS8618
    {
    }

    public override string Label => historyChapterDef.historyLabel.Label;

    public override void ExposeData()
    {
        Scribe_Defs.Look(ref historyChapterDef, "historyChapterDef");
    }
}

[Obsolete("Use ManagerJobHistoryChapterDefs instead of this directly; " +
        "this method will be removed in a future version")]
public class TranslationHistoryLabel : HistoryLabel
{
    private string translationKey;

    public TranslationHistoryLabel(string translationKey)
    {
        this.translationKey = translationKey;
    }

#pragma warning disable CS8618 // Used by scribe and defs
    public TranslationHistoryLabel()
#pragma warning restore CS8618
    {
    }

    public override string Label => translationKey.Translate();

    public override void ExposeData()
    {
        Scribe_Values.Look(ref translationKey, "translationKey", string.Empty);
    }

    public static implicit operator TranslationHistoryLabel(string translationKey)
    {
        return new(translationKey);
    }

    public static DirectHistoryLabel FromString(string translationKey)
    {
        return translationKey;
    }
}
