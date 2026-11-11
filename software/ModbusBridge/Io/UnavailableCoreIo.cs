namespace ModbusBridge.Io;

/// <summary>Offline diagnostic mode: never serves fabricated real I/O.</summary>
public sealed class UnavailableCoreIo : ISigCoreIo
{
    private static CoreUnavailableException Offline() =>
        new("Unavailable provider is selected; Core is not connected.");

    public ValueTask<bool> ReadRelayFeedbackAsync(int index, CancellationToken ct) => ValueTask.FromException<bool>(Offline());
    public ValueTask<bool> ReadDigitalInputAsync(int index, CancellationToken ct) => ValueTask.FromException<bool>(Offline());
    public ValueTask<float> ReadAnalogInputAsync(int index, CancellationToken ct) => ValueTask.FromException<float>(Offline());
    public ValueTask<float> ReadAnalogOutputFeedbackAsync(int index, CancellationToken ct) => ValueTask.FromException<float>(Offline());
    public ValueTask WriteRelayCommandAsync(int index, bool logicalOn, CancellationToken ct) => ValueTask.FromException(Offline());
    public ValueTask WriteAnalogOutputCommandAsync(int index, float engineeringValue, CancellationToken ct) => ValueTask.FromException(Offline());
}
