// ManagerJobTemplatesTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using RimTestRedux;

namespace ColonyManagerRedux.Tests;

[TestSuite]
internal static class ManagerJobTemplatesTests
{
    [Test]
    public static void AppendsExtensionToPlainName() =>
        Assert
            .That(ManagerJobTemplates.FilePath("/save/ManagerJobTemplates", "MyTemplate"))
            .Is.EqualTo("/save/ManagerJobTemplates/MyTemplate.cmt");

    [Test]
    public static void HandlesNameWithSpaces() =>
        Assert
            .That(ManagerJobTemplates.FilePath("/save/ManagerJobTemplates", "My Cool Template"))
            .Is.EqualTo("/save/ManagerJobTemplates/My Cool Template.cmt");

    [Test]
    public static void HandlesNameWithUnicodeCharacters() =>
        Assert
            .That(ManagerJobTemplates.FilePath("/save/ManagerJobTemplates", "Basér 城"))
            .Is.EqualTo("/save/ManagerJobTemplates/Basér 城.cmt");

    // Pins down current behavior: a name that already ends in ".cmt" gets the extension appended
    // a second time rather than being treated as already-complete. Not necessarily desired, but
    // worth guarding explicitly so a future change to this is a deliberate decision, not an
    // accidental regression.
    [Test]
    public static void DoublesUpExtensionWhenNameAlreadyEndsWithIt() =>
        Assert
            .That(ManagerJobTemplates.FilePath("/save/ManagerJobTemplates", "Already.cmt"))
            .Is.EqualTo("/save/ManagerJobTemplates/Already.cmt.cmt");
}
