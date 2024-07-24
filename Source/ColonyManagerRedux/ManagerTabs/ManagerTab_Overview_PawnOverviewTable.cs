// ManagerTab_Overview_PawnOverviewTable.cs
// Copyright Karel Kroeze, 2020-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

partial class ManagerTab_Overview
{
    public abstract class PawnColumnWorker_Overview : PawnColumnWorker
    {
#pragma warning disable CS8618
        public ManagerTab_Overview instance;
#pragma warning restore CS8618
    }

    [HotSwappable]
    public class PawnColumnWorker_Label : RimWorld.PawnColumnWorker_Label
    {
#pragma warning disable CS8618
        public ManagerTab_Overview instance;
#pragma warning restore CS8618

        public static void DrawHeader(Rect rect, PawnColumnWorker_Label @this)
        {
            Widgets.Label(rect, @this.instance.WorkTypeDef.gerundLabel.CapitalizeFirst().Truncate(rect.width));
        }


        public override void DoHeader(Rect rect, PawnTable table)
        {
            RimWorld_PawnColumnWorker_DoHeader.CustomLabelDoHeader(this, rect, table,
                (a, _, _, d) => DrawHeader(a, (PawnColumnWorker_Label)d));
        }

        protected override string GetHeaderTip(PawnTable table)
        {
            var headerTip = def.headerTip;
            def.headerTip = "";
            var newHeaderTip = instance.WorkTypeDef.gerundLabel.CapitalizeFirst()
                + "\n\n"
                + base.GetHeaderTip(table);
            def.headerTip = headerTip;
            return newHeaderTip;
        }
    }

    [HotSwappable]
    public class PawnColumnWorker_CurrentActivity : PawnColumnWorker_Overview
    {
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            string activityString = GetPawnActivityString(pawn);
            Widgets_Labels.Label(rect, activityString, activityString,
                TextAnchor.MiddleCenter, margin: Constants.Margin, font: GameFont.Tiny);
        }

        private static string GetPawnActivityString(Pawn pawn)
        {
            return pawn.jobs.curDriver?.GetReport() ?? "ColonyManagerRedux.ManagerNoCurJob".Translate();
        }

        public override int GetOptimalWidth(PawnTable table)
        {
            var maxActivityWidth = (int)table.PawnsListForReading
                .Select(pawn => Text.CalcSize(GetPawnActivityString(pawn)).x)
                .Max();
            return maxActivityWidth;
        }

        public override int GetMinWidth(PawnTable table)
        {
            return (int)Text.CalcSize(def.LabelCap).x;
        }

        public override int GetMaxWidth(PawnTable table)
        {
            return (int)table.Size.x;
        }
    }

    [HotSwappable]
    public class PawnColumnWorker_WorkPriorities : PawnColumnWorker_Overview
    {
        public override void DoCell(Rect rect, Pawn pawn, PawnTable table)
        {
            var workBoxRect = new Rect(0f, 0f, 24f, 24f).CenteredIn(rect).RoundToInt();

            bool incapable = Utilities.IsIncapableOfWholeWorkType(pawn, instance.WorkTypeDef);
            var priority = pawn.workSettings.GetPriority(instance.WorkTypeDef);
            WidgetsWork.DrawWorkBoxFor(workBoxRect.xMin, workBoxRect.yMin, pawn, instance.WorkTypeDef, incapable);
            var priorityAfter = pawn.workSettings.GetPriority(instance.WorkTypeDef);
            if (priority != priorityAfter)
            {
                instance.pawnOverviewTable!.SetDirty();
            }
            if (Mouse.IsOver(workBoxRect))
            {
                TooltipHandler.TipRegion(workBoxRect,
                    () => WidgetsWork.TipForPawnWorker(
                        pawn,
                        instance.WorkTypeDef,
                        incapable),
                        pawn.thingIDNumber ^ instance.WorkTypeDef.GetHashCode());
            }
        }

        public override void DoHeader(Rect rect, PawnTable table)
        {
            base.DoHeader(rect, table);

            Text.Font = DefaultHeaderFont;
            GUI.color = DefaultHeaderColor;
            Text.Anchor = DefaultHeaderAlignment;
            Rect rect2 = rect;
            rect2.xMin += GetHeaderOffsetX(rect);
            Widgets.Label(rect2, GetLabel().Truncate(rect.width));
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
        }

        protected override string GetHeaderTip(PawnTable table)
        {
            return GetLabel()
                + "\n\n"
                + base.GetHeaderTip(table);
        }

        private static TaggedString GetLabel()
        {
            return Find.PlaySettings.useWorkPriorities
                ? "ColonyManagerRedux.Overview.WorkPriority".Translate()
                : "ColonyManagerRedux.Overview.WorkEnabled".Translate();
        }

        public override int GetMinWidth(PawnTable table)
        {
            return Math.Min(24, (int)Text.CalcSize(GetHeaderTip(table)).x);
        }

        public override int GetMaxWidth(PawnTable table)
        {
            return Math.Max(24, (int)Text.CalcSize(GetHeaderTip(table)).x);
        }

        public override int GetOptimalWidth(PawnTable table)
        {
            return GetMaxWidth(table);
        }

        public override int Compare(Pawn a, Pawn b)
        {
            var aValues = PawnComparisonValue(a);
            var bValues = PawnComparisonValue(b);

            if (aValues.priority != bValues.priority)
            {
                return aValues.priority - bValues.priority;
            }
            else
            {
                return (int)(bValues.skill - aValues.skill);
            }

            (int priority, float skill) PawnComparisonValue(Pawn pawn)
            {
                if (Utilities.IsIncapableOfWholeWorkType(pawn, instance.WorkTypeDef))
                {
                    return (short.MaxValue + 1, -1);
                }

                var priority = pawn.workSettings.GetPriority(instance.WorkTypeDef);
                if (priority == 0)
                {
                    priority = short.MaxValue;
                }
                float skill = pawn.skills.AverageOfRelevantSkillsFor(instance.WorkTypeDef);
                return (priority, skill);
            }
        }
    }

    private PawnTable? pawnOverviewTable;
    private PawnTable CreatePawnOverviewTable()
    {
        return (PawnTable)Activator.CreateInstance(
            ManagerPawnTableDefOf.CM_ManagerJobWorkTable.workerClass,
            ManagerPawnTableDefOf.CM_ManagerJobWorkTable,
            (Func<IEnumerable<Pawn>>)(() =>
            {
                // PawnTables aren't very customizable, so we'll hijack this function to inject our
                // instance into the columns, since we need it there
                foreach (var item in Traverse.Create(pawnOverviewTable!).Field<PawnTableDef>("def").Value.columns
                    .Where(c => c.workerClass.IsSubclassOf(
                        typeof(PawnColumnWorker_Overview))))
                {
                    ((PawnColumnWorker_Overview)item.Worker).instance = this;
                }
                foreach (var item in pawnOverviewTable!.Columns
                    .Where(c => c.workerClass == typeof(PawnColumnWorker_Label)))
                {
                    ((PawnColumnWorker_Label)item.Worker).instance = this;
                }

                return Workers;
            }),
            UI.screenWidth - (int)(Constants.Margin * 2f),
            (int)(UI.screenHeight - 35 - Constants.Margin * 2f));
    }
}
