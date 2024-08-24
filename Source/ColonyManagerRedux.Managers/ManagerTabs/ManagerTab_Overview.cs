// ManagerTab_Overview.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.Extensions;
using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed partial class ManagerTab_Overview(Manager manager) : ManagerTab(manager)
{
    public const float OverviewWidthRatio = .6f;

    private float _overviewHeight = 9999f;
    private Vector2 _overviewScrollPosition = Vector2.zero;
    private readonly List<Pawn> _workers = [];

    private SkillDef? SkillDef { get; set; }

    private WorkTypeDef? _workType;
    private WorkTypeDef WorkTypeDef
    {
        get
        {
            _workType ??= ManagerWorkTypeDefOf.Managing;

            return _workType;
        }
        set
        {
            _workType = value;
            RefreshWorkers();
        }
    }

    public override void PreOpen()
    {
        RefreshWorkers();
    }

    public override void PostOpen()
    {
        pawnOverviewTable?.SetDirty();
    }

    protected override void PostSelect()
    {
        WorkTypeDef = Selected?.WorkTypeDef ?? ManagerWorkTypeDefOf.Managing;
        pawnOverviewTable?.SetDirty();
    }

    protected override void Notify_PawnsChanged()
    {
        RefreshWorkers();
        pawnOverviewTable?.SetDirty();
    }

    protected override void DoTabContents(Rect canvas)
    {
        var overviewRect = new Rect(0f, 0f, OverviewWidthRatio * canvas.width, canvas.height).RoundToInt();
        var sideRectUpper = new Rect(overviewRect.xMax + Margin, 0f,
            (1 - OverviewWidthRatio) * canvas.width - Margin,
            (canvas.height - Margin) / 2).RoundToInt();
        var sideRectLower = new Rect(overviewRect.xMax + Margin, sideRectUpper.yMax + Margin,
            sideRectUpper.width,
            canvas.height - sideRectUpper.height - Margin).RoundToInt();

        // draw the listing of current jobs.
        Widgets.DrawMenuSection(overviewRect);
        DrawOverview(overviewRect);

        // draw the selected job's details
        Widgets.DrawMenuSection(sideRectUpper);
        if (Selected?.Tab is ManagerTab managerTab)
        {
            if (!managerTab.DrawOverviewDetails(Selected, sideRectUpper))
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(sideRectUpper, "ColonyManagerRedux.Overview.NoJobDetails".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.LowerLeft;
            }
        }
        else
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.gray;
            Widgets.Label(sideRectUpper, "ColonyManagerRedux.Overview.NoJobSelected".Translate());
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.LowerLeft;
        }

        // overview of managers & pawns (capable of) doing this job.
        Widgets.DrawMenuSection(sideRectLower);
        GUI.BeginGroup(sideRectLower);
        DrawPawnOverview(sideRectLower.AtZero());
        GUI.EndGroup();
    }

    public void DrawOverview(Rect rect)
    {
        if (Manager.JobTracker.HasNoJobs)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.grey;
            Widgets.Label(rect, "ColonyManagerRedux.Overview.NoJobs".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }
        else
        {
            var viewRect = rect;
            var contentRect = viewRect.AtZero();
            contentRect.height = _overviewHeight;
            if (_overviewHeight > viewRect.height)
            {
                contentRect.width -= GenUI.ScrollBarWidth;
            }

            GUI.BeginGroup(viewRect);
            Widgets.BeginScrollView(viewRect, ref _overviewScrollPosition, contentRect);

            var cur = Vector2.zero;

            var alternate = false;
            foreach (ManagerJob job in Manager.JobTracker.JobsOfType<ManagerJob>())
            {
                var row = new Rect(cur.x, cur.y, contentRect.width, 0f);
                DrawOverviewListEntry(job, ref cur, contentRect.width);
                row.height = cur.y - row.y;

                // highlights
                if (alternate)
                {
                    Widgets.DrawAltRect(row);
                }
                alternate = !alternate;

                if (job == Selected)
                {
                    Widgets.DrawHighlightSelected(row);
                }

                Widgets.DrawHighlightIfMouseover(row);

                if (job.CausedException is Exception ex)
                {
                    Widgets.DrawBox(row, 2, Resources.Error);

                    TooltipHandler.TipRegion(row, new TipSignal(
                        "ColonyManagerRedux.Job.CausedException".Translate(job.CausedExceptionText)));
                }

                if (Widgets.ButtonInvisible(row))
                {
                    if (Selected != job)
                    {
                        Selected = job;
                    }
                    else
                    {
                        Selected = null;
                    }
                }
            }

            Widgets.EndScrollView();
            GUI.EndGroup();

            _overviewHeight = cur.y;
        }
    }

    private void DrawOverviewListEntry(
        ManagerJob job,
        ref Vector2 position,
        float width)
    {
        DrawOverviewListEntryParameters? parameters = null;
        if (job.CompOfType<CompDrawOverviewListEntry>() is
            CompDrawOverviewListEntry drawExportListEntry)
        {
            var props = drawExportListEntry.Props;
            var worker = props.Worker;
            if (props.takeOverRendering)
            {
                worker.DrawOverviewListEntry(job, ref position, width);
                return;
            }
            else
            {
                parameters = props.drawListEntryParameters;
                worker.ChangeDrawListEntryParameters(job, ref parameters);
            }
        }
        parameters ??= new();

        var tab = job.Tab;

        float labelWidth = width
            - (StatusRectWidth + 4 * Margin)
            - 2 * Margin - LargeIconSize - LargeListEntryHeight;

        // create label string
        var subLabel = tab.GetSubLabel(job);
        var (label, labelSize) = tab.GetFullLabel(job, labelWidth, subLabel);

        // set up rects
        Rect iconRect = new(Margin, Margin,
            LargeIconSize, LargeIconSize);

        Rect labelRect = new(
            iconRect.xMax + Margin,
            iconRect.y,
            labelWidth,
            labelSize.y);
        Rect statusRect = new(
            labelRect.xMax + Margin,
            Margin,
            StatusRectWidth + Margin,
            LargeListEntryHeight);

        Rect stampRegionRect = new(
            statusRect.xMax - StampSize,
            statusRect.y,
            StampSize,
            statusRect.height);

        Rect lastUpdateRect = new(
            stampRegionRect.xMin - Margin - LastUpdateRectWidth,
            statusRect.y,
            LastUpdateRectWidth,
            statusRect.height);

        Rect progressRect = new(
            lastUpdateRect.xMin - Margin - ProgressRectWidth,
            statusRect.yMin,
            ProgressRectWidth,
            statusRect.height);

        Rect orderRect = new(
            statusRect.xMax + Margin,
            statusRect.y,
            LargeListEntryHeight,
            LargeListEntryHeight);

        // do the drawing
        float rowHeight = Mathf.Max(labelRect.yMax, statusRect.yMax) + Margin;
        Rect rowRect = new(
            position.x,
            position.y,
            width,
            rowHeight);
        GUI.BeginGroup(rowRect);
        rowRect = rowRect.AtZero();

        labelRect = labelRect.CenteredOnYIn(rowRect);
        iconRect = iconRect.CenteredOnYIn(rowRect);

        IlyvionDebugViewSettings.DrawIfUIHelpers(() =>
        {
            Widgets.DrawRectFast(iconRect, ColorLibrary.HotPink.ToTransparent(.5f));
            Widgets.DrawRectFast(labelRect, Color.blue.ToTransparent(.2f));
            Widgets.DrawRectFast(statusRect, Color.yellow.ToTransparent(.2f));
            Widgets.DrawRectFast(lastUpdateRect, ColorLibrary.Orange.ToTransparent(.2f));
            Widgets.DrawRectFast(stampRegionRect, Color.red.ToTransparent(.2f));
            Widgets.DrawRectFast(progressRect, Color.green.ToTransparent(.2f));
            Widgets.DrawRectFast(orderRect, ColorLibrary.Aqua.ToTransparent(.5f));
        });

        // draw label
        IlyvionWidgets.Label(
            labelRect,
            label,
            subLabel,
            TextAnchor.MiddleLeft);

        // if the bill has a manager job, give some more info.
        if (tab.Enabled)
        {
            if (Widgets.ButtonImage(iconRect, tab.Def.icon))
            {
                MainTabWindow_Manager.GoTo(tab, job);
            }
            TooltipHandler.TipRegion(iconRect,
                "ColonyManagerRedux.Common.GoToJob".Translate(job.Label.UncapitalizeFirst()));
        }
        else
        {
            using var color = GUIScope.Color(Color.gray);
            GUI.DrawTexture(iconRect, tab.Def.icon);
            TooltipHandler.TipRegion(iconRect, tab.Label +
                "ColonyManagerRedux.Common.TabDisabledBecause".Translate(tab.DisabledReason));
        }

        var stampRect = new Rect(0, 0, StampSize, StampSize)
            .CenteredIn(stampRegionRect);
        if (Utilities.DrawStampButton(stampRect, job))
        {
            job.IsSuspended = !job.IsSuspended;
        }

        if (job.IsSuspended)
        {
            if (job.CausedException != null)
            {
                TooltipHandler.TipRegion(stampRect, new TipSignal(
                    job.IsSuspendedDueToExceptionTooltip + "\n\n" +
                    "ColonyManagerRedux.Job.ClickToChangeJob".Translate(
                        "ColonyManagerRedux.Job.Unsuspend".Translate()))
                {
                    // We do this so the exception is shown after
                    priority = TooltipPriority.Pawn
                });
            }
            else
            {
                TooltipHandler.TipRegion(stampRect,
                    job.IsSuspendedTooltip + "\n\n" +
                    "ColonyManagerRedux.Job.ClickToChangeJob".Translate(
                        "ColonyManagerRedux.Job.Unsuspend".Translate()));
            }
        }
        else if (job.IsCompleted)
        {
            TooltipHandler.TipRegion(stampRect,
                job.IsCompletedTooltip + "\n\n" +
                "ColonyManagerRedux.Job.ClickToChangeJob".Translate(
                    "ColonyManagerRedux.Job.Suspend".Translate()));
        }
        else
        {
            TooltipHandler.TipRegion(stampRect,
                "ColonyManagerRedux.Job.ClickToChangeJob".Translate(
                    "ColonyManagerRedux.Job.Suspend".Translate()));
        }

        if (parameters.ShowProgressbar && job.Trigger != null)
        {
            // draw progress bar
            job.Trigger.DrawVerticalProgressBars(
                progressRect,
                !job.IsSuspended && !job.IsCompleted);
        }

        // draw update interval
        UpdateInterval.Draw(
            lastUpdateRect,
            job,
            false,
            job.IsSuspended);

        if (DrawOrderButtons(orderRect, job, Manager.JobTracker))
        {
            Refresh();
        }

        GUI.EndGroup();
        position.y += rowRect.height;

    }

    public void DrawPawnOverview(Rect rect)
    {
        if (pawnOverviewTable == null)
        {
            pawnOverviewTable = CreatePawnOverviewTable();
            pawnOverviewTable.SetFixedSize(new(rect.width, rect.height));
        }

        pawnOverviewTable.PawnTableOnGUI(Vector2.zero);
    }

    private void RefreshWorkers()
    {
        var temp = Manager.map.mapPawns.FreeColonistsSpawned.Where(
            pawn => !pawn.WorkTypeIsDisabled(WorkTypeDef));

        // sort by either specific skill def or average over job - depending on which is known.
        temp = SkillDef != null
            ? temp.OrderByDescending(pawn => pawn.skills.GetSkill(SkillDef).Level)
            : temp.OrderByDescending(pawn => pawn.skills.AverageOfRelevantSkillsFor(WorkTypeDef));

        _workers.Clear();
        _workers.AddRange(temp);
    }
}
