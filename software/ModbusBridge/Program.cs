using ModbusBridge;
using ModbusBridge.Io;
using ModbusBridge.Protocol;
using SigCoreCommon;

try
{
    string configPath = args.Length switch
    {
        0 => "config/modbusbridge.json",
        2 when args[0] == "--config" => args[1],
        _ => throw new ArgumentException("Usage: ModbusBridge [--config path-to-json]")
    };
    var config = BridgeConfig.Load(configPath);
    Log.Initialize("ModbusBridge");

    ISigCoreIo io = config.Provider switch
    {
        "Core" => new SigCoreCoreIo(config.CoreHost, config.CorePort),
        "Simulated" => new SimulatedCoreIo(),
        _ => new UnavailableCoreIo()
    };
    if (io is SigCoreCoreIo liveCore)
    {
        liveCore.Start();
    }
    else if (io is SimulatedCoreIo)
    {
        Console.Error.WriteLine("WARNING: SIMULATED I/O: never connects to Core or real hardware.");
    }
    else
    {
        Console.Error.WriteLine("WARNING: Unavailable I/O provider: requests return Modbus exception 04.");
    }

    using var stopping = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopping.Cancel(); };
    try
    {
        await new ModbusTcpServer(new ModbusProcessor(io), config).RunAsync(stopping.Token);
    }
    finally
    {
        (io as IDisposable)?.Dispose();
    }
}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    Environment.ExitCode = 1;
}
