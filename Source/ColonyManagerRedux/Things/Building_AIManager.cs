// Building_ManagerStation.cs
// Copyright Karel Kroeze, 2017-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

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
        Color.cyan
    ];

    private bool _glowDirty;

    private CompGlowerAIManager _glower;

    private bool _graphicDirty;

    private bool _powered;

    private CompPowerTrader _powerTrader;

    private Color _primaryBlinkerColour = Color.black;

    private Color _primaryColor = Color.black;

    private Color _secondaryColor = Color.black;

    private int _secondaryColourIndex;

    /// <summary>
    /// Initializes a new instance of the <see cref="Building_AIManager"/> class.
    /// </summary>
    public Building_AIManager()
    {
        _powerTrader = (CompPowerTrader)PowerComp;
        _glower = GetComp<CompGlowerAIManager>();
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
    public CompGlowerAIManager Glower => _glower ??= GetComp<CompGlowerAIManager>();

    /// <summary>
    /// Gets or sets whether the building is powered. Updates glower and LED colors accordingly.
    /// </summary>
    public bool Powered
    {
        get => _powered;
        set
        {
            _powered = value;
            Glower.IsLit = value;
            PrimaryColourBlinker = value ? PrimaryColour : Color.black;
            SecondaryColour = value ? _colors[_secondaryColourIndex] : Color.black;
        }
    }

    /// <summary>
    /// Gets the power trader component for this building.
    /// </summary>
    public CompPowerTrader PowerTrader => _powerTrader ??= (CompPowerTrader)PowerComp;

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
                (int)(value.b * 255), 0);
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
        get => _secondaryColourIndex;
        set
        {
            _secondaryColourIndex = value;
            SecondaryColour = _colors[_secondaryColourIndex];
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
            if (tick % 250 == 0)
            {
                if (handle != null)
                {
                    if (handle.IsCompleted)
                    {
                        ColonyManagerReduxMod.Instance.LogVerboseMessage(
                            $"AI Manager completed active job");
                        handle = null;
                        PrimaryColour = Color.red;
                        PowerTrader.PowerOutput = -PowerTrader.Props.idlePowerDraw;
                    }
                    else
                    {
                        PowerTrader.PowerOutput = -PowerTrader.Props.PowerConsumption;
                        PrimaryColour = Color.green;
                    }
                }
                else
                {
                    PowerTrader.PowerOutput = -PowerTrader.Props.PowerConsumption;
                    ColonyManagerReduxMod.Instance.LogVerboseMessage($"Setting up a job due to AI manager seeing there's work to do...");
                    var coroutine = Manager.For(Map).TryDoWork();
                    PrimaryColour = coroutine != null ? Color.green : Color.red;
                    if (coroutine != null)
                    {
                        ColonyManagerReduxMod.Instance.LogVerboseMessage($"...job started @ game tick {Find.TickManager.TicksGame}.");
                        PrimaryColour = Color.green;
                        handle = MultiTickCoroutineManager.StartCoroutine(coroutine);
                    }
                    else
                    {
                        ColonyManagerReduxMod.Instance.LogVerboseMessage($"...there was no job to do.");
                        PrimaryColour = Color.red;
                    }
                }
            }
            else
            {
                PowerTrader.PowerOutput = -PowerTrader.Props.idlePowerDraw;
            }

            // blinking on primary
            if (tick % 30 == 0)
            {
                PrimaryColourBlinker = PrimaryColour;
            }

            if (tick % 30 == 25)
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
}
