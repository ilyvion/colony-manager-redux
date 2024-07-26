﻿// Controller.cs
// Copyright Karel Kroeze, 2020-2020

using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace ColonyManagerRedux;

public class Controller : Mod
{
    private Settings mySettings;

    public Controller(ModContentPack content) : base(content)
    {
        // apply fixes
        var harmony = new Harmony("ColonyManagerRedux");
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        mySettings = GetSettings<Settings>();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Settings.DoSettingsWindowContents(inRect);
    }

    public override string SettingsCategory()
    {
        return "ColonyManagerRedux.ManagerHelpTitle".Translate();
    }
}
