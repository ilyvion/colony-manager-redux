// INotifyStoneChunkMined.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public interface INotifyStoneChunkMined
{
    void Notify_StoneChunkMined(Pawn pawn, Thing thing);
}
