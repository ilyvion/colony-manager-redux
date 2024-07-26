﻿// TODO: Nullability uncertain; rewrite?
#nullable disable

// Trigger_PawnKind.cs
// Copyright Karel Kroeze, 2018-2020

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace ColonyManagerRedux
{
    public class Trigger_PawnKind : Trigger
    {
        private readonly Utilities.CachedValue<string> _cachedTooltip;
        private readonly Utilities.CachedValue<bool> _state = new Utilities.CachedValue<bool>(false);

        public Dictionary<AgeAndSex, int> CountTargets;
        public PawnKindDef pawnKind;

        public Trigger_PawnKind(Manager manager) : base(manager)
        {
            CountTargets = Utilities_Livestock.AgeSexArray.ToDictionary(k => k, v => 5);

            _cachedTooltip = new Utilities.CachedValue<string>("", 250, _getTooltip);
        }

        public int[] Counts
        {
            get
            {
                return Utilities_Livestock.AgeSexArray
                                          .Select(ageSex => pawnKind.GetTame(manager, ageSex).Count())
                                          .ToArray();
            }
        }

        public ManagerJob_Livestock Job
        {
            get
            {
                return manager.JobStack.FullStack<ManagerJob_Livestock>()
                              .FirstOrDefault(job => job.Trigger == this);
            }
        }

        public override bool State
        {
            get
            {
                bool state;
                if (!_state.TryGetValue(out state))
                {
                    state = Utilities_Livestock.AgeSexArray.All(
                                ageSex => CountTargets[ageSex] ==
                                          pawnKind.GetTame(manager, ageSex).Count())
                         && AllTrainingWantedSet();
                    _state.Update(state);
                }

                return state;
            }
        }

        public override string StatusTooltip => _cachedTooltip.Value;

        public override void DrawTriggerConfig(ref Vector2 cur, float width, float entryHeight,
                                                string label = null, string tooltip = null,
                                                List<Designation> targets = null, Action onOpenFilterDetails = null,
                                                Func<Designation, string> designationLabelGetter = null)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref CountTargets, "Targets", LookMode.Value, LookMode.Value);
            Scribe_Defs.Look(ref pawnKind, "PawnKind");
        }

        private string _getTooltip()
        {
            var tooltipArgs = new List<NamedArgument>();
            tooltipArgs.Add(pawnKind.LabelCap);
            tooltipArgs.AddRange(Counts.Select(x => x.ToString()).Cast<NamedArgument>());
            tooltipArgs.AddRange(CountTargets.Values.Select(v => v.ToString()).Cast<NamedArgument>());
            return "ColonyManagerRedux.Livestock.ListEntryTooltip".Translate(tooltipArgs.ToArray());
        }

        private bool AllTrainingWantedSet()
        {
            // do a dry run of the training assignment (no assignments are set).
            // this is rediculously expensive, and should never be called on tick.
            var actionTaken = false;
            Job.DoTrainingJobs(ref actionTaken, false);
            return actionTaken;
        }
    }
}
