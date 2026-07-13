// JobPhaseOutcome.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Distinguishes "a <see cref="ManagerJob"/> gave us a coroutine to run" from "it hasn't
/// implemented this method" — used instead of a bare nullable <see cref="Coroutine"/> so
/// call sites are forced to handle both cases explicitly instead of risking a null-forgiving
/// cast or an unchecked null flowing into <c>MultiTickCoroutineManager.StartCoroutine</c>.
/// </summary>
[TaggedUnions.TaggedUnion]
internal abstract partial class JobPhaseOutcome
{
    private JobPhaseOutcome() { }

    [TaggedUnions.NoImplicitOperator]
    public sealed partial class Ready(Coroutine Value) : JobPhaseOutcome;

    public sealed class NotImplemented : JobPhaseOutcome
    {
        public NotImplemented() { }
    }

    internal static JobPhaseOutcome For(Coroutine? coroutine) =>
        coroutine != null ? new Ready(coroutine) : new NotImplemented();
}
