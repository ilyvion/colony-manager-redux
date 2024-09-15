namespace ColonyManagerRedux;

public sealed class MayRequireSurvivalistsAdditionsAttribute : MayRequireAttribute
{
    public MayRequireSurvivalistsAdditionsAttribute() : base(Constants.SurvivalistsAdditionsModId)
    {
    }
}
