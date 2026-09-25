// Building_AIManager.cs
// Copyright Karel Kroeze, 2017-2020
// Copyright (c) 2024–2026 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

/// <summary>
/// Special building with blinking LED texture/glower logic and automatic job execution for the AI manager station.
/// </summary>
[HotSwappable]
public class Building_AIManager : Building
{
    private readonly Color[] _colors =
    [
        Color.white,
        Color.green,
        Color.red,
        Color.blue,
        Color.yellow,
        Color.cyan,
    ];

    private bool _glowDirty;
    private bool _graphicDirty;
    private Color _primaryBlinkerColour = Color.black;

    private Color _primaryColor = Color.black;

    private Color _secondaryColor = Color.black;

    /// <summary>
    /// Initializes a new instance of the <see cref="Building_AIManager"/> class.
    /// </summary>
    public Building_AIManager()
    {
        PowerTrader = (CompPowerTrader)PowerComp;
        Glower = GetComp<CompGlowerAIManager>();
    }

    /// <summary>
    /// Gets the primary color for drawing (blinker color).
    /// </summary>
    public override Color DrawColor => PrimaryColourBlinker;

    /// <summary>
    /// Gets the secondary color for drawing.
    /// </summary>
    public override Color DrawColorTwo => SecondaryColour;

    /// <summary>
    /// Gets the glower component for this building.
    /// </summary>
    public CompGlowerAIManager Glower => field ??= GetComp<CompGlowerAIManager>();

    /// <summary>
    /// Gets or sets whether the building is powered. Updates glower and LED colors accordingly.
    /// </summary>
    public bool Powered
    {
        get;
        set
        {
            field = value;
            Glower.IsLit = value;
            PrimaryColourBlinker = value ? PrimaryColour : Color.black;
            SecondaryColour = value ? _colors[SecondaryColourIndex] : Color.black;
        }
    }

    /// <summary>
    /// Gets the power trader component for this building.
    /// </summary>
    public CompPowerTrader PowerTrader => field ??= (CompPowerTrader)PowerComp;

    /// <summary>
    /// Gets or sets the primary color for the building's LED and glower.
    /// </summary>
    public Color PrimaryColour
    {
        get => _primaryColor;
        set
        {
            var newColour = new ColorInt(
                (int)(value.r * 255),
                (int)(value.g * 255),
                (int)(value.b * 255),
                0
            );
            Glower.Props.glowColor = newColour;
            _primaryColor = value;
            _glowDirty = true;
        }
    }

    /// <summary>
    /// Gets or sets the current blinker color for the primary LED.
    /// </summary>
    public Color PrimaryColourBlinker
    {
        get => _primaryBlinkerColour;
        set
        {
            _primaryBlinkerColour = value;
            _graphicDirty = true;
        }
    }

    /// <summary>
    /// Gets or sets the secondary color for the building's LED.
    /// </summary>
    public Color SecondaryColour
    {
        get => _secondaryColor;
        set
        {
            _secondaryColor = value;
            _graphicDirty = true;
        }
    }

    /// <summary>
    /// Gets or sets the index for the secondary color in the color array.
    /// </summary>
    public int SecondaryColourIndex
    {
        get;
        set
        {
            field = value;
            SecondaryColour = _colors[field];
        }
    }

    private CoroutineHandle? handle;

    /// <inheritdoc/>
#if v1_5
    public override void Tick()
#else
    protected override void Tick()
#endif
    {
        base.Tick();

        if (Powered != PowerTrader.PowerOn)
        {
            Powered = PowerTrader.PowerOn;
        }

        if (Powered)
        {
            var tick = Find.TickManager.TicksGame;

            // random blinking on secondary
            if (tick % 30 == Rand.RangeInclusive(0, 25))
            {
                SecondaryColourIndex = (SecondaryColourIndex + 1) % _colors.Length;
            }

            // primary colour
            var step = DecideAIManagerStep(
                tick % 250 == 0,
                handle != null,
                handle?.IsCompleted == true
            );
            PowerTrader.PowerOutput = step.FullPower
                ? -PowerTrader.Props.PowerConsumption
                : -PowerTrader.Props.idlePowerDraw;
            if (step.ClearHandle)
            {
                ColonyManagerReduxMod.Instance.LogVerboseMessage(
                    $"AI Manager completed active job"
                );
                handle = null;
            }
            if (step.PrimaryColor is { } stepColor)
            {
                PrimaryColour = stepColor;
            }
            if (step.CheckForWork)
            {
                ColonyManagerReduxMod.Instance.LogVerboseMessage(
                    $"Setting up a job due to AI manager seeing there's work to do..."
                );
                var coroutine = Manager.For(Map).TryDoWork();
                if (coroutine != null)
                {
                    ColonyManagerReduxMod.Instance.LogVerboseMessage(
                        $"...job started @ game tick {Find.TickManager.TicksGame}."
                    );
                    PrimaryColour = Color.green;
                    handle = MultiTickCoroutineManager.StartCoroutine(coroutine);
                }
                else
                {
                    ColonyManagerReduxMod.Instance.LogVerboseMessage($"...there was no job to do.");
                    PrimaryColour = Color.red;
                }
            }

            // blinking on primary
            var blinkPhase = BlinkPhaseAt(tick);
            if (blinkPhase == BlinkPhase.On)
            {
                PrimaryColourBlinker = PrimaryColour;
            }
            else if (blinkPhase == BlinkPhase.Off)
            {
                PrimaryColourBlinker = Color.black;
            }
        }

        // apply changes
        if (_graphicDirty)
        {
            // update LED colours
            Notify_ColorChanged();
            _graphicDirty = false;
        }

        if (_glowDirty)
        {
            // Update glow grid
#if v1_5
            Map.glowGrid.DirtyCache(Position);
#else
            Map.glowGrid.DirtyCell(Position);
#endif

            // the following two should not be necesarry, but for some reason do seem to be.
            Map.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.GroundGlow);
            Map.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.Things);

            _glowDirty = false;
        }
    }

    /// <summary>What the AI manager does on a tick, before looking for work.</summary>
    /// <param name="CheckForWork">Whether to look for a job to start.</param>
    /// <param name="ClearHandle">Whether the running job has finished and its handle is dropped.</param>
    /// <param name="FullPower">Whether to draw full power instead of idle power.</param>
    /// <param name="PrimaryColor">The new primary LED colour, if the step decides one.</param>
    internal readonly record struct AIManagerStep(
        bool CheckForWork,
        bool ClearHandle,
        bool FullPower,
        Color? PrimaryColor
    );

    internal static AIManagerStep DecideAIManagerStep(
        bool isWorkCheckTick,
        bool hasHandle,
        bool handleCompleted
    ) =>
        !isWorkCheckTick ? new(false, false, false, null)
        : !hasHandle ? new(true, false, true, null)
        : handleCompleted ? new(false, true, false, Color.red)
        : new(false, false, true, Color.green);

    /// <summary>Which way the primary LED blinker changes on a tick.</summary>
    internal enum BlinkPhase
    {
        None,
        On,
        Off,
    }

    internal static BlinkPhase BlinkPhaseAt(int tick) =>
        (tick % 30) switch
        {
            0 => BlinkPhase.On,
            25 => BlinkPhase.Off,
            _ => BlinkPhase.None,
        };
}
