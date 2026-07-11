// UtilitiesLivestockTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class UtilitiesLivestockTests
{
    // Pins down the AgeAndSex <-> IsAdult/IsMale mapping. A code comment on the enum warns its
    // values are save-critical ("do not change them without the proper care"); this guards
    // against an accidental reorder silently inverting age/sex classification.
    [Test]
    public static void AgeAndSexIsAdultAndIsMaleMappingIsPinned()
    {
        Assert.That(AgeAndSex.AdultFemale.IsAdult()).Is.True();
        Assert.That(AgeAndSex.AdultFemale.IsMale()).Is.False();

        Assert.That(AgeAndSex.AdultMale.IsAdult()).Is.True();
        Assert.That(AgeAndSex.AdultMale.IsMale()).Is.True();

        Assert.That(AgeAndSex.JuvenileFemale.IsAdult()).Is.False();
        Assert.That(AgeAndSex.JuvenileFemale.IsMale()).Is.False();

        Assert.That(AgeAndSex.JuvenileMale.IsAdult()).Is.False();
        Assert.That(AgeAndSex.JuvenileMale.IsMale()).Is.True();
    }

    [Test]
    public static void IsOfAgeSexBoundaryAtLifeStageIndexTwo()
    {
        // lifeStageIndex >= 2 is adult, per the deliberate simplification documented at the call
        // site.
        Assert
            .That(Utilities_Livestock.IsOfAgeSex(Gender.Male, 2, AgeAndSex.AdultMale))
            .Is.True();
        Assert.That(Utilities_Livestock.IsOfAgeSex(Gender.Male, 1, AgeAndSex.AdultMale)).Is.False();
        Assert
            .That(Utilities_Livestock.IsOfAgeSex(Gender.Male, 1, AgeAndSex.JuvenileMale))
            .Is.True();
        Assert
            .That(Utilities_Livestock.IsOfAgeSex(Gender.Female, 2, AgeAndSex.AdultFemale))
            .Is.True();
        Assert
            .That(Utilities_Livestock.IsOfAgeSex(Gender.Female, 1, AgeAndSex.JuvenileFemale))
            .Is.True();
    }

    [Test]
    public static void IsOfAgeSexTreatsNonMaleGendersAsFemale()
    {
        // Deliberate simplification documented at the call site: anything non-male counts as
        // female, including Gender.None.
        Assert
            .That(Utilities_Livestock.IsOfAgeSex(Gender.None, 2, AgeAndSex.AdultFemale))
            .Is.True();
        Assert.That(Utilities_Livestock.IsOfAgeSex(Gender.None, 2, AgeAndSex.AdultMale)).Is.False();
    }
}
