// Dialog_MiningDebugOptions.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

[HotSwappable]
internal sealed class DebugComponent(Manager manager)
{
    private (IntVec3 source, IntVec3 target) debugPath;
    private int debugPathFrameCounter = -1;

    public void SetPath(IntVec3 source, IntVec3 target)
    {
        debugPath = (source, target);
        debugPathFrameCounter = 0;
    }

    public void Update()
    {
        if (debugPathFrameCounter >= 0)
        {
            debugPathFrameCounter++;

            var path = manager.map.pathFinder.FindPath(debugPath.source, debugPath.target,
                TraverseParms.For(TraverseMode.PassDoors, Danger.Some));
            path.DrawPath(null);
            path.ReleaseToPool();
        }
        if (debugPathFrameCounter > 300)
        {
            debugPathFrameCounter = -1;
        }
    }
}
