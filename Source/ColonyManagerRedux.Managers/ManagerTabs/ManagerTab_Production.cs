// ManagerTab_Production.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerTab_Production(Manager manager)
    : ManagerTab<ManagerJob_Production, ManagerSettings_Production>(manager)
{
    private const string ProductionOptions = "Production.Options";

    protected override void DoMainContent(Rect rect)
    {
        Widgets.DrawMenuSection(rect);

        Widgets_Section.BeginSectionColumn(
            rect,
            ProductionOptions,
            out var position,
            out var width
        );
        DrawSection(
            ProductionOptions,
            "NotYetImplemented",
            ref position,
            width,
            DrawNotYetImplemented
        );
        Widgets_Section.EndSectionColumn(ProductionOptions, position);
    }

    private static float DrawNotYetImplemented(ManagerJob_Production job, Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        Widgets.Label(rowRect, "ColonyManagerRedux.Production.NotYetImplemented".Translate());
        return ListEntryHeight;
    }
}
