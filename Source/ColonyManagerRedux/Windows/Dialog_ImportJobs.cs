// Dialog_ImportJobs.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HotSwappable]
internal sealed class Dialog_ImportJobs : Window
{
    private readonly Action<int>? _onImport;
    private readonly List<ManagerJob> _jobs;
    private List<MultiCheckboxState> _selectedJobs;

    private Vector2 _jobListScrollPosition;
    private float _jobListScrollViewHeight;

    public override Vector2 InitialSize => new(400f, 400f);

    private static readonly Vector2 ButtonSize = new(175f, 38f);

    private List<ManagerJob> SelectedJobs
    {
        get
        {
            return _jobs.Where((t, i) => _selectedJobs[i] == MultiCheckboxState.On).ToList();
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

    private void DoJobListGUI(Rect jobsRect)
    {
        Rect jobViewRect = new(0f, 0f, jobsRect.width, _jobListScrollViewHeight);
        Widgets.BeginScrollView(jobsRect, ref _jobListScrollPosition, jobViewRect);

        Text.Anchor = TextAnchor.MiddleLeft;
        float cumulativeHeight = 0f;
        for (int i = 0; i < _jobs.Count; i++)
        {
            var job = _jobs[i];
            var state = _selectedJobs[i];

            Rect jobRowRect = new(0f, cumulativeHeight, jobViewRect.width - 16f, 50f);
            Widgets.DrawHighlightIfMouseover(jobRowRect);

            if (job.IsValid)
            {
                job.Tab.DrawListEntry(job, jobRowRect, ManagerTab.ListEntryDrawMode.Export);

                if (i % 2 == 0)
                {
                    Widgets.DrawAltRect(jobRowRect);
                }
                _selectedJobs[i] = Widgets.CheckboxMulti(new Rect(jobViewRect.width - 20f - 16f - Constants.Margin, cumulativeHeight + 15f, 20f, 20f), state, paintable: true);
            }
            else
            {
                GUI.color = Color.gray;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.DrawBox(jobRowRect.TrimRight(24f));
                Widgets.Label(jobRowRect.TrimRight(24f), "ColonyManagerRedux.InvalidJob".Translate(job.Label));
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }

            cumulativeHeight += jobRowRect.height;
        }
        Text.Anchor = TextAnchor.UpperLeft;

        Widgets.EndScrollView();

        if (Event.current.type == EventType.Layout)
        {
            _jobListScrollViewHeight = cumulativeHeight;
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
        if (Widgets_Buttons.DisableableButtonText(
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
        var selectedJobs = SelectedJobs;
        foreach (var job in selectedJobs)
        {
            job.PreImport();
            Manager.For(Find.CurrentMap).JobTracker.Add(job);
            job.PostImport();
        }
        _onImport?.Invoke(selectedJobs.Count);
        Close();
    }
}
