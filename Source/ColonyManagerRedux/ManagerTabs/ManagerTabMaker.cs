// ManagerTabDef.cs
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public static class ManagerTabMaker
{
    public static ManagerTab MakeManagerTab(ManagerTabDef def, Manager manager)
    {
        ManagerTab obj = (ManagerTab)Activator.CreateInstance(def.managerTabClass, manager);
        obj.def = def;
        obj.PostMake();
        return obj;
    }
}
