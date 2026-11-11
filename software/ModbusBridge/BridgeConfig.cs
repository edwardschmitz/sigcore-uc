namespace ModbusBridge;

public sealed record BridgeConfig
{
    public string ListenAddress { get; init; } = "0.0.0.0";
    public int Port { get; init; } = 502;
    public byte UnitId { get; init; } = 1;
    public string Provider { get; init; } = "Core";
    public string CoreHost { get; init; } = "127.0.0.1";
    public int CorePort { get; init; } = 7020;
    public int RequestTimeoutSeconds { get; init; } = 10;
    public int MaxConcurrentClients { get; init; } = 16;
    public int Backlog { get; init; } = 16;

    // Compatibility with the existing Program.cs call.
    // The path is deliberately ignored: ModbusBridge has no configuration file.
    public static BridgeConfig Load(string path) => new();
}
