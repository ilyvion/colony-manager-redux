// Utilities_Cache.cs
// Copyright Karel Kroeze, 2018-2020
// Copyright (c) 2024 Alexander Krivács Schrøder

namespace ColonyManagerRedux;

public class CachedValues<TKey, TValue>(int updateInterval = 250)
{
    private readonly Dictionary<TKey, CachedValue<TValue>> _cache = [];
    private readonly int updateInterval = updateInterval;

    public TValue? this[TKey index]
    {
        get
        {
            TryGetValue(index, out var value);
            return value;
        }
        set
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }
            Update(index, value);
        }
    }

    public void Add(TKey key, Func<TValue> updater)
    {
        if (updater == null)
        {
            throw new ArgumentNullException(nameof(updater));
        }

        var value = updater();
        var cached = new CachedValue<TValue>(value, updateInterval, updater);
        _cache.Add(key, cached);
    }

    public bool TryGetValue(TKey key, out TValue? value)
    {
        if (_cache.TryGetValue(key, out var cachedValue))
        {
            return cachedValue.TryGetValue(out value);
        }

        value = default;
        return false;
    }

    public void Update(TKey key, TValue value)
    {
        if (_cache.TryGetValue(key, out var cachedValue))
        {
            cachedValue.Update(value);
        }
        else
        {
            _cache.Add(key, new CachedValue<TValue>(value, updateInterval));
        }
    }
}

public class CachedValue<T>
{
    private readonly T _default;
    private readonly int _updateInterval;
    private readonly Func<T>? _updater;
    private T _cached;
    private int? _timeSet;

    public CachedValue(T @default, int updateInterval = 250, Func<T>? updater = null)
    {
        _updateInterval = updateInterval;
        _cached = _default = @default;
        _updater = updater;
        _timeSet = null;
    }

    public T Value
    {
        get
        {
            if (TryGetValue(out var value))
            {
                return value;
            }

            throw new InvalidOperationException(
                "get_Value() on a CachedValue that is out of date, and has no updater.");
        }
    }

    public bool TryGetValue(out T value)
    {
        if (_timeSet.HasValue && Find.TickManager.TicksGame - _timeSet.Value <= _updateInterval)
        {
            value = _cached;
            return true;
        }

        if (_updater != null)
        {
            Update();
            value = _cached;
            return true;
        }

        value = _default;
        return false;
    }

    public void Update(T value)
    {
        _cached = value;
        _timeSet = Find.TickManager.TicksGame;
    }

    public void Update()
    {
        if (_updater == null)
        {
            ColonyManagerReduxMod.Instance.LogError("Calling Update() without updater");
        }
        else
        {
            Update(_updater());
        }
    }

    internal void Invalidate()
    {
        _timeSet = null;
    }
}

// count cache for multiple products
public class FilterCountCache(int count)
{
    public int Cache = count;
    public int TimeSet = Find.TickManager.TicksGame;
}
