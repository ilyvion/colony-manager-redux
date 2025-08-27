// PathFinder.cs
// Copyright (c) 2025 Alexander Krivács Schrøder

using Verse.AI;

namespace ColonyManagerRedux;

/// <summary>
/// Provides extension methods for the PathFinder class.
/// </summary>
public static class PathFinderExtensions
{
    /// <summary>
    /// Finds a path from the source to the target using the specified traversal parameters and path end mode.
    /// </summary>
    /// <param name="pathFinder">The PathFinder instance.</param>
    /// <param name="source">The starting position.</param>
    /// <param name="target">The target location.</param>
    /// <param name="traverseParams">Traversal parameters.</param>
    /// <param name="peMode">The path end mode (default is Touch).</param>
    /// <returns>A PawnPath representing the calculated path.</returns>
    public static PawnPath FindPathCmr(
        this PathFinder pathFinder,
        IntVec3 source,
        LocalTargetInfo target,
        TraverseParms traverseParams,
        PathEndMode peMode = PathEndMode.Touch)
    {
        if (pathFinder == null)
        {
            throw new ArgumentNullException(nameof(pathFinder));
        }
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
