// Controller.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Reflection;

namespace ColonyManagerRedux;

public class ColonyManagerReduxMod : Mod
{
#pragma warning disable CS8618 // Set by constructor
    internal static ColonyManagerReduxMod Instance;
#pragma warning restore CS8618

    public static Settings Settings => Instance.GetSettings<Settings>();

    public ColonyManagerReduxMod(ModContentPack content) : base(content)
    {
        // This is kind of stupid, but also kind of correct. Correct wins.
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        Instance = this;

        // apply fixes
        var harmony = new Harmony(content.Name);
        harmony.PatchAll(Assembly.GetExecutingAssembly());

        GetSettings<Settings>();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Settings.DoSettingsWindowContents(inRect);
    }

    public override string SettingsCategory()
    {
        return Content.Name;
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class HotSwappableAttribute : Attribute
{
}
