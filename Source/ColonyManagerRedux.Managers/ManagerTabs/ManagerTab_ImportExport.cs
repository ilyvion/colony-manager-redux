// ManagerTab_ImportExport.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.IO;
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

    private const string SaveNameBase = "ManagerJobs_";
    private const string SaveExtension = ".cmr";

    private List<SaveFileInfo> _saveFiles = [];

    private string _saveName = "";

    private List<ManagerJob> _jobs = [];
    private List<MultiCheckboxState> _selectedJobs = [];

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
        _jobs = Manager.JobTracker.JobsOfType<ManagerJob>().Where(j => j.IsTransferable).ToList();
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
        Manager.SetScribingMode(ScribingMode.Transfer);
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
            Manager.SetScribingMode(ScribingMode.Normal);
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
            Manager.SetScribingMode(ScribingMode.Transfer);
            List<ManagerJob> exportedJobs = [];
            try
            {
                ScribeMetaHeaderUtility.LoadGameDataHeader(ScribeMetaHeaderUtility.ScribeHeaderMode.None, logVersionConflictWarning: true);
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
                Manager.SetScribingMode(ScribingMode.Normal);
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

        if (IlyvionDebugViewSettings.DrawUIHelpers)
        {
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
        if (IlyvionWidgets.DisableableButtonText(
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
            DrawExportListEntry(job, ref cur, scrollView.ViewRect.width);
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

    internal static void DrawExportListEntry(
        ManagerJob job,
        ref Vector2 position,
        float width)
    {
        if (job.CompOfType<CompDrawExportListEntry>() is
            CompDrawExportListEntry drawExportListEntry)
        {
            var props = drawExportListEntry.Props;
            if (props.takeOverRendering)
            {
                props.Worker.DrawExportListEntry(job, ref position, width);
                return;
            }
        }

        var tab = job.Tab;

        float labelWidth = width - Constants.LargeListEntryHeight
            - LastUpdateRectWidth;

        // create label string
        var subLabel = tab.GetSubLabel(job);
        var (label, labelSize) = tab.GetFullLabel(job, labelWidth, subLabel);

        // set up rects
        Rect labelRect = new(
            Constants.Margin,
            0f,
            labelWidth,
            labelSize.y);

        Rect statusRect = new(
            labelRect.xMax + Constants.Margin,
            Constants.Margin,
            LastUpdateRectWidth,
            labelRect.height);

        float maxRowHeight = Mathf.Max(labelRect.yMax, statusRect.yMax) + Constants.Margin;
        Rect rowRect = new(
            position.x,
            position.y,
            width,
            maxRowHeight);

        Rect lastUpdateRect = new(
            statusRect.xMin,
            statusRect.y,
            LastUpdateRectWidth,
            statusRect.height);

        // do the drawing
        GUI.BeginGroup(rowRect);
        rowRect = rowRect.AtZero();

        labelRect = labelRect.CenteredOnYIn(rowRect);

        if (IlyvionDebugViewSettings.DrawUIHelpers)
        {
            Widgets.DrawRectFast(labelRect, Color.blue.ToTransparent(.2f));
            Widgets.DrawRectFast(statusRect, Color.yellow.ToTransparent(.2f));
        }

        // draw label
        IlyvionWidgets.Label(labelRect, label, subLabel, TextAnchor.MiddleLeft);

        // draw update interval
        UpdateInterval.Draw(
            lastUpdateRect,
            job,
            true,
            false);

        GUI.EndGroup();
        position.y += rowRect.height;
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
        return GenFilePaths.FolderUnderSaveData("ManagerJobs");
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

public enum ScribingMode
{
    Transfer,
    Normal
}
