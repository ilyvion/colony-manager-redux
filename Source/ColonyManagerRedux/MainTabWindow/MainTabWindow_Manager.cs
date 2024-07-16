// MainTabWindow_Manager.cs
// Copyright Karel Kroeze, 2018-2020

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

internal class MainTabWindow_Manager : MainTabWindow
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

    public static ManagerTab DefaultTab => Manager.For(Find.CurrentMap).tabs[0];

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
        // zooming in seems to cause Text.Font to start at Tiny, make sure it's set to Small for our panels.
        Text.Font = GameFont.Small;

        // three areas of icons for tabs, left middle and right.
        var leftIcons = new Rect(0f, 0f,
                                  Margin +
                                  Manager.For(Find.CurrentMap).ManagerTabsLeft.Count * (LargeIconSize + Margin),
                                  LargeIconSize);
        var middleIcons = new Rect(0f, 0f,
                                    Margin +
                                    Manager.For(Find.CurrentMap).ManagerTabsMiddle.Count *
                                    (LargeIconSize + Margin),
                                    LargeIconSize);
        var rightIcons = new Rect(0f, 0f,
                                   Margin +
                                   Manager.For(Find.CurrentMap).ManagerTabsRight.Count *
                                   (LargeIconSize + Margin),
                                   LargeIconSize);

        // finetune rects
        middleIcons = middleIcons.CenteredOnXIn(canvas);
        rightIcons.x += canvas.width - rightIcons.width;

        // left icons (probably only overview, but hey...)
        GUI.BeginGroup(leftIcons);
        var cur = new Vector2(Margin, 0f);
        foreach (var tab in Manager.For(Find.CurrentMap).ManagerTabsLeft)
        {
            var iconRect = new Rect(cur.x, cur.y, LargeIconSize, LargeIconSize);
            DrawTabIcon(iconRect, tab);
            cur.x += LargeIconSize + Margin;
        }

        GUI.EndGroup();

        // middle icons (the bulk of icons)
        GUI.BeginGroup(middleIcons);
        cur = new Vector2(Margin, 0f);
        foreach (var tab in Manager.For(Find.CurrentMap).ManagerTabsMiddle)
        {
            var iconRect = new Rect(cur.x, cur.y, LargeIconSize, LargeIconSize);
            DrawTabIcon(iconRect, tab);
            cur.x += LargeIconSize + Margin;
        }

        GUI.EndGroup();

        // right icons (probably only import/export, possbile settings?)
        GUI.BeginGroup(rightIcons);
        cur = new Vector2(Margin, 0f);
        foreach (var tab in Manager.For(Find.CurrentMap).ManagerTabsRight)
        {
            var iconRect = new Rect(cur.x, cur.y, LargeIconSize, LargeIconSize);
            DrawTabIcon(iconRect, tab);
            cur.x += LargeIconSize + Margin;
        }

        GUI.EndGroup();

        // delegate actual content to the specific manager.
        var contentCanvas = new Rect(0f, LargeIconSize + Margin, canvas.width,
                                      canvas.height - LargeIconSize - Margin);
        GUI.BeginGroup(contentCanvas);
        CurrentTab.DoWindowContents(contentCanvas);
        GUI.EndGroup();

        // for some stupid reason, we sometimes get left a bad anchor
        Text.Anchor = TextAnchor.UpperLeft;
    }

    public void DrawTabIcon(Rect rect, ManagerTab tab)
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
            TooltipHandler.TipRegion(rect, tab.Label + "ColonyManagerRedux.ManagerTabDisabledBecause".Translate(tab.DisabledReason));
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

        // TODO: reimplement help dialog
        //if ( !Manager.For( Find.CurrentMap ).HelpShown )
        //{
        //    Find.WindowStack.Add( new Dialog_Message( "ColonyManagerRedux.ManagerHelpMessage".Translate(), "ColonyManagerRedux.ManagerHelpTitle".Translate() ) );
        //    Manager.For( Find.CurrentMap ).HelpShown = true;
        //}

        // make sure the currently open tab is for this map
        if (CurrentTab.manager.map != Find.CurrentMap)
        {
            CurrentTab = DefaultTab;
        }

        CurrentTab.PreOpen();
    }
}
