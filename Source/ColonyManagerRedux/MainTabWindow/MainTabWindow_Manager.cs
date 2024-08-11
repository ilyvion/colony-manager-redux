// MainTabWindow_Manager.cs
// Copyright Karel Kroeze, 2018-2020

using ilyvion.Laboratory;
using Verse.Noise;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Microsoft.Performance",
    "CA1812:AvoidUninstantiatedInternalClasses",
    Justification = "Class is instantiated via reflection")]
[HotSwappable]
internal sealed class MainTabWindow_Manager : MainTabWindow
{
    private static ManagerTab? currentTab;

    public static ManagerTab CurrentTab
    {
        get
        {
            currentTab ??= DefaultTab;
            return currentTab;
        }
        set => currentTab = value;
    }

    public static ManagerTab DefaultTab => Manager.For(Find.CurrentMap).Tabs[0];

    public static void GoTo(ManagerTab tab, ManagerJob? job = null)
    {
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

    public override void DoWindowContents(Rect canvas)
    {
        Manager manager = Manager.For(Find.CurrentMap);

        // zooming in seems to cause Text.Font to start at Tiny, make sure it's set to Small for our panels.
        Text.Font = GameFont.Small;

        //var margin = Margin;

        // three areas of icons for tabs, left middle and right.
        var leftIcons = new Rect(0f, 0f,
            manager.ManagerTabsLeft.Count * LargeIconSize
            + Mathf.Max(0, manager.ManagerTabsLeft.Count - 1) * Margin,
            LargeIconSize);
        var rightIcons = new Rect(0f, 0f,
            manager.ManagerTabsRight.Count * LargeIconSize
            + Mathf.Max(0, manager.ManagerTabsRight.Count - 1) * Margin,
            LargeIconSize);

        var widthRemaining = canvas.width - leftIcons.width - rightIcons.width - 2 * Margin;

        var middleIcons = new Rect(0f, 0f,
            Margin + manager.ManagerTabsMiddle.Count * (LargeIconSize + Margin),
            LargeIconSize);

        var middleMargin = Margin;
        if (middleIcons.width > widthRemaining)
        {
            middleMargin -= (middleIcons.width - widthRemaining) / (manager.ManagerTabsMiddle.Count + 1);
            middleIcons.width -= middleIcons.width - widthRemaining;
            middleIcons.width = Mathf.Max(middleIcons.width, manager.ManagerTabsMiddle.Count * LargeIconSize);
        }

        var outerMargin = Margin;
        if (middleMargin < 0)
        {
            outerMargin -= -middleMargin;
            middleMargin = 0;
        }

        // finetune rects
        var middleCanvas = new Rect(canvas);
        middleCanvas.xMin += leftIcons.width;
        middleCanvas.xMax -= rightIcons.width;
        middleIcons = middleIcons.CenteredOnXIn(middleCanvas);
        rightIcons.x += canvas.width - rightIcons.width;

        if (IlyvionDebugViewSettings.DrawUIHelpers)
        {
            Widgets.DrawRectFast(leftIcons, Color.red.ToTransparent(.5f));
            Widgets.DrawRectFast(middleIcons, Color.green.ToTransparent(.5f));
            Widgets.DrawRectFast(rightIcons, Color.blue.ToTransparent(.5f));
            Widgets.DrawLineHorizontal(middleCanvas.x, LargeIconSize + 8f, middleCanvas.width);
        }

        // left icons (overview and logs from our end)
        GUI.BeginGroup(leftIcons);
        var cur = new Vector2(0f, 0f);
        foreach (var tab in manager.ManagerTabsLeft)
        {
            var iconRect = new Rect(cur.x, cur.y, LargeIconSize, LargeIconSize);
            DrawTabIcon(iconRect, tab);
            cur.x += LargeIconSize + outerMargin;
        }

        GUI.EndGroup();

        // right icons (import/export from our end)
        GUI.BeginGroup(rightIcons);
        cur = new Vector2(0f, 0f);
        foreach (var tab in manager.ManagerTabsRight)
        {
            var iconRect = new Rect(cur.x, cur.y, LargeIconSize, LargeIconSize);
            DrawTabIcon(iconRect, tab);
            cur.x += LargeIconSize + outerMargin;
        }

        GUI.EndGroup();

        // middle icons (the bulk of icons)
        GUI.BeginGroup(middleIcons);
        cur = new Vector2(middleMargin, 0f);
        foreach (var tab in manager.ManagerTabsMiddle)
        {
            var iconRect = new Rect(cur.x, cur.y, LargeIconSize, LargeIconSize);
            DrawTabIcon(iconRect, tab);
            cur.x += LargeIconSize + middleMargin;
        }

        GUI.EndGroup();

        // delegate actual content to the specific manager tab.
        var contentCanvas = new Rect(0f, LargeIconSize + Margin, canvas.width,
                                      canvas.height - LargeIconSize - Margin);
        GUI.BeginGroup(contentCanvas);
        CurrentTab.RenderTab(contentCanvas.AtZero());
        GUI.EndGroup();

        // for some stupid reason, we sometimes get left a bad anchor
        Text.Anchor = TextAnchor.UpperLeft;
    }

    public static void DrawTabIcon(Rect rect, ManagerTab tab)
    {
        if (tab.Enabled)
        {
            if (tab == CurrentTab)
            {
                GUI.color = GenUI.MouseoverColor;
                Widgets.ButtonImage(rect, tab.def.icon, GenUI.MouseoverColor);
                GUI.color = Color.white;
            }
            else if (Widgets.ButtonImage(rect, tab.def.icon))
            {
                GoTo(tab);
            }

            TooltipHandler.TipRegion(rect, tab.Label);
        }
        else
        {
            GUI.color = Color.grey;
            GUI.DrawTexture(rect, tab.def.icon);
            GUI.color = Color.white;
            TooltipHandler.TipRegion(rect, tab.Label +
                "ColonyManagerRedux.Common.TabDisabledBecause".Translate(tab.DisabledReason));
        }
    }

    public override void PostClose()
    {
        base.PostClose();
        CurrentTab.PostClose();
    }

    public override void PostOpen()
    {
        base.PostOpen();
        CurrentTab.PostOpen();
    }

    public override void PreClose()
    {
        base.PreClose();
        CurrentTab.PreClose();
    }

    public override void PreOpen()
    {
        base.PreOpen();

        // make sure the currently open tab is for this map
        if (CurrentTab.manager.map != Find.CurrentMap)
        {
            CurrentTab = DefaultTab;
        }

        CurrentTab.PreOpen();
    }
}
