// Building_ManagerStation.cs
// Copyright Karel Kroeze, 2017-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

// special blinking LED texture/glower logic + automagically doing jobs.
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

    public Building_AIManager()
    {
        _powerTrader = (CompPowerTrader)PowerComp;
        _glower = GetComp<CompGlowerAIManager>();
    }

    public override Color DrawColor => PrimaryColourBlinker;

    public override Color DrawColorTwo => SecondaryColour;

    public CompGlowerAIManager Glower => _glower ??= GetComp<CompGlowerAIManager>();

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

    public CompPowerTrader PowerTrader => _powerTrader ??= (CompPowerTrader)PowerComp;

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

    public Color PrimaryColourBlinker
    {
        get => _primaryBlinkerColour;
        set
        {
            _primaryBlinkerColour = value;
            _graphicDirty = true;
        }
    }

    public Color SecondaryColour
    {
        get => _secondaryColor;
        set
        {
            _secondaryColor = value;
            _graphicDirty = true;
        }
    }

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
    public override void Tick()
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
                    var coroutine = Manager.For(Map).TryDoWork();
                    PrimaryColour = coroutine != null ? Color.green : Color.red;
                    if (coroutine != null)
                    {
                        PrimaryColour = Color.green;
                        handle = MultiTickCoroutineManager.StartCoroutine(coroutine);
                    }
                    else
                    {
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
            Map.glowGrid.DirtyCache(Position);

            // the following two should not be necesarry, but for some reason do seem to be.
            Map.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.GroundGlow);
            Map.mapDrawer.MapMeshDirty(Position, MapMeshFlagDefOf.Things);

            _glowDirty = false;
        }
    }
}
