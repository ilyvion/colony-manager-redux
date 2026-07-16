// Resources.cs
// Copyright Karel Kroeze, 2020-2020

namespace ColonyManagerRedux.Managers;

[StaticConstructorOnStartup]
#pragma warning disable CA1724
internal static class Resources
#pragma warning restore CA1724
{
    public static readonly Color Orange = new(1f, 144 / 255f, 0f),
        SlightlyDarkBackgroundColour = new(0f, 0f, 0f, .2f);

    public static readonly Texture2D
        // sorting arrows
        ArrowUp = ContentFinder<Texture2D>.Get("UI/Buttons/CMR_ArrowUp"),
        ArrowDown = ContentFinder<Texture2D>.Get("UI/Buttons/CMR_ArrowDown"),
        // NOTE: These colors should be synchronized with the ones in HistoryChapters.xml
        AdultFemaleTexture = SolidColorMaterials.NewSolidColorTexture(new Color(0.7f, 0f, 0f)),
        AdultMaleTexture = SolidColorMaterials.NewSolidColorTexture(new Color(0f, 0.7f, 0f)),
        JuvenileFemaleTexture = SolidColorMaterials.NewSolidColorTexture(new Color(0f, 0.7f, 0.7f)),
        JuvenileMaleTexture = SolidColorMaterials.NewSolidColorTexture(new Color(0.7f, 0.7f, 0f)),
        // misc
        SlightlyDarkBackground = SolidColorMaterials.NewSolidColorTexture(
            SlightlyDarkBackgroundColour
        ),
        Error = SolidColorMaterials.NewSolidColorTexture(ColorLibrary.LogError),
        Refresh = ContentFinder<Texture2D>.Get("UI/Icons/CMR_refresh"),
        Warning = ContentFinder<Texture2D>.Get("UI/Icons/CMR_warning"),
        ClawIcon = ContentFinder<Texture2D>.Get("UI/Icons/CMR_claw"),
        // livestock header icons
        StageC = ContentFinder<Texture2D>.Get("UI/Icons/CMR_stage-3"),
        StageB = ContentFinder<Texture2D>.Get("UI/Icons/CMR_stage-2"),
        StageA = ContentFinder<Texture2D>.Get("UI/Icons/CMR_stage-1"),
        UnkownIcon = ContentFinder<Texture2D>.Get("UI/Icons/CMR_unknown"),
        TrainableNoneIcon = ContentFinder<Texture2D>.Get("UI/Icons/CMR_none"),
        TrainableIntermediateIcon = ContentFinder<Texture2D>.Get("UI/Icons/CMR_intermediate"),
        TrainableAdvancedIcon = ContentFinder<Texture2D>.Get("UI/Icons/CMR_advanced"),
        Tame = ContentFinder<Texture2D>.Get("UI/Icons/Animal/Tame"),
        Slaughter = ContentFinder<Texture2D>.Get("UI/Icons/Animal/Slaughter"),
        ReleaseToTheWild = ContentFinder<Texture2D>.Get("UI/Designators/ReleaseToTheWild"),
        Sterile = ContentFinder<Texture2D>.Get("UI/Icons/Animal/Sterile"),
        Venerated = ContentFinder<Texture2D>.Get("UI/Icons/CMR_venerated"),
        PadlockClosed = ContentFinder<Texture2D>.Get("UI/Icons/CMR_padlock_closed"),
        PadlockOpen = ContentFinder<Texture2D>.Get("UI/Icons/CMR_padlock_open"),
        Nuzzle = ContentFinder<Texture2D>.Get("UI/Icons/CMR_heart"),
        ManagedByColonyManagerIcon = ContentFinder<Texture2D>.Get("UI/Buttons/CMR_MainIcon");

    public static Texture2D GetLifeStageIcon(int lifeStageIndex) =>
        lifeStageIndex switch
        {
            0 => StageA,
            1 => StageB,
            _ => StageC, // animals with > 3 lifestages just get the adult icon.
        };
}
