// ManagerJobHistoryChapterDef.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public class ManagerJobHistoryChapterDef : Def
{
#pragma warning disable CS8618 // Ensured by ConfigErrors
    public HistoryLabel historyLabel;
#pragma warning restore CS8618
    public Color color;
    public string? suffix;

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string item in base.ConfigErrors())
        {
            yield return item;
        }

        if (historyLabel == null)
        {
            yield return "historyLabel is null";
        }
    }
}
