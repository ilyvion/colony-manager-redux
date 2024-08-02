// ManagerTab_ImportExport.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.IO;
using System.Reflection;
using ilyvion.Laboratory;
using ilyvion.Laboratory.UI;

namespace ColonyManagerRedux;

[HotSwappable]
internal sealed class ManagerTab_ImportExport(Manager manager) : ManagerTab(manager)
{
    private static readonly Color DefaultFileTextColor = new(1f, 1f, 0.6f);

    private static readonly MethodInfo FolderUnderSaveData_MI
        = AccessTools.Method(typeof(GenFilePaths), "FolderUnderSaveData");

    private string _folder = "";

    private const float IconSize = 32f;

    private const float LoadAreaRatio = .6f;

    private const float RowHeight = 40f;

    private const string SaveNameBase = "ManagerJobs_";
    private const string SaveExtension = ".cmr";

    private List<SaveFileInfo> _saveFiles = [];

    private string _saveName = "";

    private List<ManagerJob> _jobs = [];
    private List<MultiCheckboxState> _selectedJobs = [];

    public override string Label
    {
        get { return "ColonyManagerRedux.ManagerImportExport".Translate(); }
    }

    private List<ManagerJob> SelectedJobs =>
        _jobs.Where((_, i) => _selectedJobs[i] == MultiCheckboxState.On).ToList();

    protected override void DoTabContents(Rect canvas)
    {
        var loadRect = new Rect(0f, 0f, (canvas.width - Constants.Margin) * LoadAreaRatio, canvas.height);
        var saveRect = new Rect(loadRect.xMax + Constants.Margin, 0f, canvas.width - Constants.Margin - loadRect.width,
                                 canvas.height);
        Widgets.DrawMenuSection(loadRect);
        Widgets.DrawMenuSection(saveRect);

        DrawLoadSection(loadRect);
        DrawSaveSection(saveRect);
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
        _jobs = manager.JobTracker.JobsOfType<ManagerJob>().Where(j => j.IsTransferable).ToList();
        _selectedJobs = _jobs.Select(_ => new MultiCheckboxState()).ToList();

        // fetch the list of saved jobs
        _saveFiles = GetSavedFilesList();

        // set a valid default name
        _saveName = DefaultSaveName();
    }

    private string DefaultSaveName()
    {
        // keep adding 1 until we have a new name.
        var i = 1;
        string name = SaveNameBase + i;
        while (SaveExists(name))
        {
            i++;
            name = SaveNameBase + i;
        }

        return name;
    }

    private void DoExport(string name)
    {
        Manager.Mode = Manager.ScribingMode.Transfer;
        var exportJobs = SelectedJobs;
        try
        {
            try
            {
                Scribe.saver.InitSaving(FilePath(name), "ManagerJobs");
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
            ColonyManagerReduxMod.Instance.LogError(
                "Exception while exporting jobs: " + ex);
        }
        finally
        {
            Manager.Mode = Manager.ScribingMode.Normal;
            Scribe.saver.FinalizeSaving();
            Messages.Message("ColonyManagerRedux.ManagerJobsExported".Translate(exportJobs.Count), MessageTypeDefOf.TaskCompletion);
            Refresh();
        }
    }

    private void DoImport(SaveFileInfo file)
    {
        string filePath = _folder + "/" + file.FileInfo.Name;
        PreLoadUtility.CheckVersionAndLoad(filePath, ScribeMetaHeaderUtility.ScribeHeaderMode.None, () =>
        {
            Scribe.loader.InitLoading(filePath);
            Manager.Mode = Manager.ScribingMode.Transfer;
            List<ManagerJob> exportedJobs = [];
            try
            {
                ScribeMetaHeaderUtility.LoadGameDataHeader(ScribeMetaHeaderUtility.ScribeHeaderMode.None, logVersionConflictWarning: true);
                Scribe_Collections.Look(ref exportedJobs, "jobs", LookMode.Deep, manager);
                Scribe.loader.FinalizeLoading();
            }
            catch
            {
                Scribe.ForceStop();
                return;
            }
            finally
            {
                Manager.Mode = Manager.ScribingMode.Normal;
            }

            Find.WindowStack.Add(new Dialog_ImportJobs(exportedJobs, (count) =>
            {
                Messages.Message("ColonyManagerRedux.ManagerJobsImported".Translate(count), MessageTypeDefOf.TaskCompletion);
                Refresh();
            }));
        });
    }

    private void DrawFileEntry(Rect rect, SaveFileInfo file)
    {
        GUI.BeginGroup(rect);

        // set up rects
        Rect nameRect = rect.AtZero();
        nameRect.width -= (Prefs.DisableTinyText ? 250f : 200f) + IconSize + 4 * Constants.Margin;
        nameRect.xMin += 2 * Constants.Margin;
        var timeRect = new Rect(nameRect.xMax + Constants.Margin, 0f, Prefs.DisableTinyText ? 150f : 100f, rect.height);
        var buttonRect = new Rect(timeRect.xMax + Constants.Margin, 1f, 100f, rect.height - 2f);
        var deleteRect = new Rect(buttonRect.xMax + Constants.Margin, (rect.height - IconSize) / 2, IconSize, IconSize);

        if (IlyvionDebugViewSettings.DrawUIHelpers) {
            Widgets.DrawRectFast(nameRect, ColorLibrary.Aqua.ToTransparent(.5f));
            Widgets.DrawRectFast(timeRect, ColorLibrary.Beige.ToTransparent(.5f));
            Widgets.DrawRectFast(buttonRect, ColorLibrary.BrickRed.ToTransparent(.5f));
            Widgets.DrawRectFast(deleteRect, ColorLibrary.BrightPink.ToTransparent(.5f));
        }

        // name
        Text.Anchor = TextAnchor.MiddleLeft;
        GUI.color = DefaultFileTextColor;
        Widgets.Label(nameRect, Path.GetFileNameWithoutExtension(file.FileInfo.Name));
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;

        // timestamp
        GUI.color = Color.gray;
        Dialog_FileList.DrawDateAndVersion(file, timeRect);
        Text.Font = GameFont.Small;
        GUI.color = Color.white;

        // load button
        if (Widgets.ButtonText(buttonRect, "ColonyManagerRedux.ManagerImport".Translate()))
        {
            TryImport(file);
        }

        // delete button
        if (Widgets.ButtonImage(deleteRect, TexButton.Delete, Color.white, GenUI.SubtleMouseoverColor))
        {
            Find.WindowStack.Add(new Dialog_Confirm("ConfirmDelete".Translate(file.FileInfo.Name), delegate
            {
                file.FileInfo.Delete();
                Refresh();
            }));
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
            Vector2 cur = Vector2.zero;
            try
            {
                var i = 1;
                foreach (SaveFileInfo file in _saveFiles)
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
        var nameRect = new Rect(rect.xMin + Constants.Margin, infoRect.yMax + Constants.Margin, (rect.width - 3 * Constants.Margin) / 2, 30f);
        var buttonRect = new Rect(nameRect.xMax + Constants.Margin, infoRect.yMax + Constants.Margin, nameRect.width, 30f);

        Widgets.Label(infoRect, "ColonyManagerRedux.SelectExportJobs".Translate());
        infoRect.yMin += Constants.ListEntryHeight;

        DoJobList(infoRect);

        GUI.SetNextControlName("ManagerJobsNameField");
        string name = Widgets.TextField(nameRect, _saveName);
        if (GenText.IsValidFilename(name))
        {
            _saveName = name;
        }

        bool anySelected = _selectedJobs.Any(t => t != MultiCheckboxState.Off);
        if (Widgets_Buttons.DisableableButtonText(
            buttonRect,
            "ColonyManagerRedux.ManagerExport".Translate(),
            enabled: anySelected))
        {
            TryExport(_saveName);
        }
    }

    private ScrollViewStatus _scrollViewStatus = new();
    protected override void DoJobList(Rect jobsRect)
    {
        using var scrollView = GUIScope.ScrollView(jobsRect, _scrollViewStatus);
        using var _ = GUIScope.TextAnchor(TextAnchor.MiddleLeft);

        var cur = Vector2.zero;

        for (int i = 0; i < _jobs.Count; i++)
        {
            var job = _jobs[i];
            var state = _selectedJobs[i];

            var row = new Rect(0f, cur.y, scrollView.ViewRect.width, 0f);
            job.Tab.DrawListEntry(job, ref cur, scrollView.ViewRect.width, ListEntryDrawMode.Export, showOrdering: false);
            row.height = cur.y - row.y;

            Widgets.DrawHighlightIfMouseover(row);

            if (i % 2 == 0)
            {
                Widgets.DrawAltRect(row);
            }

            _selectedJobs[i] = Widgets.CheckboxMulti(new Rect(row.width - 24f, row.y + 15f, 20f, 20f), state, paintable: true);
        }

        if (Event.current.type == EventType.Layout)
        {
            scrollView.Height = cur.y;
        }
    }

    private string FilePath(string name)
    {
        return _folder + "/" + name + SaveExtension;
    }

    private List<SaveFileInfo> GetSavedFilesList()
    {
        var directoryInfo = new DirectoryInfo(_folder);

        // raw files
        IOrderedEnumerable<FileInfo> files = from f in directoryInfo.GetFiles()
                                             where f.Extension == SaveExtension
                                             orderby f.LastWriteTime descending
                                             select f;

        // convert to RW save files - mostly for the headers
        var saves = new List<SaveFileInfo>();
        foreach (FileInfo current in files)
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
                    "Exception loading " + current.Name + ": " + ex);
                continue;
            }
        }

        return saves;
    }

    private static string GetSaveLocation()
    {
        return (string)FolderUnderSaveData_MI.Invoke(null, ["ManagerJobs"]);
    }

    private bool SaveExists(string name)
    {
        return _saveFiles.Any(save => save.FileInfo.Name == name + SaveExtension);
    }

    private void TryExport(string name)
    {
        // if it exists, confirm overwrite
        if (SaveExists(name))
        {
            Find.WindowStack.Add(new Dialog_Confirm("ColonyManagerRedux.ManagerConfirmOverwrite".Translate(name),
                delegate { DoExport(name); }));
        }
        else
        {
            DoExport(name);
        }
    }

    private void TryImport(SaveFileInfo file)
    {
        DoImport(file);
    }
}
