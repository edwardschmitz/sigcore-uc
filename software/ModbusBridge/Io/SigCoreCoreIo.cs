using SigCoreCommon;

namespace ModbusBridge.Io;

/// <summary>
/// Uses exactly the SigCoreSystem connection, subscriptions, and output methods
/// used by WebBridge/CoreState.cs and its relay/AO endpoints.
/// Incoming subscribed notifications are the ONLY source of feedback values.
/// Writes do not optimistically modify those values.
/// </summary>
public sealed class SigCoreCoreIo : ISigCoreIo, IDisposable
{
    private const int DigitalCount = 8;
    private const int AnalogCount = 4;

    private readonly SigCoreSystem _core = new();
    private readonly object _sync = new();
    private readonly string _host;
    private readonly int _port;
    private readonly bool[] _digitalInputs = new bool[DigitalCount];
    private readonly bool[] _digitalValid = new bool[DigitalCount];
    private readonly bool[] _relayFeedback = new bool[DigitalCount];
    private readonly bool[] _relayValid = new bool[DigitalCount];
    private readonly float[] _analogInputs = new float[AnalogCount];
    private readonly bool[] _analogInputValid = new bool[AnalogCount];
    private readonly float[] _analogOutputFeedback = new float[AnalogCount];
    private readonly bool[] _analogOutputValid = new bool[AnalogCount];

    private bool _started;
    private bool _disposed;
    private long _connectionGeneration;

    public SigCoreCoreIo(string host, int port)
    {
        _host = host;
        _port = port;

        _core.DigitalInChanged += values =>
        {
            lock (_sync)
            {
                for (int i = 0; i < DigitalCount; i++)
                {
                    _digitalValid[i] = i < values.Length;
                    if (_digitalValid[i]) _digitalInputs[i] = values[i];
                }
            }
        };

        _core.AnalogInChanged += values =>
        {
            lock (_sync)
            {
                for (int i = 0; i < AnalogCount; i++)
                {
                    bool valid = i < values.Length && TryFiniteFloat(values[i], out _);
                    _analogInputValid[i] = valid;
                    if (valid) _analogInputs[i] = (float)values[i];
                }
            }
        };

        _core.RelayChanged += (channel, state) =>
        {
            if (channel >= DigitalCount) return;
            lock (_sync)
            {
                _relayFeedback[channel] = state;
                _relayValid[channel] = true;
            }
        };

        _core.AnalogOutChanged += (channel, value, auto) =>
        {
            if (channel >= AnalogCount) return;
            lock (_sync)
            {
                _analogOutputValid[channel] = TryFiniteFloat(value, out float converted);
                if (_analogOutputValid[channel]) _analogOutputFeedback[channel] = converted;
            }
        };

        _core.ConnectionStateChanged += state =>
        {
            long generation;
            // Never present cached values from before a disconnect as current.
            lock (_sync)
            {
                if (state != SigCoreConnectionState.Connected)
                {
                    ++_connectionGeneration;
                    InvalidateFeedback();
                }
                generation = _connectionGeneration;
            }
            // Subscribe sends only subsequent changes. Read the current values
            // once on each connection so unchanged channels are available too.
            if (state == SigCoreConnectionState.Connected)
                _ = Task.Run(() => InitializeFeedbackAsync(generation));
            Console.WriteLine($"[ModbusBridge] Core connection: {state}");
        };

        _core.ConnectionWarning += warning =>
            Console.Error.WriteLine($"[ModbusBridge] Core warning: {warning}");
    }

    public void Start()
    {
        if (_started || _disposed) throw new InvalidOperationException("Core adapter cannot be restarted.");
        _started = true;
        // WebBridge uses the same asynchronous startup pattern. SigCoreSystem
        // owns its connection management and subscription registration.
        _ = Task.Run(async () =>
        {
            try
            {
                await _core.StartAsync(_host, _port);
                Console.WriteLine("[ModbusBridge] Core connection task completed.");
            }
            catch (Exception error)
            {
                Console.Error.WriteLine($"[ModbusBridge] Core connection failed: {error}");
                lock (_sync) InvalidateFeedback();
            }
        });
    }

    // Seed the feedback cache once per Core connection. Normal operation still
    // uses subscription notifications; Modbus reads never poll Core.
    private async Task InitializeFeedbackAsync(long generation)
    {
        bool Current()
        {
            lock (_sync)
                return !_disposed && generation == _connectionGeneration &&
                    _core.ConnectionState == SigCoreConnectionState.Connected;
        }

        for (int channel = 0; channel < DigitalCount; channel++)
        {
            if (!Current()) return;
            try
            {
                bool state = await _core.GetDInAsync((uint)channel);
                lock (_sync)
                {
                    if (_disposed || generation != _connectionGeneration ||
                        _core.ConnectionState != SigCoreConnectionState.Connected) return;
                    if (!_digitalValid[channel])
                    {
                        _digitalInputs[channel] = state;
                        _digitalValid[channel] = true;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!Current()) return;
                Console.Error.WriteLine($"[ModbusBridge] Initial DIN {channel + 1} read failed: {ex.Message}");
            }

            if (!Current()) return;
            try
            {
                bool state = await _core.GetRelayAsync((uint)channel);
                lock (_sync)
                {
                    if (_disposed || generation != _connectionGeneration ||
                        _core.ConnectionState != SigCoreConnectionState.Connected) return;
                    if (!_relayValid[channel])
                    {
                        _relayFeedback[channel] = state;
                        _relayValid[channel] = true;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!Current()) return;
                Console.Error.WriteLine($"[ModbusBridge] Initial relay {channel + 1} read failed: {ex.Message}");
            }
        }

        for (int channel = 0; channel < AnalogCount; channel++)
        {
            if (!Current()) return;
            try
            {
                double value = await _core.GetAnalogInputAsync((uint)channel);
                if (TryFiniteFloat(value, out float converted))
                {
                    lock (_sync)
                    {
                        if (_disposed || generation != _connectionGeneration ||
                            _core.ConnectionState != SigCoreConnectionState.Connected) return;
                        if (!_analogInputValid[channel])
                        {
                            _analogInputs[channel] = converted;
                            _analogInputValid[channel] = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!Current()) return;
                Console.Error.WriteLine($"[ModbusBridge] Initial AIN {channel + 1} read failed: {ex.Message}");
            }

            if (!Current()) return;
            try
            {
                double value = await _core.GetAOutValueAsync((uint)channel);
                if (TryFiniteFloat(value, out float converted))
                {
                    lock (_sync)
                    {
                        if (_disposed || generation != _connectionGeneration ||
                            _core.ConnectionState != SigCoreConnectionState.Connected) return;
                        if (!_analogOutputValid[channel])
                        {
                            _analogOutputFeedback[channel] = converted;
                            _analogOutputValid[channel] = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (!Current()) return;
                Console.Error.WriteLine($"[ModbusBridge] Initial AOUT {channel + 1} read failed: {ex.Message}");
            }
        }
    }

    private static bool TryFiniteFloat(double value, out float result)
    {
        result = default;
        if (!double.IsFinite(value) || value > float.MaxValue || value < -float.MaxValue)
            return false;
        result = (float)value;
        return float.IsFinite(result);
    }

    // Caller holds _sync.
    private void InvalidateFeedback()
    {
        Array.Clear(_digitalValid);
        Array.Clear(_relayValid);
        Array.Clear(_analogInputValid);
        Array.Clear(_analogOutputValid);
    }

    private void EnsureConnected()
    {
        if (_disposed || _core.ConnectionState != SigCoreConnectionState.Connected)
            throw new CoreUnavailableException("SigCore Core is not connected.");
    }

    private static T ReadFeedback<T>(T[] values, bool[] valid, int index)
    {
        if (!valid[index])
            throw new CoreUnavailableException($"No current Core subscription value for channel {index}.");
        return values[index];
    }

    public ValueTask<bool> ReadDigitalInputAsync(int index, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureConnected();
            return ValueTask.FromResult(ReadFeedback(_digitalInputs, _digitalValid, index));
        }
    }

    public ValueTask<bool> ReadRelayFeedbackAsync(int index, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureConnected();
            return ValueTask.FromResult(ReadFeedback(_relayFeedback, _relayValid, index));
        }
    }

    public ValueTask<float> ReadAnalogInputAsync(int index, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureConnected();
            return ValueTask.FromResult(ReadFeedback(_analogInputs, _analogInputValid, index));
        }
    }

    public ValueTask<float> ReadAnalogOutputFeedbackAsync(int index, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync)
        {
            EnsureConnected();
            return ValueTask.FromResult(ReadFeedback(_analogOutputFeedback, _analogOutputValid, index));
        }
    }

    public async ValueTask WriteRelayCommandAsync(int index, bool logicalOn, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsureConnected();
        try
        {
            // The response confirms command acceptance, NOT field-state feedback.
            await _core.SetRelayAsync((uint)index, logicalOn);
        }
        catch (ArgumentException error)
        {
            throw new CoreInvalidValueException(error.Message);
        }
    }

    public async ValueTask WriteAnalogOutputCommandAsync(int index, float engineeringValue, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        EnsureConnected();
        if (!float.IsFinite(engineeringValue))
            throw new CoreInvalidValueException("Analog output command is not finite.");
        try
        {
            await _core.SetAOutValueAsync((uint)index, (double)engineeringValue);
        }
        catch (ArgumentException error)
        {
            throw new CoreInvalidValueException(error.Message);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_sync) InvalidateFeedback();
        _core.Stop();
        _core.Dispose();
    }
}
