using System.Buffers.Binary;
using ModbusBridge.Io;

namespace ModbusBridge.Protocol;

/// <summary>
/// Modbus TCP PDU handling with strictly separated command and feedback maps.
/// FC05 writes relay commands (coils 0-7) and FC16 writes AO commands
/// (holding registers 0-7). These command areas are intentionally write-only.
/// FC02 reads feedback: DI bits 0-7, relay bits 8-15.
/// FC04 reads feedback: AI registers 0-7, AO registers 8-15.
/// Each float uses two 16-bit registers, high word first, network byte order.
/// </summary>
public sealed class ModbusProcessor(ISigCoreIo io)
{
    public async ValueTask<byte[]> ProcessAsync(ReadOnlyMemory<byte> request, CancellationToken ct)
    {
        if (request.IsEmpty) throw ModbusFault.IllegalValue();
        byte function = request.Span[0];
        try
        {
            return function switch
            {
                2 => await ReadFeedbackBitsAsync(request, ct),
                4 => await ReadFeedbackRegistersAsync(request, ct),
                5 => await WriteRelayCommandAsync(request, ct),
                16 => await WriteAnalogOutputCommandAsync(request, ct),
                // No readback of command locations (FC01 or FC03).
                _ => throw ModbusFault.IllegalFunction()
            };
        }
        catch (ModbusFault fault)
        {
            return new byte[] { (byte)(function | 0x80), fault.Code };
        }
        catch (CoreInvalidValueException)
        {
            return new byte[] { (byte)(function | 0x80), 3 };
        }
        catch (CoreUnavailableException)
        {
            return new byte[] { (byte)(function | 0x80), 4 };
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new byte[] { (byte)(function | 0x80), 4 };
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            Console.Error.WriteLine($"Core I/O error FC {function:X2}: {error.GetType().Name}: {error.Message}");
            return new byte[] { (byte)(function | 0x80), 4 };
        }
    }

    private static ushort Word(ReadOnlySpan<byte> bytes, int offset) =>
        BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));

    private static void ValidateFixed(ReadOnlyMemory<byte> request)
    {
        if (request.Length != 5) throw ModbusFault.IllegalValue();
    }

    private async ValueTask<byte[]> ReadFeedbackBitsAsync(ReadOnlyMemory<byte> req, CancellationToken ct)
    {
        ValidateFixed(req);
        ushort start = Word(req.Span, 1);
        ushort qty = Word(req.Span, 3);
        if (qty == 0 || qty > 2000) throw ModbusFault.IllegalValue();
        if ((int)start + qty > 16) throw ModbusFault.IllegalAddress();

        int bytes = (qty + 7) / 8;
        var result = new byte[2 + bytes];
        result[0] = 2;
        result[1] = (byte)bytes;
        for (int i = 0; i < qty; i++)
        {
            int address = start + i;
            bool value = address < 8
                ? await io.ReadDigitalInputAsync(address, ct)
                : await io.ReadRelayFeedbackAsync(address - 8, ct);
            if (value) result[2 + i / 8] |= (byte)(1 << (i % 8));
        }
        return result;
    }

    private async ValueTask<byte[]> ReadFeedbackRegistersAsync(ReadOnlyMemory<byte> req, CancellationToken ct)
    {
        ValidateFixed(req);
        ushort start = Word(req.Span, 1);
        ushort qty = Word(req.Span, 3);
        if (qty == 0 || qty > 125) throw ModbusFault.IllegalValue();
        if ((int)start + qty > 16) throw ModbusFault.IllegalAddress();

        var result = new byte[2 + qty * 2];
        result[0] = 4;
        result[1] = (byte)(qty * 2);

        int firstChannel = start / 2;
        int lastChannel = (start + qty - 1) / 2;
        var words = new ushort[(lastChannel - firstChannel + 1) * 2];
        for (int channel = firstChannel; channel <= lastChannel; channel++)
        {
            float value = channel < 4
                ? await io.ReadAnalogInputAsync(channel, ct)
                : await io.ReadAnalogOutputFeedbackAsync(channel - 4, ct);
            if (!float.IsFinite(value)) throw ModbusFault.DeviceFailure();
            uint bits = BitConverter.SingleToUInt32Bits(value);
            int offset = (channel - firstChannel) * 2;
            words[offset] = (ushort)(bits >> 16);
            words[offset + 1] = (ushort)bits;
        }
        for (int i = 0; i < qty; i++)
        {
            ushort word = words[start + i - firstChannel * 2];
            BinaryPrimitives.WriteUInt16BigEndian(result.AsSpan(2 + i * 2, 2), word);
        }
        return result;
    }

    private async ValueTask<byte[]> WriteRelayCommandAsync(ReadOnlyMemory<byte> req, CancellationToken ct)
    {
        ValidateFixed(req);
        ushort address = Word(req.Span, 1);
        ushort value = Word(req.Span, 3);
        if (address >= 8) throw ModbusFault.IllegalAddress();
        if (value is not (0x0000 or 0xFF00)) throw ModbusFault.IllegalValue();
        await io.WriteRelayCommandAsync(address, value == 0xFF00, ct);
        return req.ToArray();
    }

    private async ValueTask<byte[]> WriteAnalogOutputCommandAsync(ReadOnlyMemory<byte> req, CancellationToken ct)
    {
        if (req.Length < 6) throw ModbusFault.IllegalValue();
        ushort address = Word(req.Span, 1);
        ushort qty = Word(req.Span, 3);
        byte byteCount = req.Span[5];
        // One complete float per command, never a half-float or multi-channel batch.
        if (qty != 2 || byteCount != 4 || req.Length != 10) throw ModbusFault.IllegalValue();
        if ((address & 1) != 0 || address > 6) throw ModbusFault.IllegalAddress();
        uint bits = BinaryPrimitives.ReadUInt32BigEndian(req.Span.Slice(6, 4));
        float value = BitConverter.UInt32BitsToSingle(bits);
        if (!float.IsFinite(value)) throw ModbusFault.IllegalValue();
        await io.WriteAnalogOutputCommandAsync(address / 2, value, ct);
        return req.Slice(0, 5).ToArray();
    }
}
