// ScribeModMismatchUtility.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Reimplements the mod-mismatch peek performed by <see cref="PreLoadUtility.CheckVersionAndLoad"/>
/// so callers can show their own, context-appropriate warning (<see cref="Dialog_ScribeModMismatch"/>)
/// instead of vanilla's <c>Dialog_ModMismatch</c> (the save-game "Added mods / Missing mods /
/// Shared mods" dialog), which is confusing when the file being loaded is a manager job save or
/// template rather than an actual save game.
/// </summary>
public static class ScribeModMismatchUtility
{
    /// <summary>
    /// Peeks the meta header of <paramref name="filePath"/> and determines whether the mod list
    /// it was written with differs from the currently active mod list. Does not affect any
    /// in-progress Scribe save/load session; safe to call before a real load. On a mismatch,
    /// <paramref name="loadedModIds"/>/<paramref name="loadedModNames"/> are populated with the
    /// file's recorded mod list; otherwise they're empty.
    /// </summary>
    public static bool TryDetectModMismatch(
        string filePath,
        out List<string> loadedModIds,
        out List<string> loadedModNames
    )
    {
        try
        {
            Scribe.loader.InitLoadingMetaHeaderOnly(filePath);
            ScribeMetaHeaderUtility.LoadGameDataHeader(
                ScribeMetaHeaderUtility.ScribeHeaderMode.None,
                logVersionConflictWarning: false
            );
            Scribe.loader.FinalizeLoading();
        }
        catch (Exception ex)
        {
            ColonyManagerReduxMod.Instance.LogWarning(
                $"Exception peeking meta header of '{filePath}': {ex}"
            );
            Scribe.ForceStop();
            loadedModIds = [];
            loadedModNames = [];
            return false;
        }

        if (ScribeMetaHeaderUtility.LoadedModsMatchesActiveMods(out _, out _))
        {
            loadedModIds = [];
            loadedModNames = [];
            return false;
        }

        loadedModIds = [.. ScribeMetaHeaderUtility.loadedModIdsList];
        loadedModNames = [.. ScribeMetaHeaderUtility.loadedModNamesList];
        return true;
    }

    /// <summary>
    /// Loads <paramref name="filePath"/> by invoking <paramref name="loadAct"/>, but if the
    /// file's mod list doesn't match the currently active mods, first shows a
    /// <see cref="Dialog_ScribeModMismatch"/> (with <paramref name="headerText"/> as the
    /// context-specific explanation) instead of loading immediately.
    /// </summary>
    public static void LoadWithModMismatchConfirmation(
        string filePath,
        string headerText,
        Action loadAct
    )
    {
        if (headerText == null)
        {
            throw new ArgumentNullException(nameof(headerText));
        }
        if (loadAct == null)
        {
            throw new ArgumentNullException(nameof(loadAct));
        }

        if (TryDetectModMismatch(filePath, out var loadedModIds, out var loadedModNames))
        {
            Find.WindowStack.Add(
                new Dialog_ScribeModMismatch(headerText, loadedModIds, loadedModNames, loadAct)
            );
        }
        else
        {
            loadAct();
        }
    }
}
