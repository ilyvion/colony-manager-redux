﻿// ManagerTab.cs
// Copyright Karel Kroeze, 2018-2020

using UnityEngine;

namespace ColonyManagerRedux
{
    public abstract class ManagerTab
    {
        public enum IconAreas
        {
            Left = 0,
            Middle = 1,
            Right = 2
        }

        public float DefaultLeftRowSize = 300f;

        public Manager manager;

        public ManagerTab(Manager manager)
        {
            this.manager = manager;
        }

        public virtual string DisabledReason => "";

        public virtual bool Enabled => true;

        public virtual Texture2D Icon => Resources.IconHammer;

        public virtual IconAreas IconArea => IconAreas.Middle;

        public virtual string Label => GetType().ToString();

        public abstract ManagerJob? Selected { get; set; }

        public abstract void DoWindowContents(Rect canvas);

        public virtual void PostClose()
        {
        }

        public virtual void PostOpen()
        {
        }

        public virtual void PreClose()
        {
        }

        public virtual void PreOpen()
        {
        }

        public virtual void Tick()
        {
        }
    }
}
