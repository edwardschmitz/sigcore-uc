using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace ModbusBridge.Protocol;

public sealed class ModbusTcpServer(ModbusProcessor processor, BridgeConfig config)
{
    private readonly ConcurrentDictionary<long, Task> _sessions = new();
    private long _nextSessionId;

    public async Task RunAsync(CancellationToken stopped)
    {
        var listener = new TcpListener(IPAddress.Parse(config.ListenAddress), config.Port);
        listener.Start(config.Backlog);
        Console.WriteLine($"ModbusBridge listening at {config.ListenAddress}:{config.Port}; unit {config.UnitId}; provider={config.Provider}");
        try
        {
            while (!stopped.IsCancellationRequested)
            {
                TcpClient client = await listener.AcceptTcpClientAsync(stopped);
                if (_sessions.Count >= config.MaxConcurrentClients)
                {
                    client.Dispose();
                    continue;
                }
                long id = Interlocked.Increment(ref _nextSessionId);
                var task = Task.Run(() => HandleClientAsync(client, stopped), CancellationToken.None);
                _sessions[id] = task;
                _ = task.ContinueWith(finished => { _sessions.TryRemove(id, out var ignored); }, TaskScheduler.Default);
            }
        }
        catch (OperationCanceledException) when (stopped.IsCancellationRequested) { }
        finally
        {
            listener.Stop();
            await Task.WhenAll(_sessions.Values);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken stopped)
    {
        using (client)
        {
            client.NoDelay = true;
            try
            {
                using NetworkStream stream = client.GetStream();
                while (!stopped.IsCancellationRequested)
                {
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stopped);
                    deadline.CancelAfter(TimeSpan.FromSeconds(config.RequestTimeoutSeconds));
                    CancellationToken ct = deadline.Token;
                    byte[] header = new byte[7];
                    if (!await ReadExactlyAsync(stream, header, ct)) return;
                    ushort transactionId = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(0, 2));
                    ushort protocolId = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2, 2));
                    ushort length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
                    byte unitId = header[6];
                    // 1 unit byte + 1..253 PDU bytes. Invalid framing closes the socket.
                    if (protocolId != 0 || length < 2 || length > 254) return;
                    byte[] request = new byte[length - 1];
                    if (!await ReadExactlyAsync(stream, request, ct)) return;
                    if (unitId != config.UnitId) continue; // Not addressed to this device.
                    byte[] response = await processor.ProcessAsync(request, ct);
                    byte[] replyHeader = new byte[7];
                    BinaryPrimitives.WriteUInt16BigEndian(replyHeader.AsSpan(0, 2), transactionId);
                    BinaryPrimitives.WriteUInt16BigEndian(replyHeader.AsSpan(2, 2), 0);
                    BinaryPrimitives.WriteUInt16BigEndian(replyHeader.AsSpan(4, 2), checked((ushort)(response.Length + 1)));
                    replyHeader[6] = unitId;
                    await stream.WriteAsync(replyHeader, ct);
                    await stream.WriteAsync(response, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (IOException) { }
            catch (SocketException) { }
            catch (ObjectDisposedException) { }
            catch (Exception e)
            {
                Console.Error.WriteLine($"Modbus client error: {e.GetType().Name}: {e.Message}");
            }
        }
    }

    private static async ValueTask<bool> ReadExactlyAsync(NetworkStream stream, Memory<byte> buffer, CancellationToken ct)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int count = await stream.ReadAsync(buffer[offset..], ct);
            if (count == 0)
            {
                if (offset == 0) return false;
                throw new IOException("Unexpected EOF inside Modbus ADU");
            }
            offset += count;
        }
        return true;
    }
}
