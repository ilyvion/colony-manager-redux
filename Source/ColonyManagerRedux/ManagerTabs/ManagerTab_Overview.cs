// ManagerTab_Overview.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[HotSwappable]
internal sealed partial class ManagerTab_Overview(Manager manager) : ManagerTab(manager)
{
    public const float OverviewWidthRatio = .6f;

    private float _overviewHeight = 9999f;
    private Vector2 _overviewScrollPosition = Vector2.zero;
    private List<Pawn> Workers = [];

    public override string Label { get; } = "ColonyManagerRedux.Overview".Translate();

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

    internal override void Notify_PawnsChanged()
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
        if (manager.JobTracker.HasNoJobs)
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
                contentRect.width -= ScrollbarWidth;
            }

            GUI.BeginGroup(viewRect);
            Widgets.BeginScrollView(viewRect, ref _overviewScrollPosition, contentRect);

            var cur = Vector2.zero;

            var alternate = false;
            foreach (ManagerJob job in manager.JobTracker.JobsOfType<ManagerJob>())
            {
                var row = new Rect(cur.x, cur.y, contentRect.width, 0f);
                DrawListEntry(job, ref cur, contentRect.width, ListEntryDrawMode.Overview);
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
