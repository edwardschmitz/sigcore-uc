namespace ModbusBridge.Io;

/// <summary>
/// Core I/O boundary. Read operations return subscribed *feedback*; write operations
/// forward commands. Command locations have no readable mirror in this interface.
/// All channels are zero-based, matching the SigCoreCommon API.
/// </summary>
public interface ISigCoreIo
{
    ValueTask<bool> ReadDigitalInputAsync(int index, CancellationToken ct);
    ValueTask<bool> ReadRelayFeedbackAsync(int index, CancellationToken ct);
    ValueTask<float> ReadAnalogInputAsync(int index, CancellationToken ct);
    ValueTask<float> ReadAnalogOutputFeedbackAsync(int index, CancellationToken ct);
    ValueTask WriteRelayCommandAsync(int index, bool logicalOn, CancellationToken ct);
    ValueTask WriteAnalogOutputCommandAsync(int index, float engineeringValue, CancellationToken ct);
}

public sealed class CoreUnavailableException : Exception
{
    public CoreUnavailableException(string message) : base(message) { }
}

public sealed class CoreInvalidValueException : Exception
{
    public CoreInvalidValueException(string message) : base(message) { }
}
