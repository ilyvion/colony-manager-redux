// Resources.cs
// Copyright Karel Kroeze, 2020-2020

using UnityEngine;
using Verse;

namespace ColonyManagerRedux;

[StaticConstructorOnStartup]
public static class Resources
{
    public static Texture2D
        // sorting arrows
        ArrowTop,
        ArrowUp,
        ArrowDown,
        ArrowBottom,

        // stamps
        StampCompleted,
        StampSuspended,
        StampStart,

        // misc
        SlightlyDarkBackground,
        Cog,
        BarBackgroundActiveTexture,
        BarBackgroundInactiveTexture,
        Search,
        BarShader,
        Refresh,
        Stopwatch,
        Claw,

        // livestock header icons
        WoolIcon,
        MilkIcon,
        StageC,
        StageB,
        StageA,
        FemaleIcon,
        MaleIcon,
        MeatIcon,
        UnkownIcon;

    public static readonly Color Orange, SlightlyDarkBackgroundColour;

    //public static Texture2D[] LifeStages = {StageA, StageB, StageC};

    static Resources()
    {
        Orange = new Color(1f, 144 / 255f, 0f);
        SlightlyDarkBackgroundColour = new Color(0f, 0f, 0f, .2f);

        ArrowTop = ContentFinder<Texture2D>.Get("UI/Buttons/CMR_ArrowTop");
        ArrowUp = ContentFinder<Texture2D>.Get("UI/Buttons/CMR_ArrowUp");
        ArrowDown = ContentFinder<Texture2D>.Get("UI/Buttons/CMR_ArrowDown");
        ArrowBottom = ContentFinder<Texture2D>.Get("UI/Buttons/CMR_ArrowBottom");

        // stamps
        StampCompleted = ContentFinder<Texture2D>.Get("UI/Stamps/CMR_Completed");
        StampSuspended = ContentFinder<Texture2D>.Get("UI/Stamps/CMR_Suspended");
        StampStart = ContentFinder<Texture2D>.Get("UI/Stamps/CMR_Start");

        // misc
        SlightlyDarkBackground = SolidColorMaterials.NewSolidColorTexture(SlightlyDarkBackgroundColour);
        Cog = ContentFinder<Texture2D>.Get("UI/Buttons/CMR_Cog");
        BarBackgroundActiveTexture = SolidColorMaterials.NewSolidColorTexture(new Color(0.2f, 0.8f, 0.85f));
        BarBackgroundInactiveTexture = SolidColorMaterials.NewSolidColorTexture(new Color(0.7f, 0.7f, 0.7f));
        Search = ContentFinder<Texture2D>.Get("UI/Buttons/CMR_Search");
        BarShader = ContentFinder<Texture2D>.Get("UI/Misc/CMR_BarShader");
        Refresh = ContentFinder<Texture2D>.Get("UI/Icons/CMR_refresh");
        Stopwatch = ContentFinder<Texture2D>.Get("UI/Icons/CMR_stopwatch");
        Claw = ContentFinder<Texture2D>.Get("UI/Icons/CMR_claw");

        // livestock header icons
        WoolIcon = ContentFinder<Texture2D>.Get("UI/Icons/CMR_wool");
        MilkIcon = ContentFinder<Texture2D>.Get("UI/Icons/CMR_milk");
        StageC = ContentFinder<Texture2D>.Get("UI/Icons/CMR_stage-3");
        StageB = ContentFinder<Texture2D>.Get("UI/Icons/CMR_stage-2");
        StageA = ContentFinder<Texture2D>.Get("UI/Icons/CMR_stage-1");
        FemaleIcon = ContentFinder<Texture2D>.Get("UI/Icons/CMR_female");
        MaleIcon = ContentFinder<Texture2D>.Get("UI/Icons/CMR_male");
        MeatIcon = ContentFinder<Texture2D>.Get("UI/Icons/CMR_meat");
        UnkownIcon = ContentFinder<Texture2D>.Get("UI/Icons/CMR_unknown");
    }

    public static Texture2D LifeStages(int lifeStageIndex)
    {
        return lifeStageIndex switch
        {
            0 => StageA,
            1 => StageB,
            _ => StageC,// animals with > 3 lifestages just get the adult icon.
        };
    }
}
