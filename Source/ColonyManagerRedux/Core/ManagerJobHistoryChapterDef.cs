// ManagerJobHistoryChapterDef.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Definition for a manager job history chapter, including label, color, and suffix.
/// </summary>
public class ManagerJobHistoryChapterDef : Def
{
#pragma warning disable CS8618 // Ensured by ConfigErrors
    /// <summary>
    /// The label used for this history chapter.
    /// </summary>
    public HistoryLabel historyLabel;
#pragma warning restore CS8618
    /// <summary>
    /// The color used for this history chapter.
    /// </summary>
    public Color color;

    /// <summary>
    /// The optional suffix for this history chapter.
    /// </summary>
    public string? suffix;

    /// <inheritdoc/>
    public override IEnumerable<string> ConfigErrors()
    {
        foreach (var item in base.ConfigErrors())
        {
            yield return item;
        }

        if (historyLabel == null)
        {
            yield return "historyLabel is null";
        }
    }
}
