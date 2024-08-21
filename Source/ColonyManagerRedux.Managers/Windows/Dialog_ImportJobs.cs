// Dialog_ImportJobs.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.UI;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed class Dialog_ImportJobs : Window
{
    private readonly Action<int>? _onImport;
    private readonly List<ManagerJob> _jobs;
    private List<MultiCheckboxState> _selectedJobs;

    public override Vector2 InitialSize => new(400f, 400f);

    private static readonly Vector2 ButtonSize = new(175f, 38f);

    private IEnumerable<ManagerJob> SelectedJobs
    {
        get
        {
            return _jobs.Where((t, i) => _selectedJobs[i] == MultiCheckboxState.On);
        }
    }

    public Dialog_ImportJobs(List<ManagerJob> jobs, Action<int>? onImport = null)
    {
        _jobs = jobs;
        _selectedJobs = jobs.Select(_ => MultiCheckboxState.On).ToList();

        _onImport = onImport;

        forcePause = true;
        closeOnClickedOutside = true;
        closeOnAccept = false;
    }

    private ScrollViewStatus _scrollViewStatus = new();
    private void DoJobListGUI(Rect jobsRect)
    {
        using var scrollView = GUIScope.ScrollView(jobsRect, _scrollViewStatus);
        using var _ = GUIScope.TextAnchor(TextAnchor.MiddleLeft);

        var cur = Vector2.zero;
        for (int i = 0; i < _jobs.Count; i++)
        {
            var job = _jobs[i];
            var state = _selectedJobs[i];

            if (!job.IsValid)
            {
                cur = DrawInvalidJob(scrollView, cur, job, "");
                continue;
            }

            var row = new Rect(0f, cur.y, scrollView.ViewRect.width, 0f);
            try
            {
                ManagerTab_ImportExport.DrawExportListEntry(
                    job,
                    ref cur,
                    scrollView.ViewRect.width);
            }
            catch (Exception e)
            {
                cur = DrawInvalidJob(scrollView, cur, job, e.Message);
                _selectedJobs[i] = MultiCheckboxState.Off;
                continue;
            }
            finally
            {
                row.height = cur.y - row.y;
            }

            if (i % 2 == 0)
            {
                Widgets.DrawAltRect(row);
            }
            _selectedJobs[i] = Widgets.CheckboxMulti(new Rect(scrollView.ViewRect.width - 20f - Constants.Margin, row.y + 15f, 20f, 20f), state, paintable: true);
        }

        if (Event.current.type == EventType.Layout)
        {
            scrollView.Height = cur.y;
        }

        static Vector2 DrawInvalidJob(ScrollViewScope scrollView, Vector2 cur, ManagerJob job, string reason)
        {
            Rect jobRowRect = new(0f, cur.y, scrollView.ViewRect.width, Constants.LargeListEntryHeight);
            GUI.color = Color.gray;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.DrawBox(jobRowRect);
            string label;
            try
            {
                label = job.Label;
            }
            catch
            {
                label = job.GetType().FullName;
            }
            Widgets.Label(jobRowRect, "ColonyManagerRedux.InvalidJob".Translate(label));
            if (!string.IsNullOrEmpty(reason))
            {
                Widgets.DrawHighlightIfMouseover(jobRowRect);
                TooltipHandler.TipRegion(jobRowRect, reason);
            }
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
            cur.y += Constants.LargeListEntryHeight;
            return cur;
        }
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Font = GameFont.Small;

        float labelHeight = Text.CalcHeight("ColonyManagerRedux.SelectImportJobs".Translate(), inRect.width);
        Widgets.Label(new Rect(0f, 0f, inRect.width, labelHeight), "ColonyManagerRedux.SelectImportJobs".Translate());
        float nextY = labelHeight + 5f;

        Rect jobsRect = new(inRect)
        {
            y = nextY
        };
        jobsRect.height -= nextY + ButtonSize.y + 5f;
        Widgets.BeginGroup(jobsRect);
        DoJobListGUI(jobsRect.AtZero());
        Widgets.EndGroup();

        Rect buttonsRect = new(inRect)
        {
            y = jobsRect.yMax + 5f,
            height = ButtonSize.y
        };

        if (Widgets.ButtonText(new Rect(0f, inRect.height - ButtonSize.y, ButtonSize.x, ButtonSize.y), "Close".Translate()))
        {
            Close();
        }

        bool anySelected = _selectedJobs.Any(t => t != MultiCheckboxState.Off);
        if (IlyvionWidgets.DisableableButtonText(
            new Rect(inRect.width - ButtonSize.x, inRect.height - ButtonSize.y, ButtonSize.x, ButtonSize.y),
            "ColonyManagerRedux.ManagerImport".Translate(),
            enabled: anySelected))
        {
            OnAccept();
        }
    }

    public override void OnAcceptKeyPressed()
    {
        base.OnAcceptKeyPressed();
        bool anySelected = _selectedJobs.Any(t => t != MultiCheckboxState.Off);
        if (anySelected)
        {
            OnAccept();
        }
    }

    private void OnAccept()
    {
        var jobCount = 0;
        foreach (var job in SelectedJobs)
        {
            jobCount++;
            job.PreImport();
            Manager.For(Find.CurrentMap).JobTracker.Add(job);
            job.PostImport();
        }
        _onImport?.Invoke(jobCount);
        Close();
    }
}
