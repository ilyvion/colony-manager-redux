// Trigger_Threshold.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[HotSwappable]
public class Trigger_Threshold : Trigger
{
    public enum Ops
    {
        LowerThan,
        Equals,
        HigherThan
    }

    public bool CountAllOnMap;

    public int MaxUpperThreshold;

    public Ops Op;

    public ThingFilter ParentFilter;

    public Zone_Stockpile? stockpile;

    public int TargetCount;

    private ThingFilter thresholdFilter;
    public ThingFilter ThresholdFilter { get => thresholdFilter; }
    private CachedValue<int> _cachedCurrentCount = new(0);

    private string? _stockpile_scribe;

    public Action? SettingsChanged;

    public Trigger_Threshold(ManagerJob job) : base(job)
    {
        if (job == null)
        {
            throw new ArgumentNullException(nameof(job));
        }

        Settings settings = ColonyManagerReduxMod.Settings;
        CountAllOnMap = settings.DefaultCountAllOnMap;

        ParentFilter = new ThingFilter();
        ParentFilter.SetDisallowAll();

        thresholdFilter = new ThingFilter(ThresholdFilter_SettingsChanged);
        ThresholdFilter.SetDisallowAll();

        Op = Ops.LowerThan;
        MaxUpperThreshold = job.MaxUpperThreshold;
        TargetCount = settings.DefaultTargetCount;
    }

    private void ThresholdFilter_SettingsChanged()
    {
        _cachedCurrentCount.Invalidate();
        SettingsChanged?.Invoke();
    }

    private int CurrentCountRaw =>
        job.Manager.map.CountProducts(ThresholdFilter, stockpile, CountAllOnMap);

    public int GetCurrentCount(bool cached = true)
    {
        return cached && _cachedCurrentCount.TryGetValue(out var value)
            ? value
            : _cachedCurrentCount.Update(CurrentCountRaw);
    }

    public WindowTriggerThresholdDetails DetailsWindow
    {
        get
        {
            var window = new WindowTriggerThresholdDetails(this)
            {
                closeOnClickedOutside = true,
                draggable = true
            };
            return window;
        }
    }

    public bool IsValid => ThresholdFilter.AllowedDefCount > 0;

    public virtual string OpString
    {
        get
        {
            return Op switch
            {
                Ops.LowerThan => " < ",
                Ops.Equals => " = ",
                Ops.HigherThan => " > ",
                _ => " ? ",
            };
        }
    }

    public override bool State
    {
        get
        {
            switch (Op)
            {
                case Ops.LowerThan:
                    return GetCurrentCount() < TargetCount;

                case Ops.Equals:
                    return GetCurrentCount() == TargetCount;

                case Ops.HigherThan:
                    return GetCurrentCount() > TargetCount;

                default:
                    ColonyManagerReduxMod.Instance.LogWarning(
                        "Trigger_ThingThreshold was defined without a correct operator");
                    return true;
            }
        }
    }

    public override string StatusTooltip => "ColonyManagerRedux.Thresholds.ThresholdCount".Translate(GetCurrentCount(), TargetCount);

    public override void DrawProgressBars(Rect progressRect, bool active)
    {
        progressRect.xMin += progressRect.width - 10;
        DrawProgressBar(
            progressRect,
            GetCurrentCount(),
            TargetCount,
            StatusTooltip,
            active,
            Resources.BarBackgroundActiveTexture);
    }

    public override void DrawTriggerConfig(ref Vector2 cur, float width, float entryHeight, string? label = null,
        string? tooltip = null, List<Designation>? targets = null,
        Action? onOpenFilterDetails = null,
        Func<Designation, string>? designationLabelGetter = null)
    {
        if (targets == null)
        {
            throw new ArgumentNullException(nameof(targets));
        }

        var hasTargets = !targets.NullOrEmpty();

        // target threshold
        var thresholdLabelRect = new Rect(
            cur.x,
            cur.y,
            width - (hasTargets ? SmallIconSize + Margin * 2 : 0f),
            entryHeight);
        var detailsWindowButtonRect = new Rect(
            thresholdLabelRect.xMax - SmallIconSize - Margin,
            cur.y + (entryHeight - SmallIconSize) / 2f,
            SmallIconSize,
            SmallIconSize);
        var targetsButtonRect = new Rect(
            thresholdLabelRect.xMax + Margin,
            cur.y + (entryHeight - SmallIconSize) / 2f,
            SmallIconSize,
            SmallIconSize
        );
        cur.y += entryHeight;

        var thresholdRect = new Rect(
            cur.x,
            cur.y,
            width,
            SliderHeight);
        cur.y += entryHeight;

        var useResourceListerToggleRect = new Rect(
            cur.x,
            cur.y,
            width,
            entryHeight);
        cur.y += entryHeight;


        Widgets.DrawHighlightIfMouseover(thresholdLabelRect);
        if (label.NullOrEmpty())
        {
            label = "ColonyManagerRedux.Thresholds.ThresholdCount".Translate(GetCurrentCount(), TargetCount) + ":";
        }

        if (tooltip.NullOrEmpty())
        {
            tooltip = "ColonyManagerRedux.Thresholds.ThresholdCountTooltip".Translate(GetCurrentCount(), TargetCount);
        }

        Widgets_Labels.Label(thresholdLabelRect, label!, tooltip);

        // add a little icon to mark interactivity
        GUI.color = Mouse.IsOver(thresholdLabelRect) ? GenUI.MouseoverColor : Color.white;
        GUI.DrawTexture(detailsWindowButtonRect, Resources.Cog);
        GUI.color = Color.white;
        if (Widgets.ButtonInvisible(thresholdLabelRect))
        {
            onOpenFilterDetails?.Invoke();
            Find.WindowStack.Add(DetailsWindow);
        }

        // target list
        if (hasTargets)
        {
            if (Widgets.ButtonImage(targetsButtonRect, Resources.Search))
            {
                var options = new List<FloatMenuOption>();
                foreach (var designation in targets!)
                {
                    var option = string.Empty;
                    Action onClick = () => Find.WindowStack.TryRemove(typeof(MainTabWindow_Manager), false);
                    Action<UnityEngine.Rect>? onHover = null;
                    if (designation.target.HasThing)
                    {
                        var thing = designation.target.Thing;
                        option = designationLabelGetter?.Invoke(designation) ?? thing.LabelCap;
                        onClick += () => CameraJumper.TryJumpAndSelect(thing);
                        onHover += (c) =>
                        {
                            if (!Find.CameraDriver.IsPanning())
                            {
                                CameraJumper.TryJump(thing);
                            }
                        };
                    }
                    else
                    {
                        var cell = designation.target.Cell;
                        var map = Find.CurrentMap;
                        // designation.map would be better, but that's private. We should only ever be looking at jobs on the current map anyway,
                        // so I suppose it doesn't matter -- ColonyManagerRedux.
                        option = designationLabelGetter?.Invoke(designation) ?? cell.GetTerrain(map).LabelCap;
                        onClick += () => CameraJumper.TryJump(cell, map);
                        onHover += (c) =>
                        {
                            if (!Find.CameraDriver.IsPanning())
                            {
                                CameraJumper.TryJump(cell, map);
                            }
                        };
                    }

                    options.Add(new FloatMenuOption(option, onClick, MenuOptionPriority.Default, onHover));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        Utilities.DrawToggle(useResourceListerToggleRect, "ColonyManagerRedux.ManagerCountAllOnMap".Translate(),
            "ColonyManagerRedux.ManagerCountAllOnMap.Tip".Translate(), ref CountAllOnMap, true);
        TargetCount = (int)GUI.HorizontalSlider(thresholdRect, TargetCount, 0, MaxUpperThreshold);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref TargetCount, "count");
        Scribe_Values.Look(ref MaxUpperThreshold, "maxUpperThreshold");
        Scribe_Values.Look(ref Op, "operator");
        Scribe_Deep.Look(ref thresholdFilter, "thresholdFilter", (object)ThresholdFilter_SettingsChanged);
        Scribe_Values.Look(ref CountAllOnMap, "countAllOnMap");

        // stockpile needs special treatment - is not referenceable.
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            _stockpile_scribe = stockpile?.ToString() ?? "null";
        }

        Scribe_Values.Look(ref _stockpile_scribe, "stockpile", "null");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            stockpile =
                job.Manager.map.zoneManager.AllZones.FirstOrDefault(z => z is Zone_Stockpile &&
                    z.label == _stockpile_scribe) as Zone_Stockpile;
        }
    }
}
