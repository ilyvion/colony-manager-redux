// CompProperties_ManagerStation.cs
// Copyright Karel Kroeze, 2017-2020

namespace ColonyManagerRedux;

public class CompProperties_ManagerStation : CompProperties
{
    public int speed = 250;

    public CompProperties_ManagerStation()
    {
        compClass = typeof(CompManagerStation);
    }
}
