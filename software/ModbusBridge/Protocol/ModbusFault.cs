namespace ModbusBridge.Protocol;

public sealed class ModbusFault : Exception
{
    public byte Code { get; }
    public ModbusFault(byte code, string message) : base(message) => Code = code;

    public static ModbusFault IllegalFunction() => new(1, "Illegal function");
    public static ModbusFault IllegalAddress() => new(2, "Illegal data address");
    public static ModbusFault IllegalValue() => new(3, "Illegal data value");
    public static ModbusFault DeviceFailure() => new(4, "Server device failure");
}
