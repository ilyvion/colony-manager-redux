// PathFinder.cs
// Copyright (c) 2025 Alexander Krivács Schrøder

using Verse.AI;

namespace ColonyManagerRedux;

public static class PathFinderExtensions
{
    public static PawnPath FindPathCmr(
        this PathFinder pathFinder,
        IntVec3 source,
        LocalTargetInfo target,
        TraverseParms traverseParams,
        PathEndMode peMode = PathEndMode.Touch)
    {
#if v1_5
        return pathFinder.FindPath(source, target,
            TraverseParms.For(TraverseMode.PassDoors, Danger.Some),
                PathEndMode.Touch);
#else
        return pathFinder.FindPathNow(source, target,
            TraverseParms.For(TraverseMode.PassDoors, Danger.Some),
                peMode: PathEndMode.Touch);
#endif
    }
}
