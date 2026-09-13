// ManagerSettings_Production.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ilyvion.Laboratory.UI;
using Verse.Sound;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class ManagerSettings_Production : ManagerSettings
{
    /// <summary>
    /// How much of each <see cref="ThingDef"/> production bills should never consume, regardless
    /// of which job's bills would otherwise draw it down — the mod-wide default consulted by
    /// every <see cref="ManagerJob_Production"/> unless overridden per-job (see
    /// <see cref="ManagerJob_Production.ReservedStockOverrides"/>).
    /// </summary>
    public Dictionary<ThingDef, int> ReservedStock = [];

    // Transient per-row text buffers for the amount fields below, keyed by ThingDef so a
    // partially typed value survives redraws without needing a control-name lookup per row.
    private readonly Dictionary<ThingDef, string> _inputBuffers = [];

    public override void DoTabContents(Rect rect)
    {
        var panelRect = new Rect(rect.xMin, rect.yMin, rect.width, rect.height - Margin);

        Widgets_Section.BeginSectionColumn(
            panelRect,
            "Production.Settings",
            out var position,
            out var width
        );
        Widgets_Section.Section(
            ref position,
            width,
            DrawReservedStock,
            "ColonyManagerRedux.Production.ReservedStock".Translate()
        );
        Widgets_Section.EndSectionColumn("Production.Settings", position);
    }

    private float DrawReservedStock(Vector2 pos, float width)
    {
        var start = pos;

        var tipHeight = Text.CalcHeight(
            "ColonyManagerRedux.Production.ReservedStock.Tip".Translate(),
            width
        );
        Widgets.Label(
            new Rect(pos.x, pos.y, width, tipHeight),
            "ColonyManagerRedux.Production.ReservedStock.Tip".Translate()
        );
        pos.y += tipHeight + Margin;

        foreach (
            var thingDef in ReservedStock
                .Keys.OrderBy(td => td.LabelCap.ToString(), StringComparer.OrdinalIgnoreCase)
                .ToList()
        )
        {
            pos.y += DrawReservedStockRow(thingDef, pos, width);
        }

        var addRect = new Rect(pos.x, pos.y, width, ListEntryHeight);
        if (
            Widgets.ButtonText(
                addRect,
                "ColonyManagerRedux.Production.AddReservedStock".Translate()
            )
        )
        {
            Find.WindowStack.Add(new FloatMenu(BuildAddReservedStockOptions()));
        }
        pos.y += ListEntryHeight;

        return pos.y - start.y;
    }

    private float DrawReservedStockRow(ThingDef thingDef, Vector2 pos, float width)
    {
        var rowRect = new Rect(pos.x, pos.y, width, ListEntryHeight);

        var deleteRect = new Rect(
            rowRect.xMax - ListEntryHeight,
            rowRect.y,
            ListEntryHeight,
            ListEntryHeight
        );
        var fieldRect = new Rect(deleteRect.xMin - 60f - Margin, rowRect.y, 60f, ListEntryHeight);
        var iconRect = new Rect(rowRect.x, rowRect.y, ListEntryHeight, ListEntryHeight);
        var labelRect = new Rect(
            iconRect.xMax + Margin,
            rowRect.y,
            fieldRect.xMin - iconRect.xMax - (2 * Margin),
            ListEntryHeight
        );

        Widgets.DefIcon(iconRect, thingDef);
        IlyvionWidgets.Label(labelRect, thingDef.LabelCap, TextAnchor.MiddleLeft);

        if (!_inputBuffers.TryGetValue(thingDef, out var buffer))
        {
            buffer = ReservedStock[thingDef].ToString(CultureInfo.InvariantCulture);
        }
        buffer = Widgets.TextField(fieldRect, buffer);
        if (int.TryParse(buffer, out var parsed) && parsed >= 0)
        {
            ReservedStock[thingDef] = parsed;
        }
        _inputBuffers[thingDef] = buffer;

        if (
            Widgets.ButtonImage(
                deleteRect,
                TexButton.Delete,
                Color.white,
                GenUI.SubtleMouseoverColor
            )
        )
        {
            _ = ReservedStock.Remove(thingDef);
            _ = _inputBuffers.Remove(thingDef);
            SoundDefOf.Tick_Low.PlayOneShotOnCamera();
        }

        return ListEntryHeight;
    }

    private List<FloatMenuOption> BuildAddReservedStockOptions()
    {
        var opts = new List<FloatMenuOption>();
        var candidateFilter = ThingFilter.CreateOnlyEverStorableThingFilter();
        foreach (
            var thingDef in candidateFilter
                .AllowedThingDefs.Where(td => !ReservedStock.ContainsKey(td))
                .OrderBy(td => td.LabelCap.ToString(), StringComparer.OrdinalIgnoreCase)
        )
        {
            var thingDefLocal = thingDef;
            opts.Add(
                new FloatMenuOption(thingDefLocal.LabelCap, () => ReservedStock[thingDefLocal] = 0)
            );
        }
        return opts;
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Collections.Look(ref ReservedStock, "reservedStock", LookMode.Def, LookMode.Value);
        ReservedStock ??= [];
    }
}
