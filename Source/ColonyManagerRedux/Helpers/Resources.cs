// Resources.cs
// Copyright Karel Kroeze, 2020-2020

namespace ColonyManagerRedux;

[StaticConstructorOnStartup]
#pragma warning disable CA1724
internal static class Resources
#pragma warning restore CA1724
{
    public static readonly Color
        Orange = new(1f, 144 / 255f, 0f),
        SlightlyDarkBackgroundColour = new(0f, 0f, 0f, .2f);

    public static readonly Texture2D
        // sorting arrows
        ArrowTop = ContentFinder<Texture2D>.Get("UI/Buttons/CMR_ArrowTop"),
        ArrowUp = ContentFinder<Texture2D>.Get("UI/Buttons/CMR_ArrowUp"),
        ArrowDown = ContentFinder<Texture2D>.Get("UI/Buttons/CMR_ArrowDown"),
        ArrowBottom = ContentFinder<Texture2D>.Get("UI/Buttons/CMR_ArrowBottom"),

        // stamps
        StampCompleted = ContentFinder<Texture2D>.Get("UI/Stamps/CMR_Completed"),
        StampSuspended = ContentFinder<Texture2D>.Get("UI/Stamps/CMR_Suspended"),
        StampStart = ContentFinder<Texture2D>.Get("UI/Stamps/CMR_Start"),
        StampException = ContentFinder<Texture2D>.Get("UI/Stamps/CMR_Exception"),

        // progress bar textures
        BarBackgroundActiveTexture = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.8f, 0.85f)),
        BarBackgroundInactiveTexture = SolidColorMaterials.NewSolidColorTexture(new Color(0.7f, 0.7f, 0.7f)),

        // misc
        SlightlyDarkBackground = SolidColorMaterials.NewSolidColorTexture(SlightlyDarkBackgroundColour),
        Error = SolidColorMaterials.NewSolidColorTexture(ColorLibrary.LogError),
        Cog = ContentFinder<Texture2D>.Get("UI/Buttons/CMR_Cog"),
        Search = ContentFinder<Texture2D>.Get("UI/Buttons/CMR_Search"),
        BarShader = ContentFinder<Texture2D>.Get("UI/Misc/CMR_BarShader"),
        Stopwatch = ContentFinder<Texture2D>.Get("UI/Icons/CMR_stopwatch"),
        Warning = ContentFinder<Texture2D>.Get("UI/Icons/CMR_warning"),

        // livestock header icons
        StageC = ContentFinder<Texture2D>.Get("UI/Icons/CMR_stage-3"),
        StageB = ContentFinder<Texture2D>.Get("UI/Icons/CMR_stage-2"),
        StageA = ContentFinder<Texture2D>.Get("UI/Icons/CMR_stage-1"),
        ManagerTab_Gizmo = ContentFinder<Texture2D>.Get("UI/Commands/CMR_ManagerTab_Gizmo");

    public static Texture2D GetLifeStageIcon(int lifeStageIndex) => lifeStageIndex switch
    {
        0 => StageA,
        1 => StageB,
        _ => StageC,// animals with > 3 lifestages just get the adult icon.
    };
}
