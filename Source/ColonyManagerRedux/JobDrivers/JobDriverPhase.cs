// JobDriverPhase.cs
// Copyright (c) 2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Tracks which phase <see cref="JobDriver_ManagingAtManagingStation"/>'s toil is currently in,
/// together with only the state that's actually valid for that phase — used instead of a set of
/// independently-nullable fields (a handle, a pending-work box, a start tick) whose validity was
/// only correlated by convention, which forced null-forgiving reads at every phase transition.
/// </summary>
[TaggedUnions.TaggedUnion]
internal abstract partial class JobDriverPhase
{
    private JobDriverPhase() { }

    [TaggedUnions.NoImplicitOperator]
    public sealed class NotStarted : JobDriverPhase
    {
        public NotStarted() { }
    }

    [TaggedUnions.NoImplicitOperator]
    public sealed partial class Gathering(
        CoroutineHandle Handle,
        AnyBoxed<JobTracker.PendingJobWork?> PendingWork,
        int StartTick
    ) : JobDriverPhase;

    [TaggedUnions.NoImplicitOperator]
    public sealed class NoWorkFound : JobDriverPhase
    {
        public NoWorkFound() { }
    }

    [TaggedUnions.NoImplicitOperator]
    public sealed partial class Executing(
        CoroutineHandle Handle,
        JobTracker.PendingJobWork PendingWork,
        int StartTick
    ) : JobDriverPhase;
}
