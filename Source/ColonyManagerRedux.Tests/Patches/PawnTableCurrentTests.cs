// PawnTableCurrentTests.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

using DevTools.Testing;

namespace ColonyManagerRedux.Tests;

/// <summary>
/// Which <see cref="PawnTable"/> <see cref="PawnTableCurrentExtensions.IsCurrentTable"/> reports
/// as current while a table's <c>Columns</c> getter runs.
/// </summary>
[TestFixture(TestType.MainMenu)]
internal sealed class PawnTableCurrentTests
{
    private static PawnTable NewTable() => new(new PawnTableDef(), () => [], 100, 100);

    [Test]
    public static void OnlyTheTableBeingReadIsCurrent()
    {
        var outer = NewTable();
        var other = NewTable();

        RimWorld_PawnTable_Columns.Prefix(outer, out var state);
        try
        {
            Expect.IsTrue(outer.IsCurrentTable(), "table being read");
            Expect.IsFalse(other.IsCurrentTable(), "other table");
        }
        finally
        {
            RimWorld_PawnTable_Columns.Postfix(state);
        }
    }

    [Test]
    public static void NoTableIsCurrentOnceTheReadFinishes()
    {
        var table = NewTable();

        RimWorld_PawnTable_Columns.Prefix(table, out var state);
        RimWorld_PawnTable_Columns.Postfix(state);

        Expect.IsNull(RimWorld_PawnTable_Columns.CurrentPawnTable);
        Expect.IsFalse(table.IsCurrentTable());
    }

    // A column worker's VisibleCurrently can read another table's Columns while the outer
    // table's read is still filtering its columns; the outer table must stay current after.
    [Test]
    public static void NestedReadRestoresTheOuterTable()
    {
        var outer = NewTable();
        var inner = NewTable();

        RimWorld_PawnTable_Columns.Prefix(outer, out var outerState);
        try
        {
            RimWorld_PawnTable_Columns.Prefix(inner, out var innerState);
            Expect.IsTrue(inner.IsCurrentTable(), "inner during its read");
            RimWorld_PawnTable_Columns.Postfix(innerState);

            Expect.IsTrue(outer.IsCurrentTable(), "outer after the nested read");
            Expect.IsFalse(inner.IsCurrentTable(), "inner after its read");
        }
        finally
        {
            RimWorld_PawnTable_Columns.Postfix(outerState);
        }
        Expect.IsNull(RimWorld_PawnTable_Columns.CurrentPawnTable);
    }

#if v1_7_OR_GREATER
    private sealed class RecordingColumnWorker : PawnColumnWorker
    {
        public static readonly List<PawnTable?> Seen = [];

        public override bool VisibleCurrently
        {
            get
            {
                Seen.Add(RimWorld_PawnTable_Columns.CurrentPawnTable);
                return true;
            }
        }

        public override void DoCell(Rect rect, Pawn pawn, PawnTable table) { }

        public override int GetMinHeaderHeight(PawnTable table) => 0;
    }

    // On 1.7 the JIT can inline the small Columns getter into PawnTable's own methods, so the
    // patch is checked through one of those vanilla callers.
    [Test]
    public static void TableIsCurrentWhenVanillaCodeReadsItsColumns()
    {
        var column = new PawnColumnDef
        {
            defName = "CMR_Test_RecordingColumn",
            workerClass = typeof(RecordingColumnWorker),
        };
        var table = new PawnTable(new PawnTableDef { columns = [column] }, () => [], 100, 100);
        RecordingColumnWorker.Seen.Clear();

        _ = table.CalculateHeaderHeight();

        Expect.IsTrue(RecordingColumnWorker.Seen.SequenceEqual([table]), "current during the read");
        Expect.IsNull(RimWorld_PawnTable_Columns.CurrentPawnTable, "current after the read");
    }
#endif
}
