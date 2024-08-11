// DetailedLegendRenderer.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[HotSwappable]
public class DetailedLegendRenderer : IExposable
{
    // Settings for detailed legend
    private bool _drawCounts = true;
    public bool DrawCounts { get => _drawCounts; set => _drawCounts = value; }

    private bool _drawIcons = true;
    public bool DrawIcons { get => _drawIcons; set => _drawIcons = value; }

    private bool _drawInfoInBar;
    public bool DrawInfoInBar { get => _drawInfoInBar; set => _drawInfoInBar = value; }

    private bool _drawMaxMarkers;
    public bool DrawMaxMarkers { get => _drawMaxMarkers; set => _drawMaxMarkers = value; }

    private bool _maxPerChapter;
    public bool MaxPerChapter { get => _maxPerChapter; set => _maxPerChapter = value; }

    public void DrawDetailedLegend(History history, Rect canvas, ref Vector2 scrollPos, int? max, bool positiveOnly = false,
        bool negativeOnly = false)
    {
        if (history == null)
        {
            throw new ArgumentNullException(nameof(history));
        }

        // set sign
        var sign = negativeOnly ? -1 : 1;

        var chaptersOrdered = history._chapters
            .Where(chapter => !positiveOnly || chapter.counts[(int)history.PeriodShown].Any(i => i > 0))
            .Where(chapter => !negativeOnly || chapter.counts[(int)history.PeriodShown].Any(i => i < 0))
            .OrderByDescending(chapter => chapter.Last(history.PeriodShown).count * sign).ToList();

        if (IlyvionDebugViewSettings.DrawUIHelpers)
        {
            Widgets.DrawRectFast(canvas, ColorLibrary.NeonGreen.ToTransparent(.5f));
        }

        // get out early if no chapters.
        if (chaptersOrdered.Count == 0)
        {
            GUI.DrawTexture(canvas.ContractedBy(Margin), Resources.SlightlyDarkBackground);
            Widgets_Labels.Label(canvas, "ColonyManagerRedux.History.NoChapters".Translate(), TextAnchor.MiddleCenter,
                color: Color.grey);
            return;
        }

        // max
        float _max = max
            ?? (DrawMaxMarkers
                ? chaptersOrdered.Max(chapter => chapter.TrueMax)
                : chaptersOrdered.FirstOrDefault()?.Last(history.PeriodShown).count * sign)
            ?? 0;

        // cell height
        var height = 30f;
        var barHeight = 18f;

        // n rows
        var n = chaptersOrdered.Count;

        // scrolling region
        var viewRect = canvas;
        viewRect.height = n * height;
        if (viewRect.height > canvas.height)
        {
            viewRect.width -= 16f + Margin;
            canvas.width -= Margin;
            canvas.height -= 1f;
        }

        Widgets.BeginScrollView(canvas, ref scrollPos, viewRect);
        for (var i = 0; i < n; i++)
        {
            History.Chapter chapter = chaptersOrdered[i];

            // set up rects
            var row = new Rect(0f, height * i, viewRect.width, height);
            var icon = new Rect(Margin, height * i, height, height).ContractedBy(Margin / 2f);
            // icon is square, size defined by height.
            var bar = new Rect(Margin + height, height * i, viewRect.width - height - Margin, height);

            if (IlyvionDebugViewSettings.DrawUIHelpers)
            {
                Widgets.DrawRectFast(row, ColorLibrary.Red.ToTransparent(.5f));
                Widgets.DrawRectFast(icon, ColorLibrary.Teal.ToTransparent(.5f));
                Widgets.DrawRectFast(bar, ColorLibrary.Yellow.ToTransparent(.5f));
            }

            // if icons should not be drawn make the bar full size.
            if (!DrawIcons)
            {
                bar.xMin -= height + Margin;
            }

            // bar details.
            var barBox = bar.ContractedBy((height - barHeight) / 2f);
            var barFill = barBox.ContractedBy(2f);
            var maxWidth = barFill.width;
            if (MaxPerChapter)
            {
                barFill.width *= chapter.Last(history.PeriodShown).count * sign / (float)chapter.TrueMax;
            }
            else
            {
                barFill.width *= chapter.Last(history.PeriodShown).count * sign / _max;
            }

            GUI.BeginGroup(viewRect);

            // if DrawIcons and a thing is set, draw the icon.
            var thing = chapter.ThingDefCount.thingDef;
            if (DrawIcons && thing != null)
            {
                // draw the icon in correct proportions
                var proportion = GenUI.IconDrawScale(thing);
                Widgets.DrawTextureFitted(icon, thing.uiIcon, proportion);

                // draw counts in upper left corner
                if (DrawCounts)
                {
                    Utilities.LabelOutline(icon, chapter.ThingDefCount.count.ToString(), null,
                        TextAnchor.UpperLeft, 0f, GameFont.Tiny, Color.white, Color.black);
                }
            }

            // if desired, draw ghost bar
            if (DrawMaxMarkers)
            {
                var ghostBarFill = barFill;
                ghostBarFill.width = MaxPerChapter ? maxWidth : maxWidth * (chapter.TrueMax / _max);
                GUI.color = new Color(1f, 1f, 1f, .2f);
                GUI.DrawTexture(ghostBarFill, chapter.Texture); // coloured texture
                GUI.color = Color.white;
            }

            // draw the main bar.
            GUI.DrawTexture(barBox, Resources.SlightlyDarkBackground);
            GUI.DrawTexture(barFill, chapter.Texture); // coloured texture
            GUI.DrawTexture(barFill, Resources.BarShader);        // slightly fancy overlay (emboss).

            // draw on bar info
            if (DrawInfoInBar)
            {
                var info = chapter.label + ": " +
                    Utils.FormatCount(chapter.Last(history.PeriodShown).count * sign, chapter.ChapterSuffix ?? history.YAxisSuffix);

                if (DrawMaxMarkers)
                {
                    info += " / " + Utils.FormatCount(chapter.TrueMax, chapter.ChapterSuffix ?? history.YAxisSuffix);
                }

                // offset label a bit downwards and to the right
                var rowInfoRect = row;
                rowInfoRect.y += 1f;
                rowInfoRect.x += Margin * 2;

                // x offset
                var xOffset = DrawIcons && thing != null ? height + Margin * 2 : Margin * 2;

                Utilities.LabelOutline(rowInfoRect, info, null, TextAnchor.MiddleLeft, xOffset, GameFont.Tiny,
                    Color.white, Color.black);
            }

            // are we currently showing this line?
            var shown = history._chaptersShown.Contains(chapter);

            // tooltip on entire row
            var tooltip = $"{chapter.label}: " +
                Utils.FormatCount(
                    Mathf.Abs(chapter.Last(history.PeriodShown).count),
                    chapter.ChapterSuffix ?? history.YAxisSuffix) + "\n\n" +
                "ColonyManagerRedux.History.ClickToEnable"
                    .Translate(shown
                        ? "ColonyManagerRedux.History.Hide".Translate()
                        : "ColonyManagerRedux.History.Show".Translate(),
                        chapter.label.Label.UncapitalizeFirst());
            TooltipHandler.TipRegion(row, tooltip);

            // handle input
            if (Widgets.ButtonInvisible(row))
            {
                if (Event.current.button == 0)
                {
                    if (shown)
                    {
                        history._chaptersShown.Remove(chapter);
                    }
                    else
                    {
                        history._chaptersShown.Add(chapter);
                    }
                }
                else if (Event.current.button == 1)
                {
                    history._chaptersShown.Clear();
                    history._chaptersShown.Add(chapter);
                }
            }

            // UI feedback for disabled row
            if (!shown)
            {
                GUI.DrawTexture(row.ContractedBy(1f), Resources.SlightlyDarkBackground);
            }

            GUI.EndGroup();
        }

        Widgets.EndScrollView();
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref _drawIcons, "drawIcons", true);
        Scribe_Values.Look(ref _drawCounts, "drawCounts", true);
        Scribe_Values.Look(ref _drawInfoInBar, "drawInfoInBar");
        Scribe_Values.Look(ref _drawMaxMarkers, "drawMaxMarkers", true);
        Scribe_Values.Look(ref _maxPerChapter, "maxPerChapter");
    }
}
