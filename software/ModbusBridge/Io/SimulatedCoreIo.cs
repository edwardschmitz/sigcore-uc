namespace ModbusBridge.Io;

/// <summary>
/// Development-only I/O. Simulates a Core notification following each output
/// command, but the command and feedback address spaces remain separate.
/// Not connected to hardware.
/// </summary>
public sealed class SimulatedCoreIo : ISigCoreIo
{
    private readonly object _sync = new();
    private readonly bool[] _relayFeedback = new bool[8];
    private readonly bool[] _digital = new bool[8];
    private readonly float[] _analogIn = new float[4];
    private readonly float[] _analogOutFeedback = new float[4];

    public ValueTask<bool> ReadRelayFeedbackAsync(int index, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync) return ValueTask.FromResult(_relayFeedback[index]);
    }
    public ValueTask<bool> ReadDigitalInputAsync(int index, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync) return ValueTask.FromResult(_digital[index]);
    }
    public ValueTask<float> ReadAnalogInputAsync(int index, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync) return ValueTask.FromResult(_analogIn[index]);
    }
    public ValueTask<float> ReadAnalogOutputFeedbackAsync(int index, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync) return ValueTask.FromResult(_analogOutFeedback[index]);
    }
    public ValueTask WriteRelayCommandAsync(int index, bool logicalOn, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync) _relayFeedback[index] = logicalOn;
        return ValueTask.CompletedTask;
    }
    public ValueTask WriteAnalogOutputCommandAsync(int index, float engineeringValue, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        lock (_sync) _analogOutFeedback[index] = engineeringValue;
        return ValueTask.CompletedTask;
    }
}
