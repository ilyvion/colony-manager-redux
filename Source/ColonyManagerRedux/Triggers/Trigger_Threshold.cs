// Trigger_Threshold.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

using ilyvion.Laboratory.Extensions;
using ilyvion.Laboratory.UI;
using static ColonyManagerRedux.Constants;

namespace ColonyManagerRedux;

[HotSwappable]
public sealed class Trigger_Threshold : Trigger
{
    public enum Ops
    {
        LowerThan,
        Equals,
        HigherThan
    }

    private bool countAllOnMap;
    public bool CountAllOnMap { get => countAllOnMap; set => countAllOnMap = value; }

    private int maxUpperThreshold;
    public int MaxUpperThreshold { get => maxUpperThreshold; set => maxUpperThreshold = value; }

    private Ops op;
    public Ops Op { get => op; set => op = value; }

    private ThingFilter parentFilter;
    public ThingFilter ParentFilter { get => parentFilter; set => parentFilter = value; }

    private Zone_Stockpile? stockpile;
    public Zone_Stockpile? Stockpile { get => stockpile; set => stockpile = value; }
    public ref Zone_Stockpile? StockpileRef { get => ref stockpile; }

    private int targetCount;
    public int TargetCount { get => targetCount; set => targetCount = value; }

    private ThingFilter thresholdFilter;
    public ThingFilter ThresholdFilter { get => thresholdFilter; }
    private readonly CachedValue<int> _cachedCurrentCount = new(0);

    private string? _stockpile_scribe;

    public Action? SettingsChanged { get; set; }

    public Trigger_Threshold(ManagerJob job) : base(job)
    {
        if (job == null)
        {
            throw new ArgumentNullException(nameof(job));
        }

        Settings settings = ColonyManagerReduxMod.Settings;
        countAllOnMap = settings.DefaultCountAllOnMap;

        parentFilter = new ThingFilter();
        parentFilter.SetDisallowAll();

        thresholdFilter = new ThingFilter(ThresholdFilter_SettingsChanged);
        ThresholdFilter.SetDisallowAll();

        op = Ops.LowerThan;
        maxUpperThreshold = job.MaxUpperThreshold;
        targetCount = settings.DefaultTargetCount;
    }

    private void ThresholdFilter_SettingsChanged()
    {
        _cachedCurrentCount.Invalidate();
        SettingsChanged?.Invoke();
    }

    private int CurrentCountRaw =>
        Job.Manager.map.CountProducts(ThresholdFilter, stockpile, CountAllOnMap);

    public int GetCurrentCount(bool cached = true)
    {
        return cached && _cachedCurrentCount.TryGetValue(out var value)
            ? value
            : _cachedCurrentCount.Update(CurrentCountRaw);
    }

    public Coroutine GetCurrentCountCoroutine(Boxed<int> count)
    {
        return Job.Manager.map.CountProductsCoroutine(
            ThresholdFilter, count, stockpile, CountAllOnMap);
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

    public string OpString
    {
        get
        {
            return op switch
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
            switch (op)
            {
                case Ops.LowerThan:
                    return GetCurrentCount() < targetCount;

                case Ops.Equals:
                    return GetCurrentCount() == targetCount;

                case Ops.HigherThan:
                    return GetCurrentCount() > targetCount;

                default:
                    ColonyManagerReduxMod.Instance.LogWarning(
                        "Trigger_ThingThreshold was defined without a correct operator");
                    return true;
            }
        }
    }

    public override string StatusTooltip => "ColonyManagerRedux.Thresholds.ThresholdCount".Translate(GetCurrentCount(), targetCount);

    public override void DrawVerticalProgressBars(Rect progressRect, bool active)
    {
        progressRect.xMin += progressRect.width - 10;
        DrawVerticalProgressBar(
            progressRect,
            GetCurrentCount(),
            targetCount,
            StatusTooltip,
            active,
            Resources.BarBackgroundActiveTexture);
    }

    public override void DrawHorizontalProgressBars(Rect progressRect, bool active)
    {
        progressRect.height = SmallIconSize;
        DrawHorizontalProgressBar(
            progressRect,
            GetCurrentCount(),
            targetCount,
            StatusTooltip,
            active,
            Resources.BarBackgroundActiveTexture);
    }

    public override void DrawTriggerConfig(ref Vector2 cur, float width, float entryHeight, string? label = null,
        string? tooltip = null, List<Designation>? targets = null,
        Action? onOpenFilterDetails = null,
        Func<Designation, string?>? designationLabelGetter = null)
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
        cur.y += SliderHeight;

        var useResourceListerToggleRect = new Rect(
            cur.x,
            cur.y,
            width,
            entryHeight);
        cur.y += entryHeight;


        Widgets.DrawHighlightIfMouseover(thresholdLabelRect);
        if (label.NullOrEmpty())
        {
            label = "ColonyManagerRedux.Thresholds.ThresholdCount".Translate(GetCurrentCount(), targetCount) + ":";
        }

        if (tooltip.NullOrEmpty())
        {
            tooltip = "ColonyManagerRedux.Thresholds.ThresholdCountTooltip".Translate(GetCurrentCount(), targetCount);
        }

        IlyvionWidgets.Label(thresholdLabelRect, label!, tooltip, TextAnchor.MiddleLeft);

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
                    Action? onClick = () => Find.WindowStack.TryRemove(typeof(MainTabWindow_Manager), false);
                    Action<Rect>? onHover = null;
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
                        if (cell.IsValid)
                        {
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
                        else
                        {
                            option = "Invalid designation. This should never happen.";
                            onClick = null;
                        }
                    }

                    options.Add(new FloatMenuOption(option, onClick, MenuOptionPriority.Default, onHover));
                }

                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        Utilities.DrawToggle(useResourceListerToggleRect, "ColonyManagerRedux.Threshold.CountAllOnMap".Translate(),
            "ColonyManagerRedux.Threshold.CountAllOnMap.Tip".Translate(), ref countAllOnMap, true);
        targetCount = (int)GUI.HorizontalSlider(thresholdRect, targetCount, 0, maxUpperThreshold);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref targetCount, "count");
        Scribe_Values.Look(ref maxUpperThreshold, "maxUpperThreshold");
        Scribe_Values.Look(ref op, "operator");
        Scribe_Deep.Look(ref thresholdFilter, "thresholdFilter", (object)ThresholdFilter_SettingsChanged);
        Scribe_Values.Look(ref countAllOnMap, "countAllOnMap");

        // stockpile needs special treatment - is not referenceable.
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            _stockpile_scribe = stockpile?.ToString() ?? "null";
        }

        Scribe_Values.Look(ref _stockpile_scribe, "stockpile", "null");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            stockpile =
                Job.Manager.map.zoneManager.AllZones.FirstOrDefault(z => z is Zone_Stockpile &&
                    z.label == _stockpile_scribe) as Zone_Stockpile;
        }
    }
}
