// ManagerTabDef.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ColonyManagerRedux;

public class ManagerTabDef : Def
{
    public int order;
    public Type managerTabClass = typeof(ManagerTab);

    public IconArea iconArea = IconArea.Middle;
    [Unsaved(false)]
    public Texture2D icon = BaseContent.BadTex;
    [NoTranslate]
    public string iconPath = "UI/Icons/CMR_Hammer";

    public override void PostLoad()
    {
        if (!iconPath.NullOrEmpty())
        {
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                icon = ContentFinder<Texture2D>.Get(iconPath);
            });
        }
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string item in base.ConfigErrors())
        {
            yield return item;
        }
        if (managerTabClass == null)
        {
            yield return "managerTabClass is null";
        }
        if (!typeof(ManagerTab).IsAssignableFrom(managerTabClass))
        {
            yield return "managerTabClass is not ManagerTab or a subclass thereof";
        }
    }
}

public enum IconArea
{
    Left = 0,
    Middle = 1,
    Right = 2
}
