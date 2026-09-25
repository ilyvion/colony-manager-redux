// Dialog_ImportJobsTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using ColonyManagerRedux.Managers;
using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class Dialog_ImportJobsTests
{
    private static readonly string[] TwoJobs = ["a", "b"];

    private const MultiCheckboxState On = MultiCheckboxState.On;
    private const MultiCheckboxState Off = MultiCheckboxState.Off;
    private const MultiCheckboxState Partial = MultiCheckboxState.Partial;

    [Test]
    public static void OnlyFullyCheckedJobsAreImportable()
    {
        var jobs = new[] { "a", "b", "c", "d" };

        var result = Dialog_ImportJobs.SelectImportable(jobs, [On, Off, On, Partial]);

        Expect.AreEqual("a,c", string.Join(",", result));
    }

    [Test]
    public static void NothingIsImportableWhenNothingIsChecked() =>
        Expect.IsEmpty(Dialog_ImportJobs.SelectImportable(TwoJobs, [Off, Off]));

    [Test]
    public static void AnySelectedIsTrueWhenOneJobIsChecked()
    {
        Expect.IsTrue(Dialog_ImportJobs.AnySelected([Off, On]));
        Expect.IsFalse(Dialog_ImportJobs.AnySelected([Off, Off]));
        Expect.IsFalse(Dialog_ImportJobs.AnySelected([]));
    }

    // The import button must only be enabled when accepting would import something, so a
    // partially checked job alone can't enable a button that then imports zero jobs.
    [Test]
    public static void AnySelectedAgreesWithWhatWouldBeImported()
    {
        MultiCheckboxState[][] cases =
        [
            [On, On],
            [Off, Off],
            [Partial],
            [Partial, Off],
            [Partial, On],
            [],
        ];

        foreach (var states in cases)
        {
            var importable = Dialog_ImportJobs
                .SelectImportable(states.Select((_, i) => i).ToArray(), states)
                .Any();

            Expect.AreEqual(importable, Dialog_ImportJobs.AnySelected(states));
        }
    }
}
