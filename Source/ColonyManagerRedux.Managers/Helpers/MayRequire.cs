namespace ColonyManagerRedux;

internal sealed class MayRequireSurvivalistsAdditionsAttribute : MayRequireAttribute
{
    public MayRequireSurvivalistsAdditionsAttribute() : base(Constants.SurvivalistsAdditionsModId)
    {
    }
}
