// MainTabWindow_Manager.cs
// Copyright Karel Kroeze, 2018-2020

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

/// <summary>
/// Main tab window for the Colony Manager, handling tab navigation and rendering.
/// </summary>
[HotSwappable]
public sealed class MainTabWindow_Manager : MainTabWindow
{
    private Manager? _manager;
    private Manager Manager
    {
        get
        {
            _manager ??= Manager.For(Find.CurrentMap);
            return _manager;
        }
    }
    private static ManagerTab? currentTab;

    /// <summary>
    /// Gets or sets the currently selected manager tab.
    /// </summary>
    public static ManagerTab CurrentTab
    {
        get
        {
            currentTab ??= DefaultTab;
            return currentTab;
        }
        set => currentTab = value;
    }

    private List<ManagerTab>? _managerTabsLeft;
    private List<ManagerTab> ManagerTabsLeft
    {
        get
        {
            _managerTabsLeft ??=
            [
                .. Manager.Tabs.Where(tab => tab.Def.iconArea == IconArea.Left && tab.Show),
            ];
            return _managerTabsLeft;
        }
    }

    private List<ManagerTab>? _managerTabsMiddle;
    private List<ManagerTab> ManagerTabsMiddle
    {
        get
        {
            _managerTabsMiddle ??=
            [
                .. Manager.Tabs.Where(tab => tab.Def.iconArea == IconArea.Middle && tab.Show),
            ];
            return _managerTabsMiddle;
        }
    }

    private List<ManagerTab>? _managerTabsRight;
    private List<ManagerTab> ManagerTabsRight
    {
        get
        {
            _managerTabsRight ??=
            [
                .. Manager.Tabs.Where(tab => tab.Def.iconArea == IconArea.Right && tab.Show),
            ];
            return _managerTabsRight;
        }
    }

    /// <summary>
    /// Gets the default manager tab for the current map.
    /// </summary>
    public static ManagerTab DefaultTab => Manager.For(Find.CurrentMap).Tabs[0];

    /// <summary>
    /// Navigates to the specified manager tab, optionally selecting a job.
    /// </summary>
    /// <param name="tab">The tab to navigate to.</param>
    /// <param name="job">The job to select, if any.</param>
    public static void GoTo(ManagerTab tab, ManagerJob? job = null)
    {
        if (tab == null)
        {
            throw new ArgumentNullException(nameof(tab));
        }
        // call pre/post open/close methods
        var old = CurrentTab;
        old.PreClose();
        tab.PreOpen();
        CurrentTab = tab;
        old.PostClose();
        tab.PostOpen();

        // if desired, set selected.
        if (job != null)
        {
            tab.Selected = job;
        }
    }

    /// <inheritdoc/>
    public override void DoWindowContents(Rect inRect)
    {
        // zooming in seems to cause Text.Font to start at Tiny, make sure it's set to Small for our panels.
        Text.Font = GameFont.Small;

        // three areas of icons for tabs, left middle and right.
        var leftIcons = new Rect(
            0f,
            0f,
            (ManagerTabsLeft.Count * LargeIconSize)
                + (Mathf.Max(0, ManagerTabsLeft.Count - 1) * Margin),
            LargeIconSize
        );
        var rightIcons = new Rect(
            0f,
            0f,
            (ManagerTabsRight.Count * LargeIconSize)
                + (Mathf.Max(0, ManagerTabsRight.Count - 1) * Margin),
            LargeIconSize
        );

        var widthRemaining = inRect.width - leftIcons.width - rightIcons.width - (2 * Margin);

        var middleIcons = new Rect(
            0f,
            0f,
            Margin + (ManagerTabsMiddle.Count * (LargeIconSize + Margin)),
            LargeIconSize
        );

        var middleMargin = Margin;
        if (middleIcons.width > widthRemaining)
        {
            middleMargin -= (middleIcons.width - widthRemaining) / (ManagerTabsMiddle.Count + 1);
            middleIcons.width -= middleIcons.width - widthRemaining;
            middleIcons.width = Mathf.Max(
                middleIcons.width,
                ManagerTabsMiddle.Count * LargeIconSize
            );
        }

        var outerMargin = Margin;
        if (middleMargin < 0)
        {
            outerMargin -= -middleMargin;
            middleMargin = 0;
        }

        // finetune rects
        var middleCanvas = new Rect(inRect);
        middleCanvas.xMin += leftIcons.width;
        middleCanvas.xMax -= rightIcons.width;
        middleIcons = middleIcons.CenteredOnXIn(middleCanvas);
        rightIcons.x += inRect.width - rightIcons.width;

        IlyvionDebugViewSettings.DrawIfUIHelpers(() =>
        {
            Widgets.DrawRectFast(leftIcons, Color.red.ToTransparent(.5f));
            Widgets.DrawRectFast(middleIcons, Color.green.ToTransparent(.5f));
            Widgets.DrawRectFast(rightIcons, Color.blue.ToTransparent(.5f));
            Widgets.DrawLineHorizontal(middleCanvas.x, LargeIconSize + 8f, middleCanvas.width);
        });

        // left icons (overview and logs from our end)
        GUI.BeginGroup(leftIcons);
        var cur = new Vector2(0f, 0f);
        foreach (var tab in ManagerTabsLeft)
        {
            var iconRect = new Rect(cur.x, cur.y, LargeIconSize, LargeIconSize);
            DrawTabIcon(iconRect, tab);
            cur.x += LargeIconSize + outerMargin;
        }

        GUI.EndGroup();

        // right icons (import/export from our end)
        GUI.BeginGroup(rightIcons);
        cur = new Vector2(0f, 0f);
        foreach (var tab in ManagerTabsRight)
        {
            var iconRect = new Rect(cur.x, cur.y, LargeIconSize, LargeIconSize);
            DrawTabIcon(iconRect, tab);
            cur.x += LargeIconSize + outerMargin;
        }

        GUI.EndGroup();

        // middle icons (the bulk of icons)
        GUI.BeginGroup(middleIcons);
        cur = new Vector2(middleMargin, 0f);
        foreach (var tab in ManagerTabsMiddle)
        {
            var iconRect = new Rect(cur.x, cur.y, LargeIconSize, LargeIconSize);
            DrawTabIcon(iconRect, tab);
            cur.x += LargeIconSize + middleMargin;
        }

        GUI.EndGroup();

        // delegate actual content to the specific manager tab.
        var contentCanvas = new Rect(
            0f,
            LargeIconSize + Margin,
            inRect.width,
            inRect.height - LargeIconSize - Margin
        );
        GUI.BeginGroup(contentCanvas);
        CurrentTab.RenderTab(contentCanvas.AtZero());
        GUI.EndGroup();

        // for some stupid reason, we sometimes get left a bad anchor
        Text.Anchor = TextAnchor.UpperLeft;
    }

    /// <summary>
    /// Draws the icon for a manager tab, handling selection and tooltips.
    /// </summary>
    /// <param name="rect">The rectangle in which to draw.</param>
    /// <param name="tab">The manager tab to draw.</param>
    public static void DrawTabIcon(Rect rect, ManagerTab tab)
    {
        if (tab == null)
        {
            throw new ArgumentNullException(nameof(tab));
        }

        if (tab.Enabled)
        {
            if (tab == CurrentTab)
            {
                GUI.color = GenUI.MouseoverColor;
                _ = Widgets.ButtonImage(rect, tab.Def.icon, GenUI.MouseoverColor);
                GUI.color = Color.white;
            }
            else if (Widgets.ButtonImage(rect, tab.Def.icon))
            {
                GoTo(tab);
            }

            TooltipHandler.TipRegion(rect, tab.Label);
        }
        else
        {
            GUI.color = Color.grey;
            GUI.DrawTexture(rect, tab.Def.icon);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(
                rect,
                tab.Label
                    + " "
                    + "ColonyManagerRedux.Common.TabDisabledBecause".Translate(tab.DisabledReason)
            );
        }
    }

    /// <inheritdoc/>
    public override void PostClose()
    {
        base.PostClose();
        CurrentTab.PostClose();
    }

    /// <inheritdoc/>
    public override void PostOpen()
    {
        base.PostOpen();
        CurrentTab.PostOpen();
    }

    /// <inheritdoc/>
    public override void PreClose()
    {
        base.PreClose();
        CurrentTab.PreClose();
    }

    /// <inheritdoc/>
    public override void PreOpen()
    {
        base.PreOpen();

        // Reset these caches so we're not holding on to outdated values
        _manager = null;
        _managerTabsLeft = null;
        _managerTabsMiddle = null;
        _managerTabsRight = null;

        // make sure the currently open tab is for this map
        if (CurrentTab.Manager.map != Find.CurrentMap)
        {
            CurrentTab = DefaultTab;
        }

        CurrentTab.PreOpen();
    }
}
