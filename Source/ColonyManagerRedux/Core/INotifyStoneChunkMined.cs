// INotifyStoneChunkMined.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Interface for objects that need to be notified when a stone chunk is mined.
/// </summary>
public interface INotifyStoneChunkMined
{
    /// <summary>
    /// Notifies the implementer that a stone chunk has been mined by a pawn.
    /// </summary>
    /// <param name="pawn">The pawn that mined the stone chunk.</param>
    /// <param name="thing">The stone chunk that was mined.</param>
    void Notify_StoneChunkMined(Pawn pawn, Thing thing);
}
