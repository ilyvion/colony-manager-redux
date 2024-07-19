// ManagerTab_Overview.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

public class ManagerTab_Overview(Manager manager) : ManagerTab(manager)
{
    public const float OverviewWidthRatio = .6f;

    private float _overviewHeight = 9999f;
    private Vector2 _overviewScrollPosition = Vector2.zero;
    private Vector2 _workersScrollPosition = Vector2.zero;
    private WorkTypeDef? _workType;
    private List<Pawn> Workers = [];

    public override string Label { get; } = "ColonyManagerRedux.ManagerOverview".Translate();

    private SkillDef? SkillDef { get; set; }

    private WorkTypeDef WorkTypeDef
    {
        get
        {
            _workType ??= Utilities.WorkTypeDefOf_Managing;

            return _workType;
        }
        set
        {
            _workType = value;
            RefreshWorkers();
        }
    }

    public override void DoWindowContents(Rect canvas)
    {
        var overviewRect = new Rect(0f, 0f, OverviewWidthRatio * canvas.width, canvas.height).RoundToInt();
        var sideRectUpper = new Rect(overviewRect.xMax + Margin, 0f,
                                      (1 - OverviewWidthRatio) * canvas.width - Margin,
                                      (canvas.height - Margin) / 2).RoundToInt();
        var sideRectLower = new Rect(overviewRect.xMax + Margin, sideRectUpper.yMax + Margin,
                                      sideRectUpper.width,
                                      sideRectUpper.height - 1).RoundToInt();

        // draw the listing of current jobs.
        Widgets.DrawMenuSection(overviewRect);
        DrawOverview(overviewRect);

        // draw the selected job's details
        Widgets.DrawMenuSection(sideRectUpper);
        Selected?.Tab?.DrawOverviewDetails(Selected, sideRectUpper);

        // overview of managers & pawns (capable of) doing this job.
        Widgets.DrawMenuSection(sideRectLower);
        DrawPawnOverview(sideRectLower);
    }

    public void DrawOverview(Rect rect)
    {
        if (manager.JobStack.IsEmpty)
        {
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = Color.grey;
            Widgets.Label(rect, "ColonyManagerRedux.ManagerNoJobs".Translate());
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
                contentRect.width -= ScrollbarWidth;
            }

            GUI.BeginGroup(viewRect);
            Widgets.BeginScrollView(viewRect, ref _overviewScrollPosition, contentRect);

            var cur = Vector2.zero;

            var alternate = false;
            foreach (ManagerJob job in manager.JobStack.JobsOfType<ManagerJob>())
            {
                var row = new Rect(cur.x, cur.y, contentRect.width, 50f);

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

                // go to job icon
                var iconRect = new Rect(Margin, row.yMin + (LargeListEntryHeight - LargeIconSize) / 2,
                                         LargeIconSize, LargeIconSize);
                var tab = job.Tab;
                if (tab != null && Widgets.ButtonImage(iconRect, tab.def.icon))
                {
                    MainTabWindow_Manager.GoTo(tab, job);
                }

                // order buttons
                DrawOrderButtons(new Rect(row.xMax - 50f, row.yMin, 50f, 50f), manager, job);

                // job specific overview.
                var jobRect = row;
                jobRect.width -= LargeListEntryHeight + LargeIconSize + 2 * Margin; // - (a + b)?
                jobRect.x += LargeIconSize + 2 * Margin;
                job.Tab.DrawListEntry(job, jobRect, ListEntryDrawMode.Overview);
                Widgets.DrawHighlightIfMouseover(row);
                if (Widgets.ButtonInvisible(jobRect))
                {
                    Selected = job;
                }

                cur.y += 50f;
            }

            Widgets.EndScrollView();
            GUI.EndGroup();

            _overviewHeight = cur.y;
        }
    }

    public void DrawPawnOverview(Rect rect)
    {
        // table body viewport
        var tableOutRect = new Rect(0f, ListEntryHeight, rect.width, rect.height - ListEntryHeight).RoundToInt();
        var tableViewRect =
            new Rect(0f, ListEntryHeight, rect.width, Workers.Count * ListEntryHeight).RoundToInt();
        if (tableViewRect.height > tableOutRect.height)
        {
            tableViewRect.width -= ScrollbarWidth;
        }

        // column width
        var colWidth = tableViewRect.width / 4 - Margin;

        // column headers
        var nameColumnHeaderRect = new Rect(colWidth * 0, 0f, colWidth, ListEntryHeight).RoundToInt();
        var activityColumnHeaderRect = new Rect(colWidth * 1, 0f, colWidth * 2.5f, ListEntryHeight).RoundToInt();
        var priorityColumnHeaderRect =
            new Rect(colWidth * 3.5f, 0f, colWidth * .5f, ListEntryHeight).RoundToInt();

        // label for priority column
        var workLabel = Find.PlaySettings.useWorkPriorities
            ? "ColonyManagerRedux.ManagerPriority".Translate()
            : "ColonyManagerRedux.ManagerEnabled".Translate();

        // begin drawing
        GUI.BeginGroup(rect);

        // draw labels
        Widgets_Labels.Label(nameColumnHeaderRect, WorkTypeDef.pawnLabel + "ColonyManagerRedux.ManagerPluralSuffix".Translate(),
                              TextAnchor.LowerCenter);
        Widgets_Labels.Label(activityColumnHeaderRect, "ColonyManagerRedux.ManagerActivity".Translate(), TextAnchor.LowerCenter);
        Widgets_Labels.Label(priorityColumnHeaderRect, workLabel, TextAnchor.LowerCenter);

        // begin scrolling area
        Widgets.BeginScrollView(tableOutRect, ref _workersScrollPosition, tableViewRect);
        GUI.BeginGroup(tableViewRect);

        // draw pawn rows
        var cur = Vector2.zero;
        for (var i = 0; i < Workers.Count; i++)
        {
            var row = new Rect(cur.x, cur.y, tableViewRect.width, ListEntryHeight);
            if (i % 2 == 0)
            {
                Widgets.DrawAltRect(row);
            }

            try
            {
                DrawPawnOverviewRow(Workers[i], row);
            }
            catch // pawn death, etc.
            {
                // rehresh the list and skip drawing untill the next GUI tick.
                RefreshWorkers();
                Widgets.EndScrollView();
                return;
            }

            cur.y += ListEntryHeight;
        }

        // end scrolling area
        GUI.EndGroup();
        Widgets.EndScrollView();

        // done!
        GUI.EndGroup();
    }

    public override void PreOpen()
    {
        RefreshWorkers();
    }

    private void DrawPawnOverviewRow(Pawn pawn, Rect rect)
    {
        // column width
        var colWidth = rect.width / 4 - Margin;

        // cell rects
        var nameRect = new Rect(colWidth * 0, rect.yMin, colWidth, ListEntryHeight).RoundToInt();
        var activityRect = new Rect(colWidth * 1, rect.yMin, colWidth * 2.5f, ListEntryHeight).RoundToInt();
        var priorityRect = new Rect(colWidth * 3.5f, rect.yMin, colWidth * .5f, ListEntryHeight).RoundToInt();

        // name
        Widgets.DrawHighlightIfMouseover(nameRect);

        // on click select and jump to location
        if (Widgets.ButtonInvisible(nameRect))
        {
            Find.MainTabsRoot.EscapeCurrentTab();
            CameraJumper.TryJump(pawn.PositionHeld, pawn.Map);
            Find.Selector.ClearSelection();
            if (pawn.Spawned)
            {
                Find.Selector.Select(pawn);
            }
        }

        Widgets_Labels.Label(nameRect, pawn.Name.ToStringShort, "ColonyManagerRedux.ManagerClickToJumpTo".Translate(pawn.LabelCap),
                              TextAnchor.MiddleLeft, margin: Margin);

        // current activity (if curDriver != null)
        var activityString = pawn.jobs.curDriver?.GetReport() ?? "ColonyManagerRedux.ManagerNoCurJob".Translate();
        Widgets_Labels.Label(activityRect, activityString, pawn.jobs.curDriver?.GetReport(),
                              TextAnchor.MiddleCenter, margin: Margin, font: GameFont.Tiny);

        // priority button
        var priorityPosition = new Rect(0f, 0f, 24f, 24f).CenteredIn(priorityRect).RoundToInt();
        Text.Font = GameFont.Medium;
        bool incapable = Utilities.IsIncapableOfWholeWorkType(pawn, WorkTypeDef);
        WidgetsWork.DrawWorkBoxFor(priorityPosition.xMin, priorityPosition.yMin, pawn, WorkTypeDef, incapable);
        if (Mouse.IsOver(priorityPosition))
        {
            TooltipHandler.TipRegion(priorityPosition, () => WidgetsWork.TipForPawnWorker(pawn, WorkTypeDef, incapable), pawn.thingIDNumber ^ WorkTypeDef.GetHashCode());
        }
        Text.Font = GameFont.Small;
    }

    private void RefreshWorkers()
    {
        var temp =
            manager.map.mapPawns.FreeColonistsSpawned.Where(
                pawn => !pawn.WorkTypeIsDisabled(WorkTypeDef));

        // sort by either specific skill def or average over job - depending on which is known.
        temp = SkillDef != null
            ? temp.OrderByDescending(pawn => pawn.skills.GetSkill(SkillDef).Level)
            : temp.OrderByDescending(pawn => pawn.skills.AverageOfRelevantSkillsFor(WorkTypeDef));

        Workers = temp.ToList();
    }
}
