// Dialog_GravshipJobPicker.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

#if !v1_5
using Verse.Sound;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

/// <summary>
/// Lets the player choose, job by job, which of the map's existing manager jobs and which of a
/// landing gravship's manager jobs to keep, when both already have jobs configured. Reached from
/// <see cref="Dialog_GravshipJobConflict"/>'s "choose individually" option.
/// </summary>
[HotSwappable]
internal sealed class Dialog_GravshipJobPicker(
    List<ManagerJob> localJobs,
    List<ManagerJob> gravshipJobs,
    Action<List<ManagerJob>, List<ManagerJob>> onConfirm
) : Window
{
    private const float ButtonWidth = 200f;
    private const float ButtonHeight = 40f;
    private const float HeaderRowHeight = 32f;
    private const float RowHeight = LargeListEntryHeight;
    private const float LastUpdateWidth = ManagerTab.LastUpdateRectWidth;

    private readonly HashSet<ManagerJob> _keptLocalJobs = [.. localJobs];
    private readonly HashSet<ManagerJob> _keptGravshipJobs = [.. gravshipJobs];

    private Vector2 _localScrollPosition = Vector2.zero;
    private Vector2 _gravshipScrollPosition = Vector2.zero;

    public override Vector2 InitialSize => new(760f, 560f);

    public override void PreOpen()
    {
        base.PreOpen();

        // The player must explicitly confirm their selection; there's no sensible default to
        // fall back to if they dismiss the dialog without doing so.
        closeOnCancel = false;
        closeOnClickedOutside = false;
        forcePause = true;
        absorbInputAroundWindow = true;
    }

    public override void DoWindowContents(Rect inRect)
    {
        var columnWidth = (inRect.width - 20f) / 2f;
        var y = 0f;

        Text.Font = GameFont.Medium;
        Widgets.Label(
            new Rect(0f, y, inRect.width, Text.LineHeight),
            "ColonyManagerRedux.Gravship.JobPickerTitle".Translate()
        );
        y += Text.LineHeight + 10f;
        Text.Font = GameFont.Small;

        var text = "ColonyManagerRedux.Gravship.JobPickerText".Translate();
        var textHeight = Text.CalcHeight(text, inRect.width);
        Widgets.Label(new Rect(0f, y, inRect.width, textHeight), text);
        y += textHeight + 10f;

        var listHeight = inRect.height - y - ButtonHeight - 10f - HeaderRowHeight - 5f;

        var localHeaderRect = new Rect(0f, y, columnWidth, HeaderRowHeight);
        var gravshipHeaderRect = new Rect(columnWidth + 20f, y, columnWidth, HeaderRowHeight);

        Utilities.DrawToggle(
            localHeaderRect,
            "ColonyManagerRedux.Gravship.LocalJobs".Translate(),
            null,
            localJobs.Count > 0 && localJobs.All(_keptLocalJobs.Contains),
            localJobs.Count == 0 || localJobs.All(job => !_keptLocalJobs.Contains(job)),
            () => _keptLocalJobs.UnionWith(localJobs),
            () => _keptLocalJobs.ExceptWith(localJobs)
        );
        Utilities.DrawToggle(
            gravshipHeaderRect,
            "ColonyManagerRedux.Gravship.GravshipJobs".Translate(),
            null,
            gravshipJobs.Count > 0 && gravshipJobs.All(_keptGravshipJobs.Contains),
            gravshipJobs.Count == 0 || gravshipJobs.All(job => !_keptGravshipJobs.Contains(job)),
            () => _keptGravshipJobs.UnionWith(gravshipJobs),
            () => _keptGravshipJobs.ExceptWith(gravshipJobs)
        );
        y += HeaderRowHeight + 5f;

        DoJobList(
            new Rect(0f, y, columnWidth, listHeight),
            localJobs,
            _keptLocalJobs,
            ref _localScrollPosition
        );
        DoJobList(
            new Rect(columnWidth + 20f, y, columnWidth, listHeight),
            gravshipJobs,
            _keptGravshipJobs,
            ref _gravshipScrollPosition
        );

        var confirmRect = new Rect(
            (inRect.width - ButtonWidth) / 2f,
            inRect.height - ButtonHeight,
            ButtonWidth,
            ButtonHeight
        );
        if (Widgets.ButtonText(confirmRect, "Confirm".Translate()))
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
            onConfirm(
                [.. localJobs.Where(_keptLocalJobs.Contains)],
                [.. gravshipJobs.Where(_keptGravshipJobs.Contains)]
            );
            Close();
        }
    }

    private static void DoJobList(
        Rect rect,
        List<ManagerJob> jobs,
        HashSet<ManagerJob> kept,
        ref Vector2 scrollPosition
    )
    {
        Widgets.DrawMenuSection(rect);

        var viewRect = new Rect(0f, 0f, rect.width - 16f, jobs.Count * RowHeight);
        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
        for (var i = 0; i < jobs.Count; i++)
        {
            var job = jobs[i];
            var rowRect = new Rect(0f, i * RowHeight, viewRect.width, RowHeight);
            if (i % 2 == 1)
            {
                Widgets.DrawAltRect(rowRect);
            }

            var contentRect = rowRect.ContractedBy(2f);
            var lastUpdateRect = contentRect.RightPartPixels(LastUpdateWidth);
            var checkboxRect = new Rect(
                contentRect.x,
                contentRect.y,
                contentRect.width - LastUpdateWidth - Constants.Margin,
                contentRect.height
            );

            var (label, _) = job.Tab.GetFullLabel(
                job,
                checkboxRect.width - SmallIconSize - (3 * Constants.Margin)
            );

            var isKept = kept.Contains(job);
            var wasKept = isKept;
            Utilities.DrawToggle(checkboxRect, label, null, ref isKept);
            if (isKept != wasKept)
            {
                _ = isKept ? kept.Add(job) : kept.Remove(job);
            }

            UpdateInterval.Draw(lastUpdateRect, job, exporting: true, suspended: job.IsSuspended);
        }
        Widgets.EndScrollView();
    }
}
#endif // !v1_5
