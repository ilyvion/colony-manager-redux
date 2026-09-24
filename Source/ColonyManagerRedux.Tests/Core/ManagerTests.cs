// ManagerTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ManagerTests
{
    [Test]
    public static void FindLowestUnusedIdReturnsZeroWhenNoneUsed() =>
        Expect.AreEqual(0, Manager.FindLowestUnusedId([]));

    [Test]
    public static void FindLowestUnusedIdReturnsFirstGapAfterContiguousRun() =>
        Expect.AreEqual(4, Manager.FindLowestUnusedId([0, 1, 2, 3]));

    [Test]
    public static void FindLowestUnusedIdReturnsGapInMiddle() =>
        Expect.AreEqual(2, Manager.FindLowestUnusedId([0, 1, 3]));

    [Test]
    public static void FindLowestUnusedIdReturnsZeroWhenZeroNotUsed() =>
        Expect.AreEqual(0, Manager.FindLowestUnusedId([1, 2, 3]));

    [Test]
    public static void FindLowestUnusedIdHandlesDuplicateAndUnorderedInput() =>
        Expect.AreEqual(4, Manager.FindLowestUnusedId([3, 1, 1, 0, 3, 2]));

    // Regression guard for the CHANGELOG [Unreleased] fix: an ID being requested before a
    // manager had finished loading, or the internal ID counter wrapping back to 0 after
    // int.MaxValue job creations, could previously hand out a duplicate ID.
    [Test]
    public static void FindLowestUnusedIdNeverReturnsAnAlreadyUsedId()
    {
        var used = new[] { 0, 1, 2, 4, 5 };
        var result = Manager.FindLowestUnusedId(used);
        Expect.IsFalse(used.Contains(result));
        Expect.AreEqual(3, result);
    }

    [Test]
    public static void ShouldApplyDefaultTemplateFalseWhenAlreadyChecked() =>
        Expect.IsFalse(
            Manager.ShouldApplyDefaultTemplate(
                alreadyChecked: true,
                autoApplyEnabled: true,
                templateName: "Foo",
                templateExists: _ => true
            )
        );

    [Test]
    public static void ShouldApplyDefaultTemplateFalseWhenAutoApplyDisabled() =>
        Expect.IsFalse(
            Manager.ShouldApplyDefaultTemplate(
                alreadyChecked: false,
                autoApplyEnabled: false,
                templateName: "Foo",
                templateExists: _ => true
            )
        );

    [Test]
    public static void ShouldApplyDefaultTemplateFalseWhenTemplateNameNullOrEmpty()
    {
        Expect.IsFalse(
            Manager.ShouldApplyDefaultTemplate(
                alreadyChecked: false,
                autoApplyEnabled: true,
                templateName: null,
                templateExists: _ => true
            )
        );

        Expect.IsFalse(
            Manager.ShouldApplyDefaultTemplate(
                alreadyChecked: false,
                autoApplyEnabled: true,
                templateName: "",
                templateExists: _ => true
            )
        );
    }

    [Test]
    public static void ShouldApplyDefaultTemplateFalseWhenTemplateDoesNotExist() =>
        Expect.IsFalse(
            Manager.ShouldApplyDefaultTemplate(
                alreadyChecked: false,
                autoApplyEnabled: true,
                templateName: "Foo",
                templateExists: _ => false
            )
        );

    [Test]
    public static void ShouldApplyDefaultTemplateTrueWhenAllConditionsSatisfied() =>
        Expect.IsTrue(
            Manager.ShouldApplyDefaultTemplate(
                alreadyChecked: false,
                autoApplyEnabled: true,
                templateName: "Foo",
                templateExists: _ => true
            )
        );

    [Test]
    public static void ShouldSkipDefaultTemplateForModMismatchTrueOnlyWhenBothTrue()
    {
        Expect.IsTrue(
            Manager.ShouldSkipDefaultTemplateForModMismatch(
                wouldApply: true,
                modMismatchDetected: true
            )
        );

        Expect.IsFalse(
            Manager.ShouldSkipDefaultTemplateForModMismatch(
                wouldApply: true,
                modMismatchDetected: false
            )
        );

        Expect.IsFalse(
            Manager.ShouldSkipDefaultTemplateForModMismatch(
                wouldApply: false,
                modMismatchDetected: true
            )
        );

        Expect.IsFalse(
            Manager.ShouldSkipDefaultTemplateForModMismatch(
                wouldApply: false,
                modMismatchDetected: false
            )
        );
    }
}
