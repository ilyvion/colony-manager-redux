// Dialog_ScribeModMismatch.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using Verse.Sound;

namespace ColonyManagerRedux;

/// <summary>
/// A reimplementation of vanilla's <c>Dialog_ModMismatch</c> for manager job saves/templates:
/// same three-column added/missing/shared mod list layout, but with wording that makes clear
/// this is about a Colony Manager Redux file rather than a save game, and without the
/// save-game-specific "Go back" / "Save mod list" / "Change loaded mods" actions, which don't
/// apply here.
/// </summary>
internal sealed class Dialog_ScribeModMismatch(
    string headerText,
    List<string> loadedModIds,
    List<string> loadedModNames,
    Action loadAction
) : Window
{
    private const float ButtonWidth = 200f;
    private const float ButtonHeight = 30f;
    private const float ModRowHeight = 24f;

    private List<string> _addedModsList = [];
    private List<string> _missingModsList = [];
    private List<string> _sharedModsList = [];

    private Vector2 _addedModListScrollPosition = Vector2.zero;
    private Vector2 _missingModListScrollPosition = Vector2.zero;
    private Vector2 _sharedModListScrollPosition = Vector2.zero;

    public override Vector2 InitialSize => new(900f, 750f);

    public override void PreOpen()
    {
        base.PreOpen();

        var runningIds = LoadedModManager.RunningMods.Select(mod => mod.PackageId).ToList();
        var runningNameById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var id in runningIds)
        {
            runningNameById[id] = ModLister.GetModWithIdentifier(id).Name;
        }

        var loadedNameById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < loadedModIds.Count; i++)
        {
            loadedNameById[loadedModIds[i]] = loadedModNames[i];
        }

        var loadedIdSet = new HashSet<string>(loadedModIds, StringComparer.OrdinalIgnoreCase);
        var runningIdSet = new HashSet<string>(runningIds, StringComparer.OrdinalIgnoreCase);

        _addedModsList =
        [
            .. runningIds.Where(id => !loadedIdSet.Contains(id)).Select(id => runningNameById[id]),
        ];
        _missingModsList =
        [
            .. loadedModIds
                .Where(id => !runningIdSet.Contains(id))
                .Select(id => loadedNameById[id]),
        ];
        _sharedModsList =
        [
            .. runningIds.Where(loadedIdSet.Contains).Select(id => runningNameById[id]),
        ];
    }

    public override void DoWindowContents(Rect inRect)
    {
        var columnWidth = (inRect.width - 20f) / 3f;
        var y = 0f;

        Text.Font = GameFont.Medium;
        Widgets.Label(
            new Rect(0f, y, inRect.width, Text.LineHeight),
            "ColonyManagerRedux.Templates.ModMismatchTitle".Translate()
        );
        y += Text.LineHeight + 10f;
        Text.Font = GameFont.Small;

        var headerHeight = Text.CalcHeight(headerText, inRect.width);
        Widgets.Label(new Rect(0f, y, inRect.width, headerHeight), headerText);
        y += headerHeight + 17f;

        if (_addedModsList.Count == 0 && _missingModsList.Count == 0)
        {
            var orderChangedHeight = Text.CalcHeight(
                "ModsMismatchOrderChanged".Translate(),
                inRect.width
            );
            Widgets.Label(
                new Rect(0f, y, inRect.width, orderChangedHeight),
                "ModsMismatchOrderChanged".Translate()
            );
        }
        else
        {
            var listHeight = inRect.height - y - ButtonHeight - 10f - Text.LineHeight - 10f;

            Widgets.Label(
                new Rect(0f, y, columnWidth, Text.LineHeight),
                "AddedModsList".Translate()
            );
            Widgets.Label(
                new Rect(columnWidth + 10f, y, columnWidth, Text.LineHeight),
                "MissingModsList".Translate()
            );
            Widgets.Label(
                new Rect((columnWidth + 10f) * 2f, y, columnWidth, Text.LineHeight),
                "SharedModsList".Translate()
            );
            y += Text.LineHeight + 10f;

            DoModList(
                new Rect(0f, y, columnWidth, listHeight),
                _addedModsList,
                ref _addedModListScrollPosition,
                new Color(0.27f, 0.4f, 0.1f)
            );
            DoModList(
                new Rect(columnWidth + 10f, y, columnWidth, listHeight),
                _missingModsList,
                ref _missingModListScrollPosition,
                new Color(0.38f, 0.07f, 0.09f)
            );
            DoModList(
                new Rect((columnWidth + 10f) * 2f, y, columnWidth, listHeight),
                _sharedModsList,
                ref _sharedModListScrollPosition,
                null
            );
        }

        var buttonY = inRect.height - ButtonHeight;
        var cancelRect = new Rect(
            (inRect.width / 2f) - ButtonWidth - 4f,
            buttonY,
            ButtonWidth,
            ButtonHeight
        );
        var loadAnywayRect = new Rect((inRect.width / 2f) + 4f, buttonY, ButtonWidth, ButtonHeight);

        if (Widgets.ButtonText(cancelRect, "Cancel".Translate()))
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
            Close();
        }
        if (Widgets.ButtonText(loadAnywayRect, "LoadAnyway".Translate()))
        {
            SoundDefOf.Click.PlayOneShotOnCamera();
            loadAction();
            Close();
        }
    }

    private static void DoModList(
        Rect rect,
        List<string> modList,
        ref Vector2 scrollPosition,
        Color? rowColor
    )
    {
        var viewRect = new Rect(0f, 0f, rect.width - 16f, modList.Count * ModRowHeight);
        Widgets.BeginScrollView(rect, ref scrollPosition, viewRect);
        for (var i = 0; i < modList.Count; i++)
        {
            var rowRect = new Rect(0f, i * ModRowHeight, rect.width, ModRowHeight);
            if (rowColor.HasValue)
            {
                Widgets.DrawBoxSolid(rowRect, rowColor.Value);
            }
            DoModRow(rowRect, modList[i], i);
        }
        Widgets.EndScrollView();
    }

    private static void DoModRow(Rect rect, string modName, int index)
    {
        if (index % 2 == 0)
        {
            Widgets.DrawLightHighlight(rect);
        }
        rect.xMin += 4f;
        rect.xMax -= 4f;
        Widgets.Label(rect, modName);
    }
}
