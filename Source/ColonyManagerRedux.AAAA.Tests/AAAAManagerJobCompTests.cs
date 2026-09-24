// AAAAManagerJobCompTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.AAAA.Core;
using DevTools.Testing;

namespace ColonyManagerRedux.AAAA.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class AAAAManagerJobCompTests
{
    /// <summary>
    /// An <see cref="ICollection{T}"/> that throws part way through <see cref="Add"/> once it has
    /// accepted <paramref name="failAfter"/> items, to simulate the kind of mid-swap failure
    /// <see cref="AAAAManagerJobCompField.TrySwapCollectionContents{T}"/> needs to recover from.
    /// </summary>
    private sealed class FlakyCollection<T>(List<T> items, int failAfter) : ICollection<T>
    {
        public int Count => items.Count;
        public bool IsReadOnly => false;

        public void Add(T item)
        {
            if (items.Count >= failAfter)
            {
                throw new InvalidOperationException("Simulated failure");
            }
            items.Add(item);
        }

        public void Clear() => items.Clear();

        public bool Contains(T item) => items.Contains(item);

        public void CopyTo(T[] array, int arrayIndex) => items.CopyTo(array, arrayIndex);

        public bool Remove(T item) => items.Remove(item);

        public IEnumerator<T> GetEnumerator() => items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Test]
    public static void SuccessfulSwapReplacesContentsAndReportsNoFailures()
    {
        var backing = new List<string> { "old1", "old2" };
        var collection = backing;
        var swapFailed = false;
        var restoreFailed = false;

        AAAAManagerJobCompField.TrySwapCollectionContents(
            collection,
            ["new1", "new2", "new3"],
            _ => swapFailed = true,
            _ => restoreFailed = true
        );

        Expect.IsFalse(swapFailed);
        Expect.IsFalse(restoreFailed);
        Expect.AreEqual(3, collection.Count);
        Expect.IsTrue(collection.Contains("new1"));
        Expect.IsTrue(collection.Contains("new3"));
    }

    [Test]
    public static void FailedSwapRestoresOriginalContents()
    {
        // Regression guard: a swap that fails part way through must not leave the collection
        // in a partially cleared/filled, corrupted state.
        var backing = new List<string> { "old1", "old2" };
        var collection = new FlakyCollection<string>(backing, failAfter: 2);
        var swapFailed = false;
        var restoreFailed = false;

        AAAAManagerJobCompField.TrySwapCollectionContents(
            collection,
            ["new1", "new2", "new3"],
            _ => swapFailed = true,
            _ => restoreFailed = true
        );

        Expect.IsTrue(swapFailed);
        Expect.IsFalse(restoreFailed);
        Expect.AreEqual(2, backing.Count);
        Expect.IsTrue(backing.Contains("old1"));
        Expect.IsTrue(backing.Contains("old2"));
    }

    [Test]
    public static void FailedSwapAndFailedRestoreBothReportFailure()
    {
        var backing = new List<string> { "old1", "old2" };
        // failAfter: 0 means even re-adding the original contents during restore will fail.
        var collection = new FlakyCollection<string>(backing, failAfter: 0);
        var swapFailed = false;
        var restoreFailed = false;

        AAAAManagerJobCompField.TrySwapCollectionContents(
            collection,
            ["new1"],
            _ => swapFailed = true,
            _ => restoreFailed = true
        );

        Expect.IsTrue(swapFailed);
        Expect.IsTrue(restoreFailed);
    }

    [Test]
    public static void TryGetAreaToRestoreReturnsFirstAreaWhenPreviousAreasIsNonEmpty()
    {
        var previousAreas = new List<string?> { "old1", "old2" };

        var found = AAAAManagerJobCompField.TryGetAreaToRestore(previousAreas, out var area);

        Expect.IsTrue(found);
        Expect.AreEqual("old1", area);
    }

    [Test]
    public static void TryGetAreaToRestoreFailsWhenPreviousAreasIsEmpty()
    {
        // Regression guard: normal mode firing without a matching prior danger-mode
        // call (e.g. a comp added mid-danger-mode) must not throw an IndexOutOfRangeException
        // from indexing an empty previous-areas list.
        var previousAreas = new List<string?>();

        var found = AAAAManagerJobCompField.TryGetAreaToRestore(previousAreas, out var area);

        Expect.IsFalse(found);
        Expect.IsTrue(area is null);
    }

    private const string Suffix = " \\(Safe\\)";

    [Test]
    public static void MatchesSafeAreaLabelMatchesPlainLabelWithSuffix() =>
        Expect.IsTrue(AAAAManagerJobCompField.MatchesSafeAreaLabel("Home (Safe)", "Home", Suffix));

    [Test]
    public static void MatchesSafeAreaLabelIsCaseInsensitive() =>
        Expect.IsTrue(AAAAManagerJobCompField.MatchesSafeAreaLabel("HOME (SAFE)", "home", Suffix));

    [Test]
    public static void MatchesSafeAreaLabelEscapesRegexMetacharactersInAreaLabel()
    {
        // Regression guard for commit 3466824: an area label containing regex metacharacters
        // must be escaped, not interpreted as a pattern, or this either throws or silently
        // matches the wrong areas.
        Expect.IsTrue(
            AAAAManagerJobCompField.MatchesSafeAreaLabel("a.b*c (Safe)", "a.b*c", Suffix)
        );
        Expect.IsFalse(
            AAAAManagerJobCompField.MatchesSafeAreaLabel("aXbXXXc (Safe)", "a.b*c", Suffix)
        );
    }

    [Test]
    public static void MatchesSafeAreaLabelHandlesParenthesesInAreaLabel() =>
        // e.g. an area literally named "Home (1)".
        Expect.IsTrue(
            AAAAManagerJobCompField.MatchesSafeAreaLabel("Home (1) (Safe)", "Home (1)", Suffix)
        );

    [Test]
    public static void MatchesSafeAreaLabelRejectsLabelWithoutSuffix() =>
        Expect.IsFalse(AAAAManagerJobCompField.MatchesSafeAreaLabel("Home", "Home", Suffix));

    [Test]
    public static void BuildSafeAreaPatternEscapesAreaLabel() =>
        Expect.AreEqual(
            "a\\.b\\*c \\(Safe\\)",
            AAAAManagerJobCompField.BuildSafeAreaPattern("a.b*c", Suffix)
        );
}
