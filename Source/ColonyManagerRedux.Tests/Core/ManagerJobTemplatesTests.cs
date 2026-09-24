// ManagerJobTemplatesTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ManagerJobTemplatesTests
{
    [Test]
    public static void AppendsExtensionToPlainName() =>
        Expect.AreEqual(
            "/save/ManagerJobTemplates/MyTemplate.cmt",
            ManagerJobTemplates.FilePath("/save/ManagerJobTemplates", "MyTemplate")
        );

    [Test]
    public static void HandlesNameWithSpaces() =>
        Expect.AreEqual(
            "/save/ManagerJobTemplates/My Cool Template.cmt",
            ManagerJobTemplates.FilePath("/save/ManagerJobTemplates", "My Cool Template")
        );

    [Test]
    public static void HandlesNameWithUnicodeCharacters() =>
        Expect.AreEqual(
            "/save/ManagerJobTemplates/Basér 城.cmt",
            ManagerJobTemplates.FilePath("/save/ManagerJobTemplates", "Basér 城")
        );

    // Pins down current behavior: a name that already ends in ".cmt" gets the extension appended
    // a second time rather than being treated as already-complete. Not necessarily desired, but
    // worth guarding explicitly so a future change to this is a deliberate decision, not an
    // accidental regression.
    [Test]
    public static void DoublesUpExtensionWhenNameAlreadyEndsWithIt() =>
        Expect.AreEqual(
            "/save/ManagerJobTemplates/Already.cmt.cmt",
            ManagerJobTemplates.FilePath("/save/ManagerJobTemplates", "Already.cmt")
        );

    // An empty name would otherwise silently collapse onto the base path (Path.Combine treats
    // an empty second segment as a no-op), producing a file that isn't actually named after
    // anything. Reject it instead of allowing a nameless template to be saved/loaded.
    [Test]
    public static void RejectsEmptyName() =>
        Expect.Throws<ArgumentException>(() =>
            ManagerJobTemplates.FilePath("/save/ManagerJobTemplates", "")
        );
}
