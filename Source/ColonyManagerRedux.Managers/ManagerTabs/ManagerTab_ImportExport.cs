// ManagerTab_ImportExport.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.UI;

namespace ColonyManagerRedux.Managers;

[HotSwappable]
internal sealed partial class ManagerTab_ImportExport(Manager manager) : ManagerTab(manager)
{
    private static readonly Color DefaultFileTextColor = new(1f, 1f, 0.6f);

    private string _folder = "";

    private const float IconSize = 32f;

    private const float LoadAreaRatio = .6f;

    private const float RowHeight = 40f;

    private const float ModeBarHeight = 32f;

    private const float MinButtonWidth = 70f;

    private const string SaveNameBase = "ManagerJobs_";
    private const string SaveExtension = ".cmr";
    private const string TemplateNameBase = "Template_";

    private bool _templateMode;

    private string CurrentSaveNameBase => _templateMode ? TemplateNameBase : SaveNameBase;

    private string CurrentSaveExtension =>
        _templateMode ? ManagerJobTemplates.TemplateExtension : SaveExtension;

    private List<SaveFileInfo> _saveFiles = [];

    private string _saveName = "";

    private List<ManagerJob> _jobs = [];
    private List<MultiCheckboxState> _selectedJobs = [];

    private IEnumerable<ManagerJob> SelectedJobs =>
        _jobs.Where((_, i) => _selectedJobs[i] == MultiCheckboxState.On);

    protected override void DoTabContents(Rect canvas)
    {
        var modeBarRect = new Rect(0f, 0f, canvas.width, ModeBarHeight);
        DrawModeBar(modeBarRect);

        var contentY = modeBarRect.yMax + Constants.Margin;
        var contentHeight = canvas.height - contentY;

        var loadRect = new Rect(
            0f,
            contentY,
            (canvas.width - Constants.Margin) * LoadAreaRatio,
            contentHeight
        );
        var saveRect = new Rect(
            loadRect.xMax + Constants.Margin,
            contentY,
            canvas.width - Constants.Margin - loadRect.width,
            contentHeight
        );
        Widgets.DrawMenuSection(loadRect);
        Widgets.DrawMenuSection(saveRect);

        DrawLoadSection(loadRect);
        DrawSaveSection(saveRect);
    }

    private void DrawModeBar(Rect rect)
    {
        var buttonWidth = (rect.width - Constants.Margin) / 2f;
        var jobSavesRect = new Rect(rect.x, rect.y, buttonWidth, rect.height);
        var templatesRect = new Rect(
            jobSavesRect.xMax + Constants.Margin,
            rect.y,
            buttonWidth,
            rect.height
        );

        if (
            Widgets.ButtonText(
                jobSavesRect,
                "ColonyManagerRedux.ManagerImportExport.JobSaves".Translate()
            ) && _templateMode
        )
        {
            SetMode(false);
        }
        if (
            Widgets.ButtonText(
                templatesRect,
                "ColonyManagerRedux.ManagerImportExport.Templates".Translate()
            ) && !_templateMode
        )
        {
            SetMode(true);
        }

        Widgets.DrawBox(_templateMode ? templatesRect : jobSavesRect, 2);
    }

    private void SetMode(bool templateMode)
    {
        if (_templateMode == templateMode)
        {
            return;
        }

        _templateMode = templateMode;
        _folder = GetSaveLocation();
        Refresh();
    }

    public override void PreOpen()
    {
        // set save location
        _folder = GetSaveLocation();

        // variable stuff
        Refresh();
    }

    public override void PostClose()
    {
        _jobs.Clear();
        _selectedJobs.Clear();
        _saveFiles.Clear();
    }

    protected override void Refresh()
    {
        _ = Manager.SetScribingMode(ScribingMode.Transfer);
        _jobs = [.. Manager.JobTracker.JobsOfType<ManagerJob>().Where(j => j.IsTransferable)];
        _ = Manager.SetScribingMode(ScribingMode.Normal);

        _selectedJobs = [.. _jobs.Select(_ => new MultiCheckboxState())];

        // fetch the list of saved jobs
        _saveFiles = GetSavedFilesList();

        // set a valid default name
        _saveName = DefaultSaveName();
    }

    private string DefaultSaveName()
    {
        // keep adding 1 until we have a new name.
        var i = 1;
        var name = CurrentSaveNameBase + i;
        while (SaveExists(name))
        {
            i++;
            name = CurrentSaveNameBase + i;
        }

        return name;
    }

    private void DoExport(string name)
    {
        _ = Manager.SetScribingMode(ScribingMode.Transfer);
        var exportJobs = SelectedJobs.ToList();
        try
        {
            try
            {
                Scribe.saver.InitSaving(
                    FilePath(name),
                    _templateMode ? "ManagerJobTemplate" : "ManagerJobs"
                );
            }
            catch (Exception ex)
            {
                GenUI.ErrorDialog("ProblemSavingFile".Translate(ex.ToString()));
                return;
            }

            ScribeMetaHeaderUtility.WriteMetaHeader();

            foreach (var job in exportJobs)
            {
                job.PreExport();
            }
            Scribe_Collections.Look(ref exportJobs, "jobs", LookMode.Deep);
            foreach (var job in exportJobs)
            {
                job.PostExport();
            }
        }
        catch (Exception ex)
        {
            ColonyManagerReduxMod.Instance.LogError("Exception while exporting jobs: " + ex);
        }
        finally
        {
            _ = Manager.SetScribingMode(ScribingMode.Normal);
            Scribe.saver.FinalizeSaving();
            Messages.Message(
                (
                    _templateMode
                        ? "ColonyManagerRedux.ManagerTemplateSaved"
                        : "ColonyManagerRedux.ManagerJobsExported"
                ).Translate(exportJobs.Count),
                MessageTypeDefOf.TaskCompletion
            );
            Refresh();
        }
    }

    private void DoImport(SaveFileInfo file)
    {
        var filePath = _folder + "/" + file.FileInfo.Name;
        var fileName = Path.GetFileNameWithoutExtension(file.FileInfo.Name);
        ScribeModMismatchUtility.LoadWithModMismatchConfirmation(
            filePath,
            "ColonyManagerRedux.Templates.ModMismatchHeader".Translate(
                (
                    _templateMode
                        ? "ColonyManagerRedux.Templates.FileTypeTemplate"
                        : "ColonyManagerRedux.Templates.FileTypeJobSave"
                ).Translate(),
                fileName
            ),
            () =>
            {
                Scribe.loader.InitLoading(filePath);
                _ = Manager.SetScribingMode(ScribingMode.Transfer);
                List<ManagerJob> exportedJobs = [];
                try
                {
                    ScribeMetaHeaderUtility.LoadGameDataHeader(
                        ScribeMetaHeaderUtility.ScribeHeaderMode.None,
                        logVersionConflictWarning: true
                    );
                    Scribe_Collections.Look(ref exportedJobs, "jobs", LookMode.Deep, Manager);
                    Scribe.loader.FinalizeLoading();
                }
                catch
                {
                    Scribe.ForceStop();
                    return;
                }
                finally
                {
                    _ = Manager.SetScribingMode(ScribingMode.Normal);
                }

                Find.WindowStack.Add(
                    new Dialog_ImportJobs(
                        Manager,
                        exportedJobs,
                        (count) =>
                        {
                            Messages.Message(
                                "ColonyManagerRedux.ManagerJobsImported".Translate(count),
                                MessageTypeDefOf.TaskCompletion
                            );
                            Refresh();
                        }
                    )
                );
            }
        );
    }

    private static float ButtonWidthFor(string label) =>
        Mathf.Max(MinButtonWidth, Text.CalcSize(label).x + (4 * Constants.Margin));

    private static float ButtonWidthFor(params string[] labels) => labels.Max(ButtonWidthFor);

    private void DrawFileEntry(Rect rect, SaveFileInfo file)
    {
        GUI.BeginGroup(rect);

        var templateName = Path.GetFileNameWithoutExtension(file.FileInfo.Name);
        var isDefaultTemplate =
            _templateMode && ColonyManagerReduxMod.Settings.DefaultTemplateName == templateName;

        var loadButtonLabel = (
            _templateMode
                ? "ColonyManagerRedux.ManagerApplyTemplate"
                : "ColonyManagerRedux.ManagerImport"
        ).Translate();
        var loadButtonWidth = ButtonWidthFor(loadButtonLabel);

        var defaultButtonLabel = (
            isDefaultTemplate
                ? "ColonyManagerRedux.ManagerTemplateIsDefault"
                : "ColonyManagerRedux.ManagerSetTemplateAsDefault"
        ).Translate();
        // Use the wider of the two possible labels so the column doesn't shift width between
        // rows depending on whether that row's template happens to be the default.
        var defaultButtonWidth = _templateMode
            ? ButtonWidthFor(
                "ColonyManagerRedux.ManagerTemplateIsDefault".Translate(),
                "ColonyManagerRedux.ManagerSetTemplateAsDefault".Translate()
            )
            : 0f;
        var defaultButtonReserve = _templateMode ? defaultButtonWidth + Constants.Margin : 0f;

        // set up rects
        var nameRect = rect.AtZero();
        nameRect.width -=
            (Prefs.DisableTinyText ? 150f : 100f)
            + loadButtonWidth
            + defaultButtonReserve
            + IconSize
            + (4 * Constants.Margin);
        nameRect.xMin += 2 * Constants.Margin;
        var timeRect = new Rect(
            nameRect.xMax + Constants.Margin,
            0f,
            Prefs.DisableTinyText ? 150f : 100f,
            rect.height
        );
        var buttonRect = new Rect(
            timeRect.xMax + Constants.Margin,
            1f,
            loadButtonWidth,
            rect.height - 2f
        );
        var defaultRect = new Rect(
            buttonRect.xMax + Constants.Margin,
            1f,
            defaultButtonWidth,
            rect.height - 2f
        );
        var deleteRect = new Rect(
            (_templateMode ? defaultRect.xMax : buttonRect.xMax) + Constants.Margin,
            (rect.height - IconSize) / 2,
            IconSize,
            IconSize
        );

        IlyvionDebugViewSettings.DrawIfUIHelpers(() =>
        {
            Widgets.DrawRectFast(nameRect, ColorLibrary.Aqua.ToTransparent(.5f));
            Widgets.DrawRectFast(timeRect, ColorLibrary.Beige.ToTransparent(.5f));
            Widgets.DrawRectFast(buttonRect, ColorLibrary.BrickRed.ToTransparent(.5f));
            Widgets.DrawRectFast(deleteRect, ColorLibrary.BrightPink.ToTransparent(.5f));
        });

        // name
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = DefaultFileTextColor;
        Widgets.Label(nameRect, templateName);
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;

        // timestamp
        GUI.color = Color.gray;
        Dialog_FileList.DrawDateAndVersion(file, timeRect);
        Text.Font = GameFont.Small;
        GUI.color = Color.white;

        // load button
        if (Widgets.ButtonText(buttonRect, loadButtonLabel))
        {
            TryImport(file);
        }

        // set-as-default button (templates only)
        if (_templateMode)
        {
            if (
                IlyvionWidgets.DisableableButtonText(
                    defaultRect,
                    defaultButtonLabel,
                    enabled: !isDefaultTemplate
                )
            )
            {
                ColonyManagerReduxMod.Settings.DefaultTemplateName = templateName;
            }
            TooltipHandler.TipRegionByKey(
                defaultRect,
                "ColonyManagerRedux.ManagerSetTemplateAsDefault.Tip"
            );
        }

        // delete button
        if (
            Widgets.ButtonImage(
                deleteRect,
                TexButton.Delete,
                Color.white,
                GenUI.SubtleMouseoverColor
            )
        )
        {
            Find.WindowStack.Add(
                new Dialog_Confirm(
                    "ConfirmDelete".Translate(file.FileInfo.Name),
                    delegate
                    {
                        if (isDefaultTemplate)
                        {
                            ColonyManagerReduxMod.Settings.DefaultTemplateName = null;
                        }
                        file.FileInfo.Delete();
                        Refresh();
                    }
                )
            );
        }
        TooltipHandler.TipRegionByKey(deleteRect, "ColonyManagerRedux.DeleteThisManagerFile");

        GUI.EndGroup();
    }

    private void DrawLoadSection(Rect rect)
    {
        if (_saveFiles.NullOrEmpty())
        {
            // no saves found.
            GUI.color = Color.gray;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, "ColonyManagerRedux.ManagerNoSaves".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
        }
        else
        {
            GUI.BeginGroup(rect);
            var cur = Vector2.zero;
            try
            {
                var i = 1;
                foreach (var file in _saveFiles)
                {
                    var row = new Rect(0f, cur.y, rect.width, RowHeight);
                    if (i++ % 2 == 0)
                    {
                        Widgets.DrawAltRect(row);
                    }
                    DrawFileEntry(row, file);
                    cur.y += RowHeight;
                }
            }
            finally
            {
                // make sure it gets ended even if something fails.
                GUI.EndGroup();
            }
        }
    }

    private void DrawSaveSection(Rect rect)
    {
        var infoRect = new Rect(rect.ContractedBy(Constants.Margin));
        infoRect.height -= 30f + Constants.Margin;
        var nameRect = new Rect(
            rect.xMin + Constants.Margin,
            infoRect.yMax + Constants.Margin,
            (rect.width - (3 * Constants.Margin)) / 2,
            30f
        );
        var buttonRect = new Rect(
            nameRect.xMax + Constants.Margin,
            infoRect.yMax + Constants.Margin,
            nameRect.width,
            30f
        );

        Widgets.Label(
            infoRect,
            (
                _templateMode
                    ? "ColonyManagerRedux.SelectTemplateJobs"
                    : "ColonyManagerRedux.SelectExportJobs"
            ).Translate()
        );
        infoRect.yMin += Constants.ListEntryHeight;

        DoJobList(infoRect);

        GUI.SetNextControlName("ManagerJobsNameField");
        var name = Widgets.TextField(nameRect, _saveName);
        if (GenText.IsValidFilename(name))
        {
            _saveName = name;
        }

        var anySelected = _selectedJobs.Any(t => t != MultiCheckboxState.Off);
        if (
            IlyvionWidgets.DisableableButtonText(
                buttonRect,
                (
                    _templateMode
                        ? "ColonyManagerRedux.SaveAsTemplate"
                        : "ColonyManagerRedux.ManagerExport"
                ).Translate(),
                enabled: anySelected
            )
        )
        {
            TryExport(_saveName);
        }
    }

    private readonly ScrollViewStatus _scrollViewStatus = new();

    protected override void DoJobList(Rect jobsRect)
    {
        using var scrollView = GUIScope.ScrollView(jobsRect, _scrollViewStatus);
        using var _ = GUIScope.TextAnchor(TextAnchor.MiddleLeft);

        var cur = Vector2.zero;

        for (var i = 0; i < _jobs.Count; i++)
        {
            var job = _jobs[i];
            var state = _selectedJobs[i];

            var row = new Rect(0f, cur.y, scrollView.ViewRect.width, 0f);
            DrawExportListEntry(job, ref cur, scrollView.ViewRect.width);
            row.height = cur.y - row.y;

            Widgets.DrawHighlightIfMouseover(row);

            if (i % 2 == 0)
            {
                Widgets.DrawAltRect(row);
            }

            _selectedJobs[i] = Widgets.CheckboxMulti(
                new Rect(row.width - 24f, row.y + 15f, 20f, 20f),
                state,
                paintable: true
            );
        }

        if (Event.current.type == EventType.Layout)
        {
            scrollView.Height = cur.y;
        }
    }

    internal static void DrawExportListEntry(ManagerJob job, ref Vector2 position, float width)
    {
        if (
            job.CompOfType<CompDrawExportListEntry>() is CompDrawExportListEntry drawExportListEntry
        )
        {
            var props = drawExportListEntry.Props;
            if (props.takeOverRendering)
            {
                props.Worker.DrawExportListEntry(job, ref position, width);
                return;
            }
        }

        var tab = job.Tab;

        var labelWidth = width - Constants.LargeListEntryHeight - LastUpdateRectWidth;

        // create label string
        var subLabel = tab.GetSubLabel(job);
        var (label, labelSize) = tab.GetFullLabel(job, labelWidth, subLabel);

        // set up rects
        Rect labelRect = new(Constants.Margin, 0f, labelWidth, labelSize.y);

        Rect statusRect = new(
            labelRect.xMax + Constants.Margin,
            Constants.Margin,
            LastUpdateRectWidth,
            labelRect.height
        );

        var maxRowHeight = Mathf.Max(labelRect.yMax, statusRect.yMax) + Constants.Margin;
        Rect rowRect = new(position.x, position.y, width, maxRowHeight);

        Rect lastUpdateRect = new(
            statusRect.xMin,
            statusRect.y,
            LastUpdateRectWidth,
            statusRect.height
        );

        // do the drawing
        GUI.BeginGroup(rowRect);
        rowRect = rowRect.AtZero();

        labelRect = labelRect.CenteredOnYIn(rowRect);

        IlyvionDebugViewSettings.DrawIfUIHelpers(() =>
        {
            Widgets.DrawRectFast(labelRect, Color.blue.ToTransparent(.2f));
            Widgets.DrawRectFast(statusRect, Color.yellow.ToTransparent(.2f));
        });

        // draw label
        IlyvionWidgets.Label(labelRect, label, subLabel, TextAnchor.MiddleLeft);

        // draw update interval
        UpdateInterval.Draw(lastUpdateRect, job, true, false);

        GUI.EndGroup();
        position.y += rowRect.height;
    }

    private string FilePath(string name) => _folder + "/" + name + CurrentSaveExtension;

    private List<SaveFileInfo> GetSavedFilesList()
    {
        var directoryInfo = new DirectoryInfo(_folder);
        if (!directoryInfo.Exists)
        {
            return [];
        }

        // raw files
        var files =
            from f in directoryInfo.GetFiles()
            where f.Extension == CurrentSaveExtension
            orderby f.LastWriteTime descending
            select f;

        // convert to RW save files - mostly for the headers
        var saves = new List<SaveFileInfo>();
        foreach (var current in files)
        {
            try
            {
                var saveFileInfo = new SaveFileInfo(current);
                saveFileInfo.LoadData();
                saves.Add(saveFileInfo);
            }
            catch (Exception ex)
            {
                ColonyManagerReduxMod.Instance.LogError(
                    "Exception loading " + current.Name + ": " + ex
                );
                continue;
            }
        }

        return saves;
    }

    private string GetSaveLocation() =>
        _templateMode
            ? ManagerJobTemplates.GetTemplateSaveLocation()
            : GenFilePaths.FolderUnderSaveData("ManagerJobs");

    private bool SaveExists(string name) =>
        _saveFiles.Any(save => save.FileInfo.Name == name + CurrentSaveExtension);

    private void TryExport(string name)
    {
        // if it exists, confirm overwrite
        if (SaveExists(name))
        {
            Find.WindowStack.Add(
                new Dialog_Confirm(
                    "ColonyManagerRedux.ManagerConfirmOverwrite".Translate(name),
                    delegate
                    {
                        DoExport(name);
                    }
                )
            );
        }
        else
        {
            DoExport(name);
        }
    }

    private void TryImport(SaveFileInfo file) => DoImport(file);
}

internal enum ScribingMode
{
    Transfer,
    Normal,
}
