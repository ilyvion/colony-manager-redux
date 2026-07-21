// MayRequire.cs
// Copyright (c) 2025 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

internal sealed class MayRequireSurvivalistsAdditionsAttribute : MayRequireAttribute
{
    public MayRequireSurvivalistsAdditionsAttribute()
        : base(Constants.SurvivalistsAdditionsModId) { }
}
