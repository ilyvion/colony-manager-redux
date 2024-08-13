// Controller.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using System.Reflection;

namespace ColonyManagerRedux;

public class ColonyManagerReduxMod : IlyvionMod
{
#pragma warning disable CS8618 // Set by constructor
    private static ColonyManagerReduxMod _instance;
    public static ColonyManagerReduxMod Instance
    {
        get => _instance;
        private set => _instance = value;
    }
#pragma warning restore CS8618

    protected override bool HasSettings => true;

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
        //Harmony.DEBUG = true;
        harmony.PatchAll(Assembly.GetExecutingAssembly());
        //Harmony.DEBUG = false;

        GetSettings<Settings>();
    }

    public override void DoSettingsWindowContents(Rect inRect)
    {
        Settings.DoSettingsWindowContents(inRect);
    }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class HotSwappableAttribute : Attribute
{
}
